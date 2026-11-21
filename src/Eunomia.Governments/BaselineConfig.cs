namespace Eunomia.Governments;

public sealed record StatusQuoParams
{
    public required double DeficitLimit { get; init; }
    public required double DeficitCut { get; init; }
    public required double ConsumptionTaxStep { get; init; }
    public required double UnwindBelow { get; init; }
    public required int UnwindYears { get; init; }
    public required double RecessionGrowth { get; init; }
    public required double RecoveryGrowth { get; init; }
    public required double CrisisDeficitRise { get; init; }
    public required double CrisisShareShift { get; init; }
    public required double ElectionShareShift { get; init; }
}

public sealed record TechnocratTargets
{
    public required double Education { get; init; }
    public required double Infrastructure { get; init; }
    public required double Administration { get; init; }
    public required double Health { get; init; }
}

public sealed record TechnocratParams
{
    public required double YearlyShift { get; init; }
    public required double TransfersFloorShare { get; init; }
    public required TechnocratTargets Targets { get; init; }
    public required double RecessionGrowth { get; init; }
    public required double RecessionDeficitRise { get; init; }
}

public sealed record BaselineConfig
{
    public required StatusQuoParams StatusQuo { get; init; }
    public required TechnocratParams Technocrat { get; init; }
}
