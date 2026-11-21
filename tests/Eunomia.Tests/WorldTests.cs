using Eunomia.Core.Config;
using Eunomia.Core.World;

namespace Eunomia.Tests;

public class WorldTests
{
    private static WorldConfig World => Repo.LoadPortugal().World;

    [Fact]
    public void SameSeedGivesSameWorld()
    {
        var a = WorldGenerator.Generate(World, 11, 20);
        var b = WorldGenerator.Generate(World, 11, 20);
        Assert.Equal(a.Select(r => r.ExportDemand), b.Select(r => r.ExportDemand));
        Assert.Equal(a.SelectMany(r => r.StartingEvents), b.SelectMany(r => r.StartingEvents));
    }

    [Fact]
    public void DifferentSeedsGiveDifferentWorlds()
    {
        var a = WorldGenerator.Generate(World, 1, 20);
        var b = WorldGenerator.Generate(World, 2, 20);
        Assert.NotEqual(a.Select(r => r.EnergyPrice), b.Select(r => r.EnergyPrice));
    }

    [Fact]
    public void FlatWorldIsCalm()
    {
        var rows = WorldGenerator.Flat(World, 20);
        Assert.All(rows, r =>
        {
            Assert.Equal(1.0, r.ExportDemand);
            Assert.Empty(r.ActiveEvents);
            Assert.False(r.Crisis);
        });
    }

    [Fact]
    public void EventIsALevelShiftForItsDuration()
    {
        // A world with no noise and one certain three-year event.
        var calm = new SeriesParams { Start = 1, Mean = 1, Pull = 0, Noise = 0, Kind = "log" };
        var config = new WorldConfig
        {
            Series = new WorldSeries
            {
                ExportDemand = calm, EnergyPrice = calm, FoodPrice = calm,
                ExternalRate = calm with { Start = 0.02, Mean = 0.02, Kind = "level" },
                FinancialStress = calm with { Start = 0, Mean = 0, Kind = "level" },
            },
            Events =
            [
                new EventType
                {
                    Name = "Recession", AnnualOdds = 1.0, StressCoefficient = 0, MinYears = 3, MaxYears = 3, Crisis = true,
                    Effects = [new EventEffect { Series = "exportDemand", Multiply = 0.9 }],
                },
            ],
        };
        var rows = WorldGenerator.Generate(config, 5, 7);

        // Starts in year 0, lasts years 0-2, can start again in year 3.
        Assert.Equal(["Recession"], rows[0].StartingEvents);
        Assert.True(rows[0].Crisis);
        Assert.Equal(0.9, rows[1].ExportDemand, 12);
        Assert.Equal(0.9, rows[2].ExportDemand, 12);
        Assert.Empty(rows[1].StartingEvents);
        Assert.Equal(["Recession"], rows[3].StartingEvents);
    }

    [Fact]
    public void OverlappingEventsMultiply()
    {
        var calm = new SeriesParams { Start = 1, Mean = 1, Pull = 0, Noise = 0, Kind = "log" };
        EventType Certain(string name, double f) => new()
        {
            Name = name, AnnualOdds = 1.0, StressCoefficient = 0, MinYears = 1, MaxYears = 1, Crisis = false,
            Effects = [new EventEffect { Series = "exportDemand", Multiply = f }],
        };
        var config = new WorldConfig
        {
            Series = new WorldSeries
            {
                ExportDemand = calm, EnergyPrice = calm, FoodPrice = calm,
                ExternalRate = calm with { Kind = "level" }, FinancialStress = calm with { Start = 0, Mean = 0, Kind = "level" },
            },
            Events = [Certain("A", 0.9), Certain("B", 0.5)],
        };
        Assert.Equal(0.45, WorldGenerator.Generate(config, 1, 1)[0].ExportDemand, 12);
    }

    [Fact]
    public void EventsHappenAtPlausibleRates()
    {
        // Over many seeded worlds, a global recession should start in roughly 9% of years.
        int starts = 0, years = 0;
        for (ulong seed = 0; seed < 500; seed++)
        {
            var rows = WorldGenerator.Generate(World, seed, 20);
            starts += rows.Count(r => r.StartingEvents.Contains("Global recession"));
            years += rows.Count;
        }
        Assert.InRange(starts / (double)years, 0.05, 0.12);
    }
}
