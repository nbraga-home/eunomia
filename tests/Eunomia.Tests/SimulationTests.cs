using Eunomia.Core;
using Eunomia.Core.Model;
using Eunomia.Core.World;

namespace Eunomia.Tests;

public class SimulationTests
{
    private const double Tight = 1e-9;

    [Fact]
    public void OpeningYearReproducesThePackOutput()
    {
        var c = Repo.LoadPortugal();
        var sim = new Simulation(c, "central", WorldGenerator.Flat(c.World, c.Government.HorizonYears));
        Assert.Equal(c.Pack.Output, sim.Opening.Output, c.Pack.Output * Tight);
        Assert.Equal(c.Pack.Debt / c.Pack.Output, sim.Opening.DebtRatio, 12);
    }

    [Fact]
    public void SteadyStateHoldsForTwentyYears()
    {
        var run = Fixtures.RunFlat(Fixtures.Stationary(), Fixtures.Frozen);
        foreach (var y in run.Years)
        {
            Assert.Equal(run.Opening.Output, y.Output, run.Opening.Output * Tight);
            Assert.Equal(run.Opening.MedianIncome, y.MedianIncome, run.Opening.MedianIncome * Tight);
            foreach (double stock in new[] { y.Skills, y.Health, y.Infrastructure, y.FarmProductivity, y.StateCapacity, y.Stability, y.PrivateCapital })
                Assert.Equal(1.0, stock, 9);
            Assert.Equal(0.0, y.Debt, run.Opening.Output * Tight);
        }
        Assert.Equal(RunStatus.Clean, run.Summary.Status);
    }

    [Fact]
    public void SameSeedGivesIdenticalRuns()
    {
        var c = Repo.LoadPortugal();
        RunResult Once() => new Simulation(c, "central", WorldGenerator.Generate(c.World, 9, c.Government.HorizonYears)).Run(Fixtures.Frozen);
        var a = Once();
        var b = Once();
        Assert.Equal(a.Years.Select(y => y.MedianIncome), b.Years.Select(y => y.MedianIncome));
        Assert.Equal(a.Years.Select(y => y.Debt), b.Years.Select(y => y.Debt));
    }

    [Fact]
    public void EducationRaisesSkillsAfterItsDelayAndNotBefore()
    {
        var c = Fixtures.Stationary();
        var frozen = Fixtures.RunFlat(c, Fixtures.Frozen);
        var boosted = Fixtures.RunFlat(c, Fixtures.Shift(BudgetLine.SocialTransfers, BudgetLine.Education, 0.02));
        int delay = c.Economy.Stocks.Skills.DelayYears;

        // Year-0 spending enters the stock at the end of year `delay`, so it first shows in year delay + 1.
        for (int y = 0; y <= delay; y++) Assert.Equal(frozen.Years[y].Skills, boosted.Years[y].Skills, 12);
        Assert.True(boosted.Years[delay + 1].Skills > frozen.Years[delay + 1].Skills);
        Assert.True(boosted.Years[^1].Output > frozen.Years[^1].Output);
    }

    [Fact]
    public void LessSecurityLowersStabilityAndOutput()
    {
        var c = Fixtures.Stationary();
        var frozen = Fixtures.RunFlat(c, Fixtures.Frozen);
        var cut = Fixtures.RunFlat(c, Fixtures.Shift(BudgetLine.Security, BudgetLine.SocialTransfers, 0.03));
        Assert.True(cut.Years[5].Stability < 1);
        Assert.True(cut.Years[5].Output < frozen.Years[5].Output);
    }

    [Fact]
    public void AgricultureRaisesFarmProductivity()
    {
        var c = Fixtures.Stationary();
        var run = Fixtures.RunFlat(c, Fixtures.Shift(BudgetLine.SocialTransfers, BudgetLine.Agriculture, 0.01));
        Assert.True(run.Years[^1].FarmProductivity > 1);
    }

    [Fact]
    public void AHigherTaxRateShrinksItsBase()
    {
        var c = Fixtures.Stationary();
        var frozen = Fixtures.RunFlat(c, Fixtures.Frozen);
        var higher = Fixtures.RunFlat(c, new Scripted("tax", ctx => ctx.StartingPolicy with
        {
            Taxes = ctx.StartingPolicy.Taxes with { Income = ctx.StartingPolicy.Taxes.Income + 0.03 },
        }));
        double t0 = c.Pack.Policy.Taxes.Income;
        double proportional = frozen.Years[0].RevenueIncome * (t0 + 0.03) / t0;
        Assert.True(higher.Years[0].RevenueIncome > frozen.Years[0].RevenueIncome);
        Assert.True(higher.Years[0].RevenueIncome < proportional);
    }

    [Fact]
    public void InflationShrinksRealDebt()
    {
        var c = Fixtures.Stationary();
        c = c with { Economy = c.Economy with { Inflation = 0.02 }, Pack = c.Pack with { Debt = 100e9 } };
        var run = Fixtures.RunFlat(c, Fixtures.Frozen);
        var y0 = run.Years[0];
        Assert.Equal((c.Pack.Debt + y0.Deficit) / 1.02, y0.Debt, 1.0);
        Assert.Equal(c.Pack.EffectiveRate * c.Pack.Debt, y0.Interest, 1.0);
    }

    [Fact]
    public void InvalidProposalsAreRejectedAndCarriedOver()
    {
        var c = Repo.LoadPortugal();
        var bad = new Scripted("bad", ctx => ctx.StartingPolicy with
        {
            BudgetShares = ctx.StartingPolicy.BudgetShares with { Health = -0.1, SocialTransfers = ctx.StartingPolicy.BudgetShares.SocialTransfers + 0.1 + ctx.StartingPolicy.BudgetShares.Health },
        });
        var run = Fixtures.RunFlat(c, bad);
        Assert.All(run.Decisions, d => Assert.True(d.CarriedOver));
        Assert.Contains(run.Decisions[0].Errors, e => e.Contains("Health"));
    }

    [Fact]
    public void TaxCanOnlyMoveSoFarInAYear()
    {
        var c = Repo.LoadPortugal();
        var sim = new Simulation(c, "central", WorldGenerator.Flat(c.World, c.Government.HorizonYears));
        var p = c.Pack.Policy;
        var result = sim.Check(p with { Taxes = p.Taxes with { Income = p.Taxes.Income + 0.05 } });
        Assert.False(result.Ok);
    }

    [Fact]
    public void LargeReallocationIsScaledToTheAllowance()
    {
        var c = Repo.LoadPortugal();
        var sim = new Simulation(c, "central", WorldGenerator.Flat(c.World, c.Government.HorizonYears));
        var p = c.Pack.Policy;
        var s = p.BudgetShares.ToArray();
        s[(int)BudgetLine.SocialTransfers] -= 0.20;
        s[(int)BudgetLine.Education] += 0.20;
        var result = sim.Check(p with { BudgetShares = BudgetShares.FromArray(s) });

        Assert.True(result.Ok);
        Assert.True(result.Scaled);
        double moved = result.Feasible!.BudgetShares.Education - p.BudgetShares.Education;
        Assert.Equal(c.Government.ReallocationAllowance, moved, 9); // capacity is 1 at the start
        Assert.Equal(1.0, result.Feasible.BudgetShares.Sum(), 9);
    }

    [Fact]
    public void CuttingEducationBreachesTheEndStateChecks()
    {
        var c = Repo.LoadPortugal();
        var cut = new Scripted("cut", ctx =>
        {
            var s = ctx.Enacted.BudgetShares.ToArray();
            double move = Math.Min(0.05, s[(int)BudgetLine.Education]);
            s[(int)BudgetLine.Education] -= move;
            s[(int)BudgetLine.SocialTransfers] += move;
            return ctx.Enacted with { BudgetShares = BudgetShares.FromArray(s) };
        });
        var run = Fixtures.RunFlat(c, cut);
        Assert.Equal(RunStatus.Breached, run.Summary.Status);
        Assert.Contains(run.Summary.Breaches, b => b.Floor == "endStatePipelines" && b.Detail.StartsWith("skills"));
    }

    [Fact]
    public void ABorrowingSpreeBreachesTheDebtFloor()
    {
        var c = Repo.LoadPortugal();
        var spree = new Scripted("spree", ctx => ctx.StartingPolicy with { PlannedDeficit = c.Government.DeficitBounds.Max });
        var run = Fixtures.RunFlat(c, spree);
        Assert.Contains(run.Summary.Breaches, b => b.Floor == "debt");
    }
}
