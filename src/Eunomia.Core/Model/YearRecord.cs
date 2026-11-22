namespace Eunomia.Core.Model;

/// <summary>One year of results. Year -1 is the opening year, reconstructed from the pack.</summary>
public sealed record YearRecord
{
    public required int Year { get; init; }

    // Economy
    public required double Output { get; init; }
    public required double OutputGrowth { get; init; }
    public required double Wages { get; init; }
    public required double Profits { get; init; }
    public required double Investment { get; init; }
    public required double Consumption { get; init; }
    public required double NetTrade { get; init; }
    public required double PriceIndex { get; init; }
    public required double Households { get; init; }
    public required double Labour { get; init; }

    // Public finances
    public required double Envelope { get; init; }
    public required IReadOnlyList<double> Paid { get; init; }
    public required double Leaked { get; init; }
    public required double RevenueIncome { get; init; }
    public required double RevenueCorporate { get; init; }
    public required double RevenueConsumption { get; init; }
    public double Revenue => RevenueIncome + RevenueCorporate + RevenueConsumption;
    public required double Spending { get; init; }
    public required double Interest { get; init; }
    public required double Deficit { get; init; }
    public double DeficitRatio => Deficit / Output;
    /// <summary>Closing debt, in constant euros.</summary>
    public required double Debt { get; init; }
    public double DebtRatio => Debt / Output;
    public required double MarketRate { get; init; }
    public required double EffectiveRate { get; init; }
    public double InterestShareOfRevenue => Revenue > 0 ? Interest / Revenue : double.PositiveInfinity;
    /// <summary>The rating in force after this year's review, its outlook, and what the agency announced (null if nothing).</summary>
    public required string Rating { get; init; }
    public required Outlook Outlook { get; init; }
    public string? RatingAction { get; init; }
    /// <summary>True when a crisis-flagged event was active: the EU escape clause applies.</summary>
    public required bool EscapeClause { get; init; }
    public required double LeakageRate { get; init; }
    public required double CollectionRate { get; init; }

    // People
    /// <summary>Real disposable income per household for each fifth, lowest first.</summary>
    public required IReadOnlyList<double> GroupIncome { get; init; }
    /// <summary>Estimated median household: the middle fifth's real disposable income per household.</summary>
    public double MedianIncome => GroupIncome[2];

    // Stocks at the start of the year (the values that produced this year's output)
    public required double Skills { get; init; }
    public required double Health { get; init; }
    public required double Infrastructure { get; init; }
    public required double FarmProductivity { get; init; }
    public required double StateCapacity { get; init; }
    public required double Stability { get; init; }
    public required double PrivateCapital { get; init; }
    /// <summary>Each line's efficiency (management index ^ exponent, capped), in budget-line order; 1 for social transfers.</summary>
    public required IReadOnlyList<double> Efficiency { get; init; }

    // Policy in force and the world
    public required Policy Policy { get; init; }
    public required bool CarriedOver { get; init; }
    public required double ExportDemand { get; init; }
    public required double EnergyPrice { get; init; }
    public required double FoodPrice { get; init; }
    public required double ExternalRate { get; init; }
    public required double FinancialStress { get; init; }
    public required IReadOnlyList<string> Events { get; init; }
}
