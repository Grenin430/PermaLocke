using PermaLocke.Core.Abstractions;
using pk3DS.Core;

namespace PermaLocke.Randomizer.Modules;

/// <summary>
/// The set of species a module may hand out, and how it picks one.
/// <para>
/// Built from base stat totals rather than from pk3DS directly, so the selection rules can be
/// tested without a cartridge.
/// </para>
/// </summary>
public sealed class SpeciesPool
{
    private readonly int[] _allowed;
    private readonly int[] _baseStatTotals;
    private readonly RandomizerOptions _options;
    private readonly Dictionary<int, int> _oneToOne = [];

    /// <param name="baseStatTotals">Indexed by species id; index 0 is unused.</param>
    public SpeciesPool(int[] baseStatTotals, RandomizerOptions options)
    {
        _baseStatTotals = baseStatTotals;
        _options = options;

        var max = options.EffectiveMaxSpecies(baseStatTotals.Length - 1);
        var banned = options.BannedSpecies.ToHashSet();
        _allowed = [.. Enumerable.Range(1, Math.Max(max, 0)).Where(s => !banned.Contains(s))];

        if (_allowed.Length == 0)
        {
            throw new ArgumentException(
                "No queda ninguna especie disponible: la lista de prohibidas las excluye todas.", nameof(options));
        }
    }

    private SpeciesPool(int[] allowed, int[] baseStatTotals, RandomizerOptions options)
    {
        _allowed = allowed;
        _baseStatTotals = baseStatTotals;
        _options = options;
    }

    public int Count => _allowed.Length;

    /// <summary>
    /// The regional forms a picked species may come out in. None unless the pool was read from a
    /// game with <see cref="FromGame"/>: a pool built by hand, as the tests do, picks species only.
    /// </summary>
    public RegionalForms Forms { get; init; } = RegionalForms.None;

    /// <summary>
    /// A narrower pool: the same species and the same picking rules, minus whatever fails the test.
    /// </summary>
    /// <remarks>
    /// For a module that needs more of a species than the others do — the starters have to be the
    /// first stage of a three-stage family. Narrowing the pool rather than rolling until something
    /// fits keeps the pick honest: with a filter there is no "give up after sixteen tries and hand
    /// over whatever came out", which is how a constraint quietly stops being one.
    /// </remarks>
    /// <exception cref="ArgumentException">Nothing survives, so there is nothing to hand out.</exception>
    public SpeciesPool Where(Func<int, bool> keep, string what)
    {
        var kept = _allowed.Where(keep).ToArray();

        if (kept.Length == 0)
        {
            throw new ArgumentException($"Ninguna especie disponible cumple: {what}.", nameof(keep));
        }

        return new SpeciesPool(kept, _baseStatTotals, _options) { Forms = Forms, HasType = HasType };
    }

    /// <summary>
    /// Whether a species, in a given form, has a type: (species, form, PKHeX type id). Read from the loaded world by
    /// <see cref="FromGame"/>; null for a pool built by hand, which cannot be narrowed by type.
    /// </summary>
    public Func<int, int, int, bool>? HasType { get; init; }

    /// <summary>
    /// Only the species that have the type in some form (§223), and their forms narrowed to those that do: a Vulpix is a
    /// FUEGO one only in its ordinary form, a Galarian Ponyta is the PSÍQUICO one.
    /// </summary>
    public SpeciesPool OfType(int type)
    {
        var has = HasType ?? throw new InvalidOperationException("Este pool no sabe los tipos de las especies.");
        bool Keep(int species, int form) => has(species, form, type);

        return Where(species => Keep(species, 0) || Forms.Of(species).Any(form => Keep(species, form)), $"tener el tipo {type}")
            .WithForms(Forms.Restrict(Keep));
    }

    private SpeciesPool WithForms(RegionalForms forms) =>
        new(_allowed, _baseStatTotals, _options) { Forms = forms, HasType = HasType };

    /// <summary>Reads the base stat totals out of the loaded personal table.</summary>
    /// <param name="gameMaxSpecies">
    /// How many species the loaded tables describe, from <c>RomWorkspace.MaxSpecies</c>.
    /// </param>
    /// <remarks>
    /// This used to clamp against <c>config.MaxSpeciesID</c>, which is a pk3DS <b>constant</b> fixed
    /// at 807 that never looks at the files. On the cartridge the two agree; on a mod that adds
    /// Pokémon the clamp would have silently thrown away everything past 807 — the pool would build,
    /// the randomization would succeed, and not one of the new species would ever appear. Nothing
    /// would have failed to say so.
    /// </remarks>
    public static SpeciesPool FromGame(GameConfig config, RandomizerOptions options,
        int gameMaxSpecies)
    {
        var max = options.EffectiveMaxSpecies(gameMaxSpecies);
        var totals = new int[max + 1];
        for (var species = 1; species <= max; species++)
        {
            var entry = config.Personal[species];
            totals[species] = entry.HP + entry.ATK + entry.DEF + entry.SPA + entry.SPD + entry.SPE;
        }

        // Las formas que ESTE mundo declara: el cartucho solo trae las de Alola (§138).
        return new SpeciesPool(totals, options)
        {
            Forms = RegionalForms.From(options.RegionalForms, species => config.Personal[species].FormeCount),
            HasType = (species, form, type) => config.Personal.GetFormEntry(species, form).Types.Contains(type)
        };
    }

    /// <summary>Base stat total, used to keep replacements in the original's league.</summary>
    public int BaseStatTotal(int species) =>
        species > 0 && species < _baseStatTotals.Length ? _baseStatTotals[species] : 0;

    /// <summary>
    /// Picks a replacement for <paramref name="original"/>. Under
    /// <see cref="SpeciesPickMode.OneToOne"/> the answer is remembered, so the same species is
    /// always replaced by the same one for the whole run.
    /// </summary>
    public int Pick(IRandomSource random, int original)
    {
        if (_options.PickMode == SpeciesPickMode.PerSlot)
        {
            return PickFrom(random, original);
        }

        if (!_oneToOne.TryGetValue(original, out var mapped))
        {
            mapped = PickFrom(random, original);
            _oneToOne[original] = mapped;
        }
        return mapped;
    }

    private int PickFrom(IRandomSource random, int original)
    {
        if (!_options.SimilarStrength)
        {
            return _allowed[random.Next(_allowed.Length)];
        }

        var target = BaseStatTotal(original);
        var tolerance = _options.StrengthTolerance;

        // Widen the band rather than fail on an outlier such as Shedinja or Blissey.
        for (var attempt = 0; attempt < 8; attempt++, tolerance *= 2)
        {
            var low = target * (1 - tolerance);
            var high = target * (1 + tolerance);
            var candidates = _allowed.Where(s => BaseStatTotal(s) >= low && BaseStatTotal(s) <= high).ToArray();
            if (candidates.Length > 0)
            {
                return candidates[random.Next(candidates.Length)];
            }
        }

        return _allowed[random.Next(_allowed.Length)];
    }
}
