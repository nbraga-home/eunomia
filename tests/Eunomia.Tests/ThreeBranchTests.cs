using Eunomia.Core;
using Eunomia.Core.Model;
using Eunomia.Governments.Ai;

namespace Eunomia.Tests;

public class ThreeBranchTests
{
    private static readonly Mandates M = new("preamble", "executive mandate", "legislature mandate", "judiciary mandate");

    private static string Budget(Policy p, string rationale = "Because.") => $$"""
        {"taxes":{"income":{{p.Taxes.Income}},"corporate":{{p.Taxes.Corporate}},"consumption":{{p.Taxes.Consumption}}},
         "plannedDeficit":{{p.PlannedDeficit}},
         "budgetShares":{"health":{{p.BudgetShares.Health}},"education":{{p.BudgetShares.Education}},"infrastructure":{{p.BudgetShares.Infrastructure}},
           "agriculture":{{p.BudgetShares.Agriculture}},"socialTransfers":{{p.BudgetShares.SocialTransfers}},"security":{{p.BudgetShares.Security}},
           "administration":{{p.BudgetShares.Administration}}},
         "rationale":"{{rationale}}","expectations":"Things improve."}
        """;

    private static string Vote(string decision, string extra = "") => $$"""{"decision":"{{decision}}","rationale":"My reasons."{{extra}}}""";

    /// <summary>Routes each call by the role named in the task, and counts calls per role.</summary>
    private sealed class Chamber
    {
        public Func<int, string> Executive = _ => "";
        public Func<int, string> Legislature = _ => Vote("approve");
        public Func<int, string> ExecutiveOnAmendment = _ => Vote("accept");
        public Func<int, string> Judiciary = _ => Vote("pass");
        public readonly Dictionary<string, int> Calls = new();
        public readonly List<string> Prompts = [];

        public string Reply(string prompt)
        {
            Prompts.Add(prompt);
            string role = prompt.Contains("You are the executive. The legislature amended") ? "amendment"
                : prompt.Contains("You are the executive.") ? "executive"
                : prompt.Contains("You are the legislature.") ? "legislature"
                : "judiciary";
            int n = Calls[role] = Calls.GetValueOrDefault(role) + 1;
            return role switch
            {
                "executive" => Executive(n),
                "legislature" => Legislature(n),
                "amendment" => ExecutiveOnAmendment(n),
                _ => Judiciary(n),
            };
        }
    }

    private static (RunResult Run, ThreeBranch Gov) RunOneYear(Chamber chamber)
    {
        var c = Repo.LoadPortugal();
        c = c with { Government = c.Government with { HorizonYears = 1 } };
        var model = new ScriptedModel(chamber.Reply);
        var gov = new ThreeBranch(new BranchModels(model, model, model), M);
        return (Fixtures.RunFlat(c, gov), gov);
    }

    private static Policy Start => Repo.LoadPortugal().Pack.Policy;

    private static Policy MoreEducation
    {
        get
        {
            var s = Start.BudgetShares.ToArray();
            s[(int)BudgetLine.SocialTransfers] -= 0.01;
            s[(int)BudgetLine.Education] += 0.01;
            return Start with { BudgetShares = BudgetShares.FromArray(s) };
        }
    }

    [Fact]
    public void ApprovedAndPassedIsEnactedInOneRound()
    {
        var chamber = new Chamber { Executive = _ => Budget(MoreEducation) };
        var (run, gov) = RunOneYear(chamber);

        Assert.False(run.Decisions[0].CarriedOver);
        Assert.Equal(MoreEducation.BudgetShares.Education, run.Years[0].Policy.BudgetShares.Education, 9);
        Assert.Equal(3, gov.Transcript.Count);
        Assert.Equal(["proposes R1", "approves R1", "passes R1", "enacts R1"], gov.Deliberations[0].Steps.Select(s => s.Action));
    }

    [Fact]
    public void ALegislatureThatAlwaysRejectsMeansCarryOver()
    {
        var chamber = new Chamber { Executive = _ => Budget(MoreEducation), Legislature = _ => Vote("reject") };
        var (run, gov) = RunOneYear(chamber);

        Assert.True(run.Decisions[0].CarriedOver);
        Assert.Equal(3, chamber.Calls["executive"]);
        Assert.Equal(3, chamber.Calls["legislature"]);
        Assert.False(chamber.Calls.ContainsKey("judiciary"));
        Assert.True(gov.Deliberations[0].Abstained);
    }

    [Fact]
    public void AnAcceptedAmendmentIsWhatGetsEnacted()
    {
        var chamber = new Chamber
        {
            Executive = _ => Budget(Start),
            Legislature = _ => Vote("amend", ",\"amendment\":" + Budget(MoreEducation)),
        };
        var (run, gov) = RunOneYear(chamber);

        Assert.Equal(MoreEducation.BudgetShares.Education, run.Years[0].Policy.BudgetShares.Education, 9);
        Assert.Equal(["proposes R1", "amends R1 into R2", "accepts R2", "passes R2", "enacts R2"], gov.Deliberations[0].Steps.Select(s => s.Action));
    }

    [Fact]
    public void ADeclinedAmendmentEndsTheRound()
    {
        var chamber = new Chamber
        {
            Executive = _ => Budget(Start),
            Legislature = n => n == 1 ? Vote("amend", ",\"amendment\":" + Budget(MoreEducation)) : Vote("approve"),
            ExecutiveOnAmendment = _ => Vote("decline"),
        };
        var (run, gov) = RunOneYear(chamber);

        Assert.Equal(Start.BudgetShares.Education, run.Years[0].Policy.BudgetShares.Education, 9);
        Assert.Contains("declines R2", gov.Deliberations[0].Steps.Select(s => s.Action));
        Assert.Contains("enacts R3", gov.Deliberations[0].Steps.Select(s => s.Action));
    }

    [Fact]
    public void AValidBlockSendsTheExecutiveBackWithTheReasons()
    {
        var chamber = new Chamber
        {
            Executive = n => Budget(n == 1 ? Start with { PlannedDeficit = 0.06 } : Start),
            Judiciary = n => n == 1
                ? Vote("block", ",\"floor\":\"fiscalRules\",\"breachYear\":2025,\"path\":\"A 6% deficit is above the 3% limit.\"")
                : Vote("pass"),
        };
        var (run, gov) = RunOneYear(chamber);

        Assert.Equal(Start.PlannedDeficit, run.Years[0].Policy.PlannedDeficit, 9);
        Assert.Contains("blocks R1", gov.Deliberations[0].Steps.Select(s => s.Action));
        Assert.Contains("A 6% deficit is above the 3% limit.", chamber.Prompts.Last(p => p.Contains("Propose a revised budget")));
    }

    [Fact]
    public void ABlockWithoutAFloorStopsNothing()
    {
        var chamber = new Chamber
        {
            Executive = _ => Budget(MoreEducation),
            Judiciary = _ => Vote("block", ",\"floor\":\"I dislike it\",\"breachYear\":2025,\"path\":\"Bad policy.\""),
        };
        var (run, gov) = RunOneYear(chamber);

        Assert.False(run.Decisions[0].CarriedOver);
        Assert.Contains("dissents (invalid block)", gov.Deliberations[0].Steps.Select(s => s.Action));
    }

    [Fact]
    public void AnExecutiveThatCannotProduceABudgetLosesTheRoundNotTheYear()
    {
        var chamber = new Chamber { Executive = n => n <= 2 ? "no json" : Budget(MoreEducation) };
        var (run, gov) = RunOneYear(chamber);

        Assert.False(run.Decisions[0].CarriedOver);
        Assert.Equal(["abstains", "proposes R1", "approves R1", "passes R1", "enacts R1"], gov.Deliberations[0].Steps.Select(s => s.Action));
    }
}
