using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Infrastructure;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The automatic update (§196): only a newer, published release with its package counts, the download is checked, and
/// installing it replaces the program and the shipped Data files and nothing of the player's.
/// </summary>
public sealed class AppUpdateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "permalocke-actualizar-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static string Release(string tag, bool draft = false, bool pre = false, string asset = "PermaLocke-actualizacion-1.2.0.zip") => $$"""
        { "tag_name": "{{tag}}", "draft": {{(draft ? "true" : "false")}}, "prerelease": {{(pre ? "true" : "false")}}, "body": "Novedades",
          "assets": [
            { "name": "PermaLocke-completo.zip", "size": 1, "browser_download_url": "https://x/completo.zip" },
            { "name": "{{asset}}", "size": 42, "digest": "sha256:ab", "browser_download_url": "https://x/actualizacion.zip" } ] }
        """;

    [Theory]
    [InlineData("v1.2.0", "1.1.9", true)]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("v1.2", "1.1.0", true)]
    [InlineData("v1.0.0", "1.2.0", false)]
    [InlineData("sin-version", "1.0.0", false)]
    [InlineData("v1.0.4.1", "1.0.4.0", true)]
    [InlineData("v1.0.4.1", "1.0.4", true)]
    [InlineData("v1.0.4", "1.0.4.0", false)]
    [InlineData("v1.0.4.1", "1.0.4.1", false)]
    [InlineData("v1.0.5", "1.0.4.9", true)]
    [InlineData("v1.0.4.9", "1.0.5.0", false)]
    public void Only_a_newer_version_counts(string tag, string current, bool newer)
    {
        Assert.Equal(newer, AppUpdate.IsNewer(tag, Version.Parse(current)));
    }

    [Theory]
    [InlineData("1.0.4.0", "1.0.4")]
    [InlineData("1.0.4", "1.0.4")]
    [InlineData("1.0.4.1", "1.0.4.1")]
    [InlineData("2.1", "2.1.0")]
    public void Shows_the_fourth_number_only_when_it_is_not_zero(string version, string shown)
    {
        Assert.Equal(shown, AppUpdate.Display(Version.Parse(version)));
    }

    [Fact]
    public void Picks_the_update_package_of_a_newer_published_release()
    {
        var asset = AppUpdate.Pick(Release("v1.2.0"), new Version(1, 1, 0));

        Assert.NotNull(asset);
        Assert.Equal("1.2.0", asset!.Version);
        Assert.Equal("https://x/actualizacion.zip", asset.Url);
        Assert.Equal(42, asset.Size);
        Assert.Equal("sha256:ab", asset.Digest);

        Assert.Null(AppUpdate.Pick(Release("v1.2.0"), new Version(1, 2, 0)));
        Assert.Null(AppUpdate.Pick(Release("v1.2.0", draft: true), new Version(1, 1, 0)));
        Assert.Null(AppUpdate.Pick(Release("v1.2.0", pre: true), new Version(1, 1, 0)));
        Assert.Null(AppUpdate.Pick(Release("v1.2.0", asset: "otra-cosa.zip"), new Version(1, 1, 0)));
    }

    [Fact]
    public void A_download_is_checked_by_size_and_hash()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "p.zip");
        File.WriteAllText(file, "paquete");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        var size = new FileInfo(file).Length;

        Assert.True(AppUpdate.Matches(file, new UpdateAsset("1.0.0", "p.zip", "u", size, $"sha256:{hash.ToLowerInvariant()}", "")));
        Assert.True(AppUpdate.Matches(file, new UpdateAsset("1.0.0", "p.zip", "u", size, null, "")));
        Assert.False(AppUpdate.Matches(file, new UpdateAsset("1.0.0", "p.zip", "u", size + 1, null, "")));
        Assert.False(AppUpdate.Matches(file, new UpdateAsset("1.0.0", "p.zip", "u", size, "sha256:00", "")));
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void Installing_replaces_the_program_and_the_shipped_data_and_nothing_else()
    {
        // La carpeta del jugador.
        Write(Path.Combine(_root, "PermaLocke.exe"), "programa viejo");
        Write(Path.Combine(_root, "Data", "species.json"), "especies viejas");
        Write(Path.Combine(_root, "Data", "shop.json"), "tienda oficial del organizador");
        Write(Path.Combine(_root, "Data", "mio.json"), "algo que no viene en el paquete");
        Write(Path.Combine(_root, "Saves", "permalocke.db"), "mi run");
        Write(Path.Combine(_root, "Config", "jugador.json"), "yo");
        Write(Path.Combine(_root, "Emulator", "user", "config", "qt-config.ini"), "mi emulador");

        // El paquete.
        var build = Path.Combine(_root, "paquete");
        Write(Path.Combine(build, "PermaLocke.exe"), "programa nuevo");
        Write(Path.Combine(build, "Data", "species.json"), "especies nuevas");
        Write(Path.Combine(build, "Data", "shop.json"), "tienda del paquete");
        Write(Path.Combine(build, "Saves", "permalocke.db"), "NO");
        var package = Path.Combine(_root, "PermaLocke-actualizacion-1.2.0.zip");
        ZipFile.CreateFromDirectory(build, package);

        var result = AppUpdate.Apply(package, _root, "PermaLocke.exe", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "shop.json" },
            NullLogger.Instance);

        Assert.True(result.Done, string.Join(" / ", result.Log));
        Assert.Equal("programa nuevo", File.ReadAllText(Path.Combine(_root, "PermaLocke.exe")));
        Assert.Equal("programa viejo", File.ReadAllText(Path.Combine(_root, "PermaLocke.exe.old")));
        Assert.Equal("especies nuevas", File.ReadAllText(Path.Combine(_root, "Data", "species.json")));
        Assert.Equal("tienda oficial del organizador", File.ReadAllText(Path.Combine(_root, "Data", "shop.json")));
        Assert.Equal("algo que no viene en el paquete", File.ReadAllText(Path.Combine(_root, "Data", "mio.json")));
        Assert.Equal("mi run", File.ReadAllText(Path.Combine(_root, "Saves", "permalocke.db")));
        Assert.Equal("yo", File.ReadAllText(Path.Combine(_root, "Config", "jugador.json")));
        Assert.Equal("mi emulador", File.ReadAllText(Path.Combine(_root, "Emulator", "user", "config", "qt-config.ini")));

        Write(Path.Combine(_root, "PermaLocke.exe.old-apartado"), "uno que no se dejó borrar");
        AppUpdate.CleanUp(_root, "PermaLocke.exe");
        Assert.False(File.Exists(Path.Combine(_root, "PermaLocke.exe.old-apartado")));
        Assert.False(File.Exists(Path.Combine(_root, "PermaLocke.exe.old")));
        Assert.False(Directory.Exists(Path.Combine(_root, AppUpdate.Staging)));
    }

    [Fact]
    public void A_package_without_the_program_changes_nothing()
    {
        Write(Path.Combine(_root, "PermaLocke.exe"), "programa viejo");
        var build = Path.Combine(_root, "paquete");
        Write(Path.Combine(build, "Data", "species.json"), "especies nuevas");
        var package = Path.Combine(_root, "PermaLocke-actualizacion-1.2.0.zip");
        ZipFile.CreateFromDirectory(build, package);

        var result = AppUpdate.Apply(package, _root, "PermaLocke.exe", new HashSet<string>(), NullLogger.Instance);

        Assert.False(result.Done);
        Assert.Equal("programa viejo", File.ReadAllText(Path.Combine(_root, "PermaLocke.exe")));
    }
}
