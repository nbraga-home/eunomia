using Eunomia.Core.Config;

namespace Eunomia.Core.Model;

/// <summary>One year of a sector's money, split by the template.</summary>
public readonly record struct SectorSplit(double Management, double FrontLine, double Absorbed, double Effective)
{
    /// <summary>Front-line money above what the sector could absorb this year.</summary>
    public double Excess => FrontLine - Absorbed;
}

/// <summary>
/// The sector template for one budget line. Part of the line (the management share) goes to running the sector
/// and builds its management index, a slow stock. The rest is front-line money. Only front-line money times
/// efficiency builds the sector's own stock (skills, health, ...), and only as fast as the sector can absorb:
/// front-line money can grow by absorptionGrowth × efficiency a year over what it absorbed last year, and money
/// above that builds only excessEffectiveness as much. At the starting share and spending, nothing changes:
/// the index stays at 1 and every euro of front-line money is absorbed.
/// </summary>
public sealed class Sector
{
    private readonly ManagementParams _p;
    private readonly Stock _management;

    public Sector(BudgetLine line, ManagementParams p, double startShare, double delivered)
    {
        Line = line;
        _p = p;
        _management = new Stock(new StockParams { DelayYears = p.DelayYears, Depreciation = p.Depreciation }, delivered * startShare);
        Absorbed = delivered * (1 - startShare);
    }

    public BudgetLine Line { get; }

    /// <summary>Front-line money the sector absorbed last year: the base its capacity grows from.</summary>
    public double Absorbed { get; private set; }

    public double ManagementIndex => _management.Value;

    /// <summary>How much each euro of front-line money achieves, against the start. Diminishing returns, with a ceiling.</summary>
    public double Efficiency => Math.Min(_p.MaxEfficiency, Math.Pow(Math.Max(ManagementIndex, 1e-9), _p.Exponent));

    /// <summary>How fast front-line money can grow this year without waste.</summary>
    public double AbsorbableGrowth => _p.AbsorptionGrowth * Efficiency;

    public SectorSplit Split(double delivered, double managementShare)
    {
        double management = delivered * managementShare;
        double front = delivered - management;
        double absorbed = Absorbed > 0 ? Math.Min(front, Absorbed * (1 + AbsorbableGrowth)) : front;
        double effective = Efficiency * (absorbed + _p.ExcessEffectiveness * (front - absorbed));
        return new SectorSplit(management, front, absorbed, effective);
    }

    /// <summary>Applies this year's money: the management stock moves and next year's absorption base is set.</summary>
    public SectorSplit Advance(double delivered, double managementShare)
    {
        var split = Split(delivered, managementShare);
        _management.Advance(split.Management);
        Absorbed = split.Absorbed;
        return split;
    }
}
