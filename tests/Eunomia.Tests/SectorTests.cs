using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Governments.Ai;

namespace Eunomia.Tests;

public class SectorTests
{
    private static ManagementParams P => Repo.LoadPortugal().Economy.Management;

    [Fact]
    public void AtTheStartingShareAndSpendingNothingChanges()
    {
        var s = new Sector(BudgetLine.Health, P, 0.05, delivered: 100);
        for (int i = 0; i < 10; i++)
        {
            var split = s.Advance(100, 0.05);
            Assert.Equal(95, split.Effective, 9);
        }
        Assert.Equal(1.0, s.ManagementIndex, 9);
        Assert.Equal(1.0, s.Efficiency, 9);
    }

    [Fact]
    public void GrowthAboveTheLimitIsMostlyWasted()
    {
        var p = P;
        var s = new Sector(BudgetLine.Agriculture, p, 0.05, delivered: 100);
        var split = s.Split(300, 0.05); // triple the line in one year

        double allowed = 95 * (1 + p.AbsorptionGrowth);
        Assert.Equal(allowed, split.Absorbed, 9);
        Assert.Equal(allowed + p.ExcessEffectiveness * (285 - allowed), split.Effective, 9);
    }

    [Fact]
    public void SustainedSpendingIsAbsorbedOverTheYears()
    {
        var s = new Sector(BudgetLine.Agriculture, P, 0.05, delivered: 100);
        SectorSplit split = default;
        for (int i = 0; i < 15; i++) split = s.Advance(200, 0.05);
        Assert.Equal(split.FrontLine, split.Absorbed, 9); // capacity has caught up with a doubled line
    }

    [Fact]
    public void MoreManagementRaisesEfficiencyWithDiminishingReturns()
    {
        double Run(double share)
        {
            var s = new Sector(BudgetLine.Health, P, 0.05, delivered: 100);
            for (int i = 0; i < 30; i++) s.Advance(100, share);
            return s.Efficiency;
        }
        double at10 = Run(0.10), at20 = Run(0.20);
        Assert.True(at10 > 1);
        Assert.True(at20 > at10);
        Assert.True((at20 - at10) / 0.10 < (at10 - 1) / 0.05); // each extra point of share buys less
    }

    [Fact]
    public void AbsorptionLimitsTheReturnOfATinyLineTripled()
    {
        var c = Repo.LoadPortugal();
        var unlimited = c with { Economy = c.Economy with { Management = c.Economy.Management with { AbsorptionGrowth = 100 } } };
        var probe = new Eunomia.Governments.ReturnsProbe(BudgetLine.Agriculture, 0.01);

        double limitedFarm = Fixtures.RunFlat(c, probe).Years[9].FarmProductivity;
        double unlimitedFarm = Fixtures.RunFlat(unlimited, new Eunomia.Governments.ReturnsProbe(BudgetLine.Agriculture, 0.01)).Years[9].FarmProductivity;
        Assert.True(limitedFarm - 1 < 0.6 * (unlimitedFarm - 1));
    }

    [Fact]
    public void ManagementSharesFollowTheRules()
    {
        var c = Repo.LoadPortugal();
        var sim = new Simulation(c, "central", Eunomia.Core.World.WorldGenerator.Flat(c.World, c.Government.HorizonYears));
        var start = sim.Opening.Policy;

        var tooFast = sim.Check(start with { ManagementShares = start.ManagementShares! with { Health = 0.10 } });
        Assert.Contains(tooFast.Errors, e => e.Contains("Management share for Health can move at most"));

        var ok = sim.Check(start with { ManagementShares = start.ManagementShares! with { Health = 0.07 } });
        Assert.True(ok.Ok);

        var kept = sim.Check(start with { ManagementShares = null });
        Assert.Equal(start.ManagementShares, kept.Feasible!.ManagementShares);
    }

    [Fact]
    public void ParserReadsManagementSharesOrKeepsThem()
    {
        const string withShares = """
            {"taxes":{"income":0.42,"corporate":0.2,"consumption":0.24},"plannedDeficit":0,
             "budgetShares":{"health":0.165,"education":0.1,"infrastructure":0.14,"agriculture":0.012,"socialTransfers":0.42,"security":0.057,"administration":0.106},
             "managementShares":{"health":0.07,"education":0.05,"infrastructure":0.05,"agriculture":0.05,"security":0.05,"administration":0.05}}
            """;
        Assert.Equal(0.07, ReplyParser.Parse(withShares).Policy!.ManagementShares!.Health);

        string without = withShares[..withShares.IndexOf(",\n \"managementShares\"", StringComparison.Ordinal)] + "}";
        var p = ReplyParser.Parse(without);
        Assert.True(p.Ok, string.Join("; ", p.Errors));
        Assert.Null(p.Policy!.ManagementShares);
    }

    [Fact]
    public void TheReportShowsSectorsAndUnits()
    {
        var c = Repo.LoadPortugal();
        string? prompt = null;
        var ai = new SingleAi(new ScriptedModel(p => { prompt ??= p; return "no json"; }), "mandate");
        Fixtures.RunFlat(c with { Government = c.Government with { HorizonYears = 1 } }, ai);
        Assert.Contains("## Sectors", prompt);
        Assert.Contains("0.42 means 42%", prompt);
        Assert.Contains("\"managementShares\"", prompt);
    }
}
