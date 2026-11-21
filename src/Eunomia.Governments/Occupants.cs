using Eunomia.Core;

namespace Eunomia.Governments;

public static class Occupants
{
    public static IReadOnlyList<string> Names => ["frozen", "statusquo", "technocrat"];

    /// <summary>A fresh occupant. Scripted occupants keep state across years, so create one per run.</summary>
    public static IGovernment Create(string name, BaselineConfig baseline) => name.ToLowerInvariant() switch
    {
        "frozen" => new Frozen(),
        "statusquo" => new StatusQuo(baseline.StatusQuo),
        "technocrat" => new Technocrat(baseline.Technocrat),
        _ => throw new ArgumentException($"Unknown occupant '{name}'. Known: {string.Join(", ", Names)}"),
    };
}
