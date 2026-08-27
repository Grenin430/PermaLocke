using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Spends points on a banner and produces a Pokémon.
/// </summary>
/// <remarks>
/// <para>
/// Every roll is reproducible. The run seed plus the roll number derive the randomness, and both
/// travel in the event, so any roll can be recomputed later and checked against what the player
/// claims they got. That is the difference between a gacha and a promise.
/// </para>
/// <para>
/// Nothing is charged for a Pokémon that was not produced: the pull is generated first — it is
/// pure and touches nothing — and only then are the points spent. If spending fails, no Pokémon
/// is stored and no event is written.
/// </para>
/// </remarks>
public sealed class GachaService(
    IGachaCatalog catalog,
    ISpeciesStatsCatalog speciesStats,
    IPointsService points,
    IEventStore events,
    IPokemonRepository pokemon,
    IClock clock)
{
    /// <summary>Salt for the gacha random stream, so other modules do not shift it.</summary>
    private const string GachaSalt = "gacha";

    private IReadOnlyList<GachaTier>? _ordered;

    /// <summary>Tiers sorted by their ceiling, which is what turns them into ranges.</summary>
    private IReadOnlyList<GachaTier> Ordered =>
        _ordered ??= [.. catalog.Tiers.OrderBy(tier => tier.MaxBaseStatTotal)];

    public IReadOnlyList<GachaBanner> Banners => catalog.Banners;

    /// <summary>The tiers, cheapest band first. The screen draws one portal per tier from this.</summary>
    public IReadOnlyList<GachaTier> Tiers => Ordered;

    public GachaBanner? Find(string bannerId) =>
        catalog.Banners.FirstOrDefault(banner => banner.Id == bannerId);

    /// <summary>
    /// Species a tier can yield: those whose base stat total falls in its band. The lower bound
    /// is the previous tier's ceiling, so every species belongs to exactly one tier.
    /// </summary>
    public IReadOnlyList<SpeciesStats> PoolOf(GachaTier tier, bool legendary)
    {
        var index = Ordered.ToList().FindIndex(t => t.Id == tier.Id);
        var floor = index > 0 ? Ordered[index - 1].MaxBaseStatTotal : 0;

        return [.. speciesStats.All.Where(s =>
            s.BaseStatTotal > floor
            && s.BaseStatTotal <= tier.MaxBaseStatTotal
            && s.Legendary == legendary)];
    }

    /// <summary>How many rolls this run has already made. Also the next roll number.</summary>
    public async Task<int> CountRollsAsync(Guid runId, CancellationToken ct = default)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);
        return all.Count(e => e.Type == GameEventType.GachaRoll);
    }

    /// <summary>
    /// Recomputes a roll from the run seed without touching anything, which is what makes the
    /// result auditable rather than merely recorded.
    /// </summary>
    public GachaPull? Preview(GachaBanner banner, ulong runSeed, int number)
    {
        var source = new SeededRandomSource(runSeed).Derive(GachaSalt).Derive($"roll-{number}");
        return Generate(banner, source, number);
    }

    /// <param name="free">
    /// True when the roll has already been paid for by something else and must not be charged.
    /// </param>
    /// <remarks>
    /// A flag and not a second method because everything after the charge is identical, and the
    /// one thing that must never drift between a paid roll and a free one is <em>the roll</em>: the
    /// number, the seed and the entry. Today the only caller that passes it is the LUDÓPATA wheel,
    /// which hands out rolls the player did not buy.
    /// </remarks>
    public async Task<GachaRollResult> RollAsync(Run run, string bannerId, bool free = false,
        CancellationToken ct = default)
    {
        var balance = await points.GetBalanceAsync(run.Id, ct).ConfigureAwait(false);

        if (Find(bannerId) is not { } banner)
        {
            return new GachaRollResult(false, balance, Error: $"No existe el banner «{bannerId}».");
        }

        if (banner.TotalWeight <= 0)
        {
            return new GachaRollResult(false, balance,
                Error: $"El banner «{banner.Name}» no tiene rarezas configuradas.");
        }

        if (!free && balance < banner.Cost)
        {
            return new GachaRollResult(false, balance,
                Error: $"Te faltan {banner.Cost - balance} puntos: «{banner.Name}» cuesta {banner.Cost}.");
        }

        // Se genera antes de cobrar. La tirada es pura y no toca nada, así que si el cobro
        // fallara no habría nada que deshacer ni el jugador perdería puntos por una tirada que
        // no llegó a existir.
        var number = await CountRollsAsync(run.Id, ct).ConfigureAwait(false);

        if (Preview(banner, run.Seed, number) is not { } pull)
        {
            return new GachaRollResult(false, balance,
                Error: $"El banner «{banner.Name}» no tiene ninguna especie que ofrecer. "
                       + "¿Está generado Data/species.json?");
        }

        // Un banner gratuito no genera gasto: cobrar cero no es cobrar, y PointsService rechaza
        // con razón que se le pidan gastos de cero. Es lo que permite dejar un banner a coste 0
        // para probar sin tocar el motor de puntos.
        var spent = banner.Cost > 0 && !free
            ? await points.SpendAsync(run.Id, banner.Cost,
                $"Tirada de gacha en «{banner.Name}».", EventSource.Player, run.PlayerName, ct)
                .ConfigureAwait(false)
            : new PointsResult(true, balance);

        if (!spent.Success)
        {
            return new GachaRollResult(false, spent.NewBalance, Error: spent.FailureReason);
        }

        var entry = new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Species = pull.Species,
            SpeciesName = pull.SpeciesName,
            Level = pull.Level,
            IsShiny = pull.IsShiny,
            Origin = PokemonOrigin.Gacha,
            EncounterType = EncounterType.Special,
            ObtainedAt = clock.Now,

            // Una tirada no gasta el encuentro de ninguna zona: no viene de ninguna.
            ConsumedZoneEncounter = false
        };

        await pokemon.SaveAsync(entry, ct).ConfigureAwait(false);
        await RecordAsync(run, banner, pull, entry, free, ct).ConfigureAwait(false);

        return new GachaRollResult(true, spent.NewBalance, pull, entry);
    }

    private Task RecordAsync(Run run, GachaBanner banner, GachaPull pull, PokemonEntry entry,
        bool free, CancellationToken ct) =>
        events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.GachaRoll,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Gacha «{banner.Name}»: {pull.SpeciesName} Nv.{pull.Level}"
                          + (pull.Legendary ? " (legendario)" : string.Empty)
                          + (pull.IsShiny ? " shiny" : string.Empty),
            PokemonId = entry.Id,
            Seed = pull.Seed,
            Data = new Dictionary<string, string>
            {
                ["banner"] = banner.Id,

                // Lo que convierte una tirada en gastada de las gratis. El credito disponible es
                // lo ganado menos los eventos con esta marca, asi que sin ella una tirada gratis
                // no se descontaria de nada.
                ["gratis"] = free.ToString(),
                ["rareza"] = pull.TierId,
                ["tirada"] = pull.Number.ToString(),
                ["especie"] = pull.Species.ToString(),
                ["total"] = pull.BaseStatTotal.ToString(),
                ["legendario"] = pull.Legendary.ToString(),
                ["nivel"] = pull.Level.ToString(),
                ["shiny"] = pull.IsShiny.ToString(),
                ["ivs"] = string.Join('/', pull.Ivs),
                ["naturaleza"] = pull.NatureName,
                ["habilidad"] = pull.Ability
            }
        }, ct);

    /// <summary>
    /// The roll itself. Pure: same source, same result, on any machine and any build.
    /// </summary>
    private GachaPull? Generate(GachaBanner banner, IRandomSource source, int number)
    {
        if (PickTier(banner, source) is not { } tier)
        {
            return null;
        }

        var wantLegendary = source.Chance(tier.LegendaryChance);
        var pool = PoolOf(tier, wantLegendary);

        // Un tier puede quedarse sin candidatos de la clase que salió — por ejemplo, no hay
        // legendarios flojos. Se cae a la otra clase en vez de no dar nada.
        if (pool.Count == 0)
        {
            wantLegendary = !wantLegendary;
            pool = PoolOf(tier, wantLegendary);
        }

        if (pool.Count == 0)
        {
            return null;
        }

        var chosen = pool[source.Next(pool.Count)];

        var level = tier.MaxLevel > tier.MinLevel
            ? source.Next(tier.MinLevel, tier.MaxLevel + 1)
            : tier.MinLevel;

        // Habilidad al azar de entre TODAS las del juego, no solo las de la especie: es un
        // gacha, y que un Magikarp salga con Levitación es parte de la gracia. La posición en
        // la lista es el id que el juego usa, así que se sortea sobre ella y se descartan los
        // huecos sin nombre en vez de compactarla, que desplazaría todos los ids.
        var abilities = speciesStats.Abilities;
        var abilityId = 0;

        for (var attempt = 0; attempt < 12 && abilities.Count > 1; attempt++)
        {
            var candidate = source.Next(1, abilities.Count);

            if (!string.IsNullOrWhiteSpace(abilities[candidate]) && abilities[candidate] != "-")
            {
                abilityId = candidate;
                break;
            }
        }

        var ability = abilityId > 0 && abilityId < abilities.Count ? abilities[abilityId] : string.Empty;

        var nature = source.Next(25);
        var natures = speciesStats.Natures;

        return new GachaPull(
            banner.Id,
            tier.Id,
            chosen.Id,
            chosen.Name,
            chosen.Legendary,
            chosen.BaseStatTotal,
            level,
            source.Chance(tier.ShinyChance),
            RollIvs(source, tier.PerfectIvs),
            nature,
            nature < natures.Count ? natures[nature] : string.Empty,
            abilityId,
            ability,
            source.Seed,
            number);
    }

    /// <summary>
    /// Weighted pick among the tiers the banner offers. The chances are relative, so a banner is
    /// retuned by editing one number, and they are walked in tier order so that the same seed
    /// always lands on the same tier regardless of how the file happens to be sorted.
    /// </summary>
    private GachaTier? PickTier(GachaBanner banner, IRandomSource source)
    {
        var available = Ordered.Where(tier => banner.TierChances.ContainsKey(tier.Id)).ToList();

        if (available.Count == 0 || banner.TotalWeight <= 0)
        {
            return null;
        }

        var roll = source.NextDouble() * banner.TotalWeight;
        var running = 0.0;

        foreach (var tier in available)
        {
            running += banner.TierChances[tier.Id];

            if (roll < running)
            {
                return tier;
            }
        }

        return available[^1];
    }

    /// <summary>
    /// Six IVs, of which <paramref name="perfect"/> are forced to 31 in random stats. Which ones
    /// matters: always perfecting the same two would make every rare of a banner feel identical.
    /// </summary>
    private static int[] RollIvs(IRandomSource source, int perfect)
    {
        var ivs = new int[6];

        for (var stat = 0; stat < ivs.Length; stat++)
        {
            ivs[stat] = source.Next(32);
        }

        var remaining = Math.Clamp(perfect, 0, ivs.Length);
        var candidates = Enumerable.Range(0, ivs.Length).ToList();

        while (remaining-- > 0)
        {
            var index = source.Next(candidates.Count);
            ivs[candidates[index]] = 31;
            candidates.RemoveAt(index);
        }

        return ivs;
    }
}
