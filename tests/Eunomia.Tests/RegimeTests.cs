using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Core.World;
using Eunomia.Governments.Ai;

namespace Eunomia.Tests;

public class RegimeTests
{
    private static SimulationConfig C => Repo.LoadPortugal();
    private static EducationRegime Start => C.Pack.Policy.EducationRegime!;

    /// <summary>Keeps the starting policy but enacts a regime in year 0.</summary>
    private static Scripted With(EducationRegime regime) => new("regime", ctx => ctx.StartingPolicy with { EducationRegime = regime });

    private static RunResult Run(EducationRegime regime, SimulationConfig? c = null) => Fixtures.RunFlat(c ?? C, With(regime));

    [Fact]
    public void TheStartingRegimeChangesNothing()
    {
        var p = C.Economy.EducationRegimes;
        var state = new EducationRegimeState(p, Start, 18, 18);
        var effects = state.Effects(capacity: 1.0);
        Assert.All(effects.QualityFactor, q => Assert.Equal(1.0, q, 12));
        Assert.All(effects.OddsFactor.SelectMany(x => x), o => Assert.Equal(1.0, o, 12));
        var flows = state.Flows(100, [0.2, 0.3, 0.4, 0.6, 0.8], 4, 13, [10, 20, 30, 40, 60], 1.0);
        Assert.Equal(0, flows.SchoolMoney, 12);
        Assert.All(flows.HouseholdNet, n => Assert.Equal(0, n, 12));
    }

    [Fact]
    public void FreeUniversityHelpsThePoorestMost()
    {
        var baseRun = Fixtures.RunFlat(C, Fixtures.Frozen);
        var free = Run(Start with { UniversityFees = UniversityFees.Free });
        double poorGain = free.Years[^1].HigherByFifth[0] - baseRun.Years[^1].HigherByFifth[0];
        double richGain = free.Years[^1].HigherByFifth[4] - baseRun.Years[^1].HigherByFifth[4];
        Assert.True(poorGain > 0);
        Assert.True(poorGain > richGain);
    }

    [Fact]
    public void SelectiveSchoolsTeachMoreButKeepFewerOfThePoorest()
    {
        var baseRun = Fixtures.RunFlat(C, Fixtures.Frozen);
        var selective = Run(Start with { Standards = Standards.Selective });
        Assert.True(selective.Years[^1].SecondaryByFifth[0] < baseRun.Years[^1].SecondaryByFifth[0]);
        Assert.True(selective.Years[^1].SchoolQuality >= baseRun.Years[^1].SchoolQuality * 0.98); // national quality, before the selective bonus per pupil
        var state = new EducationRegimeState(C.Economy.EducationRegimes, Start, 18, 18);
        state.Enact(Start with { Standards = Standards.Selective }, 0.10);
        for (int i = 0; i < 4; i++) state.Advance();
        Assert.True(state.Effects(1.0).QualityFactor[2] > 1.0);
    }

    [Fact]
    public void PrivateProvisionSortsQualityTowardTheRichest()
    {
        var p = C.Economy.EducationRegimes;
        var state = new EducationRegimeState(p, Start, 18, 18);
        state.Enact(Start with { Provision = Provision.Private }, 0.10);
        for (int i = 0; i < p.TransitionYears; i++) state.Advance();
        var q = state.Effects(1.0).QualityFactor;
        Assert.True(q[4] > q[0]);
        var flows = state.Flows(100, [0.2, 0.3, 0.4, 0.6, 0.8], 4, 13, [10, 20, 30, 40, 60], 1.0);
        Assert.True(flows.HouseholdNet[4] < 0); // the richest pay top-ups
        Assert.Equal(0, flows.HouseholdNet[0], 12);
    }

    [Fact]
    public void ARegimeChangePhasesIn()
    {
        var p = C.Economy.EducationRegimes;
        var state = new EducationRegimeState(p, Start, 18, 18);
        state.Enact(Start with { Provision = Provision.Mixed }, 0.10);
        state.Advance();
        Assert.Equal(p.PrivatePupilShare.Mixed / p.TransitionYears, state.PrivateShare, 12);
        for (int i = 1; i < p.TransitionYears; i++) state.Advance();
        Assert.Equal(p.PrivatePupilShare.Mixed, state.PrivateShare, 12);
        Assert.False(state.Transitioning);
    }

    [Fact]
    public void RegimeChangesUseTheCapacityForChange()
    {
        var c = C;
        var sim = new Simulation(c, "central", WorldGenerator.Flat(c.World, c.Government.HorizonYears));
        var start = sim.Opening.Policy;
        var s = start.BudgetShares.ToArray();
        s[(int)BudgetLine.SocialTransfers] -= 0.05;
        s[(int)BudgetLine.Education] += 0.05;
        var twoChanges = start with
        {
            BudgetShares = BudgetShares.FromArray(s),
            EducationRegime = Start with { UniversityFees = UniversityFees.Free, Standards = Standards.Selective },
        };
        var result = sim.Check(twoChanges);
        Assert.True(result.Ok);
        Assert.True(result.Scaled);
        double moved = result.Feasible!.BudgetShares.Education - start.BudgetShares.Education;
        Assert.Equal(c.Government.ReallocationAllowance - 2 * c.Economy.EducationRegimes.RegimeChangeCost, moved, 9);
    }

    [Fact]
    public void AnEarmarkHoldsTheEducationShareAndThenExpires()
    {
        var c = C;
        var cutAfterEarmark = new Scripted("earmark", ctx =>
        {
            var p = ctx.StartingPolicy with { EducationRegime = Start with { Earmark = true } };
            if (ctx.Year == 0) return p;
            var s = ctx.Enacted.BudgetShares.ToArray();
            s[(int)BudgetLine.Education] -= 0.01;
            s[(int)BudgetLine.SocialTransfers] += 0.01;
            return ctx.Enacted with { BudgetShares = BudgetShares.FromArray(s) };
        });
        var run = Fixtures.RunFlat(c, cutAfterEarmark);
        int years = c.Economy.EducationRegimes.EarmarkYears;
        Assert.True(run.Decisions[1].CarriedOver);
        Assert.Contains(run.Decisions[1].Errors, e => e.Contains("earmark"));
        Assert.True(run.Years[years - 1].EarmarkActive);
        Assert.False(run.Years[years].EarmarkActive);
        Assert.True(run.Years[^1].Policy.BudgetShares.Education < c.Pack.Policy.BudgetShares.Education); // cuts resume after expiry
    }

    [Fact]
    public void DroppingCompulsorySchoolingLowersCoverageAndCanBreakTheRightToEducation()
    {
        var c = C with { Government = C.Government with { Constitution = new Constitution { CompulsoryAge = 0 } } };
        var run = Fixtures.RunFlat(c, Fixtures.Frozen);
        var baseRun = Fixtures.RunFlat(C, Fixtures.Frozen);
        Assert.True(run.Years[^1].SecondaryByFifth[0] < baseRun.Years[^1].SecondaryByFifth[0]);
        Assert.Contains(run.Summary.Breaches, b => b.Floor == "rightToEducation");
        Assert.DoesNotContain(baseRun.Summary.Breaches, b => b.Floor == "rightToEducation");
    }

    [Fact]
    public void ParserReadsTheRegimeAndRejectsUnknownWords()
    {
        const string budget = """
            {"taxes":{"income":0.42,"corporate":0.2,"consumption":0.24},"plannedDeficit":0,
             "budgetShares":{"health":0.165,"education":0.1,"infrastructure":0.14,"agriculture":0.012,"socialTransfers":0.42,"security":0.057,"administration":0.106},
             "educationRegime":{"provision":"mixed","universityFees":"incomeScaled","support":"universal","standards":"comprehensive","earmark":true}}
            """;
        var p = ReplyParser.Parse(budget);
        Assert.True(p.Ok, string.Join("; ", p.Errors));
        Assert.Equal(new EducationRegime(Provision.Mixed, UniversityFees.IncomeScaled, StudentSupport.Universal, Standards.Comprehensive, true), p.Policy!.EducationRegime);

        var bad = ReplyParser.Parse(budget.Replace("\"mixed\"", "\"voucher\""));
        Assert.Contains(bad.Errors, e => e.Contains("provision"));
    }

    [Fact]
    public void TheReportShowsTheRegimeAndTheConstitution()
    {
        string? prompt = null;
        var ai = new SingleAi(new ScriptedModel(p => { prompt ??= p; return "no json"; }), "mandate");
        Fixtures.RunFlat(C with { Government = C.Government with { HorizonYears = 1 } }, ai);
        Assert.Contains("## Education regime", prompt);
        Assert.Contains("schooling compulsory to 18", prompt);
        Assert.Contains("\"educationRegime\"", prompt);
    }
}
