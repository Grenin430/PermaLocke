namespace PermaLocke.Core.Domain;

/// <param name="BaseStatTotal">Sum of the six base stats, read from the cartridge.</param>
/// <param name="Legendary">Legendary, sub-legendary, mythical or Ultra Beast.</param>
/// <param name="Forms">
/// The regional forms it may come out in from the gacha or a wonder trade, besides its ordinary one
/// (§139). Empty for most species.
/// </param>
public sealed record SpeciesStats(int Id, string Name, int BaseStatTotal, bool Legendary,
    IReadOnlyList<string> Abilities, IReadOnlyList<SpeciesForm>? Forms = null)
{
    /// <summary>The regional forms, never null.</summary>
    public IReadOnlyList<SpeciesForm> RegionalForms => Forms ?? [];
}

/// <summary>A regional form of a species: its index and how the game names it («Alola», «Galar»...).</summary>
public sealed record SpeciesForm(int Form, string Name);

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

    /// <summary>Ability ids the gacha and the wonder trade never deal.</summary>
    /// <remarks>
    /// The expansion mod's abilities tied to one Pokémon's forms: their code is written for that
    /// Pokémon. The same list the randomizer keeps out of the world (§136).
    /// </remarks>
    IReadOnlyCollection<int> BannedAbilities => [];

    /// <summary>Every evolution family, which is what the gacha actually hands out.</summary>
    IReadOnlyList<EvolutionLine> Lines { get; }
}

/// <summary>
/// One evolution family: ordered stages, each holding the species on that rung.
/// </summary>
/// <remarks>
/// <para>
/// The gacha rolls a <b>family</b> and then a <b>stage</b>, so the rarity of a tier is about where
/// the line ENDS and what you are handed is usually its beginning. A tier five is a Gible, and what
/// tier five promises is Garchomp.
/// </para>
/// <para>
/// A stage holds more than one species when the family branches — Wurmple's second rung is Silcoon
/// and Cascoon, Eevee's is all eight — and the roll picks between them. Read from the cartridge by
/// <c>RomTool species</c>, never a hand-kept list.
/// </para>
/// </remarks>
public sealed record EvolutionLine(IReadOnlyList<IReadOnlyList<int>> Stages)
{
    /// <summary>The rung a roll lands on, with anything past the end clamped to the last.</summary>
    /// <remarks>
    /// Clamping and not skipping: a two-stage family asked for its third rung gives its second,
    /// and Farfetch'd is always Farfetch'd. Without it the short families would simply refuse a
    /// share of the rolls, and the rarest outcome of a tier would land on the longest lines.
    /// </remarks>
    public IReadOnlyList<int> StageAt(int stage) =>
        Stages[Math.Clamp(stage, 0, Stages.Count - 1)];

    /// <summary>Everything this family can ever be, in one list.</summary>
    public IEnumerable<int> AllSpecies => Stages.SelectMany(stage => stage);
}

/// <summary>
/// How likely each rung of a family is, at one point of the run.
/// </summary>
/// <remarks>
/// <para>
/// What is left over after <paramref name="Second"/> and <paramref name="Final"/> is the chance of
/// the <b>first</b> stage, so a row of zeroes means "always the base form". That is deliberate:
/// the safe reading of a missing or broken table is the start of the game, not a free Garchomp.
/// </para>
/// <para>
/// Keyed by stages cleared, which is the run's own measure of how far along it is — the same
/// number the level cap is deduced from (§49) rather than a second notion of progress that could
/// disagree with it.
/// </para>
/// </remarks>
/// <param name="Cleared">Stages cleared at or above which this row applies.</param>
/// <param name="Second">Percentage chance of the second rung.</param>
/// <param name="Final">Percentage chance of the last rung.</param>
public sealed record StageOdds(int Cleared, int Second, int Final)
{
    public int First => Math.Max(0, 100 - Second - Final);
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
    int Number,
    int Form = 0,
    string FormName = "")
{
    /// <summary>Sum of the six IVs, which is the number players actually compare.</summary>
    public int IvTotal => Ivs.Sum();

    /// <summary>The name to show: «Vulpix de Alola» for a regional form, the species otherwise.</summary>
    public string DisplayName => string.IsNullOrEmpty(FormName) ? SpeciesName : $"{SpeciesName} de {FormName}";

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
                Nature, AbilityId, Seed, Number, Form)
           == (other.BannerId, other.TierId, other.Species, other.SpeciesName, other.Legendary,
                other.BaseStatTotal, other.Level, other.IsShiny, other.Nature, other.AbilityId, other.Seed, other.Number,
                other.Form)
        && Ivs.SequenceEqual(other.Ivs);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BannerId);
        hash.Add(TierId);
        hash.Add(Species);
        hash.Add(Form);
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

    /// <summary>
    /// Which rung of a family a roll lands on, by how far the run has got.
    /// </summary>
    /// <remarks>
    /// Empty means the first stage always, which is what a run that has cleared nothing gets
    /// anyway — so a missing table degrades into the start of the game rather than into a
    /// jackpot.
    /// </remarks>
    IReadOnlyList<StageOdds> StageOdds { get; }
}
