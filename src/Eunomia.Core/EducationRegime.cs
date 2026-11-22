namespace Eunomia.Core;

public enum Provision { Public, Mixed, Private }
public enum UniversityFees { Free, Flat, IncomeScaled }
public enum StudentSupport { Universal, Tested }
public enum Standards { Comprehensive, Selective }

/// <summary>
/// The structure of a country's education: who provides it, what university costs, how students are supported,
/// how selective schools are, and whether education money is earmarked. Changed rarely: each change uses part of
/// the year's capacity for change and phases in over several years.
/// </summary>
public sealed record EducationRegime(
    Provision Provision,
    UniversityFees UniversityFees,
    StudentSupport Support,
    Standards Standards,
    bool Earmark)
{
    /// <summary>How many of the five choices differ from another regime.</summary>
    public int Changes(EducationRegime other) =>
        (Provision != other.Provision ? 1 : 0) + (UniversityFees != other.UniversityFees ? 1 : 0) +
        (Support != other.Support ? 1 : 0) + (Standards != other.Standards ? 1 : 0) + (Earmark != other.Earmark ? 1 : 0);
}
