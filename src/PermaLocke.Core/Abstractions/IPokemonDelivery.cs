using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Abstractions;

public enum DeliveryOutcome
{
    /// <summary>Written into a box of the save and read back to confirm it.</summary>
    Delivered,

    /// <summary>The game is loaded in the emulator. Writing now would be overwritten on save.</summary>
    GameRunning,

    /// <summary>No save file was found for Ultra Moon.</summary>
    SaveNotFound,

    /// <summary>The file exists but is not a save this can write to.</summary>
    SaveUnreadable,

    /// <summary>All 32 boxes are full.</summary>
    BoxesFull,

    /// <summary>
    /// The slot no longer holds what the screen thought it did, so nothing was overwritten.
    /// </summary>
    SlotChanged,

    /// <summary>Something else went wrong; the save was left untouched.</summary>
    Failed
}

/// <param name="Box">One-based box number, for telling the player where to look.</param>
public sealed record DeliveryResult(DeliveryOutcome Outcome, string Message, int Box = 0, int Slot = 0)
{
    public bool Delivered => Outcome == DeliveryOutcome.Delivered;
}

/// <summary>
/// Puts a Pokémon the run has granted into the player's game.
/// </summary>
/// <remarks>
/// A port so the gacha never learns whether delivery means a save file, memory, or nothing at
/// all. Today it is the save, which is why it can only happen with the game closed: the
/// emulator holds the save in memory and would write its own copy over ours on the next save.
/// </remarks>
public interface IPokemonDelivery
{
    /// <summary>True when a delivery could be attempted right now.</summary>
    bool CanDeliverNow(out string reason);

    Task<DeliveryResult> DeliverAsync(GachaPull pull, Run run, CancellationToken ct = default);
}

/// <summary>
/// Replaces one Pokémon of the player's game with another, in the same slot.
/// </summary>
/// <remarks>
/// Separate from <see cref="IPokemonDelivery"/> because it is a different promise: delivery only
/// ever fills an empty hole, whereas this one <b>destroys</b> what was there. The implementation
/// is expected to refuse if the slot no longer holds what the caller believes.
/// </remarks>
public interface IPokemonSwap
{
    /// <summary>True when a swap could be attempted right now.</summary>
    bool CanSwapNow(out string reason);

    Task<DeliveryResult> SwapAsync(WonderTradeOffer offer, int box, int slot,
        CancellationToken ct = default);
}
