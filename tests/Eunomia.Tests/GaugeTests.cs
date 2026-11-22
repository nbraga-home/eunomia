using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Governments.Ai;

namespace Eunomia.Tests;

public class GaugeTests
{
    [Fact]
    public void PipelineAfterMatchesWhatAdvanceDoes()
    {
        var stock = new Stock(new StockParams { DelayYears = 3, Depreciation = 0.05 }, baseline: 10);
        double predicted = stock.PipelineAverageAfter(4);
        stock.Advance(4);
        Assert.Equal(predicted, stock.PipelineAverage, 12);
    }

    [Fact]
    public void AnUnchangedBudgetCommitsWhatHoldsEachIndexAtOne()
    {
        var c = Fixtures.Stationary();
        IReadOnlyList<Commitment>? seen = null;
        Fixtures.RunFlat(c, new Scripted("look", ctx => { seen ??= ctx.Commitments; return ctx.StartingPolicy; }));

        Assert.Equal(4, seen!.Count);
        double expected = 1 / c.Government.Floors.EndStateStocks.Threshold; // holding at 1, measured against 0.98
        Assert.All(seen, g => Assert.Equal(expected, g.Committed, 9));
    }

    [Theory]
    [InlineData(0.00)]  // keep education: no breach
    [InlineData(0.05)]  // cut education hard from year 25: breach
    public void TheGaugeInTheFinalYearPredictsTheFloor(double cut)
    {
        var c = Repo.LoadPortugal();
        Commitment? finalSkills = null;
        var g = new Scripted("cutter", ctx =>
        {
            var p = ctx.Enacted;
            if (ctx.Year >= 25 && cut > 0)
            {
                var s = p.BudgetShares.ToArray();
                double move = Math.Min(cut, s[(int)BudgetLine.Education]);
                s[(int)BudgetLine.Education] -= move;
                s[(int)BudgetLine.SocialTransfers] += move;
                p = p with { BudgetShares = BudgetShares.FromArray(s) };
            }
            var check = ctx.Check(p);
            if (ctx.Year == ctx.HorizonYears - 1) finalSkills = check.Commitments.Single(x => x.Stock == "schools");
            return check.Feasible;
        });
        var run = Fixtures.RunFlat(c, g);

        bool gaugeSaysBreach = finalSkills!.Committed < c.Government.Floors.EndStatePipelines.Share;
        bool breached = run.Summary.Breaches.Any(b => b.Floor == "endStatePipelines" && b.Detail.StartsWith("schools"));
        Assert.Equal(gaugeSaysBreach, breached);
        Assert.Equal(cut > 0, breached);
    }

    [Fact]
    public void TheReportShowsTheGauge()
    {
        var c = Repo.LoadPortugal();
        string? prompt = null;
        var ai = new SingleAi(new ScriptedModel(p => { prompt ??= p; return "no json"; }), "mandate");
        Fixtures.RunFlat(c with { Government = c.Government with { HorizonYears = 1 } }, ai);
        Assert.Contains("Committed spending (for the final-year floor)", prompt);
        Assert.Contains("| state capacity |", prompt);
    }
}
