using Eunomia.Core;

namespace Eunomia.Governments;

/// <summary>Keeps the starting tax rates, deficit and budget shares every year. A sanity check, not a real government.</summary>
public sealed class Frozen : IGovernment
{
    public string Name => "frozen";

    public Policy? Decide(MeetingContext context) => context.StartingPolicy;
}
