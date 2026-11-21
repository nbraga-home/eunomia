namespace Eunomia.Core.Model;

public sealed record Breach(string Floor, int Year, bool CarriedOver, string Detail);

public enum RunStatus { Clean, Breached }

/// <summary>How a run is judged: floors first, then the median household in the final and the worst year.</summary>
public sealed record RunSummary
{
    public required string Occupant { get; init; }
    public required string ParameterSet { get; init; }
    public required RunStatus Status { get; init; }
    public required IReadOnlyList<Breach> Breaches { get; init; }
    public required double StartMedian { get; init; }
    public required double FinalMedian { get; init; }
    public required double WorstMedian { get; init; }

    // Companion measures: how the median rose, not only whether it did.
    public required double FinalLowestFifth { get; init; }
    public required double StartLowestFifth { get; init; }
    public required double FinalDebtRatio { get; init; }
    public required double FinalOutput { get; init; }
    public required double StartOutput { get; init; }

    public static RunSummary From(string occupant, string parameterSet, YearRecord opening, IReadOnlyList<YearRecord> years, IReadOnlyList<Breach> breaches) => new()
    {
        Occupant = occupant,
        ParameterSet = parameterSet,
        Status = breaches.Count == 0 ? RunStatus.Clean : RunStatus.Breached,
        Breaches = breaches,
        StartMedian = opening.MedianIncome,
        FinalMedian = years[^1].MedianIncome,
        WorstMedian = years.Min(y => y.MedianIncome),
        FinalLowestFifth = years[^1].GroupIncome[0],
        StartLowestFifth = opening.GroupIncome[0],
        FinalDebtRatio = years[^1].DebtRatio,
        FinalOutput = years[^1].Output,
        StartOutput = opening.Output,
    };
}

public enum Outcome { AWins, BWins, BothBreached, TradeOff }

public static class Judge
{
    /// <summary>
    /// A clean run beats a breached one. Between clean runs, one wins only if it is at least as good on both
    /// the final and the worst median and better on one, beyond the tolerance. Anything else is a trade-off.
    /// </summary>
    public static Outcome Compare(RunSummary a, RunSummary b, double tolerance)
    {
        bool aClean = a.Status == RunStatus.Clean, bClean = b.Status == RunStatus.Clean;
        if (aClean && !bClean) return Outcome.AWins;
        if (!aClean && bClean) return Outcome.BWins;
        if (!aClean && !bClean) return Outcome.BothBreached;

        int final = Order(a.FinalMedian, b.FinalMedian, tolerance);
        int worst = Order(a.WorstMedian, b.WorstMedian, tolerance);
        if (final >= 0 && worst >= 0 && (final > 0 || worst > 0)) return Outcome.AWins;
        if (final <= 0 && worst <= 0 && (final < 0 || worst < 0)) return Outcome.BWins;
        return Outcome.TradeOff;
    }

    /// <summary>1 if x beats y by more than the relative tolerance, -1 if the reverse, 0 if tied.</summary>
    private static int Order(double x, double y, double tolerance)
    {
        double scale = Math.Max(Math.Abs(x), Math.Abs(y));
        if (x - y > tolerance * scale) return 1;
        if (y - x > tolerance * scale) return -1;
        return 0;
    }
}
