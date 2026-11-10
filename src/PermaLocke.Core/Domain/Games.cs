namespace PermaLocke.Core.Domain;

/// <summary>The console a game is for, which decides its emulator.</summary>
public enum GamePlatform
{
    Nds,
    N3ds
}

/// <summary>
/// One game of the launcher (2026-11-10): what it is called, what it runs on, and whether PermaLocke can play it yet. A game still in
/// development is shown, but nothing of it pretends to work.
/// </summary>
/// <param name="Note">What the player is told about it when it is not playable.</param>
/// <param name="Folder">Its data folder under Data/.</param>
public sealed record GameInfo(GameVersion Id, string Name, GamePlatform Platform, bool Playable, string Folder, string Note = "");

/// <summary>The games the launcher offers, in the order they are shown.</summary>
public static class GameCatalog
{
    public static IReadOnlyList<GameInfo> All { get; } =
    [
        new(GameVersion.UltraMoon, "Ultra Luna", GamePlatform.N3ds, true, "ultraluna"),
        new(GameVersion.HeartGold, "HeartGold", GamePlatform.Nds, true, "heartgold",
            "De momento solo randomizar con el Universal Randomizer y jugar con melonDS.")
    ];

    public static GameInfo Of(GameVersion id) => All.FirstOrDefault(g => g.Id == id) ?? All[0];
}
