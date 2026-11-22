using System.Globalization;
using System.Text;
using System.Text.Json;
using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Core.World;

namespace Eunomia.Cli;

/// <summary>Writes a run folder: years.csv, summary.json, decisions.json, feed.json.</summary>
public static class RunWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Write(string dir, RunResult run, IReadOnlyList<FeedRow> feed, ulong? seed)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "years.csv"), YearsCsv(run));
        File.WriteAllText(Path.Combine(dir, "summary.json"), JsonSerializer.Serialize(new { seed, run.Summary }, ConfigLoader.Options));
        File.WriteAllText(Path.Combine(dir, "decisions.json"), JsonSerializer.Serialize(run.Decisions, ConfigLoader.Options));
        File.WriteAllText(Path.Combine(dir, "feed.json"), JsonSerializer.Serialize(feed, ConfigLoader.Options));
    }

    private static readonly (string Name, Func<YearRecord, object> Value)[] Columns =
    [
        ("year", r => r.Year),
        ("outputBn", r => r.Output / 1e9),
        ("growth", r => r.OutputGrowth),
        ("medianIncome", r => r.MedianIncome),
        ("fifth1", r => r.GroupIncome[0]),
        ("fifth2", r => r.GroupIncome[1]),
        ("fifth3", r => r.GroupIncome[2]),
        ("fifth4", r => r.GroupIncome[3]),
        ("fifth5", r => r.GroupIncome[4]),
        ("revenueRatio", r => r.Revenue / r.Output),
        ("spendingRatio", r => r.Spending / r.Output),
        ("interestRatio", r => r.Interest / r.Output),
        ("deficitRatio", r => r.DeficitRatio),
        ("debtRatio", r => r.DebtRatio),
        ("marketRate", r => r.MarketRate),
        ("effectiveRate", r => r.EffectiveRate),
        ("rating", r => r.Rating),
        ("outlook", r => r.Outlook.ToString()),
        ("ratingAction", r => r.RatingAction ?? ""),
        ("escapeClause", r => r.EscapeClause),
        ("netTradeRatio", r => r.NetTrade / r.Output),
        ("priceIndex", r => r.PriceIndex),
        ("leakage", r => r.LeakageRate),
        ("collection", r => r.CollectionRate),
        ("skills", r => r.Skills),
        ("health", r => r.Health),
        ("infrastructure", r => r.Infrastructure),
        ("farmProductivity", r => r.FarmProductivity),
        ("stateCapacity", r => r.StateCapacity),
        ("stability", r => r.Stability),
        ("privateCapital", r => r.PrivateCapital),
        ("schools", r => r.Schools),
        ("schoolQuality", r => r.SchoolQuality),
        ("skillsFifth1", r => r.SkillsByFifth[0]),
        ("skillsFifth3", r => r.SkillsByFifth[2]),
        ("skillsFifth5", r => r.SkillsByFifth[4]),
        ("secondaryAvg", r => r.SecondaryByFifth.Average()),
        ("higherAvg", r => r.HigherByFifth.Average()),
        ("higherFifth1", r => r.HigherByFifth[0]),
        ("effHealth", r => r.Efficiency[(int)BudgetLine.Health]),
        ("effEducation", r => r.Efficiency[(int)BudgetLine.Education]),
        ("effInfrastructure", r => r.Efficiency[(int)BudgetLine.Infrastructure]),
        ("effAgriculture", r => r.Efficiency[(int)BudgetLine.Agriculture]),
        ("effSecurity", r => r.Efficiency[(int)BudgetLine.Security]),
        ("effAdministration", r => r.Efficiency[(int)BudgetLine.Administration]),
        ("mgmtHealth", r => r.Policy.ManagementShares?.Health ?? 0),
        ("mgmtEducation", r => r.Policy.ManagementShares?.Education ?? 0),
        ("mgmtInfrastructure", r => r.Policy.ManagementShares?.Infrastructure ?? 0),
        ("mgmtAgriculture", r => r.Policy.ManagementShares?.Agriculture ?? 0),
        ("mgmtSecurity", r => r.Policy.ManagementShares?.Security ?? 0),
        ("mgmtAdministration", r => r.Policy.ManagementShares?.Administration ?? 0),
        ("provision", r => r.Policy.EducationRegime?.Provision.ToString() ?? ""),
        ("universityFees", r => r.Policy.EducationRegime?.UniversityFees.ToString() ?? ""),
        ("support", r => r.Policy.EducationRegime?.Support.ToString() ?? ""),
        ("standards", r => r.Policy.EducationRegime?.Standards.ToString() ?? ""),
        ("earmark", r => r.EarmarkActive),
        ("privateShare", r => r.PrivateShare),
        ("basicMin", r => r.BasicByFifth.Min()),
        ("strikeLoss", r => r.StrikeLoss),
        ("protests", r => string.Join("; ", r.Protests)),
        ("taxIncome", r => r.Policy.Taxes.Income),
        ("taxCorporate", r => r.Policy.Taxes.Corporate),
        ("taxConsumption", r => r.Policy.Taxes.Consumption),
        ("plannedDeficit", r => r.Policy.PlannedDeficit),
        ("shareHealth", r => r.Policy.BudgetShares.Health),
        ("shareEducation", r => r.Policy.BudgetShares.Education),
        ("shareInfrastructure", r => r.Policy.BudgetShares.Infrastructure),
        ("shareAgriculture", r => r.Policy.BudgetShares.Agriculture),
        ("shareTransfers", r => r.Policy.BudgetShares.SocialTransfers),
        ("shareSecurity", r => r.Policy.BudgetShares.Security),
        ("shareAdministration", r => r.Policy.BudgetShares.Administration),
        ("exportDemand", r => r.ExportDemand),
        ("energyPrice", r => r.EnergyPrice),
        ("externalRate", r => r.ExternalRate),
        ("stress", r => r.FinancialStress),
        ("carriedOver", r => r.CarriedOver),
        ("events", r => string.Join("; ", r.Events)),
    ];

    public static string YearsCsv(RunResult run)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', Columns.Select(c => c.Name))).Append('\n');
        foreach (var r in run.Years.Prepend(run.Opening))
            sb.Append(string.Join(',', Columns.Select(c => Format(c.Value(r))))).Append('\n');
        return sb.ToString();
    }

    private static string Format(object v) => v switch
    {
        double d => d.ToString("G6", Inv),
        string s => s.Contains(',') ? $"\"{s}\"" : s,
        bool b => b ? "1" : "0",
        _ => Convert.ToString(v, Inv) ?? "",
    };
}
