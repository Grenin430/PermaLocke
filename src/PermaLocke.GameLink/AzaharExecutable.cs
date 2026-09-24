namespace PermaLocke.GameLink;

/// <param name="Executable">The <c>azahar.exe</c> to start, or null when none will do.</param>
/// <param name="Detail">Which one and why, or why none, in words for the checklist.</param>
public sealed record EmulatorChoice(string? Executable, string Detail);

/// <summary>
/// Which <c>azahar.exe</c> the launcher starts (§125).
/// </summary>
/// <remarks>
/// <para>
/// The one thing that must not happen is starting an emulator that keeps its saves somewhere other
/// than where PermaLocke reads them. Azahar goes <b>portable</b> when a <c>user</c> folder sits beside its
/// executable, and then its saves, its mods and its settings live there. PermaLocke decides where
/// to read with <see cref="AzaharInstallation.Locate"/>: the bundled emulator is portable, anything else
/// is read from the installed <c>AppData\Roaming\Azahar</c>. So the rule is simple and strict — the
/// bundled one when there is one, otherwise only executables that are <b>not</b> portable. A portable copy
/// found elsewhere would boot a different save while every screen went on reading the other.
/// </para>
/// <para>
/// Between several valid ones, the one the player picked wins, then the newest build: the repository
/// holds the official Azahar from August next to two copies of the fork, and only the fork can write
/// into the game.
/// </para>
/// </remarks>
public static class AzaharExecutable
{
    private const string UserFolderName = "user";

    public static EmulatorChoice Choose(AzaharLocation location, string? configured, IEnumerable<string> candidates)
    {
        if (location.IsPortable && location.ExecutablePath is { } bundled && File.Exists(bundled))
        {
            return new EmulatorChoice(bundled, "El emulador que viaja con PermaLocke.");
        }

        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!File.Exists(configured))
            {
                return Newest(candidates, "El Azahar que elegiste ya no está.");
            }

            if (IsPortable(configured))
            {
                return Newest(candidates,
                    "Ese Azahar no sirve: guarda la partida en otro sitio.");
            }

            return new EmulatorChoice(configured, "El Azahar que elegiste.");
        }

        return Newest(candidates, null);
    }

    /// <summary>True when Azahar would keep everything beside the executable instead of in AppData.</summary>
    public static bool IsPortable(string executable) =>
        Directory.Exists(Path.Combine(Path.GetDirectoryName(executable) ?? string.Empty, UserFolderName));

    private static EmulatorChoice Newest(IEnumerable<string> candidates, string? why)
    {
        var usable = candidates
            .Where(File.Exists)
            .Where(path => !IsPortable(path))
            .Select((path, order) => (Path: path, Order: order, Built: File.GetLastWriteTimeUtc(path)))
            .OrderByDescending(c => c.Built)
            .ThenBy(c => c.Order)
            .FirstOrDefault();

        if (usable.Path is null)
        {
            return new EmulatorChoice(null, (why is null ? string.Empty : why + " ")
                                            + "No se encuentra Azahar. Elige azahar.exe a mano.");
        }

        return new EmulatorChoice(usable.Path, why is null
            ? "Encontrado junto a PermaLocke."
            : $"{why} Se usa {usable.Path}.");
    }
}
