using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eunomia.Core.Config;

/// <summary>Everything a run needs besides the seed and the government.</summary>
public sealed record SimulationConfig(
    EconomyConfig Economy,
    WorldConfig World,
    GovernmentConfig Government,
    CountryPack Pack,
    IReadOnlyDictionary<string, ParameterSet> ParameterSets);

public static class ConfigLoader
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static T LoadFile<T>(string path)
    {
        string json = File.ReadAllText(path);
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                ?? throw new InvalidDataException($"{path} is empty.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{path}: {e.Message}", e);
        }
    }

    /// <summary>Loads config/*.json from <paramref name="configDir"/> and the pack from <paramref name="packDir"/>.</summary>
    public static SimulationConfig Load(string configDir, string packDir) => new(
        LoadFile<EconomyConfig>(Path.Combine(configDir, "economy.json")),
        LoadFile<WorldConfig>(Path.Combine(configDir, "world.json")),
        LoadFile<GovernmentConfig>(Path.Combine(configDir, "government.json")),
        LoadFile<CountryPack>(Path.Combine(packDir, "country.json")),
        LoadFile<Dictionary<string, ParameterSet>>(Path.Combine(configDir, "parameter-sets.json")));

    /// <summary>The economy config with a named assumption set applied.</summary>
    public static EconomyConfig Economy(this SimulationConfig config, string parameterSet)
    {
        if (!config.ParameterSets.TryGetValue(parameterSet, out var set))
            throw new ArgumentException($"Unknown parameter set '{parameterSet}'. Known: {string.Join(", ", config.ParameterSets.Keys)}");
        var economy = config.Economy.WithReturnsMultiplier(set.ReturnsMultiplier);
        if (set.EducationSpendingElasticity is double e)
            economy = economy with { Education = economy.Education with { SpendingElasticity = e } };
        if (set.PrivateEfficiency is double pe)
            economy = economy with { EducationRegimes = economy.EducationRegimes with { PrivateEfficiency = pe } };
        if (set.SelectiveQualityGain is double sg)
            economy = economy with { EducationRegimes = economy.EducationRegimes with { SelectiveQualityGain = sg } };
        return economy;
    }
}
