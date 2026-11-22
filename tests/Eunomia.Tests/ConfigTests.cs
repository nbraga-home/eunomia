using Eunomia.Core;
using Eunomia.Core.Config;

namespace Eunomia.Tests;

public class ConfigTests
{
    [Fact]
    public void RepositoryConfigLoads()
    {
        var c = Repo.LoadPortugal();
        Assert.Equal("Portugal", c.Pack.Name);
        Assert.Equal(40, c.Government.HorizonYears);
        Assert.Equal(6, c.World.Events.Count);
        Assert.Contains("central", c.ParameterSets.Keys);
    }

    [Fact]
    public void StartingBudgetSharesSumToOne()
    {
        var c = Repo.LoadPortugal();
        Assert.Equal(1.0, c.Pack.Policy.BudgetShares.Sum(), 9);
    }

    [Fact]
    public void GroupWeightsSumToOne()
    {
        var g = Repo.LoadPortugal().Pack.Groups;
        Assert.Equal(1.0, g.WageWeights.Sum(), 9);
        Assert.Equal(1.0, g.OwnershipWeights.Sum(), 9);
        Assert.Equal(1.0, g.TransferWeights.Sum(), 9);
    }

    [Fact]
    public void ParameterSetScalesReturns()
    {
        var c = Repo.LoadPortugal();
        Assert.Equal(c.Economy.Stocks.Skills.Exponent * 0.5, c.Economy("lowReturns").Stocks.Skills.Exponent, 12);
    }

    [Fact]
    public void UnknownFieldIsRejected()
    {
        string path = Path.Combine(Path.GetTempPath(), $"eunomia-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "returnsMultiplier": 1.0, "typo": 2 }""");
        try { Assert.Throws<InvalidDataException>(() => ConfigLoader.LoadFile<ParameterSet>(path)); }
        finally { File.Delete(path); }
    }
}
