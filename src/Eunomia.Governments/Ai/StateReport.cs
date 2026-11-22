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

    public static string Body(MeetingContext ctx, IReadOnlyList<string> memory, string memoryTitle)
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
        sb.AppendLine("- Management shares for six sectors (every line except social transfers, which are cash): the part of each line spent on running the sector. The rest is front-line money. Management builds slowly into how much each front-line euro achieves, and how fast the sector can grow without waste.");
        sb.AppendLine();
        sb.AppendLine("## Rules (fixed for the whole run)");
        sb.AppendLine($"- Tax bounds: income {P(r.TaxBounds.Income.Min)}–{P(r.TaxBounds.Income.Max)}, corporate {P(r.TaxBounds.Corporate.Min)}–{P(r.TaxBounds.Corporate.Max)}, consumption {P(r.TaxBounds.Consumption.Min)}–{P(r.TaxBounds.Consumption.Max)}. Each rate can move at most {Pts(r.MaxTaxChangePerYear)} per year.");
        sb.AppendLine($"- Planned deficit between {P(r.DeficitBounds.Min)} and {P(r.DeficitBounds.Max)} of output.");
        sb.AppendLine($"- State capacity limits change: this year at most {P(ctx.ReallocationAllowance)} of the budget can move between lines (half the sum of the absolute changes in shares). A larger change is scaled down to fit.");
        sb.AppendLine($"- Management shares between {P(r.ManagementShareBounds.Min)} and {P(r.ManagementShareBounds.Max)} of a line; each can move at most {Pts(r.MaxManagementChangePerYear)} per year.");
        sb.AppendLine("- Each sector can absorb only so much growth in its front-line money in a year (see Sectors). Money above that is paid out but mostly wasted.");
        sb.AppendLine("- Part of every line is lost before delivery (leakage). Lenders price the country's credit rating, which an agency reviews once a year.");
        sb.AppendLine();
        sb.AppendLine("## Floors (breaching any one loses the run, whatever the income)");
        if (f.Debt.Enabled) sb.AppendLine($"- Debt stays payable: interest at most {P(f.Debt.MaxInterestShareOfRevenue)} of revenue, borrowing rate at most {P(f.Debt.RateCeiling)}.");
        if (eu.Enabled) sb.AppendLine($"- EU fiscal rules: deficit at most {P(eu.DeficitLimit)} of output; debt above {P(eu.DebtReference)} must fall on average over {eu.WindowYears} years (at least {Pts(eu.RequiredDeclineHigh)} a year above {P(eu.HighDebt)}, {Pts(eu.RequiredDeclineMid)} between). Suspended in years with an active crisis.");
        if (f.IncomeGroups.Enabled) sb.AppendLine($"- No income fifth falls below {P(f.IncomeGroups.MinShareOfStart)} of its starting real income.");
        if (f.EndStateStocks.Enabled) sb.AppendLine($"- In the final year, the skills, health, infrastructure and state capacity indices are at least {f.EndStateStocks.Threshold.ToString("0.00", Inv)}, and the spending already committed to each is enough to keep it at {f.EndStateStocks.Threshold.ToString("0.00", Inv)} or above in the long run. A stock built above that level may drift down toward it.");
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
        sb.AppendLine($"- Taxes: income {P2(t.Income)}, corporate {P2(t.Corporate)}, consumption {P2(t.Consumption)}. Planned deficit {P2(ctx.Enacted.PlannedDeficit)}.");
        if (enacted.Ok)
        {
            sb.AppendLine($"- At these settings this year's budget for the seven lines is {Bn(enacted.Envelope)} ({P(enacted.Envelope / last.Output)} of last year's output):");
            foreach (var line in BudgetLines.All)
                sb.AppendLine($"  - {Label(line)}: share {ctx.Enacted.BudgetShares[line].ToString("0.0000", Inv)}, {Bn(enacted.Appropriations[(int)line])}");
        }
        sb.AppendLine();

        if (ctx.Sectors.Count > 0)
        {
            sb.AppendLine("## Sectors");
            sb.AppendLine("Efficiency is how much a front-line euro achieves against the start (1.00). Absorbable growth is how much front-line money can grow this year over what the sector absorbed last year without waste.");
            sb.AppendLine("| Sector | Management share | Management index | Efficiency | Absorbable growth | Front-line growth at the policy in force |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var s in ctx.Sectors)
            {
                var note = enacted.Absorption.FirstOrDefault(a => a.Line == s.Line);
                string growth = note is null ? "-" : P(note.Growth) + (note.ExcessShare > 0.0005 ? $" ({P(note.ExcessShare)} of front-line money over the limit)" : "");
                sb.AppendLine($"| {Label(s.Line)} | {P(ctx.Enacted.ManagementShares?[s.Line] ?? 0)} | {N(s.ManagementIndex)} | {N(s.Efficiency)} | {P(s.AbsorbableGrowth)} | {growth} |");
            }
            sb.AppendLine();
        }

        if (ctx.Commitments.Count > 0)
        {
            var fl = ctx.Rules.Floors;
            string threshold = fl.EndStateStocks.Threshold.ToString("0.00", Inv);
            sb.AppendLine("## Committed spending (for the final-year floor)");
            sb.AppendLine($"Spending reaches these indices with a delay, so money already committed decides where they head. In the final year each index must be at least {threshold}, and its committed spending at least {P0(fl.EndStatePipelines.Share)}% of what keeps it at {threshold} in the long run.");
            sb.AppendLine("| Index | Now | Committed now | Committed after this year's budget, at the policy in force |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var c in ctx.Commitments)
            {
                var after = enacted.Commitments.FirstOrDefault(a => a.Stock == c.Stock);
                sb.AppendLine($"| {StockLabel(c.Stock)} | {N(c.Index)} | {P0(c.Committed)}% | {(after is null ? "-" : P0(after.Committed) + "%")} |");
            }
            sb.AppendLine();
        }

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
            sb.AppendLine($"## {memoryTitle}");
            foreach (string m in memory) sb.AppendLine($"- {m}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>The report plus the single government's reply instructions.</summary>
    public static string Build(MeetingContext ctx, IReadOnlyList<string> memory) =>
        Body(ctx, memory, "Your recent decisions, in your own words") +
        "## Your reply\nReply with one JSON object and nothing else, in this form (the numbers are only an example):\n" +
        PolicyJsonExample("Why this budget, in a few sentences, based on the report.") + "\n";

    /// <summary>A budget in the reply format, with example numbers.</summary>
    public static string PolicyJsonExample(string rationaleHint) => $$"""
        {
          "taxes": { "income": 0.42, "corporate": 0.20, "consumption": 0.24 },
          "plannedDeficit": 0.0,
          "budgetShares": { "health": 0.165, "education": 0.10, "infrastructure": 0.14, "agriculture": 0.012,
                            "socialTransfers": 0.42, "security": 0.057, "administration": 0.106 },
          "managementShares": { "health": 0.05, "education": 0.05, "infrastructure": 0.05, "agriculture": 0.05,
                                "security": 0.05, "administration": 0.05 },
          "rationale": "{{rationaleHint}}",
          "expectations": "What you expect to happen, and by when."
        }
        {{Units}}
        """;

    /// <summary>Most failed replies in the first runs were unit slips (42 for 42%).</summary>
    public const string Units =
        "All rates and shares are decimals: 0.42 means 42%, and a planned deficit of -0.005 means a surplus of half a percent of output. " +
        "\"managementShares\" may be left out to keep the shares in force.";

    /// <summary>A proposal as the branches see it: the numbers, and what moves against the policy in force.</summary>
    public static string DescribeRevision(string id, Policy p, CheckResult check, Policy inForce)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### Revision {id}{(check.Scaled ? " (share changes scaled down to fit the reallocation limit)" : "")}");
        var t = p.Taxes;
        sb.AppendLine($"- Taxes: income {P2(t.Income)}, corporate {P2(t.Corporate)}, consumption {P2(t.Consumption)}. Planned deficit {P2(p.PlannedDeficit)}.");
        sb.AppendLine($"- Budget for the seven lines: {Bn(check.Envelope)}.");
        foreach (var line in BudgetLines.All)
        {
            double delta = p.BudgetShares[line] - inForce.BudgetShares[line];
            string change = Math.Abs(delta) < 0.00005 ? "" : $" ({(delta > 0 ? "+" : "−")}{Math.Abs(delta * 100).ToString("0.00", Inv)} pts)";
            sb.AppendLine($"  - {Label(line)}: share {p.BudgetShares[line].ToString("0.0000", Inv)}{change}, {Bn(check.Appropriations[(int)line])}");
        }
        if (p.ManagementShares is { } m && inForce.ManagementShares is { } m0)
        {
            var moves = ManagementShares.Lines.Where(l => Math.Abs(m[l] - m0[l]) >= 0.00005)
                .Select(l => $"{Label(l)} {P2(m0[l])} → {P2(m[l])}").ToList();
            if (moves.Count > 0) sb.AppendLine($"- Management shares: {string.Join(", ", moves)}.");
        }
        foreach (var a in check.Absorption.Where(a => a.ExcessShare > 0.0005))
            sb.AppendLine($"- {Label(a.Line)}: front-line money grows {P(a.Growth)}, above the {P(a.Limit)} it can absorb; {P(a.ExcessShare)} of it is over the limit and mostly wasted.");
        if (check.Commitments.Count > 0)
            sb.AppendLine("- Committed spending after this budget: " +
                string.Join(", ", check.Commitments.Select(c => $"{StockLabel(c.Stock)} {P0(c.Committed)}%")) + ".");
        return sb.ToString();
    }

    public static string Label(BudgetLine line) => line switch
    {
        BudgetLine.SocialTransfers => "social transfers",
        _ => line.ToString().ToLowerInvariant(),
    };

    private static string StockLabel(string stock) => stock switch
    {
        "stateCapacity" => "state capacity",
        _ => stock,
    };

    private static string P(double x) => (x * 100).ToString("0.0", Inv) + "%";
    /// <summary>Two decimals for rates a government sets, so a 0.75% plan never reads as 0.8%.</summary>
    private static string P2(double x) => (x * 100).ToString("0.00", Inv) + "%";
    private static string P0(double x) => (x * 100).ToString("0.#", Inv);
    private static string Pts(double x) => (x * 100).ToString("0.##", Inv) + " points";
    private static string N(double x) => x.ToString("0.00", Inv);
    private static string E(double x) => x.ToString("N0", Inv);
    private static string Bn(double x) => "€" + (x / 1e9).ToString("0.0", Inv) + "bn";
}
