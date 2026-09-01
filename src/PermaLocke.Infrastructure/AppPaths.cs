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
        Expansion = Path.Combine(Root, "Expansion");
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

    /// <summary>
    /// Where the player drops another mod's <c>romfs</c>, to randomize on top of it instead of on
    /// top of the cartridge. Read-only, exactly like <see cref="Rom"/>.
    /// </summary>
    /// <remarks>
    /// It cannot be the folder the mod is installed into, because that is the one PermaLocke
    /// empties and writes its own result into. The base has to be a source that survives being
    /// regenerated, so it is kept apart, like the cartridge.
    /// </remarks>
    public string Expansion { get; }

    public string Randomized { get; }

    public string Data { get; }

    public string Config { get; }

    public string Logs { get; }

    public string Saves { get; }

    public string SaveBackups { get; }

    /// <summary>Creates every writable folder. The ROM folder is created too, so the player can find it.</summary>
    public void EnsureCreated()
    {
        foreach (var path in new[] { Rom, Expansion, Randomized, Data, Config, Logs, Saves, SaveBackups })
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
