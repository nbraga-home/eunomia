using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Core.World;

namespace Eunomia.Tests;

public class RatingTests
{
    private static RatingParams P => Repo.LoadPortugal().Economy.Rating;

    [Fact]
    public void StartsAtThePackRating()
    {
        var agency = new RatingAgency(P, "A-");
        Assert.Equal("A-", agency.Rating);
        Assert.Equal(Outlook.Stable, agency.Outlook);
        Assert.True(agency.InvestmentGrade);
    }

    [Fact]
    public void WarnsBeforeCutting()
    {
        var agency = new RatingAgency(P, "A-");
        double worse = agency.Notch + 1.0;

        Assert.Contains("negative", agency.Review(worse));
        Assert.Equal("A-", agency.Rating);

        Assert.Contains("cuts rating to BBB+", agency.Review(worse));
        Assert.Equal("BBB+", agency.Rating);
    }

    [Fact]
    public void ALargeGapCutsTwoNotchesAtOnce()
    {
        var agency = new RatingAgency(P, "A-");
        agency.Review(agency.Notch + 3.0);
        Assert.Equal("BBB", agency.Rating);
    }

    [Fact]
    public void UpgradesAreSlowAndWarnedToo()
    {
        var agency = new RatingAgency(P, "A-");
        double better = agency.Notch - 3.0;
        Assert.Contains("positive", agency.Review(better));
        Assert.Equal("A-", agency.Rating);
        agency.Review(better);
        Assert.Equal("A", agency.Rating);
    }

    [Fact]
    public void FallingBelowInvestmentGradeIsACliff()
    {
        var agency = new RatingAgency(P, "BBB-");
        Assert.True(agency.SpreadFor(10) - agency.SpreadFor(9) >= P.JunkCliff);
        agency.Review(agency.Notch + 1.0);
        Assert.Contains("below investment grade", agency.Review(agency.Notch + 1.0));
        Assert.False(agency.InvestmentGrade);
    }

    [Fact]
    public void CalibrationMatchesPortugalTodayAndIn2011()
    {
        var agency = new RatingAgency(P, "A-");
        // Today: debt 95%, balanced budget, debt falling about 3 points a year, growth about 1%.
        Assert.InRange(agency.Target(0.947, 0.0, -0.03, 0.011), 5.25, 6.75);
        // 2011: debt 110%, deficit 7.4%, debt rising 10 points, growth -1.7%. Real ratings were in the BB range.
        Assert.InRange(agency.Target(1.10, 0.074, 0.10, -0.017), 10.0, 12.5);
    }

    [Fact]
    public void SteadyDebtReductionEarnsUpgrades()
    {
        var run = Fixtures.RunFlat(Repo.LoadPortugal(), Fixtures.Frozen);
        var scale = P.Scale.ToList();
        Assert.True(scale.IndexOf(run.Years[^1].Rating) < scale.IndexOf("A-"));
    }

    [Fact]
    public void HeavyBorrowingIsDowngradedAndCostsMore()
    {
        var c = Repo.LoadPortugal();
        var spree = new Scripted("spree", ctx => ctx.StartingPolicy with { PlannedDeficit = 0.08 });
        var run = Fixtures.RunFlat(c, spree);
        var scale = P.Scale.ToList();

        Assert.True(scale.IndexOf(run.Years[^1].Rating) > scale.IndexOf("A-"));
        Assert.True(run.Years[^1].MarketRate > run.Years[0].MarketRate + 0.01);
        Assert.Contains(run.Years, y => y.RatingAction?.Contains("below investment grade") == true);
    }
}

public class FiscalRulesTests
{
    [Fact]
    public void FrozenPortugalKeepsTheRules()
    {
        var run = Fixtures.RunFlat(Repo.LoadPortugal(), Fixtures.Frozen);
        Assert.DoesNotContain(run.Summary.Breaches, b => b.Floor == "fiscalRules");
    }

    [Fact]
    public void ADeficitAboveTheLimitBreaches()
    {
        var c = Repo.LoadPortugal();
        var run = Fixtures.RunFlat(c, new Scripted("deficit", ctx => ctx.StartingPolicy with { PlannedDeficit = 0.05 }));
        Assert.Contains(run.Summary.Breaches, b => b.Floor == "fiscalRules" && b.Year == 0 && b.Detail.StartsWith("deficit"));
    }

    [Fact]
    public void DebtThatStopsFallingBreaches()
    {
        // A 2.5% deficit stays under the 3% limit, but debt above 90% no longer falls 1 point a year.
        var c = Repo.LoadPortugal();
        var run = Fixtures.RunFlat(c, new Scripted("drift", ctx => ctx.StartingPolicy with { PlannedDeficit = 0.025 }));
        Assert.Contains(run.Summary.Breaches, b => b.Floor == "fiscalRules" && b.Detail.StartsWith("debt ratio"));
        Assert.DoesNotContain(run.Summary.Breaches, b => b.Floor == "fiscalRules" && b.Detail.StartsWith("deficit"));
    }

    [Fact]
    public void TheEscapeClauseSuspendsTheRulesInACrisis()
    {
        var c = Repo.LoadPortugal();
        var crisis = WorldGenerator.Flat(c.World, c.Government.HorizonYears).Select(r => r with { CrisisActive = true }).ToList();
        var run = new Simulation(c, "central", crisis).Run(new Scripted("deficit", ctx => ctx.StartingPolicy with { PlannedDeficit = 0.05 }));
        Assert.DoesNotContain(run.Summary.Breaches, b => b.Floor == "fiscalRules");
    }

    [Fact]
    public void TheRulesCanBeSwitchedOff()
    {
        var c = Repo.LoadPortugal();
        c = c with
        {
            Government = c.Government with
            {
                Floors = c.Government.Floors with { FiscalRules = c.Government.Floors.FiscalRules with { Enabled = false } },
            },
        };
        var run = Fixtures.RunFlat(c, new Scripted("deficit", ctx => ctx.StartingPolicy with { PlannedDeficit = 0.05 }));
        Assert.DoesNotContain(run.Summary.Breaches, b => b.Floor == "fiscalRules");
    }
}
