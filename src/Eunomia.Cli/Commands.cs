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
        string occupant = a.Get("occupant", "frozen");
        ulong? seed = a.Has("flat") ? null : ulong.Parse(a.Get("seed", "1"));
        string label = seed is null ? "flat" : $"s{seed}";
        string outDir = a.Get("out", Path.Combine(RepoRoot(), "runs", $"{occupant}-{set}-{label}"));
        Directory.CreateDirectory(outDir);

        // Progress goes to the console and to progress.log, so a long AI run can be followed while it runs.
        string progress = Path.Combine(outDir, "progress.log");
        File.WriteAllText(progress, "");
        settings = settings with { Log = line => { Console.Error.WriteLine(line); File.AppendAllText(progress, line + "\n"); } };

        var government = occupant == "replay"
            ? new ReplayDecisions(a.Get("from", ""))
            : Occupants.Create(occupant, settings);
        var feed = Feed(config, seed);

        var run = new Simulation(config, set, feed).Run(government);
        RunWriter.Write(outDir, run, feed, seed);
        if (government is Eunomia.Governments.Ai.ITranscribed ai)
            AiWriter.Write(outDir, run, ai, config.Pack.BaseYear, $"{government.Name} ({(settings.BranchModels is null ? settings.Model : string.Join(", ", settings.BranchModels))}) / {set} / {label}");

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
    /// The regime search: every combination of education regime choices, on one scripted yearly budget, across
    /// many worlds and every named set. Each regime is paired with the starting regime in the same world, so
    /// shocks cancel. Reports what is best inside the model under each set, and whether the answer flips.
    /// </summary>
    public static int Regimes(Args a)
    {
        var (config, settings) = LoadConfig(a);
        if (a.Has("resistance"))
            config = config with { Government = config.Government with { Resistance = config.Government.Resistance with { Enabled = true } } };
        string baseName = a.Get("base", "frozen");
        var seeds = a.GetSeeds("seeds", "1-10");
        var sets = a.GetList("sets", "central,lowReturns,highReturns");
        var start = config.Pack.Policy.EducationRegime!;
        var regimes = RegimeFixed.All().ToList();

        var csv = new StringBuilder("set,regime,seed,status,finalMedian,worstMedian,finalPoorest,outcomeVsStart,breaches\n");
        Console.WriteLine($"Education regimes on the {baseName} budget, {seeds.Count} worlds per set, against Portugal's regime today ({RegimeFixed.Code(start)}).");
        Console.WriteLine($"Resistance {(config.Government.Resistance.Enabled ? "on" : "off")}. Changes against today's regime are averages over worlds; W/L/T are win rule outcomes against it.\n");

        var winners = new Dictionary<string, string>();
        foreach (string set in sets)
        {
            var rows = new List<(EducationRegime Regime, double Final, double Worst, double Poorest, int W, int L, int T, int Breached)>();
            var baseline = seeds.ToDictionary(s => s, s =>
                new Simulation(config, set, Feed(config, s)).Run(new RegimeFixed(Occupants.Create(baseName, settings), start)).Summary);
            foreach (var regime in regimes)
            {
                double final = 0, worst = 0, poorest = 0;
                int w = 0, l = 0, t = 0, breached = 0;
                foreach (ulong seed in seeds)
                {
                    var s = new Simulation(config, set, Feed(config, seed)).Run(new RegimeFixed(Occupants.Create(baseName, settings), regime)).Summary;
                    var b = baseline[seed];
                    final += s.FinalMedian / b.FinalMedian - 1;
                    worst += s.WorstMedian / b.WorstMedian - 1;
                    poorest += s.FinalLowestFifth / b.FinalLowestFifth - 1;
                    var o = Judge.Compare(s, b, config.Government.ScoreTolerance);
                    if (o == Outcome.AWins) w++; else if (o == Outcome.BWins) l++; else if (o == Outcome.TradeOff) t++;
                    if (s.Status == RunStatus.Breached) breached++;
                    csv.Append(string.Join(',', set, RegimeFixed.Code(regime), seed, s.Status, F(s.FinalMedian), F(s.WorstMedian), F(s.FinalLowestFifth), o,
                        Quote(string.Join("; ", s.Breaches.Select(x => x.Floor).Distinct())))).Append('\n');
                }
                int n = seeds.Count;
                rows.Add((regime, final / n, worst / n, poorest / n, w, l, t, breached));
            }

            var ranked = rows.OrderByDescending(r => r.Breached == 0).ThenByDescending(r => r.W).ThenByDescending(r => r.Final).ToList();
            winners[set] = RegimeFixed.Code(ranked[0].Regime);
            Console.WriteLine($"[{set}]  top 8 of {rows.Count}, clean regimes first, then by wins under the win rule, then by final median");
            Console.WriteLine($"  {"regime (provision/fees/support/standards)",-46} {"final",7} {"worst",7} {"poorest",8}  {"W/L/T",9}  breached");
            foreach (var r in ranked.Take(8))
                Console.WriteLine($"  {RegimeFixed.Code(r.Regime),-46} {Pct(r.Final),7} {Pct(r.Worst),7} {Pct(r.Poorest),8}  {$"{r.W}/{r.L}/{r.T}",9}  {r.Breached}/{seeds.Count}");
            var worstRow = ranked[^1];
            Console.WriteLine($"  {"… last: " + RegimeFixed.Code(worstRow.Regime),-46} {Pct(worstRow.Final),7} {Pct(worstRow.Worst),7} {Pct(worstRow.Poorest),8}  {$"{worstRow.W}/{worstRow.L}/{worstRow.T}",9}  {worstRow.Breached}/{seeds.Count}\n");
        }

        bool flips = winners.Values.Distinct().Count() > 1;
        Console.WriteLine(flips
            ? $"The best regime flips between sets: {string.Join("; ", winners.Select(w => $"{w.Key} → {w.Value}"))}. The answer depends on the assumptions."
            : $"The same regime is best under every set: {winners.Values.First()}.");
        string outPath = a.Get("out", Path.Combine(RepoRoot(), "runs", "regimes.csv"));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, csv.ToString());
        Console.WriteLine($"Best inside the model, under these assumptions; unvalidated. Details: {outPath}");
        return 0;
    }

    /// <summary>
    /// Scores recorded AI runs against a scripted government in the same worlds. Each recorded run is replayed
    /// (no model calls), so the comparison uses the current rules. Missing folders are reported, never dropped.
    /// </summary>
    public static int Compare(Args a)
    {
        var (config, settings) = LoadConfig(a);
        string pattern = a.Get("runs", "runs/singleai-haiku-s{seed}");
        string nameB = a.Get("b", "technocrat");
        string set = a.Get("set", "central");
        var seeds = a.GetSeeds("seeds", "1-10");

        Console.WriteLine($"{pattern} (A, replayed) against {nameB} (B), set {set}\n");
        Console.WriteLine($"{"seed",4}  {"A status",-9} {"B status",-9} {"outcome",-13} {"final A",9} {"final B",9} {"worst A",9} {"worst B",9}  {"poorest A",9} {"poorest B",9}");
        var outcomes = new List<Outcome>();
        int missing = 0;
        foreach (ulong seed in seeds)
        {
            string dir = Path.Combine(RepoRoot(), pattern.Replace("{seed}", seed.ToString()));
            if (!File.Exists(Path.Combine(dir, "decisions.json")) || !File.Exists(Path.Combine(dir, "summary.json")))
            {
                Console.WriteLine($"{seed,4}  incomplete: no finished run in {dir}");
                missing++;
                continue;
            }
            var feed = Feed(config, seed);
            var ra = new Simulation(config, set, feed).Run(new ReplayDecisions(dir)).Summary;
            var rb = new Simulation(config, set, feed).Run(Occupants.Create(nameB, settings)).Summary;
            var o = Judge.Compare(ra, rb, config.Government.ScoreTolerance);
            outcomes.Add(o);
            Console.WriteLine($"{seed,4}  {ra.Status,-9} {rb.Status,-9} {o,-13} {ra.FinalMedian,9:N0} {rb.FinalMedian,9:N0} {ra.WorstMedian,9:N0} {rb.WorstMedian,9:N0}  {ra.FinalLowestFifth,9:N0} {rb.FinalLowestFifth,9:N0}");
        }
        Console.WriteLine($"\nA wins {outcomes.Count(o => o == Outcome.AWins)}, B wins {outcomes.Count(o => o == Outcome.BWins)}, " +
            $"trade-off {outcomes.Count(o => o == Outcome.TradeOff)}, both breached {outcomes.Count(o => o == Outcome.BothBreached)}, incomplete {missing}. Unvalidated.");
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
        var branchModels = a.Has("models") ? a.GetList("models", "") : null;
        var settings = new OccupantSettings(baseline, configDir, a.Get("model", "claude-haiku"), Log: Console.Error.WriteLine, BranchModels: branchModels);
        return (config, settings);
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Eunomia.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
