namespace PermaLocke.Core.Abstractions;

/// <summary>A new nickname for a Pokémon already in the save, in the slot it occupies.</summary>
/// <param name="Pid">Identity the caller believes is there; the writer refuses another one (§96).</param>
/// <param name="Nickname">The new one; empty puts back the species name, as the game's own name rater does.</param>
public sealed record NicknameChange(int Box, int Slot, uint Pid, string Name, string Nickname)
{
    public bool IsInParty => Box == BoxedPokemon.PartyBox;

    public string Where => IsInParty ? $"el equipo, puesto {Slot + 1}" : $"la caja {Box + 1}, hueco {Slot + 1}";
}

/// <summary>Writes a nickname into the save. Like <see cref="IMoveTeacher"/>, only with the game closed.</summary>
public interface IPokemonRenamer
{
    bool CanRenameNow(out string reason);

    Task<DeliveryResult> ApplyAsync(NicknameChange change, CancellationToken ct = default);
}
