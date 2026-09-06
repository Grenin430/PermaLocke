using PKHeX.Core;

namespace PermaLocke.GameLink.Data;

/// <summary>
/// Whether a party entry really keeps its battle stats where a party <see cref="PK7"/> puts them.
/// </summary>
/// <remarks>
/// <para>
/// The party lives in several structures and they do not all hold the same thing. Until now the
/// question was answered by the stride — <c>0x104</c> meant «the stats are here» — and that is a
/// proxy, not the fact. Measured against the real game with the Gyarados on screen at 118 of 131:
/// <c>0x330128E4</c> read <b>118/131</b> in slot zero and a sane level for everybody, while
/// <c>0x3254EE60</c>, the same stride, read <b>42649/10902</b> and levels of 54, 131, 207 and 179.
/// Same stride, one real tail and one not, so writes past the encrypted block were going into bytes
/// nobody had identified — which §53 forbids for exactly the reason it cost a Ledyba.
/// </para>
/// <para>
/// The anchor is that a party Pokémon <b>carries its level twice</b>: as experience inside the
/// encrypted block, which the checksum vouches for, and as <c>Stat_Level</c> in the tail. Where the
/// tail is genuine the two agree; where it is something else they agree only by accident. The stat
/// bounds are there so that accident needs to happen seven times at once.
/// </para>
/// <para>
/// Note that none of this can be seen by searching memory for the numbers. A party entry keeps its
/// battle stats <b>encrypted</b> as well — PKHeX crypts the tail in a second pass with the same
/// seed — so on screen 118 reads as <c>EF A6</c> in memory. That is why sweeping four hundred
/// megabytes for «118» came back with nothing but the health bar's own copy.
/// </para>
/// </remarks>
public static class PartyStats
{
    /// <summary>No Pokémon has ever had more, so anything above this is not a stat.</summary>
    private const int Ceiling = 999;

    /// <summary>True when this entry's tail is the party stats and may be written.</summary>
    public static bool AreHere(PKM? pokemon)
    {
        if (pokemon is not { } entry || !entry.ChecksumValid)
        {
            return false;
        }

        // Los dos registros del nivel tienen que coincidir. Es la comprobación que decide, porque
        // uno vive dentro del bloque cifrado y el otro en la cola: donde la cola es otra cosa,
        // coincidir es casualidad.
        if (entry.Stat_Level != GameLevels.Of(entry))
        {
            return false;
        }

        int[] stats =
        [
            entry.Stat_HPMax, entry.Stat_ATK, entry.Stat_DEF,
            entry.Stat_SPE, entry.Stat_SPA, entry.Stat_SPD
        ];

        return stats.All(value => value is > 0 and <= Ceiling)
               && entry.Stat_HPCurrent >= 0
               && entry.Stat_HPCurrent <= entry.Stat_HPMax;
    }
}
