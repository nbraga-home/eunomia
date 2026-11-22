using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Governments.Ai;

namespace Eunomia.Tests;

public class EducationTests
{
    private static SimulationConfig C => Repo.LoadPortugal();

    private static EducationSystem Build(EducationData data, EducationParams? p = null) =>
        new(p ?? C.Economy.Education, C.Economy.Stocks.Skills, C.Economy.Stocks.Schools, data, effectiveSpending0: 100);

    private static EducationData Same(LevelsByFifth levels) => new() { Coverage = levels, Workforce = levels };

    [Fact]
    public void WhenTheWorkforceIsSchooledLikeTodaysLeaversNothingMoves()
    {
        var e = Build(Same(C.Pack.Education.Coverage));
        for (int i = 0; i < 40; i++) e.Advance(100, cohortSize: 1);
        Assert.Equal(1.0, e.SkillsIndex, 9);
        Assert.Equal(1.0, e.Quality, 9);
        Assert.Equal(C.Pack.Education.Coverage.Higher[0], e.HigherCoverage[0], 6);
    }

    [Fact]
    public void GenerationsRaiseSkillsWithNoPolicyChange()
    {
        // Portugal's data: today's leavers are better schooled than the workforce.
        var e = Build(C.Pack.Education);
        for (int i = 0; i < 40; i++) e.Advance(100, cohortSize: 1);
        Assert.True(e.SkillsIndex > 1.10);
    }

    [Fact]
    public void ParentsSchoolingRaisesTheNextGenerationsCoverage()
    {
        var p = C.Economy.Education;
        var with = Build(C.Pack.Education, p);
        var without = Build(C.Pack.Education, p with { ParentElasticity = 0 });
        for (int i = 0; i < 40; i++) { with.Advance(100, 1); without.Advance(100, 1); }
        Assert.True(with.HigherCoverage[0] > without.HigherCoverage[0]);
    }

    [Fact]
    public void WiderCoverageWithoutMoreMoneyLowersQuality()
    {
        // Coverage rises through the parents' schooling while spending stays flat, so each pupil gets less.
        var e = Build(C.Pack.Education);
        for (int i = 0; i < 40; i++) e.Advance(100, cohortSize: 1);
        Assert.True(e.Quality < 1.0);
    }

    [Fact]
    public void WagesFollowEachFifthsSkills()
    {
        // Only the poorest fifth's workforce lags its school leavers, so only it gains relative skills.
        var coverage = C.Pack.Education.Coverage;
        var workforce = coverage with { Higher = [0.05, .. coverage.Higher.Skip(1)] };
        var e = Build(new EducationData { Coverage = coverage, Workforce = workforce });
        for (int i = 0; i < 40; i++) e.Advance(100, 1);

        var start = C.Pack.Groups.WageWeights;
        var now = e.WageWeights(start);
        Assert.True(now[0] > start[0]);
        Assert.Equal(1.0, now.Sum(), 12);
    }

    [Fact]
    public void TheReportShowsEducationByFifth()
    {
        string? prompt = null;
        var ai = new SingleAi(new ScriptedModel(p => { prompt ??= p; return "no json"; }), "mandate");
        Fixtures.RunFlat(C with { Government = C.Government with { HorizonYears = 1 } }, ai);
        Assert.Contains("## Education, last year", prompt);
        Assert.Contains("| poorest |", prompt);
        Assert.Contains("schools, health, infrastructure and state capacity", prompt);
    }

    [Fact]
    public void TheReportExplainsTheGuardAndShowsItsGauge()
    {
        string? prompt = null;
        var ai = new SingleAi(new ScriptedModel(p => { prompt ??= p; return "no json"; }), "mandate");
        Fixtures.RunFlat(C with { Government = C.Government with { HorizonYears = 1 } }, ai);
        Assert.Contains("compared with a typical government facing the same world", prompt);
        Assert.Contains("Poorest fifth's income as a share of the median household's:", prompt);
    }
}
