using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Governments.Ai;

namespace Eunomia.Governments;

/// <summary>What occupants may need to be built: scripted numbers, and where mandates and model settings live.</summary>
public sealed record OccupantSettings(BaselineConfig Baseline, string ConfigDir, string Model = "claude-haiku", Action<string>? Log = null);

public static class Occupants
{
    public static IReadOnlyList<string> Names => ["frozen", "statusquo", "technocrat", "singleai"];

    /// <summary>A fresh occupant. Occupants keep state across years, so create one per run.</summary>
    public static IGovernment Create(string name, OccupantSettings s) => name.ToLowerInvariant() switch
    {
        "frozen" => new Frozen(),
        "statusquo" => new StatusQuo(s.Baseline.StatusQuo),
        "technocrat" => new Technocrat(s.Baseline.Technocrat),
        "singleai" => new SingleAi(LoadModel(s), File.ReadAllText(Path.Combine(s.ConfigDir, "mandates", "single.txt"))) { Log = s.Log },
        _ => throw new ArgumentException($"Unknown occupant '{name}'. Known: {string.Join(", ", Names)}"),
    };

    private static IBranchModel LoadModel(OccupantSettings s)
    {
        var models = ConfigLoader.LoadFile<Dictionary<string, CliModelConfig>>(Path.Combine(s.ConfigDir, "models.json"));
        if (!models.TryGetValue(s.Model, out var config))
            throw new ArgumentException($"Unknown model '{s.Model}'. Known: {string.Join(", ", models.Keys)}");
        return new CliModel(config);
    }
}
