using System.Globalization;
using System.Text;
using Eunomia.Core;
using Eunomia.Core.Model;

namespace Eunomia.Governments.Ai;

/// <summary>
/// Builds the report a model government reads. A pure function of the meeting context and the government's
/// own memory: nothing else can reach the prompt, not the economic parameters and not the future.
/// </summary>
public static class StateReport
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Build(MeetingContext ctx, IReadOnlyList<string> memory)
    {
        var sb = new StringBuilder();
        int calendar = ctx.BaseYear + 1 + ctx.Year;
        var r = ctx.Rules;
        var f = r.Floors;
        var eu = f.FiscalRules;

        sb.AppendLine($"# State report: {ctx.Country}, budget for {calendar} (year {ctx.Year + 1} of {ctx.HorizonYears})");
        sb.AppendLine();
        sb.AppendLine("## What you decide");
        sb.AppendLine("- Three tax rates: income (including social contributions), corporate, consumption.");
        sb.AppendLine("- The planned deficit, as a share of last year's output. It includes interest. Negative means a surplus.");
        sb.AppendLine("- Budget shares for seven lines, summing to 1: health, education, infrastructure, agriculture, social transfers, security, administration.");
        sb.AppendLine("- Money for the seven lines = projected revenue at your tax rates + planned deficit × last year's output − interest due.");
        sb.AppendLine();
        sb.AppendLine("## Rules (fixed for the whole run)");
        sb.AppendLine($"- Tax bounds: income {P(r.TaxBounds.Income.Min)}–{P(r.TaxBounds.Income.Max)}, corporate {P(r.TaxBounds.Corporate.Min)}–{P(r.TaxBounds.Corporate.Max)}, consumption {P(r.TaxBounds.Consumption.Min)}–{P(r.TaxBounds.Consumption.Max)}. Each rate can move at most {Pts(r.MaxTaxChangePerYear)} per year.");
        sb.AppendLine($"- Planned deficit between {P(r.DeficitBounds.Min)} and {P(r.DeficitBounds.Max)} of output.");
        sb.AppendLine($"- State capacity limits change: this year at most {P(ctx.ReallocationAllowance)} of the budget can move between lines (half the sum of the absolute changes in shares). A larger change is scaled down to fit.");
        sb.AppendLine("- Part of every line is lost before delivery (leakage). Lenders price the country's credit rating, which an agency reviews once a year.");
        sb.AppendLine();
        sb.AppendLine("## Floors (breaching any one loses the run, whatever the income)");
        if (f.Debt.Enabled) sb.AppendLine($"- Debt stays payable: interest at most {P(f.Debt.MaxInterestShareOfRevenue)} of revenue, borrowing rate at most {P(f.Debt.RateCeiling)}.");
        if (eu.Enabled) sb.AppendLine($"- EU fiscal rules: deficit at most {P(eu.DeficitLimit)} of output; debt above {P(eu.DebtReference)} must fall on average over {eu.WindowYears} years (at least {Pts(eu.RequiredDeclineHigh)} a year above {P(eu.HighDebt)}, {Pts(eu.RequiredDeclineMid)} between). Suspended in years with an active crisis.");
        if (f.IncomeGroups.Enabled) sb.AppendLine($"- No income fifth falls below {P(f.IncomeGroups.MinShareOfStart)} of its starting real income.");
        if (f.EndStateStocks.Enabled) sb.AppendLine($"- In the final year, the skills, health, infrastructure and state capacity indices are at least {f.EndStateStocks.Threshold.ToString("0.00", Inv)}, and current spending on them is enough to hold them there.");
        sb.AppendLine();
        sb.AppendLine("## How you are judged");
        sb.AppendLine($"Real disposable income of the median household in year {ctx.HorizonYears}, and in the worst year along the way. A run that keeps every floor beats any run that breaks one.");
        sb.AppendLine();

        var w = ctx.World;
        sb.AppendLine($"## The world in {calendar}");
        sb.AppendLine($"- Foreign demand index {N(w.ExportDemand)}, energy price index {N(w.EnergyPrice)}, food price index {N(w.FoodPrice)} (1.00 = start).");
        sb.AppendLine($"- European central bank rate {P(w.ExternalRate)}, financial stress {N(w.FinancialStress)} (about 0.10 is calm).");
        sb.AppendLine($"- Events: {(w.ActiveEvents.Count == 0 ? "none" : string.Join(", ", w.ActiveEvents))}{(w.StartingEvents.Count > 0 ? $" (starting this year: {string.Join(", ", w.StartingEvents)})" : "")}.");
        if (w.CrisisActive) sb.AppendLine("- A crisis is active: the EU escape clause applies this year.");
        sb.AppendLine();

        var last = ctx.Last;
        sb.AppendLine("## Last year");
        sb.AppendLine($"- Credit rating {last.Rating}, outlook {last.Outlook.ToString().ToLowerInvariant()}{(last.RatingAction is null ? "" : $"; latest announcement: {last.RatingAction}")}.");
        sb.AppendLine($"- Borrowing rate on new debt {P(last.MarketRate)}; average rate on all debt {P(last.EffectiveRate)}.");
        sb.AppendLine($"- Leakage {P(last.LeakageRate)} of spending; tax collection {P(last.CollectionRate)}.");
        sb.AppendLine();

        var enacted = ctx.Check(ctx.Enacted);
        sb.AppendLine("## Policy in force");
        var t = ctx.Enacted.Taxes;
        sb.AppendLine($"- Taxes: income {P(t.Income)}, corporate {P(t.Corporate)}, consumption {P(t.Consumption)}. Planned deficit {P(ctx.Enacted.PlannedDeficit)}.");
        if (enacted.Ok)
        {
            sb.AppendLine($"- At these settings this year's budget for the seven lines is {Bn(enacted.Envelope)} ({P(enacted.Envelope / last.Output)} of last year's output):");
            foreach (var line in BudgetLines.All)
                sb.AppendLine($"  - {Label(line)}: share {ctx.Enacted.BudgetShares[line].ToString("0.0000", Inv)}, {Bn(enacted.Appropriations[(int)line])}");
        }
        sb.AppendLine();

        sb.AppendLine("## History (opening year first; money in constant euros of the base year)");
        sb.AppendLine("| Year | Growth | Median € | Poorest fifth € | Richest fifth € | Prices | Revenue | Spending | Interest | Deficit | Debt | Rate | Rating | Skills | Health | Infra | Farm | Capacity | Stability | Capital | Tax I/C/V | Plan | Shares H/E/I/A/T/S/Ad | Events |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var y in ctx.History)
        {
            var s = y.Policy.BudgetShares;
            sb.AppendLine(string.Join(" | ",
                $"| {ctx.BaseYear + 1 + y.Year}", P(y.OutputGrowth), E(y.MedianIncome), E(y.GroupIncome[0]), E(y.GroupIncome[4]), N(y.PriceIndex),
                P(y.Revenue / y.Output), P(y.Spending / y.Output), P(y.Interest / y.Output), P(y.DeficitRatio), P(y.DebtRatio), P(y.MarketRate), y.Rating,
                N(y.Skills), N(y.Health), N(y.Infrastructure), N(y.FarmProductivity), N(y.StateCapacity), N(y.Stability), N(y.PrivateCapital),
                $"{P0(y.Policy.Taxes.Income)}/{P0(y.Policy.Taxes.Corporate)}/{P0(y.Policy.Taxes.Consumption)}", P(y.Policy.PlannedDeficit),
                string.Join("/", s.ToArray().Select(x => (x * 100).ToString("0.0", Inv))),
                y.Events.Count == 0 ? "-" : string.Join(", ", y.Events)) + " |");
        }
        sb.AppendLine("Stock indices (skills to capital) start at 1.00 and are measured at the start of each year.");
        sb.AppendLine();

        if (memory.Count > 0)
        {
            sb.AppendLine("## Your recent decisions, in your own words");
            foreach (string m in memory) sb.AppendLine($"- {m}");
            sb.AppendLine();
        }

        sb.AppendLine("## Your reply");
        sb.AppendLine("Reply with one JSON object and nothing else, in this form (the numbers are only an example):");
        sb.AppendLine("""
            {
              "taxes": { "income": 0.42, "corporate": 0.20, "consumption": 0.24 },
              "plannedDeficit": 0.0,
              "budgetShares": { "health": 0.165, "education": 0.10, "infrastructure": 0.14, "agriculture": 0.012,
                                "socialTransfers": 0.42, "security": 0.057, "administration": 0.106 },
              "rationale": "Why this budget, in a few sentences, based on the report.",
              "expectations": "What you expect to happen, and by when."
            }
            """);
        return sb.ToString();
    }

    public static string Label(BudgetLine line) => line switch
    {
        BudgetLine.SocialTransfers => "social transfers",
        _ => line.ToString().ToLowerInvariant(),
    };

    private static string P(double x) => (x * 100).ToString("0.0", Inv) + "%";
    private static string P0(double x) => (x * 100).ToString("0.#", Inv);
    private static string Pts(double x) => (x * 100).ToString("0.##", Inv) + " points";
    private static string N(double x) => x.ToString("0.00", Inv);
    private static string E(double x) => x.ToString("N0", Inv);
    private static string Bn(double x) => "€" + (x / 1e9).ToString("0.0", Inv) + "bn";
}
