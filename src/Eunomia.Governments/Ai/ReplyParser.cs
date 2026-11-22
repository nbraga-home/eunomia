using System.Text.Json;
using Eunomia.Core;

namespace Eunomia.Governments.Ai;

public sealed record ParsedReply(Policy? Policy, string Rationale, string Expectations, IReadOnlyList<string> Errors)
{
    public bool Ok => Errors.Count == 0 && Policy is not null;
}

/// <summary>A vote: one of a fixed set of decisions, with its reasons and the whole object for extra fields.</summary>
public sealed record ParsedChoice(string Decision, string Rationale, JsonElement Root, IReadOnlyList<string> Errors)
{
    public bool Ok => Errors.Count == 0;
    public string Str(string name) => Root.ValueKind == JsonValueKind.Object && Root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    public int? Int(string name) => Root.ValueKind == JsonValueKind.Object && Root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : null;
}

/// <summary>Extracts decision records from a model's reply. Errors come back as text the model can read.</summary>
public static class ReplyParser
{
    /// <summary>Shares summing to within this distance of 1 are rescaled; models round.</summary>
    public const double ShareSumSlack = 0.02;

    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly string[] ShareNames = ["health", "education", "infrastructure", "agriculture", "socialTransfers", "security", "administration"];

    /// <summary>A full budget reply: taxes, planned deficit, shares, rationale, expectations.</summary>
    public static ParsedReply Parse(string text)
    {
        var root = Extract(text, out string? error);
        if (root is null) return new ParsedReply(null, "", "", [error!]);
        var errors = new List<string>();
        var policy = ParsePolicy(root.Value, errors);
        return new ParsedReply(errors.Count == 0 ? policy : null, Str(root.Value, "rationale"), Str(root.Value, "expectations"), errors);
    }

    /// <summary>A vote whose "decision" must be one of <paramref name="allowed"/>.</summary>
    public static ParsedChoice ParseChoice(string text, params string[] allowed)
    {
        var root = Extract(text, out string? error);
        if (root is null) return new ParsedChoice("", "", default, [error!]);
        string decision = Str(root.Value, "decision").Trim().ToLowerInvariant();
        var errors = new List<string>();
        if (!allowed.Contains(decision))
            errors.Add($"\"decision\" must be one of: {string.Join(", ", allowed.Select(a => $"\"{a}\""))}.");
        return new ParsedChoice(decision, Str(root.Value, "rationale"), root.Value, errors);
    }

    /// <summary>Reads taxes, plannedDeficit and budgetShares from an object. Shares slightly off 1 are rescaled.</summary>
    public static Policy? ParsePolicy(JsonElement root, List<string> errors)
    {
        int before = errors.Count;
        double Num(JsonElement parent, string name, string where)
        {
            if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
                return v.GetDouble();
            errors.Add($"Missing or non-numeric \"{name}\" in {where}.");
            return double.NaN;
        }
        JsonElement Obj(string name)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object) return v;
            errors.Add($"Missing object \"{name}\".");
            return default;
        }

        var taxesEl = Obj("taxes");
        var taxes = new Taxes(Num(taxesEl, "income", "taxes"), Num(taxesEl, "corporate", "taxes"), Num(taxesEl, "consumption", "taxes"));
        double deficit = Num(root, "plannedDeficit", "the budget");
        var sharesEl = Obj("budgetShares");
        var shares = ShareNames.Select(n => Num(sharesEl, n, "budgetShares")).ToArray();
        if (errors.Count > before) return null;

        double sum = shares.Sum();
        if (shares.Any(s => s < 0)) { errors.Add("Budget shares cannot be negative."); return null; }
        if (Math.Abs(sum - 1) > ShareSumSlack) { errors.Add($"Budget shares sum to {sum:0.0000}; they must sum to 1."); return null; }

        // Optional: left out, the management shares in force are kept.
        ManagementShares? management = null;
        if (root.TryGetProperty("managementShares", out var mEl) && mEl.ValueKind == JsonValueKind.Object)
        {
            var m = ManagementNames.Select(n => Num(mEl, n, "managementShares")).ToArray();
            if (errors.Count > before) return null;
            management = ManagementShares.FromArray(m);
        }
        // Optional: left out, the education regime in force is kept.
        EducationRegime? regime = null;
        if (root.TryGetProperty("educationRegime", out var rEl) && rEl.ValueKind == JsonValueKind.Object)
        {
            T Pick<T>(string name, T fallback) where T : struct, Enum
            {
                if (!rEl.TryGetProperty(name, out var v)) { errors.Add($"Missing \"{name}\" in educationRegime."); return fallback; }
                if (v.ValueKind == JsonValueKind.String && Enum.TryParse<T>(v.GetString(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
                    return parsed;
                errors.Add($"\"{name}\" in educationRegime must be one of: {string.Join(", ", Enum.GetValues<T>().Select(e => $"\"{StateReport.Word(e)}\""))}.");
                return fallback;
            }
            var provision = Pick("provision", Provision.Public);
            var fees = Pick("universityFees", UniversityFees.Flat);
            var support = Pick("support", StudentSupport.Tested);
            var standards = Pick("standards", Standards.Comprehensive);
            bool earmark = false;
            if (!rEl.TryGetProperty("earmark", out var eEl) || (eEl.ValueKind != JsonValueKind.True && eEl.ValueKind != JsonValueKind.False))
                errors.Add("\"earmark\" in educationRegime must be true or false.");
            else earmark = eEl.GetBoolean();
            if (errors.Count > before) return null;
            regime = new EducationRegime(provision, fees, support, standards, earmark);
        }
        return new Policy(taxes, deficit, BudgetShares.FromArray(shares.Select(s => s / sum).ToArray()), management, regime);
    }

    private static readonly string[] ManagementNames = ["health", "education", "infrastructure", "agriculture", "security", "administration"];

    private static JsonElement? Extract(string text, out string? error)
    {
        error = null;
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) { error = "The reply contained no JSON object."; return null; }
        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)], Options);
            return doc.RootElement.Clone();
        }
        catch (JsonException e) { error = $"The JSON could not be read: {e.Message}"; return null; }
    }

    private static string Str(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
