namespace Eunomia.Core;

/// <summary>The seven budget lines, in the fixed order every loop uses.</summary>
public enum BudgetLine
{
    Health,
    Education,
    Infrastructure,
    Agriculture,
    SocialTransfers,
    Security,
    Administration,
}

public static class BudgetLines
{
    public static readonly BudgetLine[] All = Enum.GetValues<BudgetLine>();
    public const int Count = 7;
}

public sealed record Taxes(double Income, double Corporate, double Consumption);

/// <summary>Shares of the annual envelope going to each line. They sum to 1.</summary>
public sealed record BudgetShares(
    double Health,
    double Education,
    double Infrastructure,
    double Agriculture,
    double SocialTransfers,
    double Security,
    double Administration)
{
    public double this[BudgetLine line] => line switch
    {
        BudgetLine.Health => Health,
        BudgetLine.Education => Education,
        BudgetLine.Infrastructure => Infrastructure,
        BudgetLine.Agriculture => Agriculture,
        BudgetLine.SocialTransfers => SocialTransfers,
        BudgetLine.Security => Security,
        BudgetLine.Administration => Administration,
        _ => throw new ArgumentOutOfRangeException(nameof(line)),
    };

    public double[] ToArray() => [Health, Education, Infrastructure, Agriculture, SocialTransfers, Security, Administration];

    public static BudgetShares FromArray(IReadOnlyList<double> s)
    {
        if (s.Count != BudgetLines.Count) throw new ArgumentException("Need exactly seven shares.");
        return new(s[0], s[1], s[2], s[3], s[4], s[5], s[6]);
    }

    public double Sum() => ToArray().Sum();
}

/// <summary>
/// The part of each sector's money spent on running it (management, systems), as a share of the line.
/// Social transfers are cash and have no management share.
/// </summary>
public sealed record ManagementShares(
    double Health,
    double Education,
    double Infrastructure,
    double Agriculture,
    double Security,
    double Administration)
{
    /// <summary>The six lines that have a management share, in order.</summary>
    public static readonly BudgetLine[] Lines =
        [BudgetLine.Health, BudgetLine.Education, BudgetLine.Infrastructure, BudgetLine.Agriculture, BudgetLine.Security, BudgetLine.Administration];

    public double this[BudgetLine line] => line switch
    {
        BudgetLine.Health => Health,
        BudgetLine.Education => Education,
        BudgetLine.Infrastructure => Infrastructure,
        BudgetLine.Agriculture => Agriculture,
        BudgetLine.Security => Security,
        BudgetLine.Administration => Administration,
        _ => 0,
    };

    public double[] ToArray() => [Health, Education, Infrastructure, Agriculture, Security, Administration];

    public static ManagementShares FromArray(IReadOnlyList<double> m) => m.Count == 6
        ? new(m[0], m[1], m[2], m[3], m[4], m[5])
        : throw new ArgumentException("Need exactly six management shares.");

    public static ManagementShares Uniform(double share) => new(share, share, share, share, share, share);
}

/// <summary>
/// What a government decides: three tax rates, a planned deficit (share of output), the budget split, the
/// management share of each sector, and the education regime. Null management shares or regime mean
/// "keep what is in force".
/// </summary>
public sealed record Policy(
    Taxes Taxes,
    double PlannedDeficit,
    BudgetShares BudgetShares,
    ManagementShares? ManagementShares = null,
    EducationRegime? EducationRegime = null);
