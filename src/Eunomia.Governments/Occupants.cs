using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Governments.Ai;

namespace Eunomia.Governments;

/// <summary>What occupants may need to be built: scripted numbers, and where mandates and model settings live.</summary>
/// <param name="BranchModels">Executive, legislature, judiciary. When null, all three use <paramref name="Model"/>.</param>
public sealed record OccupantSettings(BaselineConfig Baseline, string ConfigDir, string Model = "claude-haiku", Action<string>? Log = null, IReadOnlyList<string>? BranchModels = null);

public static class Occupants
{
    public static IReadOnlyList<string> Names => ["frozen", "statusquo", "technocrat", "singleai", "threebranch"];

    /// <summary>A fresh occupant. Occupants keep state across years, so create one per run.</summary>
    public static IGovernment Create(string name, OccupantSettings s) => name.ToLowerInvariant() switch
    {
        "frozen" => new Frozen(),
        "statusquo" => new StatusQuo(s.Baseline.StatusQuo),
        "technocrat" => new Technocrat(s.Baseline.Technocrat),
        "singleai" => new SingleAi(LoadModel(s, s.Model), Mandate(s, "single")) { Log = s.Log },
        "threebranch" => ThreeBranch(s),
        _ => throw new ArgumentException($"Unknown occupant '{name}'. Known: {string.Join(", ", Names)}"),
    };

    private static ThreeBranch ThreeBranch(OccupantSettings s)
    {
        var names = s.BranchModels ?? [s.Model, s.Model, s.Model];
        if (names.Count != 3) throw new ArgumentException("Three-branch government needs three models: executive, legislature, judiciary.");
        var models = new BranchModels(LoadModel(s, names[0]), LoadModel(s, names[1]), LoadModel(s, names[2]));
        var mandates = new Mandates(Mandate(s, "preamble"), Mandate(s, "executive"), Mandate(s, "legislature"), Mandate(s, "judiciary"));
        return new ThreeBranch(models, mandates) { Log = s.Log };
    }

    private static string Mandate(OccupantSettings s, string name) =>
        File.ReadAllText(Path.Combine(s.ConfigDir, "mandates", name + ".txt")).Trim();

    private static IBranchModel LoadModel(OccupantSettings s, string name)
    {
        var models = ConfigLoader.LoadFile<Dictionary<string, CliModelConfig>>(Path.Combine(s.ConfigDir, "models.json"));
        if (!models.TryGetValue(name, out var config))
            throw new ArgumentException($"Unknown model '{name}'. Known: {string.Join(", ", models.Keys)}");
        return new CliModel(config);
    }
}
