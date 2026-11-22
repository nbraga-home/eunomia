using Eunomia.Core.Config;

namespace Eunomia.Core.Model;

/// <summary>What the regime does to schooling this year: quality and coverage-odds multipliers by fifth, against the start.</summary>
public sealed record RegimeEffects(double[] QualityFactor, double[][] OddsFactor)
{
    public static RegimeEffects None { get; } = new([1, 1, 1, 1, 1], [[1, 1, 1, 1, 1], [1, 1, 1, 1, 1], [1, 1, 1, 1, 1]]);
}

/// <summary>
/// Money the regime moves, as changes against what the starting regime would move this year (the starting flows
/// are already inside the calibrated data). HouseholdNet is per fifth: grants received minus fees and top-ups paid.
/// SchoolMoney is extra money for schools: fees and top-ups, minus grants and testing administration.
/// </summary>
public sealed record EducationFlows(double[] HouseholdNet, double SchoolMoney, double Fees, double Grants, double TopUps, double Admin)
{
    public static EducationFlows None { get; } = new(new double[5], 0, 0, 0, 0, 0);
}

/// <summary>
/// The education regime in force and its phase-in. Each choice maps to numbers (private pupil share, fees and
/// grants by fifth, selectiveness). A change moves the numbers in force toward the new ones in equal steps over
/// the transition years. The earmark is a commitment: a floor on education's budget share for a set number of years.
/// </summary>
public sealed class EducationRegimeState
{
    private const int Fifths = 5;
    private static readonly double[] Rank = [-1, -0.5, 0, 0.5, 1];

    private readonly EducationRegimeParams _p;
    private readonly double[] _start;
    private readonly double[] _current;
    private double[] _target;
    private double[] _step;
    private readonly int _startCompulsory;
    private readonly int _compulsory;
    private readonly EducationRegime _startRegime;

    public EducationRegimeState(EducationRegimeParams p, EducationRegime start, int startCompulsoryAge, int constitutionCompulsoryAge)
    {
        _p = p;
        _startRegime = start;
        Regime = start;
        _start = Numbers(start);
        _current = (double[])_start.Clone();
        _target = (double[])_start.Clone();
        _step = new double[_start.Length];
        _startCompulsory = startCompulsoryAge;
        _compulsory = constitutionCompulsoryAge;
    }

    /// <summary>The regime enacted (the target of any phase-in).</summary>
    public EducationRegime Regime { get; private set; }

    public bool EarmarkActive => EarmarkYearsLeft > 0;
    public int EarmarkYearsLeft { get; private set; }
    public double EarmarkFloor { get; private set; }

    /// <summary>Share of pupils in private schools now, during any phase-in.</summary>
    public double PrivateShare => _current[0];

    /// <summary>True while any choice is still phasing in.</summary>
    public bool Transitioning => _current.Zip(_target).Any(x => Math.Abs(x.First - x.Second) > 1e-12);

    /// <summary>Enacts a regime. Starts the phase-in, and the earmark's clock when one is newly enacted.</summary>
    public void Enact(EducationRegime regime, double educationShare)
    {
        if (regime.Earmark && !EarmarkActive)
        {
            EarmarkYearsLeft = _p.EarmarkYears;
            EarmarkFloor = Math.Max(0, educationShare - _p.EarmarkMargin);
        }
        if (!regime.Earmark) EarmarkYearsLeft = 0;

        if (regime with { Earmark = false } != Regime with { Earmark = false })
        {
            _target = Numbers(regime);
            _step = _target.Zip(_current, (t, c) => Math.Abs(t - c) / Math.Max(1, _p.TransitionYears)).ToArray();
        }
        Regime = regime;
    }

    /// <summary>One year of phase-in, and one year off the earmark's clock. Returns true if the earmark just expired.</summary>
    public bool Advance()
    {
        for (int i = 0; i < _current.Length; i++)
        {
            double gap = _target[i] - _current[i];
            _current[i] += Math.Sign(gap) * Math.Min(Math.Abs(gap), _step[i]);
        }
        if (EarmarkYearsLeft > 0 && --EarmarkYearsLeft == 0)
        {
            Regime = Regime with { Earmark = false };
            return true;
        }
        return false;
    }

    public double TakeUp(double capacity) => Math.Min(1, _p.TakeUpBase * Math.Pow(Math.Max(capacity, 1e-9), _p.TakeUpCapacityExponent));

    public RegimeEffects Effects(double capacity)
    {
        var (fee, grant) = Effective(_current, Regime, TakeUp(capacity));
        var (fee0, grant0) = Effective(_start, _startRegime, TakeUp(1.0));
        double s = Selective(_current), s0 = Selective(_start);
        double pi = PrivateShare, pi0 = _start[0], e = _p.PrivateEfficiency;

        var quality = new double[Fifths];
        var odds = new[] { new double[Fifths], new double[Fifths], new double[Fifths] };
        double basicRule = (_compulsory >= 15 ? _p.CompulsoryBoost.Basic : 1) / (_startCompulsory >= 15 ? _p.CompulsoryBoost.Basic : 1);
        double secondaryRule = (_compulsory >= 18 ? _p.CompulsoryBoost.Secondary : 1) / (_startCompulsory >= 18 ? _p.CompulsoryBoost.Secondary : 1);
        for (int g = 0; g < Fifths; g++)
        {
            quality[g] = (1 + pi * (e - 1)) / (1 + pi0 * (e - 1))
                * (1 + _p.SortingStrength * pi * Rank[g]) / (1 + _p.SortingStrength * pi0 * Rank[g])
                * (1 + s * _p.SelectiveQualityGain) / (1 + s0 * _p.SelectiveQualityGain);
            double selective = Math.Exp(-(s - s0) * _p.SelectivePenalty[g]);
            odds[0][g] = basicRule;
            odds[1][g] = secondaryRule * selective;
            odds[2][g] = selective * Math.Exp(-_p.PriceSensitivity[g] * ((fee[g] - grant[g]) - (fee0[g] - grant0[g])));
        }
        return new RegimeEffects(quality, odds);
    }

    /// <summary>
    /// The year's money, as changes against the starting regime. A year of higher education costs the average
    /// spending per pupil-year; students per fifth are coverage times years in higher education, per fifth.
    /// </summary>
    public EducationFlows Flows(double educationDelivered, IReadOnlyList<double> higherCoverage, double higherYears,
        double pupilYearsPerCohort, IReadOnlyList<double> disposable, double capacity)
    {
        var now = Money(_current, Regime, TakeUp(capacity), educationDelivered, higherCoverage, higherYears, pupilYearsPerCohort, disposable);
        var start = Money(_start, _startRegime, TakeUp(1.0), educationDelivered, higherCoverage, higherYears, pupilYearsPerCohort, disposable);
        return new EducationFlows(
            now.HouseholdNet.Zip(start.HouseholdNet, (a, b) => a - b).ToArray(),
            now.SchoolMoney - start.SchoolMoney,
            now.Fees - start.Fees, now.Grants - start.Grants, now.TopUps - start.TopUps, now.Admin - start.Admin);
    }

    private EducationFlows Money(double[] numbers, EducationRegime regime, double takeUp, double delivered,
        IReadOnlyList<double> higherCoverage, double higherYears, double pupilYears, IReadOnlyList<double> disposable)
    {
        var (fee, grant) = Effective(numbers, regime, takeUp);
        double costPerYear = pupilYears > 0 ? delivered / pupilYears : 0; // per cohort-unit pupil-year
        var net = new double[Fifths];
        double fees = 0, grants = 0, topUps = 0, admin = 0;
        for (int g = 0; g < Fifths; g++)
        {
            double students = higherCoverage[g] * higherYears / Fifths;
            double f = fee[g] * costPerYear * students;
            double gr = grant[g] * costPerYear * students;
            double top = Rank[g] > 0 ? _p.TopUpShareOfIncome * numbers[0] * disposable[g] : 0;
            fees += f; grants += gr; topUps += top;
            net[g] = gr - f - top;

            bool tested = regime.Support == StudentSupport.Tested && gr > 0;
            double exemption = regime.UniversityFees == UniversityFees.IncomeScaled ? Math.Max(0, _p.FlatFee - fee[g]) * costPerYear * students : 0;
            admin += _p.TestingAdminCost * ((tested ? gr : 0) + exemption);
        }
        return new EducationFlows(net, fees + topUps - grants - admin, fees, grants, topUps, admin);
    }

    /// <summary>Fees and grants actually paid by fifth: means-tested items reach only those who apply.</summary>
    private (double[] Fee, double[] Grant) Effective(double[] numbers, EducationRegime regime, double takeUp)
    {
        var fee = new double[Fifths];
        var grant = new double[Fifths];
        for (int g = 0; g < Fifths; g++)
        {
            double f = numbers[1 + g], gr = numbers[6 + g];
            fee[g] = regime.UniversityFees == UniversityFees.IncomeScaled && f < _p.FlatFee ? _p.FlatFee - takeUp * (_p.FlatFee - f) : f;
            grant[g] = regime.Support == StudentSupport.Tested ? gr * takeUp : gr;
        }
        return (fee, grant);
    }

    private static double Selective(double[] numbers) => numbers[11];

    /// <summary>[private share, fee by fifth ×5, grant by fifth ×5, selectiveness].</summary>
    private double[] Numbers(EducationRegime r)
    {
        var n = new double[12];
        n[0] = r.Provision switch
        {
            Provision.Public => _p.PrivatePupilShare.Public,
            Provision.Mixed => _p.PrivatePupilShare.Mixed,
            _ => _p.PrivatePupilShare.Private,
        };
        for (int g = 0; g < Fifths; g++)
        {
            n[1 + g] = r.UniversityFees switch
            {
                UniversityFees.Free => 0,
                UniversityFees.Flat => _p.FlatFee,
                _ => _p.FlatFee * _p.IncomeScaledFees[g],
            };
            n[6 + g] = r.Support == StudentSupport.Universal ? _p.UniversalGrant : g < _p.TestedFifths ? _p.TestedGrant : 0;
        }
        n[11] = r.Standards == Standards.Selective ? 1 : 0;
        return n;
    }
}
