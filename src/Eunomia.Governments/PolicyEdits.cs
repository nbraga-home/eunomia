using Eunomia.Core;
using Eunomia.Core.Config;

namespace Eunomia.Governments;

/// <summary>Small helpers scripted governments use to edit a policy without breaking the rules.</summary>
internal static class PolicyEdits
{
    /// <summary>Moves up to <paramref name="amount"/> of share from the <paramref name="from"/> lines (in proportion to their size) to <paramref name="to"/>.</summary>
    public static double[] Move(double[] shares, double amount, BudgetLine[] from, BudgetLine to) =>
        Move(shares, amount, from, [(to, 1.0)]);

    public static double[] Move(double[] shares, double amount, BudgetLine[] from, (BudgetLine Line, double Weight)[] to)
    {
        var s = (double[])shares.Clone();
        double available = from.Sum(l => s[(int)l]);
        double moved = Math.Min(amount, available);
        if (moved <= 0) return s;
        foreach (var l in from) s[(int)l] -= moved * shares[(int)l] / available;
        double weights = to.Sum(t => t.Weight);
        foreach (var (line, w) in to) s[(int)line] += moved * w / weights;
        return s;
    }

    /// <summary>Moves a tax rate by <paramref name="delta"/>, kept inside its bounds and the yearly change limit.</summary>
    public static double Tax(double current, double delta, Bounds bounds, double maxChange) =>
        Math.Clamp(current + Math.Clamp(delta, -maxChange, maxChange), bounds.Min, bounds.Max);

    public static double Deficit(double value, Bounds bounds) => Math.Clamp(value, bounds.Min, bounds.Max);
}
