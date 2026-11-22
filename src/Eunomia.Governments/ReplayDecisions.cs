using System.Text.Json;
using Eunomia.Core;
using Eunomia.Core.Config;

namespace Eunomia.Governments;

/// <summary>
/// Re-enacts the decisions recorded in an earlier run folder, with no model calls. The simulation is
/// deterministic, so on the same world this reproduces the run, and after a rule fix it shows how the
/// same decisions fare under the corrected rules.
/// </summary>
public sealed class ReplayDecisions : IGovernment
{
    private readonly IReadOnlyList<DecisionRecord> _decisions;

    public ReplayDecisions(string runDir)
    {
        string path = Path.Combine(runDir, "decisions.json");
        _decisions = JsonSerializer.Deserialize<List<DecisionRecord>>(File.ReadAllText(path), ConfigLoader.Options)
            ?? throw new InvalidDataException($"{path} is empty.");
        Source = Path.GetFileName(Path.TrimEndingDirectorySeparator(runDir));
    }

    public string Source { get; }
    public string Name => "replay";

    public Policy? Decide(MeetingContext ctx)
    {
        if (ctx.Year >= _decisions.Count)
            throw new InvalidOperationException($"The recorded run has {_decisions.Count} decisions; year {ctx.Year} was requested.");
        var d = _decisions[ctx.Year];
        return d.CarriedOver ? null : d.InForce;
    }
}
