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
    string TrainerName,
    int Form = 0,
    IReadOnlyList<int>? Moves = null,
    bool IsEgg = false,
    int HeldItem = 0,
    int Status = 0,
    double ExpProgress = -1,
    IReadOnlyList<int>? Pp = null);

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

    /// <summary>Size of the encrypted block, which is where the two layouts stop agreeing.</summary>
    public static int StoredSize { get; } = new PK7().SIZE_STORED;

    /// <summary>
    /// Reads and parses one slot, or null when the bytes are not a plausible Pokémon.
    /// </summary>
    /// <param name="statsOffset">
    /// Where this structure keeps the 28 bytes of battle stats. Null means straight after the
    /// encrypted block, which is where a PK7 puts them and where the save-block mirror has them;
    /// the structure the game actually reads keeps them at
    /// <see cref="PartyLayoutLocator.AuthoritativeStatsOffset"/> instead.
    /// </param>
    /// <remarks>
    /// The parameter exists because reading 260 contiguous bytes only ever worked for the mirror,
    /// and the mirror is <b>stale</b>: the game fills it when it saves and not before, so a
    /// Pokémon that faints reads as healthy until the player saves. Every death the watcher was
    /// supposed to catch live was being read from a photograph (§99).
    /// </remarks>
    public LivePokemon? TryRead(uint address, uint? statsOffset = null)
    {
        if (!client.TryReadMemory(address, StoredSize, out var stored))
        {
            return null;
        }

        var tail = statsOffset ?? (uint)StoredSize;

        if (!client.TryReadMemory(address + tail, PartySize - StoredSize, out var stats))
        {
            return null;
        }

        var data = new byte[PartySize];

        stored.CopyTo(data, 0);
        stats.CopyTo(data, StoredSize);

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
            pokemon.OriginalTrainerName,
            pokemon.Form,
            [pokemon.Move1, pokemon.Move2, pokemon.Move3, pokemon.Move4],
            pokemon.IsEgg,
            pokemon.HeldItem,
            pokemon.Status_Condition,
            ExpProgress(pokemon, level),
            [pokemon.Move1_PP, pokemon.Move2_PP, pokemon.Move3_PP, pokemon.Move4_PP]);
    }

    /// <summary>
    /// How far a Pokémon is from its next level, 0 to 1, on the curve its own game uses (the installed world's when it has one, as
    /// <see cref="GameLevels"/> does); -1 when the curve is not known or the experience does not agree with the level.
    /// </summary>
    public static double ExpProgress(PKM pokemon, int level)
    {
        if (level >= 100) return 1;

        var growth = WorldLimits.GrowthOf(pokemon.Species)
            ?? (pokemon.Species <= PersonalTable.USUM.MaxSpeciesID ? PersonalTable.USUM[pokemon.Species].EXPGrowth : (byte?)null);
        if (growth is not { } curve) return -1;

        var floor = Experience.GetEXP((byte)level, curve);
        var next = Experience.GetEXP((byte)(level + 1), curve);
        if (next <= floor || pokemon.EXP < floor) return -1;

        return Math.Clamp((pokemon.EXP - floor) / (double)(next - floor), 0, 1);
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
