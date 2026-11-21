using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Core.World;

namespace Eunomia.Core;

/// <summary>
/// The government slot. A report goes in, a policy comes out. The core never
/// knows which occupant answered.
/// </summary>
public interface IGovernment
{
    string Name { get; }

    /// <summary>Returns the proposed policy, or null for no decision (last year's policy carries over).</summary>
    Policy? Decide(MeetingContext context);
}

/// <summary>Everything a government may see at its yearly budget meeting. Nothing here reveals the model's parameters or the future.</summary>
public sealed record MeetingContext
{
    public required int Year { get; init; }
    public required int HorizonYears { get; init; }
    /// <summary>This year's world, as it stands when the budget is set.</summary>
    public required FeedRow World { get; init; }
    /// <summary>Last year's results (the opening year when Year is 0).</summary>
    public required YearRecord Last { get; init; }
    /// <summary>Every completed year, opening year first.</summary>
    public required IReadOnlyList<YearRecord> History { get; init; }
    public required Policy Enacted { get; init; }
    public required Policy StartingPolicy { get; init; }
    public required bool ElectionYear { get; init; }
    public required GovernmentConfig Rules { get; init; }
    /// <summary>Validates a proposal and returns its feasible form, envelope and appropriations. May be called freely.</summary>
    public required Func<Policy, CheckResult> Check { get; init; }
}

public sealed record CheckResult
{
    public required IReadOnlyList<string> Errors { get; init; }
    /// <summary>The proposal after the reallocation allowance is applied. Null when there are errors.</summary>
    public Policy? Feasible { get; init; }
    public double Envelope { get; init; }
    public IReadOnlyList<double> Appropriations { get; init; } = [];
    /// <summary>True when the share changes were scaled down to fit the allowance.</summary>
    public bool Scaled { get; init; }
    public bool Ok => Errors.Count == 0;
}

public sealed record DecisionRecord
{
    public required int Year { get; init; }
    public Policy? Proposed { get; init; }
    public required Policy InForce { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public required bool CarriedOver { get; init; }
    public required bool Scaled { get; init; }
}
