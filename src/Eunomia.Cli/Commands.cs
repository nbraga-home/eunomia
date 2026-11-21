using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Core.World;
using Eunomia.Governments;

namespace Eunomia.Cli;

public static class Commands
{
    public static int Run(Args a)
    {
        var config = LoadConfig(a);
        string set = a.Get("set", "central");
        var government = Occupants.Create(a.Get("occupant", "frozen"));
        ulong? seed = a.Has("flat") ? null : ulong.Parse(a.Get("seed", "1"));
        var feed = Feed(config, seed);

        var run = new Simulation(config, set, feed).Run(government);
        string label = seed is null ? "flat" : $"s{seed}";
        string outDir = a.Get("out", Path.Combine(RepoRoot(), "runs", $"{government.Name}-{set}-{label}"));
        RunWriter.Write(outDir, run, feed, seed);

        var s = run.Summary;
        string breaches = s.Breaches.Count == 0 ? "" : ": " + string.Join("; ", s.Breaches.Take(5).Select(b => $"{b.Floor} y{b.Year} ({b.Detail})"));
        Console.WriteLine($"{government.Name} / {set} / {(seed is null ? "flat world" : $"seed {seed}")}");
        Console.WriteLine($"  status        {s.Status}{breaches}");
        Console.WriteLine($"  median        start {s.StartMedian:N0}  final {s.FinalMedian:N0}  worst {s.WorstMedian:N0}  (real euros per household per year)");
        Console.WriteLine($"  lowest fifth  start {s.StartLowestFifth:N0}  final {s.FinalLowestFifth:N0}");
        Console.WriteLine($"  output        start {s.StartOutput / 1e9:N1}bn  final {s.FinalOutput / 1e9:N1}bn");
        Console.WriteLine($"  debt ratio    start {run.Opening.DebtRatio:P1}  final {s.FinalDebtRatio:P1}");
        Console.WriteLine($"  written to    {outDir}");
        return 0;
    }

    public static int Pair(Args a) => throw new ArgumentException("pair is not built yet.");

    public static int Returns(Args a) => throw new ArgumentException("returns is not built yet.");

    internal static IReadOnlyList<FeedRow> Feed(SimulationConfig config, ulong? seed) =>
        seed is null
            ? WorldGenerator.Flat(config.World, config.Government.HorizonYears)
            : WorldGenerator.Generate(config.World, seed.Value, config.Government.HorizonYears);

    internal static SimulationConfig LoadConfig(Args a) => ConfigLoader.Load(
        a.Get("config", Path.Combine(RepoRoot(), "config")),
        a.Get("pack", Path.Combine(RepoRoot(), "packs", "portugal")));

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Eunomia.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
