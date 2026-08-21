using PermaLocke.Core.Abstractions;

namespace PermaLocke.Core.Domain;

/// <summary>
/// How wide the band of acceptable trades is, as a fraction of what the player handed over.
/// </summary>
/// <remarks>
/// Asymmetric on purpose: a trade may come back a little better than what went in, but not much
/// worse. It is the one number that decides whether a wonder trade is a gamble or a laundry, so
/// it lives in <c>Data/wondertrade.json</c> and not in the code.
/// </remarks>
/// <param name="Below">Fraction the received total may fall short by. 0.08 is -8%.</param>
/// <param name="Above">Fraction it may exceed by. 0.20 is +20%.</param>
/// <param name="AllowLegendaries">
/// Whether legendaries can come back. The band already gates them — only a 600-point Pokémon can
/// draw one — but a competition may still want them out.
/// </param>
public sealed record WonderTradeWindow(double Below, double Above, bool AllowLegendaries)
{
    /// <summary>The band this base stat total can trade into, both ends inclusive.</summary>
    public (int Min, int Max) Band(int baseStatTotal) =>
        ((int)Math.Floor(baseStatTotal * (1 - Below)), (int)Math.Ceiling(baseStatTotal * (1 + Above)));
}

/// <summary>The wonder trade settings, read from configuration rather than compiled in.</summary>
public interface IWonderTradeCatalog
{
    WonderTradeWindow Window { get; }
}

/// <summary>What the player is handing over.</summary>
/// <param name="Box">Zero-based box it sits in, so the swap knows where to put the new one.</param>
public sealed record WonderTradeGift(int Species, string Name, int Level, int Box, int Slot);

/// <summary>
/// What a wonder trade produced, with everything needed to recompute it from scratch.
/// </summary>
/// <remarks>
/// Same contract as a gacha roll: the run seed plus the trade number reproduce this exact
/// Pokémon on any machine, so nobody has to be taken at their word about what they got.
/// </remarks>
/// <param name="Generation">Which generation the received species belongs to, 1 to 7.</param>
/// <param name="Ivs">
/// Six, in <b>HP/Atk/Def/Spe/SpA/SpD</b> order, which is the order the cartridge stores them in
/// and therefore the one whoever writes the save expects. It is <em>not</em> the order the boxes
/// are read back in, which is the order the game displays. Both are right; they are different
/// questions, and mixing them silently permutes two stats.
/// </param>
/// <param name="Number">Which wonder trade of this run it was, counting from zero.</param>
public sealed record WonderTradeOffer(
    int GivenSpecies,
    string GivenName,
    int GivenBaseStatTotal,
    int Species,
    string Name,
    int BaseStatTotal,
    int Generation,
    TypePair Types,
    bool Legendary,
    int Level,
    bool IsShiny,
    IReadOnlyList<int> Ivs,
    int Nature,
    string NatureName,
    int AbilityId,
    string Ability,
    int MinBaseStatTotal,
    int MaxBaseStatTotal,
    ulong Seed,
    int Number)
{
    public int IvTotal => Ivs.Sum();

    /// <summary>How the received total compares with what went in, as a percentage.</summary>
    public int Difference => GivenBaseStatTotal == 0
        ? 0
        : (int)Math.Round((BaseStatTotal - GivenBaseStatTotal) * 100.0 / GivenBaseStatTotal);

    /// <summary>
    /// Two offers are equal when they describe the same trade.
    /// </summary>
    /// <remarks>
    /// Written by hand because a record compares <see cref="Ivs"/> by reference, so a recomputed
    /// trade would never equal the recorded one — which is exactly what auditing a trade does.
    /// </remarks>
    public bool Equals(WonderTradeOffer? other) =>
        other is not null
        && (GivenSpecies, Species, BaseStatTotal, Generation, Legendary, Level, IsShiny,
                Nature, AbilityId, Seed, Number)
           == (other.GivenSpecies, other.Species, other.BaseStatTotal, other.Generation,
                other.Legendary, other.Level, other.IsShiny, other.Nature, other.AbilityId,
                other.Seed, other.Number)
        && Ivs.SequenceEqual(other.Ivs);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GivenSpecies);
        hash.Add(Species);
        hash.Add(Level);
        hash.Add(IsShiny);
        hash.Add(Nature);
        hash.Add(Seed);
        hash.Add(Number);

        foreach (var iv in Ivs)
        {
            hash.Add(iv);
        }

        return hash.ToHashCode();
    }
}

/// <param name="Error">Why nothing was traded, in words the player can act on. Null when fine.</param>
public sealed record WonderTradeResult(
    bool Success,
    WonderTradeOffer? Offer = null,
    PokemonEntry? Entry = null,
    string? Error = null);

/// <summary>Which generation a species belongs to.</summary>
/// <remarks>
/// Ranges, not a table: the National Dex numbers generations in blocks, and a block only ever
/// grows at the end. Anything past Zeraora is not in this cartridge.
/// </remarks>
public static class Generations
{
    /// <summary>Last species of each generation, in order.</summary>
    private static readonly int[] Ends = [151, 251, 386, 493, 649, 721, 807];

    public static int Of(int species)
    {
        for (var index = 0; index < Ends.Length; index++)
        {
            if (species <= Ends[index])
            {
                return index + 1;
            }
        }

        return Ends.Length;
    }
}
