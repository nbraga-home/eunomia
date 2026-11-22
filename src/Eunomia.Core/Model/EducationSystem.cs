using Eunomia.Core.Config;

namespace Eunomia.Core.Model;

/// <summary>
/// Education by income fifth, across generations.
/// <list type="bullet">
/// <item>Effective education spending builds the school stock. Quality is school stock per pupil, to a power below one.</item>
/// <item>Coverage: the share of each new generation, per fifth, completing basic, secondary and higher education.
/// It moves toward a target whose odds rise with quality and with the fifth's own workforce skills (parents' schooling).
/// More pupils dilute the school stock, so wider coverage without more money lowers quality.</item>
/// <item>Each year's school leavers carry skill = quality × (value of the levels completed). They join the workforce after
/// a delay, and a fixed share of the workforce retires each year.</item>
/// </list>
/// The state sets money and quality; each fifth's own propensity is data, never the state's achievement.
/// </summary>
public sealed class EducationSystem
{
    private const int Fifths = 5;

    private readonly EducationParams _p;
    private readonly double _retire;
    private readonly Stock _schools;
    private readonly double[] _workforce;
    private readonly double[] _workforce0;
    private readonly double[][] _coverage;       // [level][fifth]: basic, secondary, higher
    private readonly double[][] _baseOdds;       // conditional progression odds at the start
    private readonly Queue<double[]> _entrants = new();
    private readonly double _pupils0;

    public EducationSystem(EducationParams p, StockParams skills, StockParams schools, EducationData data, double effectiveSpending0)
    {
        _p = p;
        _retire = skills.Depreciation;
        _schools = new Stock(schools, effectiveSpending0);

        _coverage = [Clamp(data.Coverage.Basic), Clamp(data.Coverage.Secondary), Clamp(data.Coverage.Higher)];
        _baseOdds = new double[3][];
        for (int level = 0; level < 3; level++)
            _baseOdds[level] = Enumerable.Range(0, Fifths).Select(g => Odds(Math.Min(Progression(_coverage, level, g), 0.995))).ToArray();

        _workforce0 = Enumerable.Range(0, Fifths)
            .Select(g => Skill(data.Workforce.Basic[g], data.Workforce.Secondary[g], data.Workforce.Higher[g])).ToArray();
        _workforce = (double[])_workforce0.Clone();
        _pupils0 = PupilYears(_coverage);
        Quality = 1.0;

        var entrants0 = Enumerable.Range(0, Fifths).Select(g => Quality * Skill(_coverage[0][g], _coverage[1][g], _coverage[2][g])).ToArray();
        for (int i = 0; i < skills.DelayYears; i++) _entrants.Enqueue(entrants0);
    }

    /// <summary>The school stock: built by effective education spending. Checked by the end-state floor as "schools".</summary>
    public Stock Schools => _schools;

    /// <summary>How much a year of school teaches, against the start.</summary>
    public double Quality { get; private set; }

    /// <summary>The whole workforce's skills against the start. This is what output uses.</summary>
    public double SkillsIndex => _workforce.Average() / _workforce0.Average();

    /// <summary>Each fifth's workforce skills against its own start.</summary>
    public IReadOnlyList<double> SkillsByFifth => Enumerable.Range(0, Fifths).Select(g => _workforce[g] / _workforce0[g]).ToArray();

    public IReadOnlyList<double> BasicCoverage => _coverage[0];
    public IReadOnlyList<double> SecondaryCoverage => _coverage[1];
    public IReadOnlyList<double> HigherCoverage => _coverage[2];

    /// <summary>Pupil-years one generation spends in school, at today's coverage.</summary>
    public double PupilYearsPerCohort => PupilYears(_coverage);
    public double HigherYears => _p.LevelYears.Higher;

    /// <summary>Wage shares by fifth: the starting shares, scaled by each fifth's skills against its start, summing to 1.</summary>
    public double[] WageWeights(IReadOnlyList<double> start)
    {
        var w = Enumerable.Range(0, Fifths).Select(g => start[g] * Math.Pow(_workforce[g] / _workforce0[g], _p.WageElasticity)).ToArray();
        double sum = w.Sum();
        return w.Select(x => x / sum).ToArray();
    }

    /// <summary>One year: the school stock takes this year's effective spending, coverage and quality adjust, a generation leaves school and another retires.</summary>
    public void Advance(double effectiveSpending, double cohortSize, RegimeEffects? regime = null)
    {
        regime ??= RegimeEffects.None;
        _schools.Advance(effectiveSpending);

        // Coverage moves toward what quality and the parents' schooling make families want.
        for (int g = 0; g < Fifths; g++)
        {
            double pull = Math.Pow(Quality * regime.QualityFactor[g], _p.QualityElasticity) * Math.Pow(_workforce[g] / _workforce0[g], _p.ParentElasticity);
            double previous = 1.0;
            for (int level = 0; level < 3; level++)
            {
                double target = previous * Probability(_baseOdds[level][g] * pull * regime.OddsFactor[level][g]);
                _coverage[level][g] += _p.CoverageSpeed * (target - _coverage[level][g]);
                _coverage[level][g] = Math.Min(_coverage[level][g], previous); // levels stay nested
                previous = _coverage[level][g];
            }
        }

        // Quality: the school stock per pupil. More pupils (wider coverage, bigger generations) dilute it.
        double pupils = cohortSize * PupilYears(_coverage) / _pupils0;
        Quality = Math.Pow(_schools.Value / pupils, _p.SpendingElasticity);

        // A generation leaves school now and joins the workforce later; a share of the workforce retires.
        _entrants.Enqueue(Enumerable.Range(0, Fifths).Select(g => Quality * regime.QualityFactor[g] * Skill(_coverage[0][g], _coverage[1][g], _coverage[2][g])).ToArray());
        var arriving = _entrants.Dequeue();
        for (int g = 0; g < Fifths; g++)
            _workforce[g] = (1 - _retire) * _workforce[g] + _retire * arriving[g];
    }

    private double Skill(double basic, double secondary, double higher) =>
        _p.LevelSkill.Basic * basic + _p.LevelSkill.Secondary * secondary + _p.LevelSkill.Higher * higher;

    private double PupilYears(double[][] c) => Enumerable.Range(0, Fifths).Average(g =>
        _p.LevelYears.Basic * c[0][g] + _p.LevelYears.Secondary * c[1][g] + _p.LevelYears.Higher * c[2][g]);

    /// <summary>The chance of completing a level given the one below (or of completing basic at all).</summary>
    private static double Progression(double[][] c, int level, int g) =>
        level == 0 ? c[0][g] : c[level - 1][g] > 0 ? c[level][g] / c[level - 1][g] : 0;

    private static double Odds(double p) => p / (1 - p);
    private static double Probability(double odds) => odds / (1 + odds);
    private static double[] Clamp(IReadOnlyList<double> xs) => xs.Select(x => Math.Clamp(x, 0.001, 0.995)).ToArray();
}
