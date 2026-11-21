namespace Eunomia.Core.Random;

/// <summary>
/// xoshiro256** seeded through SplitMix64. Implemented here so a seed gives the
/// same numbers on every runtime. Each named stream gets its own generator, so
/// adding a new series never changes the numbers drawn by an existing one.
/// </summary>
public sealed class Rng
{
    private ulong _s0, _s1, _s2, _s3;

    public Rng(ulong seed)
    {
        ulong x = seed;
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
    }

    /// <summary>A generator for one named stream of a run.</summary>
    public static Rng ForStream(ulong runSeed, string streamName)
    {
        ulong x = runSeed ^ Fnv1a64(streamName);
        return new Rng(SplitMix64(ref x));
    }

    public ulong NextULong()
    {
        ulong result = RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform in [0, 1), 53 bits.</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Standard normal, Box-Muller cosine branch, two uniforms per draw.</summary>
    public double NextNormal()
    {
        double u1 = 1.0 - NextDouble(); // (0, 1], safe for the log
        double u2 = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>Uniform integer in [min, max], both included.</summary>
    public int NextInt(int min, int max)
    {
        if (max < min) throw new ArgumentException("max < min");
        ulong span = (ulong)(max - min) + 1;
        return min + (int)(NextULong() % span);
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        ulong z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong Fnv1a64(string s)
    {
        ulong hash = 0xCBF29CE484222325UL;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(s))
        {
            hash ^= b;
            hash *= 0x100000001B3UL;
        }
        return hash;
    }

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));
}
