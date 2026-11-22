using System.Text;
using Eunomia.Core;
using Eunomia.Core.Config;

namespace Eunomia.Governments.Ai;

public sealed record BranchModels(IBranchModel Executive, IBranchModel Legislature, IBranchModel Judiciary);

public sealed record Mandates(string Preamble, string Executive, string Legislature, string Judiciary);

/// <summary>
/// Three branches with different mandates. Each round: the executive proposes; the legislature approves,
/// amends (the executive then accepts or declines) or rejects; the judiciary reviews the agreed text last
/// and may block it only by naming a floor, a year and a path. The executive and the legislature must both
/// sign. With no enactment after the round limit, last year's budget carries over. Every call is stateless:
/// each branch gets the report and the meeting so far. Use one instance per run.
/// </summary>
public sealed class ThreeBranch(BranchModels models, Mandates mandates, int maxRounds = 3, int memorySize = 3) : IGovernment, ITranscribed
{
    private readonly List<TranscriptEntry> _transcript = [];
    private readonly List<Deliberation> _deliberations = [];
    private readonly List<string> _memory = [];

    public string Name => "threebranch";
    public Action<string>? Log { get; init; }
    public string System =>
        $"PREAMBLE: {mandates.Preamble}\nEXECUTIVE ({models.Executive.Name}): {mandates.Executive}\n" +
        $"LEGISLATURE ({models.Legislature.Name}): {mandates.Legislature}\nJUDICIARY ({models.Judiciary.Name}): {mandates.Judiciary}";
    public IReadOnlyList<TranscriptEntry> Transcript => _transcript;
    public IReadOnlyList<Deliberation> Deliberations => _deliberations;

    private sealed record Proposal(Policy Policy, CheckResult Check, string Rationale, string Expectations);

    public Policy? Decide(MeetingContext ctx)
    {
        int calendar = ctx.BaseYear + 1 + ctx.Year;
        string report = StateReport.Body(ctx, _memory.TakeLast(memorySize).ToList(), "Recent decisions of this government");
        var steps = new List<Step>();
        var meeting = new StringBuilder();
        int revisions = 0;

        void Note(int round, string branch, string action, string text, string? detail = null)
        {
            steps.Add(new Step(round, branch, action, text, detail));
            Log?.Invoke($"  {calendar} r{round} {branch} {action}");
        }

        for (int round = 1; round <= maxRounds; round++)
        {
            // 1. The executive proposes.
            string task = round == 1
                ? "You are the executive. Propose this year's budget."
                : "You are the executive. Your earlier proposal was not enacted; the meeting log says why. Propose a revised budget.";
            var proposal = Ask(models.Executive, "executive", mandates.Executive, ctx, round,
                Prompt(report, meeting, task, "Reply with one JSON object and nothing else, in this form (the numbers are only an example):\n" +
                    StateReport.PolicyJsonExample("Why this budget, in a few sentences, based on the report.")),
                text => ReadProposal(ReplyParser.Parse(text), ctx));
            if (proposal is null)
            {
                Note(round, "executive", "abstains", "No valid proposal after a retry.");
                meeting.AppendLine($"Round {round}: the executive made no valid proposal.\n");
                continue;
            }
            string r = $"R{++revisions}";
            Note(round, "executive", $"proposes {r}", proposal.Rationale, proposal.Expectations);
            meeting.AppendLine($"Round {round}: the executive proposed revision {r}.");
            meeting.AppendLine(StateReport.DescribeRevision(r, proposal.Policy, proposal.Check, ctx.Enacted));
            meeting.AppendLine($"Executive's reasons: {proposal.Rationale}\nExecutive expects: {proposal.Expectations}\n");

            // 2. The legislature approves, amends or rejects.
            Proposal? amendment = null;
            var vote = Ask(models.Legislature, "legislature", mandates.Legislature, ctx, round,
                Prompt(report, meeting, $"You are the legislature. Revision {r} is before you. Approve it, amend it (give your full amended budget), or reject it.",
                    """
                    Reply with one JSON object and nothing else:
                    { "decision": "approve" | "amend" | "reject",
                      "rationale": "Why, in a few sentences.",
                      "amendment": { "taxes": {...}, "plannedDeficit": ..., "budgetShares": {...} }   (only when amending; same form as the proposal)
                    }
                    """),
                text =>
                {
                    var c = ReplyParser.ParseChoice(text, "approve", "amend", "reject");
                    if (!c.Ok || c.Decision != "amend") return (c, c.Errors.ToList());
                    var errors = new List<string>();
                    var policy = c.Root.TryGetProperty("amendment", out var a) ? ReplyParser.ParsePolicy(a, errors) : null;
                    if (policy is null && errors.Count == 0) errors.Add("An amendment needs an \"amendment\" object with the full budget.");
                    if (policy is not null)
                    {
                        var check = ctx.Check(policy);
                        if (check.Ok) amendment = new Proposal(check.Feasible!, check, c.Rationale, "");
                        else errors.AddRange(check.Errors);
                    }
                    return (c, errors);
                });

            Proposal? agreed = null;
            string agreedId = r;
            if (vote is null)
            {
                Note(round, "legislature", "abstains", "No valid reply after a retry.");
                meeting.AppendLine($"The legislature gave no valid reply. Round {round} ends.\n");
                continue;
            }
            if (vote.Decision == "reject")
            {
                Note(round, "legislature", $"rejects {r}", vote.Rationale);
                meeting.AppendLine($"The legislature rejected {r}: {vote.Rationale}\n");
                continue;
            }
            if (vote.Decision == "approve")
            {
                Note(round, "legislature", $"approves {r}", vote.Rationale);
                meeting.AppendLine($"The legislature approved {r}: {vote.Rationale}\n");
                agreed = proposal;
            }
            else
            {
                string r2 = $"R{++revisions}";
                Note(round, "legislature", $"amends {r} into {r2}", vote.Rationale);
                meeting.AppendLine($"The legislature amended {r} into revision {r2}.");
                meeting.AppendLine(StateReport.DescribeRevision(r2, amendment!.Policy, amendment.Check, ctx.Enacted));
                meeting.AppendLine($"Legislature's reasons: {vote.Rationale}\n");

                var answer = Ask(models.Executive, "executive", mandates.Executive, ctx, round,
                    Prompt(report, meeting, $"You are the executive. The legislature amended your proposal into revision {r2}. Accept or decline it.",
                        """
                        Reply with one JSON object and nothing else:
                        { "decision": "accept" | "decline", "rationale": "Why, in a few sentences." }
                        """),
                    text => { var c = ReplyParser.ParseChoice(text, "accept", "decline"); return (c, c.Errors.ToList()); });
                if (answer?.Decision != "accept")
                {
                    Note(round, "executive", answer is null ? "abstains" : $"declines {r2}", answer?.Rationale ?? "No valid reply after a retry.");
                    meeting.AppendLine($"The executive {(answer is null ? "gave no valid reply" : "declined " + r2 + ": " + answer.Rationale)}. Round {round} ends.\n");
                    continue;
                }
                Note(round, "executive", $"accepts {r2}", answer.Rationale);
                meeting.AppendLine($"The executive accepted {r2}: {answer.Rationale}\n");
                agreed = amendment with { Expectations = proposal.Expectations };
                agreedId = r2;
            }

            // 3. The judiciary reviews the agreed text last.
            var floors = EnabledFloors(ctx.Rules.Floors);
            int lastYear = ctx.BaseYear + ctx.HorizonYears;
            var review = Ask(models.Judiciary, "judiciary", mandates.Judiciary, ctx, round,
                Prompt(report, meeting,
                    $"You are the judiciary. The executive and the legislature agreed revision {agreedId}. Pass it, or block it if you can name a floor it would breach, the year of the breach, and the path to it. Disagreeing with the policy is not grounds for a block.",
                    $$"""
                    Floors you may name: {{string.Join(", ", floors.Select(f => $"\"{f.Id}\" ({f.Label})"))}}.
                    Reply with one JSON object and nothing else:
                    { "decision": "pass" | "block",
                      "rationale": "Why, in a few sentences.",
                      "floor": "...",          (only when blocking)
                      "breachYear": {{calendar + 1}},       (only when blocking; between {{calendar}} and {{lastYear}})
                      "path": "How this budget leads to the breach." (only when blocking)
                    }
                    """),
                text => { var c = ReplyParser.ParseChoice(text, "pass", "block"); return (c, c.Errors.ToList()); });

            if (review?.Decision == "block")
            {
                string floor = review.Str("floor");
                int? year = review.Int("breachYear");
                string path = review.Str("path");
                bool valid = floors.Any(f => f.Id == floor) && year >= calendar && year <= lastYear && path.Trim().Length > 0;
                if (valid)
                {
                    Note(round, "judiciary", $"blocks {agreedId}", review.Rationale, $"floor {floor}, breach by {year}: {path}");
                    meeting.AppendLine($"The judiciary blocked {agreedId}: floor \"{floor}\", projected breach by {year}. Path: {path} Reasons: {review.Rationale}\n");
                    continue;
                }
                Note(round, "judiciary", "dissents (invalid block)", review.Rationale, "A block must name an enabled floor, a year inside the horizon and a path; this one did not, so it stops nothing.");
            }
            else
            {
                Note(round, "judiciary", review is null ? "abstains" : $"passes {agreedId}", review?.Rationale ?? "No valid reply after a retry.");
            }

            Note(round, "government", $"enacts {agreedId}", "Signed by the executive and the legislature.");
            _memory.Add($"{calendar}: enacted {agreedId} in round {round}. Executive: {proposal.Rationale} Expected: {agreed.Expectations}" +
                        (agreedId != r ? $" Legislature's amendment: {vote.Rationale}" : ""));
            _deliberations.Add(new Deliberation
            {
                Year = ctx.Year, Proposed = agreed.Policy, Rationale = agreed.Rationale, Expectations = agreed.Expectations,
                Errors = [], Abstained = false, Steps = steps,
            });
            return agreed.Policy;
        }

        Note(maxRounds, "government", "no decision", $"Nothing enacted after {maxRounds} rounds; last year's budget carries over.");
        _deliberations.Add(new Deliberation
        {
            Year = ctx.Year, Rationale = "", Expectations = "", Errors = [], Abstained = true, Steps = steps,
        });
        return null;
    }

    private static (Proposal?, List<string>) ReadProposal(ParsedReply p, MeetingContext ctx)
    {
        if (!p.Ok) return (null, p.Errors.ToList());
        var check = ctx.Check(p.Policy!);
        return check.Ok ? (new Proposal(check.Feasible!, check, p.Rationale, p.Expectations), []) : (null, check.Errors.ToList());
    }

    private static string Prompt(string report, StringBuilder meeting, string task, string format) =>
        report +
        (meeting.Length > 0 ? "## This meeting so far\n" + meeting : "") +
        $"## Your task\n{task}\n\n{format}\n";

    /// <summary>One branch call, with one retry carrying the errors. Returns null when both attempts fail.</summary>
    private T? Ask<T>(IBranchModel model, string branch, string mandate, MeetingContext ctx, int round, string prompt,
        Func<string, (T? Value, List<string> Errors)> interpret) where T : class
    {
        var errors = new List<string>();
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            string full = attempt == 1 ? prompt :
                prompt + "\n## Your previous reply was rejected\n" + string.Join("\n", errors.Select(e => "- " + e)) + "\nReply again with one corrected JSON object.\n";
            var reply = model.Complete(mandates.Preamble + "\n\n" + mandate, full);
            _transcript.Add(new TranscriptEntry
            {
                Year = ctx.Year, Attempt = attempt, Branch = $"{branch} r{round}", Model = model.Name,
                Prompt = full, Reply = reply.Text, Error = reply.Error, ElapsedMs = reply.ElapsedMs,
            });
            if (!reply.Ok) { errors = [$"The model call failed: {reply.Error}"]; continue; }
            var (value, errs) = interpret(reply.Text);
            if (errs.Count == 0 && value is not null) return value;
            errors = errs;
        }
        return null;
    }

    private static List<(string Id, string Label)> EnabledFloors(Floors f)
    {
        var list = new List<(string, string)>();
        if (f.Debt.Enabled) list.Add(("debt", "debt stays payable"));
        if (f.FiscalRules.Enabled) list.Add(("fiscalRules", "EU fiscal rules"));
        if (f.IncomeGroups.Enabled) list.Add(("incomeGroups", "no income fifth below its floor"));
        if (f.EndStateStocks.Enabled) list.Add(("endStateStocks", "final-year stock indices"));
        if (f.EndStatePipelines.Enabled) list.Add(("endStatePipelines", "final-year committed spending"));
        return list;
    }
}
