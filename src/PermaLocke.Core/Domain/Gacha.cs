namespace PermaLocke.Core.Domain;

/// <param name="BaseStatTotal">Sum of the six base stats, read from the cartridge.</param>
/// <param name="Legendary">Legendary, sub-legendary, mythical or Ultra Beast.</param>
public sealed record SpeciesStats(int Id, string Name, int BaseStatTotal, bool Legendary,
    IReadOnlyList<string> Abilities);

/// <summary>
/// Base stats of every species, which is what the gacha sorts its tiers by.
/// </summary>
/// <remarks>
/// A port. The table is generated from the ROM by <c>PermaLocke.RomTool species</c>, because the
/// cartridge is the authority on base stats. It survives randomization: the module that
/// randomizes Pokémon data shuffles the six stats but keeps the total.
/// </remarks>
public interface ISpeciesStatsCatalog
{
    IReadOnlyList<SpeciesStats> All { get; }

    /// <summary>The 25 nature names, in the order the cartridge numbers them.</summary>
    IReadOnlyList<string> Natures { get; }

    /// <summary>Every ability the cartridge names. The gacha draws from all of them.</summary>
    IReadOnlyList<string> Abilities { get; }
}

/// <summary>
/// One rarity band, defined by how strong its Pokémon are.
/// </summary>
/// <remarks>
/// Tiers are ranges of base stat total, not hand-written lists. That way a tier cannot go stale:
/// every species the cartridge has falls into exactly one, and nobody has to maintain 807 ids.
/// The lower bound is the previous tier's upper bound, so the bands never overlap or leave gaps.
/// </remarks>
/// <param name="MaxBaseStatTotal">Upper bound, inclusive. The last tier uses a number above any real one.</param>
/// <param name="LegendaryChance">
/// Odds that a pull from this tier is a legendary. Zero keeps legendaries out of the tier
/// altogether, which is what stops one turning up in a cheap banner.
/// </param>
public sealed record GachaTier(
    string Id,
    string Name,
    int MaxBaseStatTotal,
    double LegendaryChance,
    int MinLevel,
    int MaxLevel,
    int PerfectIvs,
    double ShinyChance);

/// <param name="Cost">Points a single roll costs.</param>
/// <param name="TierChances">Tier id to probability. They should add up to one.</param>
public sealed record GachaBanner(
    string Id,
    string Name,
    string Description,
    int Cost,
    IReadOnlyDictionary<string, double> TierChances)
{
    public double TotalWeight => TierChances.Values.Sum();
}

/// <summary>
/// What a roll produced, with everything needed to recompute it from scratch.
/// </summary>
/// <remarks>
/// <see cref="Seed"/> and <see cref="Number"/> are what make a roll auditable: the run seed plus
/// the roll number reproduce this exact Pokémon on any machine. Nobody has to be taken at their
/// word about what the gacha gave them.
/// </remarks>
/// <param name="Number">Which roll of this run it was, counting from zero.</param>
public sealed record GachaPull(
    string BannerId,
    string TierId,
    int Species,
    string SpeciesName,
    bool Legendary,
    int BaseStatTotal,
    int Level,
    bool IsShiny,
    IReadOnlyList<int> Ivs,
    int Nature,
    string NatureName,
    int AbilityId,
    string Ability,
    ulong Seed,
    int Number)
{
    /// <summary>Sum of the six IVs, which is the number players actually compare.</summary>
    public int IvTotal => Ivs.Sum();

    /// <summary>
    /// Two pulls are equal when they describe the same Pokémon.
    /// </summary>
    /// <remarks>
    /// Written by hand because a record compares <see cref="Ivs"/> by reference, so a recomputed
    /// roll would never equal the recorded one — which is exactly what auditing a roll does.
    /// </remarks>
    public bool Equals(GachaPull? other) =>
        other is not null
        && (BannerId, TierId, Species, SpeciesName, Legendary, BaseStatTotal, Level, IsShiny,
                Nature, AbilityId, Seed, Number)
           == (other.BannerId, other.TierId, other.Species, other.SpeciesName, other.Legendary,
                other.BaseStatTotal, other.Level, other.IsShiny, other.Nature, other.AbilityId, other.Seed, other.Number)
        && Ivs.SequenceEqual(other.Ivs);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BannerId);
        hash.Add(TierId);
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

/// <param name="Balance">Points left afterwards.</param>
public sealed record GachaRollResult(
    bool Success,
    int Balance,
    GachaPull? Pull = null,
    PokemonEntry? Pokemon = null,
    string? Error = null);

/// <summary>
/// The banners and tiers a run rolls on.
/// </summary>
/// <remarks>
/// A port, like <see cref="IAchievementCatalog"/>: costs, odds and thresholds are configuration
/// an admin edits, never values written in code.
/// </remarks>
public interface IGachaCatalog
{
    IReadOnlyList<GachaTier> Tiers { get; }

    IReadOnlyList<GachaBanner> Banners { get; }
}
