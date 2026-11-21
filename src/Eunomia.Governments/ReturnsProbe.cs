using Eunomia.Core;

namespace Eunomia.Governments;

/// <summary>
/// A diagnostic, not a government: keeps the starting policy but spends an extra share of output on one
/// line every year, paid for by borrowing. Compared with Frozen, it shows what that line buys.
/// </summary>
public sealed class ReturnsProbe(BudgetLine line, double extraShareOfOutput) : IGovernment
{
    public string Name => $"probe-{line}";

    public Policy? Decide(MeetingContext ctx)
    {
        var start = ctx.StartingPolicy;
        double baseEnvelope = ctx.Check(start).Envelope;
        double extra = extraShareOfOutput * ctx.Last.Output;
        double envelope = baseEnvelope + extra;

        var shares = start.BudgetShares.ToArray();
        for (int i = 0; i < shares.Length; i++)
            shares[i] = (shares[i] * baseEnvelope + (i == (int)line ? extra : 0)) / envelope;

        return start with
        {
            PlannedDeficit = start.PlannedDeficit + extraShareOfOutput,
            BudgetShares = BudgetShares.FromArray(shares),
        };
    }
}
