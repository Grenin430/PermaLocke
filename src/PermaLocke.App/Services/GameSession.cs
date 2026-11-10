using PermaLocke.Core.Domain;

namespace PermaLocke.App.Services;

/// <summary>
/// The game this run of PermaLocke is for (2026-11-10), chosen in the launcher's first window. One game per process: changing game means
/// opening PermaLocke again, so nothing has to switch while it runs.
/// </summary>
public static class GameSession
{
    public static GameInfo Current { get; set; } = GameCatalog.Of(GameVersion.UltraMoon);
}
