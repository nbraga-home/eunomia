using Eunomia.Core.Config;
using Eunomia.Core.World;

namespace Eunomia.Core.Model;

public sealed record RunResult(
    string Occupant,
    string ParameterSet,
    YearRecord Opening,
    IReadOnlyList<YearRecord> Years,
    IReadOnlyList<DecisionRecord> Decisions,
    RunSummary Summary);

/// <summary>
/// Portugal as one national economy, in yearly steps. Deterministic: all randomness is in the feed.
/// See docs/build-plan.md for the model in plain words.
/// </summary>
public sealed class Simulation
{
    private const int Groups = 5;

    private readonly EconomyConfig _e;
    private readonly GovernmentConfig _rules;
    private readonly CountryPack _pack;
    private readonly IReadOnlyList<FeedRow> _feed;
    private readonly string _parameterSet;
    private readonly Taxes _startTaxes;
    private readonly FeedRow _openingWorld;

    // State
    private double _productivity;
    private double _labour = 1.0;
    private double _households;
    private double _debt;
    private double _effectiveRate;
    private Policy _enacted;
    private Flows _previous;
    private readonly Stock _skills, _health, _infrastructure, _farm, _capacity, _stability, _capital;
    private readonly YearRecord _opening;

    public Simulation(SimulationConfig config, string parameterSet, IReadOnlyList<FeedRow> feed)
    {
        _e = config.Economy(parameterSet);
        _rules = config.Government;
        _pack = config.Pack;
        _feed = feed;
        _parameterSet = parameterSet;
        _startTaxes = _pack.Policy.Taxes;
        _enacted = _pack.Policy;
        _households = _pack.Households;
        _debt = _pack.Debt;
        _effectiveRate = _pack.EffectiveRate;
        _openingWorld = WorldGenerator.Flat(config.World, 1)[0] with { Year = -1 };

        if (feed.Count < _rules.HorizonYears)
            throw new ArgumentException($"Feed has {feed.Count} years, horizon needs {_rules.HorizonYears}.");

        // Opening year: every stock at 1, the starting policy, a calm world. The productivity
        // constant is chosen so output equals the pack's figure exactly.
        _productivity = _pack.Output / (1 - Loss(1.0));
        var opening = SolveOpening();
        _previous = opening;

        double Baseline(BudgetLine line) => opening.Delivered[(int)line];
        _skills = new Stock(_e.Stocks.Skills, Baseline(BudgetLine.Education));
        _health = new Stock(_e.Stocks.Health, Baseline(BudgetLine.Health));
        _infrastructure = new Stock(_e.Stocks.Infrastructure, Baseline(BudgetLine.Infrastructure));
        _farm = new Stock(_e.Stocks.FarmProductivity, Baseline(BudgetLine.Agriculture));
        _capacity = new Stock(_e.Stocks.StateCapacity, Baseline(BudgetLine.Administration));
        _stability = new Stock(_e.Stocks.Stability, Baseline(BudgetLine.Security));
        _capital = new Stock(_e.Stocks.PrivateCapital, opening.Investment);

        double openingInterest = _effectiveRate * _debt;
        _opening = Record(-1, opening, _pack.Policy, false, _openingWorld,
            interest: openingInterest,
            deficit: opening.TotalPaid + openingInterest - opening.Revenue,
            debt: _debt, marketRate: MarketRate(_debt, opening.Output, _openingWorld), growth: 0);
        _groupStart = opening.GroupIncome;
    }

    private readonly double[] _groupStart;

    public YearRecord Opening => _opening;

    public RunResult Run(IGovernment government)
    {
        var years = new List<YearRecord>();
        var decisions = new List<DecisionRecord>();
        var breaches = new List<Breach>();
        var history = new List<YearRecord> { _opening };

        for (int year = 0; year < _rules.HorizonYears; year++)
        {
            var world = _feed[year];

            // 1. Meeting
            var context = new MeetingContext
            {
                Year = year,
                HorizonYears = _rules.HorizonYears,
                World = world,
                Last = history[^1],
                History = history.ToList(),
                Enacted = _enacted,
                StartingPolicy = _pack.Policy,
                ElectionYear = year >= _pack.FirstElectionYear && (year - _pack.FirstElectionYear) % 4 == 0,
                Rules = _rules,
                Check = Check,
            };
            var proposal = government.Decide(context);
            var check = proposal is null ? null : Check(proposal);
            bool carried = check is null || !check.Ok;
            if (!carried) _enacted = check!.Feasible!;
            decisions.Add(new DecisionRecord
            {
                Year = year,
                Proposed = proposal,
                InForce = _enacted,
                Errors = check?.Errors ?? [],
                CarriedOver = carried,
                Scaled = check?.Scaled ?? false,
            });

            // 2. The year itself
            double envelope = Math.Max(0, Envelope(_enacted));
            var flows = Evaluate(_enacted, world, envelope, _previous);

            // 3. Debt. Interest at the nominal market rate; inflation shrinks the real value of the debt.
            double interest = _effectiveRate * _debt;
            double deficit = flows.TotalPaid + interest - flows.Revenue;
            double closingDebt = (_debt + deficit) / (1 + _e.Inflation);
            double marketRate = MarketRate(closingDebt, flows.Output, world);

            var record = Record(year, flows, _enacted, carried, world, interest, deficit, closingDebt, marketRate,
                growth: flows.Output / _previous.Output - 1);
            years.Add(record);
            history.Add(record);
            CheckYearlyFloors(record, breaches);

            _debt = closingDebt;
            _effectiveRate += (marketRate - _effectiveRate) / _e.Borrowing.AverageMaturityYears;

            // 4. Stocks: this year's delivered spending enters the pipelines.
            _skills.Advance(flows.Delivered[(int)BudgetLine.Education]);
            _health.Advance(flows.Delivered[(int)BudgetLine.Health]);
            _infrastructure.Advance(flows.Delivered[(int)BudgetLine.Infrastructure]);
            _farm.Advance(flows.Delivered[(int)BudgetLine.Agriculture]);
            _capacity.Advance(flows.Delivered[(int)BudgetLine.Administration]);
            _stability.Advance(flows.Delivered[(int)BudgetLine.Security]);
            _capital.Advance(flows.Investment);

            // 5. Trend and people
            _productivity *= 1 + _e.TrendProductivityGrowth;
            _labour *= 1 + _pack.WorkingAgeGrowth;
            _households *= 1 + _pack.HouseholdGrowth;
            _previous = flows;
        }

        CheckEndState(years[^1], breaches);
        var summary = RunSummary.From(government.Name, _parameterSet, _opening, years, breaches);
        return new RunResult(government.Name, _parameterSet, _opening, years, decisions, summary);
    }

    // ---------------- Budget ----------------

    /// <summary>Validates a proposal against the rules and the policy in force, and returns its feasible form.</summary>
    public CheckResult Check(Policy p)
    {
        var errors = new List<string>();
        var shares = p.BudgetShares.ToArray();
        for (int i = 0; i < shares.Length; i++)
            if (!double.IsFinite(shares[i]) || shares[i] < 0 || shares[i] > 1)
                errors.Add($"Budget share for {BudgetLines.All[i]} must be between 0 and 1, got {shares[i]}.");
        if (errors.Count == 0 && Math.Abs(shares.Sum() - 1) > 1e-9)
            errors.Add($"Budget shares must sum to 1, got {shares.Sum():F6}.");

        void Tax(string name, double rate, double current, Bounds b)
        {
            if (!double.IsFinite(rate) || !b.Contains(rate))
                errors.Add($"{name} tax {rate} is outside {b.Min} to {b.Max}.");
            else if (Math.Abs(rate - current) > _rules.MaxTaxChangePerYear + 1e-12)
                errors.Add($"{name} tax can move at most {_rules.MaxTaxChangePerYear} a year (from {current} to {rate}).");
        }
        Tax("Income", p.Taxes.Income, _enacted.Taxes.Income, _rules.TaxBounds.Income);
        Tax("Corporate", p.Taxes.Corporate, _enacted.Taxes.Corporate, _rules.TaxBounds.Corporate);
        Tax("Consumption", p.Taxes.Consumption, _enacted.Taxes.Consumption, _rules.TaxBounds.Consumption);

        if (!double.IsFinite(p.PlannedDeficit) || !_rules.DeficitBounds.Contains(p.PlannedDeficit))
            errors.Add($"Planned deficit {p.PlannedDeficit} is outside {_rules.DeficitBounds.Min} to {_rules.DeficitBounds.Max}.");

        if (errors.Count > 0) return new CheckResult { Errors = errors };

        // Limited state capacity: only so much of the budget can move between lines in a year.
        var current = _enacted.BudgetShares.ToArray();
        double cost = 0.5 * shares.Zip(current, (a, b) => Math.Abs(a - b)).Sum();
        double allowance = _rules.ReallocationAllowance * _capacity.Value;
        bool scaled = cost > allowance + 1e-12;
        if (scaled)
        {
            double k = allowance / cost;
            shares = current.Zip(shares, (c, s) => c + (s - c) * k).ToArray();
        }
        var feasible = p with { BudgetShares = BudgetShares.FromArray(shares) };

        double envelope = Envelope(feasible);
        if (envelope <= 0)
            return new CheckResult { Errors = [$"The envelope is {envelope:E3}: revenue plus borrowing does not cover interest."] };

        return new CheckResult
        {
            Errors = [],
            Feasible = feasible,
            Envelope = envelope,
            Appropriations = shares.Select(s => s * envelope).ToList(),
            Scaled = scaled,
        };
    }

    /// <summary>Money for the seven lines: projected revenue at the new rates on last year's bases, plus planned borrowing, minus interest due.</summary>
    private double Envelope(Policy p) =>
        ProjectedRevenue(p.Taxes, _previous, Collection(_capacity.Value))
        + p.PlannedDeficit * _previous.Output
        - _effectiveRate * _debt;

    private double ProjectedRevenue(Taxes t, Flows bases, double collection) =>
        IncomeTax(t.Income, bases.Wages, collection)
        + CorporateTax(t.Corporate, bases.Profits, collection)
        + ConsumptionTax(t.Consumption, bases.Consumption, collection);

    // ---------------- One year of the economy ----------------

    private sealed record Flows
    {
        public required double Output;
        public required double Wages;
        public required double Profits;
        public required double Investment;
        public required double Consumption;
        public required double Purchases;
        public required double TotalPaid;
        public required double[] Paid;
        public required double[] Delivered;
        public required double Leaked;
        public required double RevenueIncome;
        public required double RevenueCorporate;
        public required double RevenueConsumption;
        public required double PriceIndex;
        public required double[] GroupIncome;
        public required double LeakageRate;
        public required double CollectionRate;
        public double Revenue => RevenueIncome + RevenueCorporate + RevenueConsumption;
    }

    private Flows Evaluate(Policy policy, FeedRow world, double envelope, Flows? previous)
    {
        double capacity = _capacity?.Value ?? 1; // stocks do not exist yet while the opening year is solved
        double leakage = Math.Clamp(_e.Leakage.Base * Math.Pow(capacity, -_e.Leakage.CapacityExponent), 0, _e.Leakage.Max);
        double collection = Collection(capacity);

        // Public spending: paid, part leaked, the rest delivered.
        var paid = new double[BudgetLines.Count];
        var delivered = new double[BudgetLines.Count];
        foreach (var line in BudgetLines.All)
        {
            paid[(int)line] = policy.BudgetShares[line] * envelope;
            delivered[(int)line] = paid[(int)line] * (1 - leakage);
        }
        double transfers = delivered[(int)BudgetLine.SocialTransfers];
        double purchases = delivered.Sum() - transfers;

        // Output: capital, workers improved by skills and health, infrastructure, a farming share.
        var s = _e.Stocks;
        double alpha = _e.CapitalShare;
        double farm = (1 - _pack.FarmShare) + _pack.FarmShare * Math.Pow(_farm?.Value ?? 1, s.FarmProductivity.Exponent) * world.FarmOutput;
        double potential = _productivity
            * Math.Pow(_capital?.Value ?? 1, alpha)
            * Math.Pow(_labour * Math.Pow(_skills?.Value ?? 1, s.Skills.Exponent) * Math.Pow(_health?.Value ?? 1, s.Health.Exponent), 1 - alpha)
            * Math.Pow(_infrastructure?.Value ?? 1, s.Infrastructure.Exponent)
            * farm;
        double impulse = previous is null ? 0 : (purchases - previous.Purchases) / previous.Output;
        double demand = 1 + _e.ExternalExposure * (world.ExportDemand - 1) + _e.FiscalMultiplier * impulse;
        double output = potential * demand * (1 - Loss(_stability?.Value ?? 1));

        // Firms
        double wages = (1 - alpha) * output;
        double profits = alpha * output;
        double corporateTax = CorporateTax(policy.Taxes.Corporate, profits, collection);
        double investment = _e.InvestmentShareOfProfit * (profits - corporateTax);
        double dividends = profits - corporateTax - investment;

        // Households, by fifth
        var g = _pack.Groups;
        double incomeTax = IncomeTax(policy.Taxes.Income, wages, collection);
        double progressive = Enumerable.Range(0, Groups).Sum(i => g.WageWeights[i] * g.Progressivity[i]);
        var disposable = new double[Groups];
        double consumption = 0;
        for (int i = 0; i < Groups; i++)
        {
            disposable[i] = wages * g.WageWeights[i]
                - incomeTax * g.WageWeights[i] * g.Progressivity[i] / progressive
                + dividends * g.OwnershipWeights[i]
                + transfers * g.TransferWeights[i];
            consumption += g.SpendingShares[i] * disposable[i];
        }
        double consumptionTax = ConsumptionTax(policy.Taxes.Consumption, consumption, collection);

        double priceIndex = (1 + policy.Taxes.Consumption) / (1 + _startTaxes.Consumption)
            * (1 + _e.PriceWeights.Energy * (world.EnergyPrice - 1) + _e.PriceWeights.Food * (world.FoodPrice - 1));
        double perGroupHouseholds = _households / Groups;

        return new Flows
        {
            Output = output,
            Wages = wages,
            Profits = profits,
            Investment = investment,
            Consumption = consumption,
            Purchases = purchases,
            TotalPaid = paid.Sum(),
            Paid = paid,
            Delivered = delivered,
            Leaked = paid.Sum() - delivered.Sum(),
            RevenueIncome = incomeTax,
            RevenueCorporate = corporateTax,
            RevenueConsumption = consumptionTax,
            PriceIndex = priceIndex,
            GroupIncome = disposable.Select(d => d / perGroupHouseholds / priceIndex).ToArray(),
            LeakageRate = leakage,
            CollectionRate = collection,
        };
    }

    /// <summary>The opening year is a fixed point: transfers depend on revenue, which depends on consumption, which depends on transfers.</summary>
    private Flows SolveOpening()
    {
        double interest = _effectiveRate * _debt;
        double envelope = 0.4 * _pack.Output;
        Flows flows = null!;
        for (int i = 0; i < 200; i++)
        {
            flows = Evaluate(_pack.Policy, _openingWorld, envelope, previous: null);
            double next = flows.Revenue + _pack.Policy.PlannedDeficit * flows.Output - interest;
            if (Math.Abs(next - envelope) < 1e-9 * envelope) { envelope = next; break; }
            envelope = next;
        }
        return Evaluate(_pack.Policy, _openingWorld, envelope, previous: null);
    }

    // ---------------- Rules of the economy ----------------

    /// <summary>A higher rate shrinks the reported base: ((1 - rate) / (1 - starting rate)) ^ elasticity.</summary>
    private static double Reported(double rate, double startRate, double elasticity) =>
        Math.Pow((1 - rate) / (1 - startRate), elasticity);

    private double IncomeTax(double rate, double wages, double collection) =>
        rate * wages * Reported(rate, _startTaxes.Income, _e.TaxElasticity.Income) * collection;

    private double CorporateTax(double rate, double profits, double collection) =>
        rate * profits * Reported(rate, _startTaxes.Corporate, _e.TaxElasticity.Corporate) * collection;

    /// <summary>Spending includes the tax, so the tax is rate / (1 + rate) of it.</summary>
    private double ConsumptionTax(double rate, double spending, double collection) =>
        spending * rate / (1 + rate) * Reported(rate, _startTaxes.Consumption, _e.TaxElasticity.Consumption) * collection;

    private double Collection(double capacity) =>
        Math.Clamp(_e.Collection.Base * Math.Pow(capacity, _e.Collection.CapacityExponent), 0, 1);

    private double Loss(double stability) =>
        Math.Clamp(_e.Loss.Base * Math.Pow(stability, -_e.Loss.StabilityExponent), 0, _e.Loss.Max);

    private double MarketRate(double debt, double output, FeedRow world)
    {
        var b = _e.Borrowing;
        double excess = Math.Max(0, debt / output - b.DebtThreshold);
        return world.ExternalRate + b.SpreadConstant + b.SpreadSlope * excess * excess + b.StressCoefficient * world.FinancialStress;
    }

    // ---------------- Floors ----------------

    private void CheckYearlyFloors(YearRecord r, List<Breach> breaches)
    {
        var f = _rules.Floors;
        if (f.Debt.Enabled)
        {
            if (r.Revenue <= 0 || r.InterestShareOfRevenue > f.Debt.MaxInterestShareOfRevenue)
                breaches.Add(new Breach("debt", r.Year, r.CarriedOver, $"interest is {r.InterestShareOfRevenue:P1} of revenue"));
            else if (r.MarketRate > f.Debt.RateCeiling)
                breaches.Add(new Breach("debt", r.Year, r.CarriedOver, $"borrowing rate {r.MarketRate:P2}"));
        }
        if (f.IncomeGroups.Enabled)
        {
            for (int i = 0; i < Groups; i++)
                if (r.GroupIncome[i] < f.IncomeGroups.MinShareOfStart * _groupStart[i])
                    breaches.Add(new Breach("incomeGroup", r.Year, r.CarriedOver, $"fifth {i + 1} at {r.GroupIncome[i] / _groupStart[i]:P1} of its start"));
        }
    }

    private void CheckEndState(YearRecord last, List<Breach> breaches)
    {
        var f = _rules.Floors;
        var stocks = new (string Name, Stock Stock)[]
        {
            ("skills", _skills), ("health", _health), ("infrastructure", _infrastructure), ("stateCapacity", _capacity),
        };
        foreach (var (name, stock) in stocks)
        {
            if (!stock.Active) continue;
            if (f.EndStateStocks.Enabled && stock.Value < f.EndStateStocks.Threshold)
                breaches.Add(new Breach("endStateStocks", last.Year, last.CarriedOver, $"{name} ends at {stock.Value:F3}"));
            if (f.EndStatePipelines.Enabled && stock.Delay > 0 && stock.PipelineAverage < f.EndStatePipelines.Share * stock.HoldingAmount)
                breaches.Add(new Breach("endStatePipelines", last.Year, last.CarriedOver,
                    $"{name} pipeline holds {stock.PipelineAverage / stock.HoldingAmount:P1} of what keeps it level"));
        }
    }

    // ---------------- Records ----------------

    private YearRecord Record(int year, Flows f, Policy policy, bool carried, FeedRow world,
        double interest, double deficit, double debt, double marketRate, double growth) => new()
    {
        Year = year,
        Output = f.Output,
        OutputGrowth = growth,
        Wages = f.Wages,
        Profits = f.Profits,
        Investment = f.Investment,
        Consumption = f.Consumption,
        NetTrade = f.Output - (f.Consumption - f.RevenueConsumption) - f.Investment - f.Purchases,
        PriceIndex = f.PriceIndex,
        Households = _households,
        Labour = _labour,
        Envelope = f.TotalPaid,
        Paid = f.Paid,
        Leaked = f.Leaked,
        RevenueIncome = f.RevenueIncome,
        RevenueCorporate = f.RevenueCorporate,
        RevenueConsumption = f.RevenueConsumption,
        Spending = f.TotalPaid,
        Interest = interest,
        Deficit = deficit,
        Debt = debt,
        MarketRate = marketRate,
        EffectiveRate = _effectiveRate,
        LeakageRate = f.LeakageRate,
        CollectionRate = f.CollectionRate,
        GroupIncome = f.GroupIncome,
        Skills = _skills?.Value ?? 1,
        Health = _health?.Value ?? 1,
        Infrastructure = _infrastructure?.Value ?? 1,
        FarmProductivity = _farm?.Value ?? 1,
        StateCapacity = _capacity?.Value ?? 1,
        Stability = _stability?.Value ?? 1,
        PrivateCapital = _capital?.Value ?? 1,
        Policy = policy,
        CarriedOver = carried,
        ExportDemand = world.ExportDemand,
        EnergyPrice = world.EnergyPrice,
        FoodPrice = world.FoodPrice,
        ExternalRate = world.ExternalRate,
        FinancialStress = world.FinancialStress,
        Events = world.ActiveEvents,
    };
}
