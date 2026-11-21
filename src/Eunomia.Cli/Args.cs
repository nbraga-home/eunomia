namespace Eunomia.Cli;

/// <summary>Minimal --name value parser. Flags without a value read as "true".</summary>
public sealed class Args
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public Args(IEnumerable<string> args)
    {
        var list = args.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            if (!list[i].StartsWith("--")) throw new ArgumentException($"Unexpected argument '{list[i]}'.");
            string name = list[i][2..];
            bool hasValue = i + 1 < list.Count && !list[i + 1].StartsWith("--");
            _values[name] = hasValue ? list[++i] : "true";
        }
    }

    public string Get(string name, string fallback) => _values.TryGetValue(name, out var v) ? v : fallback;
    public bool Has(string name) => _values.ContainsKey(name);

    public IReadOnlyList<string> GetList(string name, string fallback) =>
        Get(name, fallback).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>A range "1-10" or a list "1,4,7".</summary>
    public IReadOnlyList<ulong> GetSeeds(string name, string fallback)
    {
        string s = Get(name, fallback);
        if (s.Contains('-'))
        {
            var parts = s.Split('-');
            ulong a = ulong.Parse(parts[0]), b = ulong.Parse(parts[1]);
            var seeds = new List<ulong>();
            for (ulong x = a; x <= b; x++) seeds.Add(x);
            return seeds;
        }
        return s.Split(',').Select(ulong.Parse).ToList();
    }
}
