using Eunomia.Core.Config;

namespace Eunomia.Core.Model;

/// <summary>
/// A slow-moving index (1 at the start) filled by spending after a delay and drained by depreciation:
/// next = (1 - depreciation) * value + rate * spending that entered the pipeline <c>delay</c> years ago.
/// The rate is depreciation / baseline, so spending at the baseline holds the stock at exactly 1.
/// </summary>
public sealed class Stock
{
    private readonly double _depreciation;
    private readonly double _rate;
    private readonly double _baseline;
    private readonly int _delay;
    private readonly Queue<double> _pipeline = new();

    public Stock(StockParams p, double baseline)
    {
        _depreciation = p.Depreciation;
        _delay = p.DelayYears;
        _baseline = baseline;
        _rate = baseline > 0 ? p.Depreciation / baseline : 0;
        for (int i = 0; i < _delay; i++) _pipeline.Enqueue(baseline); // starts full: no cliff in year one
    }

    public double Value { get; private set; } = 1.0;

    /// <summary>False when the starting budget for this line is zero: the line then has no effect.</summary>
    public bool Active => _baseline > 0;

    public int Delay => _delay;

    public void Advance(double spending)
    {
        if (!Active) return;
        double entering = _delay == 0 ? spending : _pipeline.Dequeue();
        if (_delay > 0) _pipeline.Enqueue(spending);
        Value = (1 - _depreciation) * Value + _rate * entering;
    }

    /// <summary>Average yearly spending still in the pipeline.</summary>
    public double PipelineAverage => _delay == 0 ? 0 : _pipeline.Average();

    /// <summary>Yearly spending that would hold the stock at its current level.</summary>
    public double HoldingAmount => Value * _baseline;

    /// <summary>Yearly spending that would hold the stock at <paramref name="level"/> in the long run.</summary>
    public double HoldingAmountAt(double level) => level * _baseline;
}
