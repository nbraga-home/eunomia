using Eunomia.Core.Model;

namespace Eunomia.Tests;

public class JudgeTests
{
    private static RunSummary S(double final, double worst, bool clean = true) => new()
    {
        Occupant = "x", ParameterSet = "central",
        Status = clean ? RunStatus.Clean : RunStatus.Breached,
        Breaches = clean ? [] : [new Breach("debt", 3, false, "")],
        StartMedian = 100, FinalMedian = final, WorstMedian = worst,
        FinalLowestFifth = 0, StartLowestFifth = 0, FinalDebtRatio = 0, FinalOutput = 0, StartOutput = 0,
    };

    [Fact] public void CleanBeatsBreached() => Assert.Equal(Outcome.AWins, Judge.Compare(S(100, 90), S(200, 190, clean: false), 0.001));
    [Fact] public void BothBreachedHasNoWinner() => Assert.Equal(Outcome.BothBreached, Judge.Compare(S(1, 1, false), S(2, 2, false), 0.001));
    [Fact] public void BetterOnBothWins() => Assert.Equal(Outcome.AWins, Judge.Compare(S(110, 95), S(100, 90), 0.001));
    [Fact] public void BetterOnOneEqualOnOtherWins() => Assert.Equal(Outcome.BWins, Judge.Compare(S(100, 90), S(110, 90), 0.001));
    [Fact] public void MixedIsATradeOff() => Assert.Equal(Outcome.TradeOff, Judge.Compare(S(120, 80), S(100, 90), 0.001));
    [Fact] public void DifferenceInsideToleranceIsATie() => Assert.Equal(Outcome.TradeOff, Judge.Compare(S(100.05, 90), S(100, 90), 0.001));
}
