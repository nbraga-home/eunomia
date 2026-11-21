using Eunomia.Core.Random;

namespace Eunomia.Tests;

public class RngTests
{
    [Fact]
    public void SameSeedAndStreamGiveSameNumbers()
    {
        var a = Rng.ForStream(42, "exportDemand");
        var b = Rng.ForStream(42, "exportDemand");
        for (int i = 0; i < 100; i++) Assert.Equal(a.NextULong(), b.NextULong());
    }

    [Fact]
    public void DifferentStreamsDiffer()
    {
        var a = Rng.ForStream(42, "exportDemand");
        var b = Rng.ForStream(42, "energyPrice");
        Assert.NotEqual(a.NextULong(), b.NextULong());
    }

    [Fact]
    public void KnownFirstValueIsStable()
    {
        // Pins the algorithm: if this changes, every seeded world changes.
        var r = new Rng(0);
        Assert.Equal(0x99EC5F36CB75F2B4UL, r.NextULong());
    }

    [Fact]
    public void NormalDrawsHaveRoughlyUnitMoments()
    {
        var r = new Rng(7);
        const int n = 200_000;
        double sum = 0, sumSq = 0;
        for (int i = 0; i < n; i++) { double x = r.NextNormal(); sum += x; sumSq += x * x; }
        double mean = sum / n, variance = sumSq / n - mean * mean;
        Assert.InRange(mean, -0.01, 0.01);
        Assert.InRange(variance, 0.98, 1.02);
    }

    [Fact]
    public void NextIntStaysInRange()
    {
        var r = new Rng(3);
        for (int i = 0; i < 1000; i++) Assert.InRange(r.NextInt(1, 3), 1, 3);
    }
}
