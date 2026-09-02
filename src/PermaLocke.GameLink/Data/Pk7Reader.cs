using PKHeX.Core;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Data;

/// <param name="Address">Where the party slot starts in the emulated address space.</param>
public sealed record LivePokemon(
    uint Address,
    int Species,
    string Nickname,
    int Level,
    int CurrentHp,
    int MaxHp,
    bool IsShiny,
    uint Pid,
    int MetLocation,
    string TrainerName);

/// <summary>
/// Reads party structures out of the running game and parses them with PKHeX.Core.
/// </summary>
/// <remarks>
/// The layout of a generation 7 Pokémon is not reimplemented here: PKHeX owns those offsets
/// and gets them right. This only supplies the bytes and decides whether the result looks
/// like a real Pokémon rather than a coincidence in memory.
/// </remarks>
public sealed class Pk7Reader(AzaharRpcClient client)
{
    /// <summary>Size of a party Pokémon, stored block plus battle stats. Taken from PKHeX itself.</summary>
    public static int PartySize { get; } = new PK7().SIZE_PARTY;

    /// <summary>
    /// Offset of the species field inside the decrypted structure, so a hit found by scanning
    /// for a species number can be turned back into the start of the slot.
    /// </summary>
    public const int SpeciesOffset = 0x08;

    /// <summary>Reads and parses one slot, or null when the bytes are not a plausible Pokémon.</summary>
    public LivePokemon? TryRead(uint address)
    {
        if (!client.TryReadMemory(address, PartySize, out var data))
        {
            return null;
        }

        var pokemon = new PK7(data);

        var level = GameLevels.Of(pokemon);

        if (!WorldLimits.IsKnownSpecies(pokemon.Species) || level is <= 0 or > 100)
        {
            return null;
        }

        // A living party member always has a max HP; zero means we are looking at noise.
        if (pokemon.Stat_HPMax <= 0 || pokemon.Stat_HPCurrent > pokemon.Stat_HPMax)
        {
            return null;
        }

        return new LivePokemon(
            address,
            pokemon.Species,
            pokemon.Nickname,
            level,
            pokemon.Stat_HPCurrent,
            pokemon.Stat_HPMax,
            pokemon.IsShiny,
            pokemon.PID,
            pokemon.MetLocation,
            pokemon.OriginalTrainerName);
    }

    /// <summary>Reads consecutive slots from the start of a party block.</summary>
    public IReadOnlyList<LivePokemon> ReadParty(uint partyAddress, int slots = 6)
    {
        var party = new List<LivePokemon>();

        for (var slot = 0; slot < slots; slot++)
        {
            var member = TryRead((uint)(partyAddress + slot * PartySize));

            if (member is null)
            {
                break;
            }

            party.Add(member);
        }

        return party;
    }
}
