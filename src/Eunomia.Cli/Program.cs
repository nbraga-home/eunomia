using System.Globalization;
using Eunomia.Cli;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

const string Help = """
    eunomia <command> [options]

      run      --occupant frozen|statusquo|technocrat|singleai|threebranch  --seed 1 | --flat  --set central  --model claude-haiku  --out runs/name
               threebranch takes --models exec,legislature,judiciary  (default: all --model)
      run      --occupant replay --from runs/<folder> --seed 1   (re-enact recorded decisions, no model calls)
      pair     --a technocrat --b statusquo --seeds 1-20 --sets central,lowReturns,highReturns
      returns  --sets central,lowReturns,highReturns
      regimes  --seeds 1-10 --sets central,lowReturns,highReturns --base frozen [--resistance]   (search education regimes, no model calls)
      compare  --runs runs/singleai-haiku-s{seed} --seeds 1-10 --b technocrat --set central   (replays recorded runs, no model calls)

    Common: --config <dir> --pack <dir> --horizon <years>   (default: the repository's config and packs/portugal)
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Help);
    return 0;
}

try
{
    var options = new Args(args.Skip(1));
    return args[0] switch
    {
        "run" => Commands.Run(options),
        "pair" => Commands.Pair(options),
        "returns" => Commands.Returns(options),
        "compare" => Commands.Compare(options),
        "regimes" => Commands.Regimes(options),
        _ => throw new ArgumentException($"Unknown command '{args[0]}'."),
    };
}
catch (Exception e) when (e is ArgumentException or InvalidDataException or FileNotFoundException)
{
    Console.Error.WriteLine($"error: {e.Message}");
    return 1;
}
