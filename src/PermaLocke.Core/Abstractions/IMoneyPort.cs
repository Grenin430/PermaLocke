namespace PermaLocke.Core.Abstractions;

/// <summary>
/// The money of the player's game (singleplayer, 2026-10-12): what a roulette face gives or takes when it is about money. A port
/// so the wheel does not learn whether that means the save file or the game's memory.
/// </summary>
public interface IMoneyPort
{
    /// <summary>
    /// Adds <paramref name="delta"/> (negative takes; never below zero nor above the game's top). Done is false, with the reason,
    /// when the money could not be reached.
    /// </summary>
    Task<MoneyChange> ChangeAsync(int delta, CancellationToken ct = default);
}

/// <param name="Applied">What really changed: less than asked when there was not that much to take, or the top was reached.</param>
public sealed record MoneyChange(bool Done, int Applied, int Balance, string? Problem = null);
