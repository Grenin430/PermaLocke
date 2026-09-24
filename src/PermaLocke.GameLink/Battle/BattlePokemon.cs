using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink.Battle;

/// <summary>
/// The whole Pokémon behind a battle block: PID, OT, and so whether it is shiny.
/// </summary>
/// <remarks>
/// <para>
/// Measured on 2026-09-14 in a wild battle against a shiny Wooloo (§118), reading around the pointer of the rival's
/// block in both battle tables: a PK7, encrypted as the game stores it, with a valid checksum and the block's own
/// species, sits at <b>pointer + 0x40</b> in both. In front of it is a heap block header («DU») and the pointer's
/// own object. The wild Pokémon already carries the player's TID and SID, so whether it is shiny is PKHeX's own
/// <see cref="PKM.IsShiny"/>, and it agreed with the PID worked against the player's trainer card.
/// </para>
/// <para>
/// It exists because the trainer card's «shiny Pokémon encountered» record does not go up when the battle starts:
/// read in the middle of that same battle it still said 5. It is counted later, too late to give the balls back.
/// </para>
/// <para>
/// Believed only with a valid checksum and the species the block says. Measured with one wild battle, in both
/// tables; a trainer battle, a double or an SOS call have not been read.
/// </para>
/// </remarks>
public static class BattlePokemon
{
    /// <summary>Matches a fallen combatant by validated PID when the battle and party positions differ.</summary>
    public static LivePartyMember? MatchPlayer(BattleFaint faint, IReadOnlyList<LivePartyMember> party,
        IEnumerable<PK7?> copies)
    {
        if (!faint.IsPlayers) return null;
        var identities = copies.OfType<PK7>()
            .Where(p => p.ChecksumValid && p.Species == faint.Species)
            .Select(p => p.PID).Distinct().ToArray();
        if (identities.Length > 1) return null;
        if (identities.Length == 1)
        {
            var matches = party.Where(p => p.Pid == identities[0] && p.Species == faint.Species).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        // Preserve the original slot check if the PK7 cannot be read, but never choose between
        // two party members of the same species without their identity.
        var sameSpecies = party.Where(p => p.Species == faint.Species).ToArray();
        return sameSpecies.Length == 1 && sameSpecies[0].Slot == faint.BattleId ? sameSpecies[0] : null;
    }

    /// <summary>From the block's pointer to the Pokémon.</summary>
    public const int Offset = 0x40;

    /// <summary>A stored PK7: the encrypted block, without the party stats.</summary>
    public const int StoredSize = 232;

    /// <summary>What to read from the pointer to parse it.</summary>
    public const int ReadLength = Offset + StoredSize;

    /// <summary>The Pokémon, or null when these bytes are not the one the block says.</summary>
    /// <param name="fromPointer">Bytes read from the block's pointer, at least <see cref="ReadLength"/>.</param>
    public static PK7? Parse(ReadOnlySpan<byte> fromPointer, int species)
    {
        if (fromPointer.Length < ReadLength)
        {
            return null;
        }

        // PK7 descifra el array que se le da: se le da una copia.
        var pokemon = new PK7(fromPointer.Slice(Offset, StoredSize).ToArray());

        return pokemon.ChecksumValid && pokemon.Species == species ? pokemon : null;
    }
}
