using System.Text.Json;
using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Core.World;
using Eunomia.Governments;

namespace Eunomia.Tests;

public class ReplayTests
{
    [Fact]
    public void ReplayingRecordedDecisionsReproducesTheRun()
    {
        var c = Repo.LoadPortugal();
        var feed = WorldGenerator.Generate(c.World, 4, c.Government.HorizonYears);
        var baseline = ConfigLoader.LoadFile<BaselineConfig>(Path.Combine(Repo.Config, "baseline.json"));
        var original = new Simulation(c, "central", feed).Run(new Technocrat(baseline.Technocrat));

        string dir = Directory.CreateTempSubdirectory("eunomia-replay-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "decisions.json"), JsonSerializer.Serialize(original.Decisions, ConfigLoader.Options));
            var replay = new Simulation(c, "central", feed).Run(new ReplayDecisions(dir));
            Assert.Equal(original.Years.Select(y => y.MedianIncome), replay.Years.Select(y => y.MedianIncome));
            Assert.Equal(original.Years.Select(y => y.Debt), replay.Years.Select(y => y.Debt));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}

public class EndStateTests
{
    [Fact]
    public void AStockBuiltUpAndLeftToDriftTowardTheThresholdIsNotABreach()
    {
        // Twenty years of extra administration, then back to the starting split: capacity ends above 1
        // and drifts down, but committed spending still holds it above the threshold.
        var c = Repo.LoadPortugal();
        var g = new Scripted("buildup", ctx =>
        {
            if (ctx.Year >= 20) return ctx.StartingPolicy;
            var s = ctx.StartingPolicy.BudgetShares.ToArray();
            s[(int)BudgetLine.SocialTransfers] -= 0.04;
            s[(int)BudgetLine.Administration] += 0.04;
            return ctx.StartingPolicy with { BudgetShares = BudgetShares.FromArray(s) };
        });
        var run = Fixtures.RunFlat(c, g);
        Assert.True(run.Years[^1].StateCapacity > 1.0);
        Assert.DoesNotContain(run.Summary.Breaches, b => b.Floor.StartsWith("endState"));
    }
}
