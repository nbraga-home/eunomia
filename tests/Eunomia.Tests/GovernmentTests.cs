using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Governments;

namespace Eunomia.Tests;

public class GovernmentTests
{
    private static BaselineConfig Baseline => ConfigLoader.LoadFile<BaselineConfig>(Path.Combine(Repo.Config, "baseline.json"));

    [Fact]
    public void StatusQuoTiltsTowardTransfersInElectionYearsAndUndoesIt()
    {
        var c = Repo.LoadPortugal();
        var run = Fixtures.RunFlat(c, new StatusQuo(Baseline.StatusQuo));
        int election = c.Pack.FirstElectionYear;
        double start = c.Pack.Policy.BudgetShares.SocialTransfers;

        Assert.Equal(start + Baseline.StatusQuo.ElectionShareShift, run.Years[election].Policy.BudgetShares.SocialTransfers, 9);
        Assert.Equal(start, run.Years[election + 1].Policy.BudgetShares.SocialTransfers, 9);
        Assert.All(run.Decisions, d => Assert.False(d.CarriedOver));
    }

    [Fact]
    public void TechnocratStopsCuttingTransfersAtItsFloor()
    {
        var c = Repo.LoadPortugal();
        var p = Baseline.Technocrat;
        var run = Fixtures.RunFlat(c, new Technocrat(p));
        double floor = p.TransfersFloorShare * c.Pack.Policy.BudgetShares.SocialTransfers;

        Assert.Equal(floor, run.Years[^1].Policy.BudgetShares.SocialTransfers, 9);
        Assert.True(run.Years[^1].Policy.BudgetShares.Infrastructure > c.Pack.Policy.BudgetShares.Infrastructure);
        Assert.All(run.Decisions, d => Assert.False(d.CarriedOver));
    }

    [Fact]
    public void ReturnsProbeAddsTheExtraToOneLineOnly()
    {
        var c = Repo.LoadPortugal();
        var frozen = Fixtures.RunFlat(c, new Frozen());
        var probe = Fixtures.RunFlat(c, new ReturnsProbe(BudgetLine.Education, 0.01));
        var y0p = probe.Years[0];
        var y0f = frozen.Years[0];

        double extra = 0.01 * frozen.Opening.Output;
        Assert.Equal(y0f.Paid[(int)BudgetLine.Education] + extra, y0p.Paid[(int)BudgetLine.Education], 1.0);
        Assert.Equal(y0f.Paid[(int)BudgetLine.Health], y0p.Paid[(int)BudgetLine.Health], 1.0);
        Assert.False(probe.Decisions[0].Scaled);
    }

    [Fact]
    public void EveryOccupantCanBeCreatedByName()
    {
        foreach (string name in Occupants.Names)
            Assert.Equal(name, Occupants.Create(name, new OccupantSettings(Baseline, Repo.Config)).Name);
    }
}
