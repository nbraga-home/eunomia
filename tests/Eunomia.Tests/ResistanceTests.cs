using Eunomia.Core;
using Eunomia.Core.Config;

namespace Eunomia.Tests;

public class ResistanceTests
{
    private static SimulationConfig On(SimulationConfig c) =>
        c with { Government = c.Government with { Resistance = c.Government.Resistance with { Enabled = true } } };

    /// <summary>Cuts health by four points of budget share in year 0, about a quarter of its money.</summary>
    private static Scripted HealthCut => new("cut", ctx =>
    {
        var s = ctx.StartingPolicy.BudgetShares.ToArray();
        s[(int)BudgetLine.Health] -= 0.04;
        s[(int)BudgetLine.SocialTransfers] += 0.04;
        return ctx.StartingPolicy with { BudgetShares = BudgetShares.FromArray(s) };
    });

    [Fact]
    public void OffByDefault()
    {
        var run = Fixtures.RunFlat(Fixtures.Stationary(), HealthCut);
        Assert.All(run.Years, y => Assert.Equal(0, y.StrikeLoss));
    }

    [Fact]
    public void ADeepCutBringsAStrikeThatCostsOutput()
    {
        var c = Fixtures.Stationary();
        var quiet = Fixtures.RunFlat(c, HealthCut);
        var loud = Fixtures.RunFlat(On(c), HealthCut);
        Assert.True(loud.Years[0].StrikeLoss > 0);
        Assert.Contains(loud.Years[0].Protests, p => p.StartsWith("health strike"));
        Assert.True(loud.Years[0].Output < quiet.Years[0].Output);
        Assert.Equal(0, loud.Years[1].StrikeLoss); // the cut is not repeated
    }

    [Fact]
    public void ARegimeChangeBringsAStrike()
    {
        var c = On(Repo.LoadPortugal());
        var run = Fixtures.RunFlat(c, new Scripted("free", ctx => ctx.StartingPolicy with
        {
            EducationRegime = ctx.StartingPolicy.EducationRegime! with { UniversityFees = UniversityFees.Free },
        }));
        Assert.Equal(c.Government.Resistance.RegimeChangeLoss, run.Years[0].StrikeLoss, 12);
        Assert.Contains(run.Years[0].Protests, p => p.StartsWith("education strike"));
    }

    [Fact]
    public void AGovernmentThatChangesNothingMeetsNoResistance()
    {
        var run = Fixtures.RunFlat(On(Fixtures.Stationary()), Fixtures.Frozen);
        Assert.All(run.Years, y => Assert.Empty(y.Protests));
    }
}
