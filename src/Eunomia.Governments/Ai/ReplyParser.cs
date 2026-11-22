using System.Text.Json;
using Eunomia.Core;

namespace Eunomia.Governments.Ai;

public sealed record ParsedReply(Policy? Policy, string Rationale, string Expectations, IReadOnlyList<string> Errors)
{
    public bool Ok => Errors.Count == 0 && Policy is not null;
}

/// <summary>Extracts the decision record from a model's reply. Errors come back as text the model can read.</summary>
public static class ReplyParser
{
    /// <summary>Shares summing to within this distance of 1 are rescaled; models round.</summary>
    public const double ShareSumSlack = 0.02;

    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static ParsedReply Parse(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return Fail("The reply contained no JSON object.");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(text[start..(end + 1)], Options); }
        catch (JsonException e) { return Fail($"The JSON could not be read: {e.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var errors = new List<string>();

            double Num(JsonElement parent, string name, string where)
            {
                if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
                    return v.GetDouble();
                errors.Add($"Missing or non-numeric \"{name}\" in {where}.");
                return double.NaN;
            }
            JsonElement Obj(string name)
            {
                if (root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object) return v;
                errors.Add($"Missing object \"{name}\".");
                return default;
            }
            string Str(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

            var taxesEl = Obj("taxes");
            var taxes = new Taxes(Num(taxesEl, "income", "taxes"), Num(taxesEl, "corporate", "taxes"), Num(taxesEl, "consumption", "taxes"));
            double deficit = Num(root, "plannedDeficit", "the reply");

            var sharesEl = Obj("budgetShares");
            string[] names = ["health", "education", "infrastructure", "agriculture", "socialTransfers", "security", "administration"];
            var shares = names.Select(n => Num(sharesEl, n, "budgetShares")).ToArray();

            if (errors.Count == 0)
            {
                double sum = shares.Sum();
                if (shares.Any(s => s < 0)) errors.Add("Budget shares cannot be negative.");
                else if (Math.Abs(sum - 1) > ShareSumSlack) errors.Add($"Budget shares sum to {sum:0.0000}; they must sum to 1.");
                else shares = shares.Select(s => s / sum).ToArray();
            }

            string rationale = Str("rationale"), expectations = Str("expectations");
            if (errors.Count > 0) return new ParsedReply(null, rationale, expectations, errors);
            return new ParsedReply(new Policy(taxes, deficit, BudgetShares.FromArray(shares)), rationale, expectations, []);
        }
    }

    private static ParsedReply Fail(string error) => new(null, "", "", [error]);
}
