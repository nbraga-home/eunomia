using System.Globalization;
using System.Text;
using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Core.World;
using Eunomia.Governments;

namespace Eunomia.Cli;

public static class Commands
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(Args a)
    {
        var (config, settings) = LoadConfig(a);
        string set = a.Get("set", "central");
        var government = Occupants.Create(a.Get("occupant", "frozen"), settings);
        ulong? seed = a.Has("flat") ? null : ulong.Parse(a.Get("seed", "1"));
        var feed = Feed(config, seed);

        var run = new Simulation(config, set, feed).Run(government);
        string label = seed is null ? "flat" : $"s{seed}";
        string outDir = a.Get("out", Path.Combine(RepoRoot(), "runs", $"{government.Name}-{set}-{label}"));
        RunWriter.Write(outDir, run, feed, seed);
        if (government is Eunomia.Governments.Ai.ITranscribed ai)
            AiWriter.Write(outDir, run, ai, config.Pack.BaseYear, $"{government.Name} ({settings.Model}) / {set} / {label}");

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

    /// <summary>Two occupants in the same worlds. Each pair shares a seed, so world shocks cancel.</summary>
    public static int Pair(Args a)
    {
        var (config, settings) = LoadConfig(a);
        string nameA = a.Get("a", "technocrat"), nameB = a.Get("b", "statusquo");
        var seeds = a.GetSeeds("seeds", "1-20");
        var sets = a.GetList("sets", "central,lowReturns,highReturns");

        var csv = new StringBuilder("set,seed,outcome,aStatus,bStatus,aFinal,bFinal,aWorst,bWorst,finalDiff,worstDiff,aBreaches,bBreaches\n");
        Console.WriteLine($"{nameA} (A) against {nameB} (B), {seeds.Count} worlds per assumption set\n");
        Console.WriteLine($"{"set",-12} {"A wins",7} {"B wins",7} {"trade-off",10} {"both breached",14}   final median A vs B: {"min",7} {"median",7} {"max",7}");

        foreach (string set in sets)
        {
            var outcomes = new List<Outcome>();
            var finalDiffs = new List<double>();
            foreach (ulong seed in seeds)
            {
                var feed = Feed(config, seed);
                var ra = new Simulation(config, set, feed).Run(Occupants.Create(nameA, settings)).Summary;
                var rb = new Simulation(config, set, feed).Run(Occupants.Create(nameB, settings)).Summary;
                var outcome = Judge.Compare(ra, rb, config.Government.ScoreTolerance);
                double fd = ra.FinalMedian / rb.FinalMedian - 1, wd = ra.WorstMedian / rb.WorstMedian - 1;
                outcomes.Add(outcome);
                finalDiffs.Add(fd);
                csv.Append(string.Join(',', set, seed, outcome, ra.Status, rb.Status,
                    F(ra.FinalMedian), F(rb.FinalMedian), F(ra.WorstMedian), F(rb.WorstMedian), F(fd), F(wd),
                    Quote(Breaches(ra)), Quote(Breaches(rb)))).Append('\n');
            }
            finalDiffs.Sort();
            Console.WriteLine($"{set,-12} {outcomes.Count(o => o == Outcome.AWins),7} {outcomes.Count(o => o == Outcome.BWins),7} " +
                $"{outcomes.Count(o => o == Outcome.TradeOff),10} {outcomes.Count(o => o == Outcome.BothBreached),14}   " +
                $"{"",21}{Pct1(finalDiffs[0]),7} {Pct1(finalDiffs[finalDiffs.Count / 2]),7} {Pct1(finalDiffs[^1]),7}");
        }

        string outPath = a.Get("out", Path.Combine(RepoRoot(), "runs", $"pair-{nameA}-{nameB}.csv"));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, csv.ToString());
        Console.WriteLine($"\nUnvalidated: results hold inside this model only. Details: {outPath}");
        return 0;
    }

    /// <summary>
    /// The returns table: for each budget line, the effect of spending an extra 1% of output on it every year,
    /// paid for by borrowing, compared with the frozen government in a calm world.
    /// </summary>
    public static int Returns(Args a)
    {
        var (config, _) = LoadConfig(a);
        var sets = a.GetList("sets", "central,lowReturns,highReturns");
        double extra = double.Parse(a.Get("extra", "0.01"), Inv);
        int horizon = config.Government.HorizonYears;
        int[] checkpoints = new[] { 5, 10, 20, 30, 40 }.Where(y => y < horizon).Append(horizon).Distinct().ToArray();
        var feed = Feed(config, null);

        var csv = new StringBuilder("set,line,year,outputDiff,medianDiff,lowestFifthDiff,debtRatioDiff\n");
        Console.WriteLine($"Extra {extra:P0} of output spent on one line every year, paid for by borrowing, against frozen policy in a calm world.");
        Console.WriteLine("Each cell: change in output / change in median household income, at that year.\n");

        foreach (string set in sets)
        {
            var frozen = new Simulation(config, set, feed).Run(new Frozen());
            Console.WriteLine($"[{set}]");
            Console.WriteLine($"  {"line",-16}" + string.Concat(checkpoints.Select(y => $"{"year " + y,18}")) + $"{"debt ratio end",16}  {"rating end",-11}floors breached (first year)");
            foreach (var line in BudgetLines.All)
            {
                var probe = new Simulation(config, set, feed).Run(new ReturnsProbe(line, extra));
                var sb = new StringBuilder($"  {line,-16}");
                foreach (int y in checkpoints)
                {
                    var p = probe.Years[y - 1];
                    var f = frozen.Years[y - 1];
                    double od = p.Output / f.Output - 1, md = p.MedianIncome / f.MedianIncome - 1;
                    sb.Append($"{Pct(od),9}/{Pct(md),-8}");
                    csv.Append(string.Join(',', set, line, y, F(od), F(md), F(p.GroupIncome[0] / f.GroupIncome[0] - 1), F(p.DebtRatio - f.DebtRatio))).Append('\n');
                }
                sb.Append($"{probe.Years[^1].DebtRatio - frozen.Years[^1].DebtRatio,16:+0.0%;-0.0%}  {probe.Years[^1].Rating,-11}");
                sb.Append(FirstBreaches(probe.Summary));
                Console.WriteLine(sb.ToString());
            }
            Console.WriteLine($"  {"(frozen)",-16}{"",54}{frozen.Years[^1].DebtRatio,16:0.0%}  {frozen.Years[^1].Rating,-11}{FirstBreaches(frozen.Summary)}");
            Console.WriteLine();
        }

        string outPath = a.Get("out", Path.Combine(RepoRoot(), "runs", "returns.csv"));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, csv.ToString());
        Console.WriteLine($"Details: {outPath}");
        return 0;
    }

    /// <summary>Each breached floor once, with the year it was first breached.</summary>
    private static string FirstBreaches(RunSummary s) => s.Breaches.Count == 0 ? "none" :
        string.Join(", ", s.Breaches.GroupBy(b => b.Floor).Select(g => $"{g.Key} y{g.Min(b => b.Year) + 1}"));

    private static string Breaches(RunSummary s) => string.Join("; ", s.Breaches.Select(b => $"{b.Floor} y{b.Year}"));
    private static string Quote(string s) => s.Length == 0 ? "" : $"\"{s}\"";
    private static string Pct(double x) => Math.Abs(x) < 0.00005 ? "0.00%" : x.ToString("+0.00%;-0.00%", Inv);
    private static string Pct1(double x) => Math.Abs(x) < 0.0005 ? "0.0%" : x.ToString("+0.0%;-0.0%", Inv);
    private static string F(double x) => x.ToString("G6", Inv);

    internal static IReadOnlyList<FeedRow> Feed(SimulationConfig config, ulong? seed) =>
        seed is null
            ? WorldGenerator.Flat(config.World, config.Government.HorizonYears)
            : WorldGenerator.Generate(config.World, seed.Value, config.Government.HorizonYears);

    internal static (SimulationConfig Config, OccupantSettings Settings) LoadConfig(Args a)
    {
        string configDir = a.Get("config", Path.Combine(RepoRoot(), "config"));
        var config = ConfigLoader.Load(configDir, a.Get("pack", Path.Combine(RepoRoot(), "packs", "portugal")));
        if (a.Has("horizon"))
            config = config with { Government = config.Government with { HorizonYears = int.Parse(a.Get("horizon", "40")) } };
        var baseline = ConfigLoader.LoadFile<BaselineConfig>(Path.Combine(configDir, "baseline.json"));
        var settings = new OccupantSettings(baseline, configDir, a.Get("model", "claude-haiku"), Log: Console.Error.WriteLine);
        return (config, settings);
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Eunomia.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
