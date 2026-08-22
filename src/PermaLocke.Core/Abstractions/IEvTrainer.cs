namespace PermaLocke.Core.Abstractions;

/// <summary>
/// One Pokémon's EVs, rewritten in the slot it already occupies.
/// </summary>
/// <param name="Box">
/// Zero-based box, or <see cref="BoxedPokemon.PartyBox"/> for a member of the party.
/// </param>
/// <param name="Slot">Zero-based slot inside that box or the party.</param>
/// <param name="Pid">
/// Identity of the Pokémon the caller believes is there. The implementation must refuse if the
/// slot now holds a different one: PIDs survive nicknames, levels and evolutions, so this is the
/// one field that still matches after the player has kept playing.
/// </param>
/// <param name="Name">What to call it when reporting back, already resolved to text.</param>
/// <param name="Evs">Six values in HP/Atk/Def/SpA/SpD/Spe order, already legal.</param>
public sealed record EvChange(int Box, int Slot, uint Pid, string Name, IReadOnlyList<int> Evs)
{
    public bool IsInParty => Box == BoxedPokemon.PartyBox;

    /// <summary>Where it is, in the words the game uses.</summary>
    public string Where => IsInParty
        ? $"el equipo, puesto {Slot + 1}"
        : $"la caja {Box + 1}, hueco {Slot + 1}";
}

/// <summary>
/// Changes the effort values of a Pokémon that is already the player's.
/// </summary>
/// <remarks>
/// <para>
/// A port, so the screen never learns whether training means a save file, memory or nothing at
/// all. Today it is the save, which is why it can only happen with the game closed: the emulator
/// holds its own copy and would write it over ours on the next save.
/// </para>
/// <para>
/// Unlike <see cref="IPokemonSwap"/> this destroys nothing — the Pokémon stays, only its EVs
/// move — but it edits something that already exists, so the implementation is expected to refuse
/// when the slot no longer holds the Pokémon the caller named.
/// </para>
/// </remarks>
public interface IEvTrainer
{
    /// <summary>True when EVs could be written right now.</summary>
    bool CanTrainNow(out string reason);

    Task<DeliveryResult> ApplyAsync(EvChange change, CancellationToken ct = default);
}
