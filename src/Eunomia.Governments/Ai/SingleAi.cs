using Eunomia.Core;

namespace Eunomia.Governments.Ai;

public sealed record TranscriptEntry
{
    public required int Year { get; init; }
    public required int Attempt { get; init; }
    public required string Model { get; init; }
    public required string Prompt { get; init; }
    public required string Reply { get; init; }
    public string? Error { get; init; }
    public required long ElapsedMs { get; init; }
}

/// <summary>One record per budget meeting, for the deliberation log.</summary>
public sealed record Deliberation
{
    public required int Year { get; init; }
    public Policy? Proposed { get; init; }
    public required string Rationale { get; init; }
    public required string Expectations { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public required bool Abstained { get; init; }
}

/// <summary>An occupant that keeps a transcript of its model calls.</summary>
public interface ITranscribed
{
    string System { get; }
    IReadOnlyList<TranscriptEntry> Transcript { get; }
    IReadOnlyList<Deliberation> Deliberations { get; }
}

/// <summary>
/// One model, one mandate, the same report and rules as everyone else. Each meeting: propose; if the
/// reply cannot be read or breaks a rule, the errors go back once; a second failure is an abstention
/// and last year's policy carries over. Use one instance per run.
/// </summary>
public sealed class SingleAi(IBranchModel model, string mandate, int memorySize = 3) : IGovernment, ITranscribed
{
    private readonly List<TranscriptEntry> _transcript = [];
    private readonly List<Deliberation> _deliberations = [];
    private readonly List<string> _memory = [];

    public string Name => "singleai";
    /// <summary>Progress lines for long runs. Optional.</summary>
    public Action<string>? Log { get; init; }
    public string System => mandate;
    public IReadOnlyList<TranscriptEntry> Transcript => _transcript;
    public IReadOnlyList<Deliberation> Deliberations => _deliberations;

    public Policy? Decide(MeetingContext ctx)
    {
        string prompt = StateReport.Build(ctx, _memory.TakeLast(memorySize).ToList());
        ParsedReply? parsed = null;
        var errors = new List<string>();

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            string fullPrompt = attempt == 1 ? prompt :
                prompt + "\n\n## Your previous reply was rejected\n" + string.Join("\n", errors.Select(e => "- " + e)) +
                "\nReply again with one corrected JSON object.";
            var reply = model.Complete(mandate, fullPrompt);
            Log?.Invoke($"  {ctx.BaseYear + 1 + ctx.Year} attempt {attempt}: {(reply.Ok ? "reply" : "failed")} in {reply.ElapsedMs / 1000.0:0.0}s");
            _transcript.Add(new TranscriptEntry
            {
                Year = ctx.Year, Attempt = attempt, Model = model.Name,
                Prompt = fullPrompt, Reply = reply.Text, Error = reply.Error, ElapsedMs = reply.ElapsedMs,
            });

            errors.Clear();
            if (!reply.Ok) { errors.Add($"The model call failed: {reply.Error}"); continue; }

            parsed = ReplyParser.Parse(reply.Text);
            if (!parsed.Ok) { errors.AddRange(parsed.Errors); continue; }

            var check = ctx.Check(parsed.Policy!);
            if (!check.Ok) { errors.AddRange(check.Errors); continue; }

            Remember(ctx, parsed);
            Log?.Invoke($"  {ctx.BaseYear + 1 + ctx.Year} decided: {Shorten(parsed.Rationale, 240)}");
            _deliberations.Add(new Deliberation
            {
                Year = ctx.Year, Proposed = parsed.Policy, Rationale = parsed.Rationale,
                Expectations = parsed.Expectations, Errors = [], Abstained = false,
            });
            return parsed.Policy;
        }

        _deliberations.Add(new Deliberation
        {
            Year = ctx.Year, Proposed = parsed?.Policy, Rationale = parsed?.Rationale ?? "",
            Expectations = parsed?.Expectations ?? "", Errors = errors.ToList(), Abstained = true,
        });
        return null;
    }

    private static string Shorten(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    private void Remember(MeetingContext ctx, ParsedReply p) =>
        _memory.Add($"{ctx.BaseYear + 1 + ctx.Year}: {p.Rationale} Expected: {p.Expectations}");
}
