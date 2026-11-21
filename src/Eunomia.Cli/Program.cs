using System.Globalization;
using Eunomia.Cli;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

const string Help = """
    eunomia <command> [options]

      run      --occupant frozen --seed 1 | --flat  --set central  --out runs/name
      pair     --a technocrat --b statusquo --seeds 1-20 --sets central,lowReturns,highReturns
      returns  --sets central,lowReturns,highReturns

    Common: --config <dir> --pack <dir>   (default: the repository's config and packs/portugal)
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
        _ => throw new ArgumentException($"Unknown command '{args[0]}'."),
    };
}
catch (Exception e) when (e is ArgumentException or InvalidDataException or FileNotFoundException)
{
    Console.Error.WriteLine($"error: {e.Message}");
    return 1;
}
