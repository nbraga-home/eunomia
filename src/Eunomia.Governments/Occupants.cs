using Eunomia.Core;

namespace Eunomia.Governments;

public static class Occupants
{
    public static IReadOnlyList<string> Names => ["frozen"];

    public static IGovernment Create(string name) => name.ToLowerInvariant() switch
    {
        "frozen" => new Frozen(),
        _ => throw new ArgumentException($"Unknown occupant '{name}'. Known: {string.Join(", ", Names)}"),
    };
}
