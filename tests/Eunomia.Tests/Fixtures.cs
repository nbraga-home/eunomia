using Eunomia.Core;
using Eunomia.Core.Config;
using Eunomia.Core.Model;
using Eunomia.Core.World;
using Eunomia.Governments;

namespace Eunomia.Tests;

/// <summary>A government defined by a function, for tests.</summary>
public sealed class Scripted(string name, Func<MeetingContext, Policy?> decide) : IGovernment
{
    public string Name => name;
    public Policy? Decide(MeetingContext context) => decide(context);
}

public static class Fixtures
{
    /// <summary>Portugal with no trend, no inflation, no population change and no debt: nothing should move.</summary>
    public static SimulationConfig Stationary()
    {
        var c = Repo.LoadPortugal();
        return c with
        {
            Economy = c.Economy with { TrendProductivityGrowth = 0, Inflation = 0 },
            // Workforce schooled like today's school leavers, so generations change nothing.
            Pack = c.Pack with { Debt = 0, WorkingAgeGrowth = 0, HouseholdGrowth = 0, Education = c.Pack.Education with { Workforce = c.Pack.Education.Coverage } },
        };
    }

    public static RunResult RunFlat(SimulationConfig c, IGovernment g, string set = "central") =>
        new Simulation(c, set, WorldGenerator.Flat(c.World, c.Government.HorizonYears)).Run(g);

    /// <summary>Moves <paramref name="amount"/> of budget share from one line to another, once, in year 0, then keeps it.</summary>
    public static IGovernment Shift(BudgetLine from, BudgetLine to, double amount) => new Scripted($"shift-{from}-{to}", ctx =>
    {
        var s = ctx.StartingPolicy.BudgetShares.ToArray();
        s[(int)from] -= amount;
        s[(int)to] += amount;
        return ctx.StartingPolicy with { BudgetShares = BudgetShares.FromArray(s) };
    });

    public static IGovernment Frozen => new Frozen();
}
