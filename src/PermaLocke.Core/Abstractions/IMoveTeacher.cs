namespace PermaLocke.Core.Abstractions;

/// <summary>
/// One move taught to a Pokémon that is already the player's, in the slot it already occupies.
/// </summary>
/// <param name="Box">Zero-based box, or <see cref="BoxedPokemon.PartyBox"/> for a member of the party.</param>
/// <param name="Slot">Zero-based slot inside that box or the party.</param>
/// <param name="Pid">
/// Identity of the Pokémon the caller believes is there; the implementation refuses when the slot holds another
/// one (§96: nothing that writes into a partida takes a position without an identity next to it).
/// </param>
/// <param name="MoveSlot">Which of its four moves, zero-based.</param>
/// <param name="Replaced">The move the caller believes is in <paramref name="MoveSlot"/>, zero for an empty one.</param>
/// <param name="PP">The move's PP in the world being played; the writer does not look them up.</param>
public sealed record MoveChange(int Box, int Slot, uint Pid, string Name, int MoveSlot, int Replaced, int Move, int PP)
{
    public bool IsInParty => Box == BoxedPokemon.PartyBox;

    /// <summary>Where it is, in the words the game uses.</summary>
    public string Where => IsInParty
        ? $"el equipo, puesto {Slot + 1}"
        : $"la caja {Box + 1}, hueco {Slot + 1}";
}

/// <summary>
/// Teaches a move to a Pokémon the player already owns.
/// </summary>
/// <remarks>
/// A port like <see cref="IEvTrainer"/>, and for the same reason it can only act with the game closed today: it
/// writes the save, and the emulator keeps its own copy in memory that it would write over ours on the next save.
/// Deciding <em>which</em> move is allowed is not its job — that is the reminder's rule — but it must refuse when
/// the slot or the move in it are no longer what the caller saw.
/// </remarks>
public interface IMoveTeacher
{
    /// <summary>True when a move could be written right now.</summary>
    bool CanTeachNow(out string reason);

    Task<DeliveryResult> ApplyAsync(MoveChange change, CancellationToken ct = default);
}
