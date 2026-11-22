using System.Globalization;
using System.Text;
using System.Text.Json;
using Eunomia.Core;
using Eunomia.Core.Model;
using Eunomia.Governments.Ai;

namespace Eunomia.Cli;

/// <summary>Writes what an AI government said: every prompt and reply, and a readable deliberation log.</summary>
public static class AiWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void Write(string dir, RunResult run, ITranscribed ai, int baseYear, string title)
    {
        var lines = new StringBuilder();
        lines.Append(JsonSerializer.Serialize(new { system = ai.System }, Json)).Append('\n');
        foreach (var entry in ai.Transcript) lines.Append(JsonSerializer.Serialize(entry, Json)).Append('\n');
        File.WriteAllText(Path.Combine(dir, "transcripts.jsonl"), lines.ToString());
        File.WriteAllText(Path.Combine(dir, "deliberations.md"), Deliberations(run, ai, baseYear, title));
    }

    public static string Deliberations(RunResult run, ITranscribed ai, int baseYear, string title)
    {
        var sb = new StringBuilder();
        sb.Append($"# {title}\n\n");
        var s = run.Summary;
        sb.Append($"**Result:** {s.Status}. Median household {E(s.StartMedian)} → {E(s.FinalMedian)} (worst year {E(s.WorstMedian)}). ");
        sb.Append($"Debt {P(run.Opening.DebtRatio)} → {P(s.FinalDebtRatio)}. Rating {run.Opening.Rating} → {run.Years[^1].Rating}.\n");
        if (s.Breaches.Count > 0)
            sb.Append($"**Floors breached:** {string.Join("; ", s.Breaches.Select(b => $"{b.Floor} in {baseYear + 1 + b.Year} ({b.Detail})"))}\n");
        sb.Append('\n');

        foreach (var d in ai.Deliberations)
        {
            var before = d.Year == 0 ? run.Opening : run.Years[d.Year - 1];
            var year = run.Years[d.Year];
            sb.Append($"## {baseYear + 1 + d.Year}\n\n");
            sb.Append($"*World:* {(year.Events.Count == 0 ? "calm" : string.Join(", ", year.Events))}. ");
            sb.Append($"*Coming in:* growth {P(before.OutputGrowth)}, debt {P(before.DebtRatio)}, rating {before.Rating}.\n\n");

            bool branches = d.Steps.Any(s => s.Branch != "government");
            if (branches)
            {
                // The meeting as it happened: each branch's move and its reasons.
                foreach (var step in d.Steps)
                {
                    string who = char.ToUpperInvariant(step.Branch[0]) + step.Branch[1..];
                    sb.Append($"- *Round {step.Round}.* **{who} {step.Action}.** {step.Text}");
                    if (!string.IsNullOrWhiteSpace(step.Detail) && step.Branch != "executive") sb.Append($" *({step.Detail})*");
                    sb.Append('\n');
                }
                sb.Append('\n');
            }

            if (d.Abstained)
            {
                sb.Append($"**No decision.** Last year's budget carries over. {string.Join(" ", d.Errors)}\n\n");
            }
            else
            {
                string changes = Changes(before.Policy, year.Policy);
                sb.Append($"**Decision:** {changes}\n\n");
                if (!branches && d.Rationale.Length > 0) sb.Append($"> {d.Rationale}\n\n");
                if (d.Expectations.Length > 0) sb.Append($"*Expects:* {d.Expectations}\n\n");
            }

            sb.Append($"*Result:* growth {P(year.OutputGrowth)}, median {E(year.MedianIncome)} ({Change(year.MedianIncome, before.MedianIncome)}), ");
            sb.Append($"deficit {P(year.DeficitRatio)}, debt {P(year.DebtRatio)}, rating {year.Rating}");
            if (year.RatingAction is not null) sb.Append($" — agency {year.RatingAction}");
            sb.Append(".\n\n");
        }
        return sb.ToString();
    }

    /// <summary>Only what moved, in plain words.</summary>
    private static string Changes(Policy a, Policy b)
    {
        var parts = new List<string>();
        void Tax(string name, double x, double y) { if (Math.Abs(x - y) > 1e-6) parts.Add($"{name} tax {P(x)} → {P(y)}"); }
        Tax("income", a.Taxes.Income, b.Taxes.Income);
        Tax("corporate", a.Taxes.Corporate, b.Taxes.Corporate);
        Tax("consumption", a.Taxes.Consumption, b.Taxes.Consumption);
        if (Math.Abs(a.PlannedDeficit - b.PlannedDeficit) > 1e-6) parts.Add($"planned deficit {P(a.PlannedDeficit)} → {P(b.PlannedDeficit)}");
        if (a.ManagementShares is { } ma && b.ManagementShares is { } mb)
        {
            var m = ManagementShares.Lines.Where(l => Math.Abs(ma[l] - mb[l]) >= 0.0005).Select(l => $"{StateReport.Label(l)} {P(ma[l])} → {P(mb[l])}").ToList();
            if (m.Count > 0) parts.Add($"management shares: {string.Join(", ", m)}");
        }
        var moves = BudgetLines.All
            .Select(l => (Line: l, Delta: b.BudgetShares[l] - a.BudgetShares[l]))
            .Where(m => Math.Abs(m.Delta) >= 0.0005)
            .OrderByDescending(m => m.Delta)
            .Select(m => $"{StateReport.Label(m.Line)} {(m.Delta > 0 ? "+" : "−")}{Math.Abs(m.Delta * 100).ToString("0.0", Inv)} pts");
        string shares = string.Join(", ", moves);
        if (shares.Length > 0) parts.Add($"budget shares: {shares}");
        return parts.Count == 0 ? "no change." : string.Join("; ", parts) + ".";
    }

    private static string P(double x) => (x * 100).ToString("0.0", Inv) + "%";
    private static string E(double x) => "€" + x.ToString("N0", Inv);
    private static string Change(double now, double then) => (now / then - 1).ToString("+0.0%;-0.0%", Inv);
}
