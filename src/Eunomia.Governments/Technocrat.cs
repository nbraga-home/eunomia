using Eunomia.Core;
using static Eunomia.Core.BudgetLine;

namespace Eunomia.Governments;

/// <summary>
/// A short, sensible rule with no intelligence: shift money steadily from transfers into long-term
/// investment, keep the deficit plan, borrow a little more in recessions. If an AI cannot beat this,
/// the AI adds nothing.
/// </summary>
public sealed class Technocrat(TechnocratParams p) : IGovernment
{
    private bool _recession;

    public string Name => "technocrat";

    public Policy? Decide(MeetingContext ctx)
    {
        var policy = ctx.Enacted;
        var shares = policy.BudgetShares.ToArray();
        double deficit = policy.PlannedDeficit;

        double floor = p.TransfersFloorShare * ctx.StartingPolicy.BudgetShares.SocialTransfers;
        double room = Math.Max(0, shares[(int)SocialTransfers] - floor);
        var t = p.Targets;
        shares = PolicyEdits.Move(shares, Math.Min(p.YearlyShift, room), [SocialTransfers],
            [(Education, t.Education), (Infrastructure, t.Infrastructure), (Administration, t.Administration), (Health, t.Health)]);

        bool recession = ctx.Last.OutputGrowth < p.RecessionGrowth;
        if (recession && !_recession) deficit += p.RecessionDeficitRise;
        if (!recession && _recession) deficit -= p.RecessionDeficitRise;
        _recession = recession;

        return policy with
        {
            PlannedDeficit = PolicyEdits.Deficit(deficit, ctx.Rules.DeficitBounds),
            BudgetShares = BudgetShares.FromArray(shares),
        };
    }
}
