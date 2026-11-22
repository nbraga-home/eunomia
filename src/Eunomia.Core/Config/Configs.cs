namespace Eunomia.Core.Config;

// Every economic number lives in these files, never in code.
// Each record mirrors one JSON file under config/ or packs/.

/// <summary>A stock built by spending: how strongly it lifts output, how late it arrives, how fast it drains.</summary>
public sealed record StockParams
{
    /// <summary>Output exponent. Unused for stocks that act through other channels (capacity, stability).</summary>
    public double Exponent { get; init; }
    public required int DelayYears { get; init; }
    public required double Depreciation { get; init; }
}

public sealed record StockSet
{
    public required StockParams Skills { get; init; }
    public required StockParams Health { get; init; }
    public required StockParams Infrastructure { get; init; }
    public required StockParams FarmProductivity { get; init; }
    public required StockParams StateCapacity { get; init; }
    public required StockParams Stability { get; init; }
    public required StockParams PrivateCapital { get; init; }
}

public sealed record LossParams
{
    public required double Base { get; init; }
    public required double StabilityExponent { get; init; }
    public required double Max { get; init; }
}

public sealed record LeakageParams
{
    public required double Base { get; init; }
    public required double CapacityExponent { get; init; }
    public required double Max { get; init; }
}

public sealed record CollectionParams
{
    public required double Base { get; init; }
    public required double CapacityExponent { get; init; }
}

public sealed record TaxElasticities
{
    public required double Income { get; init; }
    public required double Corporate { get; init; }
    public required double Consumption { get; init; }
}

public sealed record PriceWeights
{
    public required double Energy { get; init; }
    public required double Food { get; init; }
}

/// <summary>Borrowing rate = world rate + constant + rating spread + stress coefficient × financial stress.</summary>
public sealed record BorrowingParams
{
    public required double SpreadConstant { get; init; }
    public required double StressCoefficient { get; init; }
    public required double AverageMaturityYears { get; init; }
}

/// <summary>
/// The rating agency. Target notch = constant + debt weight × debt ratio + deficit weight × deficit (points)
/// + trend weight × yearly change in the debt ratio (points) + growth weight × (reference − 3-year growth) (points).
/// </summary>
public sealed record RatingParams
{
    /// <summary>Best first. Index 0 is AAA.</summary>
    public required IReadOnlyList<string> Scale { get; init; }
    /// <summary>Index of the first rating below investment grade.</summary>
    public required int JunkFrom { get; init; }
    public required double Constant { get; init; }
    public required double DebtWeight { get; init; }
    public required double DeficitWeight { get; init; }
    public required double TrendWeight { get; init; }
    public required double GrowthWeight { get; init; }
    public required double GrowthReference { get; init; }
    /// <summary>Gap in notches that triggers an outlook change (and a move at the next review).</summary>
    public required double OutlookGap { get; init; }
    /// <summary>Gap in notches that triggers an immediate two-notch cut.</summary>
    public required double CrisisGap { get; init; }
    public required double SpreadPerNotch { get; init; }
    /// <summary>Extra spread when the rating falls below investment grade: many lenders must sell.</summary>
    public required double JunkCliff { get; init; }
    public required double JunkSpreadPerNotch { get; init; }
}

public sealed record EconomyConfig
{
    public required double CapitalShare { get; init; }
    public required double TrendProductivityGrowth { get; init; }
    /// <summary>Output change per unit of change in public purchases, as shares of output. Short-run demand effect.</summary>
    public required double FiscalMultiplier { get; init; }
    public required double ExternalExposure { get; init; }
    public required StockSet Stocks { get; init; }
    public required LossParams Loss { get; init; }
    public required LeakageParams Leakage { get; init; }
    public required CollectionParams Collection { get; init; }
    public required TaxElasticities TaxElasticity { get; init; }
    public required double InvestmentShareOfProfit { get; init; }
    public required PriceWeights PriceWeights { get; init; }
    /// <summary>Yearly inflation. Money is in constant prices, so inflation shrinks real debt.</summary>
    public required double Inflation { get; init; }
    public required BorrowingParams Borrowing { get; init; }
    public required RatingParams Rating { get; init; }

    /// <summary>Applies a named assumption set: scales the returns on skills, health, infrastructure and farm productivity.</summary>
    public EconomyConfig WithReturnsMultiplier(double m) => this with
    {
        Stocks = Stocks with
        {
            Skills = Stocks.Skills with { Exponent = Stocks.Skills.Exponent * m },
            Health = Stocks.Health with { Exponent = Stocks.Health.Exponent * m },
            Infrastructure = Stocks.Infrastructure with { Exponent = Stocks.Infrastructure.Exponent * m },
            FarmProductivity = Stocks.FarmProductivity with { Exponent = Stocks.FarmProductivity.Exponent * m },
        },
    };
}

public sealed record ParameterSet
{
    public required double ReturnsMultiplier { get; init; }
}

// ---------- World ----------

public sealed record SeriesParams
{
    public required double Start { get; init; }
    public required double Mean { get; init; }
    /// <summary>Share of the gap to the mean closed each year.</summary>
    public required double Pull { get; init; }
    public required double Noise { get; init; }
    /// <summary>"log" for indices and prices, "level" for rates and stress (floored at zero).</summary>
    public required string Kind { get; init; }
}

public sealed record WorldSeries
{
    public required SeriesParams ExportDemand { get; init; }
    public required SeriesParams EnergyPrice { get; init; }
    public required SeriesParams FoodPrice { get; init; }
    public required SeriesParams ExternalRate { get; init; }
    public required SeriesParams FinancialStress { get; init; }
}

public sealed record EventEffect
{
    /// <summary>Series name: exportDemand, energyPrice, foodPrice, externalRate, financialStress, farmOutput.</summary>
    public required string Series { get; init; }
    public double Multiply { get; init; } = 1.0;
    public double Add { get; init; }
}

public sealed record EventType
{
    public required string Name { get; init; }
    public required double AnnualOdds { get; init; }
    public required double StressCoefficient { get; init; }
    public required int MinYears { get; init; }
    public required int MaxYears { get; init; }
    public required bool Crisis { get; init; }
    public required IReadOnlyList<EventEffect> Effects { get; init; }
}

public sealed record WorldConfig
{
    public required WorldSeries Series { get; init; }
    public required IReadOnlyList<EventType> Events { get; init; }
}

// ---------- Government ----------

public sealed record Bounds
{
    public required double Min { get; init; }
    public required double Max { get; init; }
    public bool Contains(double x) => x >= Min && x <= Max;
}

public sealed record TaxBounds
{
    public required Bounds Income { get; init; }
    public required Bounds Corporate { get; init; }
    public required Bounds Consumption { get; init; }
}

public sealed record DebtFloor
{
    public required bool Enabled { get; init; }
    public required double MaxInterestShareOfRevenue { get; init; }
    public required double RateCeiling { get; init; }
}

public sealed record IncomeGroupFloor
{
    public required bool Enabled { get; init; }
    public required double MinShareOfStart { get; init; }
}

public sealed record EndStateStocksFloor
{
    public required bool Enabled { get; init; }
    public required double Threshold { get; init; }
}

public sealed record EndStatePipelinesFloor
{
    public required bool Enabled { get; init; }
    public required double Share { get; init; }
}

/// <summary>
/// The EU fiscal rules, simplified. The deficit stays under its limit, and debt above the reference must fall
/// on average over the window: faster when it is above the high threshold. Years with a crisis-flagged event
/// active fall under the escape clause.
/// </summary>
public sealed record FiscalRulesFloor
{
    public required bool Enabled { get; init; }
    public required double DeficitLimit { get; init; }
    public required double DebtReference { get; init; }
    public required double HighDebt { get; init; }
    /// <summary>Required average yearly fall in the debt ratio when it is above the high threshold.</summary>
    public required double RequiredDeclineHigh { get; init; }
    /// <summary>Required average yearly fall between the reference and the high threshold.</summary>
    public required double RequiredDeclineMid { get; init; }
    public required int WindowYears { get; init; }
}

public sealed record Floors
{
    public required DebtFloor Debt { get; init; }
    public required FiscalRulesFloor FiscalRules { get; init; }
    public required IncomeGroupFloor IncomeGroups { get; init; }
    public required EndStateStocksFloor EndStateStocks { get; init; }
    public required EndStatePipelinesFloor EndStatePipelines { get; init; }
}

public sealed record GovernmentConfig
{
    public required int HorizonYears { get; init; }
    public required TaxBounds TaxBounds { get; init; }
    public required double MaxTaxChangePerYear { get; init; }
    public required Bounds DeficitBounds { get; init; }
    /// <summary>Share of the budget that can be moved between lines in one year, times state capacity.</summary>
    public required double ReallocationAllowance { get; init; }
    public required Floors Floors { get; init; }
    public required double ScoreTolerance { get; init; }
}

// ---------- Country pack ----------

public sealed record IncomeGroups
{
    /// <summary>Share of all wages earned by each fifth, lowest first.</summary>
    public required IReadOnlyList<double> WageWeights { get; init; }
    public required IReadOnlyList<double> OwnershipWeights { get; init; }
    public required IReadOnlyList<double> TransferWeights { get; init; }
    /// <summary>Share of disposable income each fifth spends, tax included.</summary>
    public required IReadOnlyList<double> SpendingShares { get; init; }
    public required IReadOnlyList<double> Progressivity { get; init; }
}

public sealed record CountryPack
{
    public required string Name { get; init; }
    public required int BaseYear { get; init; }
    /// <summary>Yearly output in constant base-year euros.</summary>
    public required double Output { get; init; }
    public required double Households { get; init; }
    public required double Debt { get; init; }
    public required double EffectiveRate { get; init; }
    /// <summary>Credit rating at the start, on the scale in economy.json.</summary>
    public required string Rating { get; init; }
    public required double WorkingAgeGrowth { get; init; }
    public required double HouseholdGrowth { get; init; }
    public required double FarmShare { get; init; }
    /// <summary>Years from the start until the first election; elections then every four years.</summary>
    public required int FirstElectionYear { get; init; }
    public required Policy Policy { get; init; }
    public required IncomeGroups Groups { get; init; }
    /// <summary>Where each number came from. Free text, kept beside the numbers.</summary>
    public string? Sources { get; init; }
}
