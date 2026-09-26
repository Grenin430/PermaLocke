using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PermaLocke.Infrastructure;

/// <summary>The update package of a GitHub release, as PermaLocke looks for it.</summary>
/// <param name="Digest">«sha256:…» when GitHub gives it, to check the download against.</param>
public sealed record UpdateAsset(string Version, string Name, string Url, long Size, string? Digest, string Notes);

/// <summary>What installing an update did.</summary>
public sealed record UpdateResult(bool Done, IReadOnlyList<string> Log);

/// <summary>
/// The pure part of the automatic update (§196, plan del próximo torneo, paso 3): which release is newer, which of its
/// files is the update, and how it is laid over a PermaLocke folder without touching anything of the player's.
/// </summary>
/// <remarks>
/// <para>
/// An update package is <c>PermaLocke-actualizacion-&lt;versión&gt;.zip</c> in a GitHub release tagged
/// <c>v&lt;versión&gt;</c>, made by <c>tools/publicar-actualizacion.ps1</c>: the executable and <c>Data/*.json</c>, nothing
/// else. The whole distribution (2 GB with the emulator and the expansion) is not what changes between versions, and a
/// release file cannot be over 2 GB anyway.
/// </para>
/// <para>
/// What it replaces: the executable (renamed to <c>.old</c> first — Windows lets a running program be renamed, not
/// overwritten — and removed on the next start) and the <c>Data/*.json</c> the package brings. Never
/// <c>Saves/</c>, <c>Config/</c>, <c>ROM/</c>, <c>Emulator/</c>, <c>Randomized/</c>, nor any Data file the package does not
/// bring. The official rules the organiser published (<c>RulesSync</c>, §193) live in <c>Data/</c> too: those files are
/// passed in <c>keep</c> and left as they are.
/// </para>
/// </remarks>
public static class AppUpdate
{
    /// <summary>The start of the update package's name in a release.</summary>
    public const string PackagePrefix = "PermaLocke-actualizacion-";

    /// <summary>The folder, in the PermaLocke folder, where a package is downloaded and unpacked.</summary>
    public const string Staging = "Actualizacion";

    /// <summary>The version in a tag: «v1.2.3» or «1.2.3»; null for anything else.</summary>
    public static Version? VersionOf(string? tag)
    {
        var text = tag?.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out var version) ? Normalise(version) : null;
    }

    /// <summary>Whether a release tag is a newer version than the one running.</summary>
    public static bool IsNewer(string? tag, Version current) =>
        VersionOf(tag) is { } offered && offered > Normalise(current);

    /// <summary>
    /// The update package of a release as the GitHub API returns it (<c>releases/latest</c>), or null when the release has
    /// none, is a draft or a pre-release, or is not newer than <paramref name="current"/>.
    /// </summary>
    public static UpdateAsset? Pick(string releaseJson, Version current)
    {
        using var document = JsonDocument.Parse(releaseJson);
        var release = document.RootElement;

        if (Bool(release, "draft") || Bool(release, "prerelease")
            || !release.TryGetProperty("tag_name", out var tag) || !IsNewer(tag.GetString(), current)
            || !release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            if (!name.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase)
                || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                || !asset.TryGetProperty("browser_download_url", out var url))
            {
                continue;
            }

            return new UpdateAsset(
                VersionOf(tag.GetString())!.ToString(3),
                name,
                url.GetString() ?? string.Empty,
                asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0,
                asset.TryGetProperty("digest", out var digest) ? digest.GetString() : null,
                release.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty);
        }

        return null;
    }

    /// <summary>Whether a downloaded file is the one the release describes: its size and, when given, its SHA-256.</summary>
    public static bool Matches(string file, UpdateAsset asset)
    {
        var info = new FileInfo(file);
        if (!info.Exists || (asset.Size > 0 && info.Length != asset.Size))
        {
            return false;
        }

        if (asset.Digest is not { } digest || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        using var stream = File.OpenRead(file);
        return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), digest["sha256:".Length..], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lays a downloaded package over a PermaLocke folder: the executable and the <c>Data/*.json</c> it brings, except
    /// the ones in <paramref name="keep"/>. Checks the package before touching anything; never throws.
    /// </summary>
    /// <param name="executable">The running executable's file name in <paramref name="root"/> («PermaLocke.exe»).</param>
    /// <param name="keep">Data files to leave as they are: the official rules of the tournament.</param>
    public static UpdateResult Apply(string package, string root, string executable, IReadOnlySet<string> keep, ILogger logger)
    {
        var log = new List<string>();
        try
        {
            var unpacked = Path.Combine(root, Staging, Path.GetFileNameWithoutExtension(package));
            if (Directory.Exists(unpacked)) Directory.Delete(unpacked, recursive: true);
            ZipFile.ExtractToDirectory(package, unpacked);

            // Lo nuevo, comprobado antes de tocar nada: sin ejecutable no se instala.
            var newExe = Directory.EnumerateFiles(unpacked, "*.exe", SearchOption.TopDirectoryOnly).SingleOrDefault();
            if (newExe is null)
            {
                return new UpdateResult(false, ["El paquete no trae el programa."]);
            }

            var target = Path.Combine(root, executable);
            var old = target + ".old";
            if (File.Exists(old)) File.Delete(old);
            if (File.Exists(target)) File.Move(target, old);
            File.Copy(newExe, target);
            log.Add($"Programa: {executable}");

            var data = Path.Combine(unpacked, "Data");
            if (Directory.Exists(data))
            {
                foreach (var file in Directory.EnumerateFiles(data, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(file);
                    if (keep.Contains(name))
                    {
                        log.Add($"Data/{name}: se queda la regla oficial del organizador");
                        continue;
                    }

                    File.Copy(file, Path.Combine(root, "Data", name), overwrite: true);
                    log.Add($"Data/{name}");
                }
            }

            foreach (var line in log) logger.LogInformation("Actualización: {Line}", line);
            return new UpdateResult(true, log);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la actualización");
            log.Add($"Ha fallado: {ex.Message}");
            return new UpdateResult(false, log);
        }
    }

    /// <summary>
    /// What an update leaves behind once the new version is running: the old executable and the downloaded package.
    /// Both are PermaLocke's own, not the player's. Never throws.
    /// </summary>
    public static void CleanUp(string root, string executable)
    {
        try
        {
            var old = Path.Combine(root, executable + ".old");
            if (File.Exists(old)) File.Delete(old);
            var staging = Path.Combine(root, Staging);
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Si aún está en uso, se intenta en el próximo arranque.
        }
    }

    private static Version Normalise(Version version) =>
        new(version.Major, Math.Max(0, version.Minor), Math.Max(0, version.Build));

    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
