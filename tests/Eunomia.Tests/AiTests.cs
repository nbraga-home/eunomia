using Eunomia.Core;
using Eunomia.Governments.Ai;

namespace Eunomia.Tests;

public class ReplyParserTests
{
    private const string Valid = """
        {"taxes":{"income":0.42,"corporate":0.20,"consumption":0.24},"plannedDeficit":0.01,
         "budgetShares":{"health":0.165,"education":0.11,"infrastructure":0.14,"agriculture":0.012,
                         "socialTransfers":0.41,"security":0.057,"administration":0.106},
         "rationale":"Shift toward schools.","expectations":"Skills rise after 2037."}
        """;

    [Fact]
    public void ReadsJsonWrappedInProseAndFences()
    {
        var p = ReplyParser.Parse("Here is my budget:\n```json\n" + Valid + "\n```\nThanks.");
        Assert.True(p.Ok);
        Assert.Equal(0.01, p.Policy!.PlannedDeficit);
        Assert.Equal("Shift toward schools.", p.Rationale);
        Assert.Equal(1.0, p.Policy.BudgetShares.Sum(), 12);
    }

    [Fact]
    public void RescalesSharesThatAreSlightlyOff()
    {
        var p = ReplyParser.Parse(Valid.Replace("\"administration\":0.106", "\"administration\":0.110"));
        Assert.True(p.Ok);
        Assert.Equal(1.0, p.Policy!.BudgetShares.Sum(), 12);
    }

    [Fact]
    public void RejectsSharesFarFromOne() =>
        Assert.Contains(ReplyParser.Parse(Valid.Replace("\"socialTransfers\":0.41", "\"socialTransfers\":0.21")).Errors, e => e.Contains("sum"));

    [Fact]
    public void RejectsMissingFields() =>
        Assert.Contains(ReplyParser.Parse(Valid.Replace("\"plannedDeficit\":0.01,", "")).Errors, e => e.Contains("plannedDeficit"));

    [Fact]
    public void RejectsReplyWithoutJson() => Assert.False(ReplyParser.Parse("I would raise taxes.").Ok);
}

public class SingleAiTests
{
    /// <summary>A valid reply that keeps the starting policy, built from the prompt so it always passes the checks.</summary>
    private static string Keep(Policy p) => $$"""
        {"taxes":{"income":{{p.Taxes.Income}},"corporate":{{p.Taxes.Corporate}},"consumption":{{p.Taxes.Consumption}}},
         "plannedDeficit":{{p.PlannedDeficit}},
         "budgetShares":{"health":{{p.BudgetShares.Health}},"education":{{p.BudgetShares.Education}},"infrastructure":{{p.BudgetShares.Infrastructure}},
           "agriculture":{{p.BudgetShares.Agriculture}},"socialTransfers":{{p.BudgetShares.SocialTransfers}},"security":{{p.BudgetShares.Security}},
           "administration":{{p.BudgetShares.Administration}}},
         "rationale":"Steady as she goes.","expectations":"Slow growth."}
        """;

    [Fact]
    public void AValidReplyIsEnactedWithOneCallPerYear()
    {
        var c = Repo.LoadPortugal();
        var ai = new SingleAi(new ScriptedModel(_ => Keep(c.Pack.Policy)), "mandate");
        var run = Fixtures.RunFlat(c, ai);

        Assert.All(run.Decisions, d => Assert.False(d.CarriedOver));
        Assert.Equal(c.Government.HorizonYears, ai.Transcript.Count);
        Assert.Equal("Steady as she goes.", ai.Deliberations[0].Rationale);
    }

    [Fact]
    public void ABadReplyIsRetriedOnceWithTheErrors()
    {
        var c = Repo.LoadPortugal();
        int calls = 0;
        var ai = new SingleAi(new ScriptedModel(prompt => ++calls % 2 == 1 ? "no json here" : Keep(c.Pack.Policy)), "mandate");
        var run = Fixtures.RunFlat(c, ai);

        Assert.All(run.Decisions, d => Assert.False(d.CarriedOver));
        Assert.Equal(2 * c.Government.HorizonYears, ai.Transcript.Count);
        Assert.Contains("previous reply was rejected", ai.Transcript[1].Prompt);
    }

    [Fact]
    public void TwoFailuresAreAnAbstentionAndThePolicyCarriesOver()
    {
        var c = Repo.LoadPortugal();
        var ai = new SingleAi(new ScriptedModel(_ => "no json"), "mandate");
        var run = Fixtures.RunFlat(c, ai);

        Assert.All(run.Decisions, d => Assert.True(d.CarriedOver));
        Assert.All(ai.Deliberations, d => Assert.True(d.Abstained));
    }

    [Fact]
    public void ARuleBreakingProposalIsSentBackWithTheRuleItBroke()
    {
        var c = Repo.LoadPortugal();
        var greedy = c.Pack.Policy with { Taxes = c.Pack.Policy.Taxes with { Income = 0.55 } };
        var ai = new SingleAi(new ScriptedModel(_ => Keep(greedy)), "mandate");
        Fixtures.RunFlat(c, ai);

        Assert.Contains("Income tax can move at most", ai.Transcript[1].Prompt);
        Assert.True(ai.Deliberations[0].Abstained);
    }

    [Fact]
    public void TheReportShowsRulesAndHistoryButNoModelParameters()
    {
        var c = Repo.LoadPortugal();
        string? firstPrompt = null;
        var ai = new SingleAi(new ScriptedModel(p => { firstPrompt ??= p; return Keep(c.Pack.Policy); }), "mandate");
        Fixtures.RunFlat(c, ai);

        Assert.Contains("Floors", firstPrompt);
        Assert.Contains("| 2024 |", firstPrompt); // the opening year
        Assert.Contains("A-", firstPrompt);
        Assert.DoesNotContain("depreciation", firstPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exponent", firstPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("| 2025 |", firstPrompt); // no future rows
    }
}
