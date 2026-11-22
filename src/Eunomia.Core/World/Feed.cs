using Eunomia.Core.Config;
using Eunomia.Core.Random;

namespace Eunomia.Core.World;

/// <summary>One year of the world, as the simulation sees it. Complete before the run starts.</summary>
public sealed record FeedRow
{
    public required int Year { get; init; }
    public required double ExportDemand { get; init; }
    public required double EnergyPrice { get; init; }
    public required double FoodPrice { get; init; }
    public required double ExternalRate { get; init; }
    public required double FinancialStress { get; init; }
    /// <summary>Multiplier on farm output, 1 in a normal year.</summary>
    public required double FarmOutput { get; init; }
    public required IReadOnlyList<string> ActiveEvents { get; init; }
    public required IReadOnlyList<string> StartingEvents { get; init; }
    /// <summary>True when a crisis-flagged event starts this year.</summary>
    public required bool Crisis { get; init; }
    /// <summary>True while any crisis-flagged event is active. The EU escape clause applies.</summary>
    public required bool CrisisActive { get; init; }
}

public static class WorldGenerator
{
    private static readonly string[] SeriesNames = ["exportDemand", "energyPrice", "foodPrice", "externalRate", "financialStress"];

    /// <summary>
    /// Generates the whole world for a run. All randomness of a run happens here;
    /// the simulation itself only reads the rows.
    /// </summary>
    public static IReadOnlyList<FeedRow> Generate(WorldConfig config, ulong seed, int years)
    {
        var processes = new[]
        {
            config.Series.ExportDemand, config.Series.EnergyPrice, config.Series.FoodPrice,
            config.Series.ExternalRate, config.Series.FinancialStress,
        };
        var streams = SeriesNames.Select(n => Rng.ForStream(seed, n)).ToArray();
        var values = processes.Select(p => p.Start).ToArray();
        var eventDraws = Rng.ForStream(seed, "events");
        var durationDraws = Rng.ForStream(seed, "eventDurations");

        var activeUntil = new int[config.Events.Count]; // exclusive end year per event type; 0 = inactive
        double previousStress = config.Series.FinancialStress.Start;
        var rows = new List<FeedRow>(years);

        for (int year = 0; year < years; year++)
        {
            for (int s = 0; s < processes.Length; s++)
                values[s] = Step(processes[s], values[s], streams[s].NextNormal());

            var starting = new List<string>();
            bool crisis = false;
            for (int e = 0; e < config.Events.Count; e++)
            {
                var type = config.Events[e];
                double u = eventDraws.NextDouble(); // always drawn, so the stream stays aligned
                if (activeUntil[e] > year) continue;
                double odds = Math.Min(1.0, type.AnnualOdds * Math.Exp(type.StressCoefficient * previousStress));
                if (u < odds)
                {
                    activeUntil[e] = year + durationDraws.NextInt(type.MinYears, type.MaxYears);
                    starting.Add(type.Name);
                    crisis |= type.Crisis;
                }
            }

            var active = new List<EventType>();
            for (int e = 0; e < config.Events.Count; e++)
                if (activeUntil[e] > year) active.Add(config.Events[e]);

            var row = new FeedRow
            {
                Year = year,
                ExportDemand = Apply(values[0], "exportDemand", active),
                EnergyPrice = Apply(values[1], "energyPrice", active),
                FoodPrice = Apply(values[2], "foodPrice", active),
                ExternalRate = Apply(values[3], "externalRate", active),
                FinancialStress = Apply(values[4], "financialStress", active),
                FarmOutput = Apply(1.0, "farmOutput", active),
                ActiveEvents = active.Select(a => a.Name).ToList(),
                StartingEvents = starting,
                Crisis = crisis,
                CrisisActive = active.Any(a => a.Crisis),
            };
            rows.Add(row);
            previousStress = row.FinancialStress;
        }
        return rows;
    }

    /// <summary>A calm world: every series at its starting value, no events.</summary>
    public static IReadOnlyList<FeedRow> Flat(WorldConfig config, int years) =>
        Enumerable.Range(0, years).Select(y => new FeedRow
        {
            Year = y,
            ExportDemand = config.Series.ExportDemand.Start,
            EnergyPrice = config.Series.EnergyPrice.Start,
            FoodPrice = config.Series.FoodPrice.Start,
            ExternalRate = config.Series.ExternalRate.Start,
            FinancialStress = config.Series.FinancialStress.Start,
            FarmOutput = 1.0,
            ActiveEvents = [],
            StartingEvents = [],
            Crisis = false,
            CrisisActive = false,
        }).ToList();

    private static double Step(SeriesParams p, double x, double epsilon) => p.Kind switch
    {
        "log" => Math.Exp(Math.Log(x) + p.Pull * (Math.Log(p.Mean) - Math.Log(x)) + p.Noise * epsilon),
        "level" => Math.Max(0.0, x + p.Pull * (p.Mean - x) + p.Noise * epsilon),
        _ => throw new InvalidDataException($"Unknown series kind '{p.Kind}'."),
    };

    /// <summary>Events are level shifts while active: factors multiply, additions add.</summary>
    private static double Apply(double value, string series, List<EventType> active)
    {
        double factor = 1.0, add = 0.0;
        foreach (var e in active)
            foreach (var effect in e.Effects)
                if (effect.Series == series) { factor *= effect.Multiply; add += effect.Add; }
        return value * factor + add;
    }
}
