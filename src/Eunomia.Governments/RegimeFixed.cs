using Eunomia.Core;

namespace Eunomia.Governments;

/// <summary>
/// A search tool, not a government: follows another occupant's yearly budget but holds one education regime
/// throughout, enacted in year 0. Comparing regimes on the same budget and worlds isolates what the regime does.
/// </summary>
public sealed class RegimeFixed(IGovernment budget, EducationRegime regime) : IGovernment
{
    public string Name => $"{budget.Name}+{Code(regime)}";

    public Policy? Decide(MeetingContext context)
    {
        var p = budget.Decide(context);
        return (p ?? context.Enacted) with { EducationRegime = regime with { Earmark = false } };
    }

    public static string Code(EducationRegime r) =>
        $"{Word(r.Provision)}/{Word(r.UniversityFees)}/{Word(r.Support)}/{Word(r.Standards)}";

    public static IEnumerable<EducationRegime> All() =>
        from p in Enum.GetValues<Provision>()
        from f in Enum.GetValues<UniversityFees>()
        from s in Enum.GetValues<StudentSupport>()
        from st in Enum.GetValues<Standards>()
        select new EducationRegime(p, f, s, st, Earmark: false);

    private static string Word<T>(T v) where T : struct, Enum { string s = v.ToString(); return char.ToLowerInvariant(s[0]) + s[1..]; }
}
