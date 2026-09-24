namespace PermaLocke.Infrastructure;

/// <summary>
/// Resolves the folder layout PermaLocke expects next to the executable and creates the
/// writable ones on first use. Nothing in the app builds paths by hand.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string? rootOverride = null)
    {
        Root = rootOverride ?? ResolveRoot(AppContext.BaseDirectory);

        Rom = Path.Combine(Root, "ROM");
        Expansion = LocalOnly ? Path.Combine(Root, "Expansion") : ResolveExpansion(Root);
        Randomized = Path.Combine(Root, "Randomized");
        Data = Path.Combine(Root, "Data");
        Config = Path.Combine(Root, "Config");
        Logs = Path.Combine(Root, "Logs");
        Saves = Path.Combine(Root, "Saves");
        SaveBackups = Path.Combine(Saves, "backup");
    }

    public string Root { get; }

    public const string LocalMarker = "PermaLocke.local";

    /// <summary>A standalone distribution never borrows data from neighbouring installations.</summary>
    public bool LocalOnly => File.Exists(Path.Combine(Root, LocalMarker));

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

    /// <summary>
    /// The application's own <c>Expansion</c> when it holds a mod, and otherwise the one in the folder above.
    /// </summary>
    /// <remarks>
    /// The mod is 2.4 GB, and the copies handed to friends live side by side inside the competition's shared
    /// folder (§148): one copy of the mod beside them serves all five instead of five copies that did not fit.
    /// A mod is recognised by its <c>romfs</c>, not by the folder, because <see cref="EnsureCreated"/> makes an
    /// empty <c>Expansion</c> in every copy and an empty folder must not hide the shared one.
    /// </remarks>
    private static string ResolveExpansion(string root)
    {
        var own = Path.Combine(root, "Expansion");

        if (Directory.Exists(Path.Combine(own, "romfs")))
        {
            return own;
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
        var shared = parent is null ? null : Path.Combine(parent, "Expansion");

        return shared is not null && Directory.Exists(Path.Combine(shared, "romfs")) ? shared : own;
    }

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
    public static string ResolveRoot(string baseDir)
    {
        if (File.Exists(Path.Combine(baseDir, LocalMarker)))
        {
            return baseDir;
        }

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
