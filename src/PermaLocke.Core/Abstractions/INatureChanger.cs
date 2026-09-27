namespace PermaLocke.Core.Abstractions;

/// <summary>A new nature for a Pokémon already in the save, in the slot it occupies (2026-09-27, the herbs).</summary>
/// <param name="Pid">Identity the caller believes is there; the writer refuses another one (§96).</param>
/// <param name="Nature">0-24, the game's own order.</param>
public sealed record NatureChange(int Box, int Slot, uint Pid, string Name, int Nature)
{
    public string Where => Box == BoxedPokemon.PartyBox ? $"el equipo, puesto {Slot + 1}" : $"la caja {Box + 1}, hueco {Slot + 1}";
}

/// <summary>Writes a nature into the save. Like <see cref="IPokemonRenamer"/>, only with the game closed.</summary>
public interface INatureChanger
{
    bool CanChangeNow(out string reason);

    Task<DeliveryResult> ApplyAsync(NatureChange change, CancellationToken ct = default);
}
