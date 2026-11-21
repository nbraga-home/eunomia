using Eunomia.Core;
using static Eunomia.Core.BudgetLine;

namespace Eunomia.Governments;

/// <summary>
/// A typical government: last year's budget plus small changes, driven by three habits.
/// It has state (which habits are active), so use one instance per run.
/// </summary>
public sealed class StatusQuo(StatusQuoParams p) : IGovernment
{
    private int _austeritySteps;
    private int _calmYears;
    private bool _inCrisisMode;
    private bool _electionTiltActive;

    public string Name => "statusquo";

    public Policy? Decide(MeetingContext ctx)
    {
        var rules = ctx.Rules;
        var policy = ctx.Enacted;
        var shares = policy.BudgetShares.ToArray();
        double deficit = policy.PlannedDeficit;
        double consumptionTax = policy.Taxes.Consumption;
        var last = ctx.Last;

        // Undo last year's election tilt first.
        if (_electionTiltActive)
        {
            shares = PolicyEdits.Move(shares, p.ElectionShareShift, [SocialTransfers], [(Infrastructure, ctx.StartingPolicy.BudgetShares.Infrastructure), (Education, ctx.StartingPolicy.BudgetShares.Education)]);
            _electionTiltActive = false;
        }

        // Deficit limit
        if (last.DeficitRatio > p.DeficitLimit)
        {
            deficit -= Math.Min(last.DeficitRatio - p.DeficitLimit, p.DeficitCut);
            consumptionTax = PolicyEdits.Tax(consumptionTax, p.ConsumptionTaxStep, rules.TaxBounds.Consumption, rules.MaxTaxChangePerYear);
            _austeritySteps++;
            _calmYears = 0;
        }
        else if (last.DeficitRatio < p.UnwindBelow && _austeritySteps > 0)
        {
            if (++_calmYears >= p.UnwindYears)
            {
                consumptionTax = PolicyEdits.Tax(consumptionTax, -p.ConsumptionTaxStep, rules.TaxBounds.Consumption, rules.MaxTaxChangePerYear);
                _austeritySteps--;
            }
        }

        // Crisis reaction
        if (!_inCrisisMode && last.OutputGrowth < p.RecessionGrowth)
        {
            deficit += p.CrisisDeficitRise;
            shares = PolicyEdits.Move(shares, p.CrisisShareShift, [Administration, Security], SocialTransfers);
            _inCrisisMode = true;
        }
        else if (_inCrisisMode && last.OutputGrowth > p.RecoveryGrowth)
        {
            deficit -= p.CrisisDeficitRise;
            shares = PolicyEdits.Move(shares, p.CrisisShareShift, [SocialTransfers],
                [(Administration, ctx.StartingPolicy.BudgetShares.Administration), (Security, ctx.StartingPolicy.BudgetShares.Security)]);
            _inCrisisMode = false;
        }

        // Election tilt
        if (ctx.ElectionYear)
        {
            shares = PolicyEdits.Move(shares, p.ElectionShareShift, [Infrastructure, Education], SocialTransfers);
            _electionTiltActive = true;
        }

        return policy with
        {
            Taxes = policy.Taxes with { Consumption = consumptionTax },
            PlannedDeficit = PolicyEdits.Deficit(deficit, rules.DeficitBounds),
            BudgetShares = BudgetShares.FromArray(shares),
        };
    }
}
