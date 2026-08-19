namespace PermaLocke.Infrastructure;

/// <summary>
/// Resolves the folder layout PermaLocke expects next to the executable and creates the
/// writable ones on first use. Nothing in the app builds paths by hand.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string? rootOverride = null)
    {
        Root = rootOverride ?? ResolveRoot();

        Rom = Path.Combine(Root, "ROM");
        Randomized = Path.Combine(Root, "Randomized");
        Data = Path.Combine(Root, "Data");
        Config = Path.Combine(Root, "Config");
        Logs = Path.Combine(Root, "Logs");
        Saves = Path.Combine(Root, "Saves");
        SaveBackups = Path.Combine(Saves, "backup");
    }

    public string Root { get; }

    /// <summary>Where the player drops the vanilla ROM. Read-only for the whole application.</summary>
    public string Rom { get; }

    public string Randomized { get; }

    public string Data { get; }

    public string Config { get; }

    public string Logs { get; }

    public string Saves { get; }

    public string SaveBackups { get; }

    /// <summary>Creates every writable folder. The ROM folder is created too, so the player can find it.</summary>
    public void EnsureCreated()
    {
        foreach (var path in new[] { Rom, Randomized, Data, Config, Logs, Saves, SaveBackups })
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Uses the executable's folder in production. When running from bin/Debug/... during
    /// development it walks up to the repository root so developers share one set of data folders.
    /// </summary>
    private static string ResolveRoot()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);

        while (dir is not null)
        {
            if (SolutionMarkers.Any(marker => File.Exists(Path.Combine(dir.FullName, marker))))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return baseDir;
    }

    /// <summary>.NET 10 scaffolds the XML solution format; both are accepted.</summary>
    private static readonly string[] SolutionMarkers = ["PermaLocke.slnx", "PermaLocke.sln"];
}
