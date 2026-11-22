using Eunomia.Core.Config;

namespace Eunomia.Core.Model;

public enum Outlook { Stable, Positive, Negative }

/// <summary>
/// One composite rating agency. Once a year it scores the country's fundamentals, and moves the rating
/// in steps: first an outlook warning, then a one-notch move at the next review. A large gap moves two
/// notches at once (a crisis downgrade). Upgrades are never faster than one notch a year.
/// Lenders price the rating in force, so the market reacts late, then suddenly, as it does in reality.
/// </summary>
public sealed class RatingAgency
{
    private readonly RatingParams _p;

    public RatingAgency(RatingParams p, string openingRating)
    {
        _p = p;
        int notch = IndexOf(openingRating);
        if (notch < 0) throw new InvalidDataException($"Unknown rating '{openingRating}'. Scale: {string.Join(", ", p.Scale)}");
        Notch = notch;
    }

    /// <summary>0 is the best rating (AAA). Higher is worse.</summary>
    public int Notch { get; private set; }
    public Outlook Outlook { get; private set; } = Outlook.Stable;
    public string Rating => _p.Scale[Notch];
    public bool InvestmentGrade => Notch < _p.JunkFrom;

    /// <summary>Extra yearly interest lenders demand for the rating in force.</summary>
    public double Spread => SpreadFor(Notch);

    public double SpreadFor(int notch) =>
        _p.SpreadPerNotch * notch
        + (notch >= _p.JunkFrom ? _p.JunkCliff + _p.JunkSpreadPerNotch * (notch - _p.JunkFrom) : 0);

    /// <summary>The rating the fundamentals deserve, in notches (fractional).</summary>
    public double Target(double debtRatio, double deficitRatio, double debtChange, double averageGrowth) =>
        Math.Clamp(
            _p.Constant
            + _p.DebtWeight * debtRatio
            + _p.DeficitWeight * Math.Max(0, deficitRatio) * 100
            + _p.TrendWeight * debtChange * 100
            + _p.GrowthWeight * (_p.GrowthReference - averageGrowth) * 100,
            0, _p.Scale.Count - 1);

    /// <summary>The yearly review. Returns a short announcement, or null when nothing changes.</summary>
    public string? Review(double target)
    {
        double gap = target - Notch; // positive: deserves worse
        int worst = _p.Scale.Count - 1;

        if (gap >= _p.CrisisGap && Notch < worst)
        {
            int to = Math.Min(worst, Notch + 2);
            return Move(to, gap - (to - Notch) >= _p.OutlookGap ? Outlook.Negative : Outlook.Stable, "cuts");
        }
        if (gap >= _p.OutlookGap && Notch < worst)
        {
            if (Outlook == Outlook.Negative)
                return Move(Notch + 1, gap - 1 >= _p.OutlookGap ? Outlook.Negative : Outlook.Stable, "cuts");
            Outlook = Outlook.Negative;
            return $"{Rating}, outlook revised to negative";
        }
        if (gap <= -_p.OutlookGap && Notch > 0)
        {
            if (Outlook == Outlook.Positive)
                return Move(Notch - 1, gap + 1 <= -_p.OutlookGap ? Outlook.Positive : Outlook.Stable, "raises");
            Outlook = Outlook.Positive;
            return $"{Rating}, outlook revised to positive";
        }
        if (Outlook != Outlook.Stable)
        {
            Outlook = Outlook.Stable;
            return $"{Rating}, outlook back to stable";
        }
        return null;
    }

    private string Move(int to, Outlook outlook, string verb)
    {
        bool wasInvestmentGrade = InvestmentGrade;
        Notch = to;
        Outlook = outlook;
        string text = $"{verb} rating to {Rating}, outlook {outlook.ToString().ToLowerInvariant()}";
        if (wasInvestmentGrade && !InvestmentGrade) text += " (below investment grade)";
        if (!wasInvestmentGrade && InvestmentGrade) text += " (back to investment grade)";
        return text;
    }

    private int IndexOf(string rating)
    {
        for (int i = 0; i < _p.Scale.Count; i++)
            if (_p.Scale[i] == rating) return i;
        return -1;
    }
}
