using Eunomia.Core.Config;

namespace Eunomia.Tests;

public static class Repo
{
    public static string Root { get; } = FindRoot();

    public static string Config => Path.Combine(Root, "config");
    public static string Portugal => Path.Combine(Root, "packs", "portugal");

    public static SimulationConfig LoadPortugal() => ConfigLoader.Load(Config, Portugal);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Eunomia.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
