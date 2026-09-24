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
    /// Families a tier can yield: those whose <b>final form</b> falls in its band.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The band is about where the line ENDS, not about the Pokémon handed over. That is the whole
    /// balance of this gacha: a tier five is the eight families that reach 600, so what it gives
    /// you early on is a Gible, and what it is promising is Garchomp. Grading by the species
    /// delivered instead — which is what this used to do — made the cheap tiers hand out things
    /// that could never become anything, and the dear one hand out finished Pokémon.
    /// </para>
    /// <para>
    /// The best ending and not the worst when a family branches, because that is what the tier is
    /// promising and the player picks the branch by playing. The lower bound is the previous
    /// tier's ceiling, so every family belongs to exactly one tier.
    /// </para>
    /// </remarks>
    public IReadOnlyList<EvolutionLine> LinesOf(GachaTier tier, bool legendary)
    {
        // Todos los legendarios, sin recortar por banda -- pero SOLO en un tier que de verdad los
        // reparta. Sin esa condicion la lista de «quien puede salir» los enseñaba en los cinco,
        // porque la pregunta se hace por tier y la respuesta no lo miraba: un tier con la
        // probabilidad a cero decia que podia dar un Mewtwo y no podia dar ninguno.
        if (legendary)
        {
            return tier.LegendaryChance > 0 ? LegendaryLines : [];
        }

        var index = Ordered.ToList().FindIndex(t => t.Id == tier.Id);
        var floor = index > 0 ? Ordered[index - 1].MaxBaseStatTotal : 0;

        return [.. speciesStats.Lines.Where(line =>
        {
            var end = EndOf(line);
            return end > floor && end <= tier.MaxBaseStatTotal && !IsLegendary(line);
        })];
    }

    /// <summary>
    /// Species a tier can yield: every rung of every family in it.
    /// </summary>
    /// <remarks>
    /// All of them and not only the first, because with the run far enough along every rung is
    /// reachable. The screen that lists this is answering «what can come out of here», and the
    /// honest answer stopped being one species per family the moment the stage became a roll.
    /// </remarks>
    public IReadOnlyList<SpeciesStats> PoolOf(GachaTier tier, bool legendary)
    {
        var byId = ById;

        return [.. LinesOf(tier, legendary)
            .SelectMany(line => line.AllSpecies)
            .Distinct()
            .Where(byId.ContainsKey)
            .Select(id => byId[id])];
    }

    private Dictionary<int, SpeciesStats>? _byId;

    private Dictionary<int, SpeciesStats> ById =>
        _byId ??= speciesStats.All.ToDictionary(s => s.Id);

    /// <summary>
    /// How strong the family gets: the highest total among the species on its last rung.
    /// </summary>
    /// <remarks>
    /// A species the catalogue does not know counts as nothing rather than throwing — the
    /// generator already refuses to name anyone it left out, and a family cannot be graded on a
    /// number that is not there.
    /// </remarks>
    private int EndOf(EvolutionLine line)
    {
        var byId = ById;

        return line.Stages[^1]
            .Select(id => byId.TryGetValue(id, out var stats) ? stats.BaseStatTotal : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>A family counts as legendary when anything it can become is.</summary>
    /// <remarks>
    /// Anything and not just the ending, because Cosmog is not a legendary you would want turning
    /// up in the cheap banner on the grounds that its first rung is weak.
    /// </remarks>
    private bool IsLegendary(EvolutionLine line)
    {
        var byId = ById;

        return line.AllSpecies.Any(id => byId.TryGetValue(id, out var stats) && stats.Legendary);
    }

    private IReadOnlyList<EvolutionLine>? _legendaryLines;

    private IReadOnlyList<EvolutionLine> LegendaryLines =>
        _legendaryLines ??= [.. speciesStats.Lines.Where(IsLegendary)];

    /// <summary>
    /// Every legendary in the game, whatever its base stat total.
    /// </summary>
    /// <remarks>
    /// Legendaries are drawn from the whole set and not from the tier's band, at the player's
    /// request. The band was doing something nobody asked for: only legendaries above 590 could
    /// ever come out, so the weak and the middling ones — the birds, the beasts, the Tapu, the
    /// regis, Type: Null — could not appear at all. Since a legendary can only come from tier five
    /// and tier five only exists in the expensive banner, «which tier» was never carrying any
    /// meaning here; what it decided was which legendaries were quietly impossible.
    /// </remarks>
    public IReadOnlyList<SpeciesStats> Legendaries =>
        [.. speciesStats.All.Where(s => s.Legendary)];

    /// <summary>Stats of one species, for a screen listing a family rung by rung.</summary>
    public SpeciesStats? StatsOf(int id) => ById.GetValueOrDefault(id);

    /// <summary>
    /// Which rung of a family a roll lands on with this much of the run behind it.
    /// </summary>
    /// <remarks>
    /// The highest row at or below the stages cleared, so the table needs a row only where the
    /// odds change and a run further along than the last row keeps the last row's odds.
    /// </remarks>
    public StageOdds OddsAt(int cleared) =>
        catalog.StageOdds
            .Where(row => row.Cleared <= cleared)
            .OrderByDescending(row => row.Cleared)
            .FirstOrDefault() ?? new StageOdds(0, 0, 0);

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
    /// <param name="cleared">
    /// Stages the run had cleared <b>at the time of the roll</b>, which is what decides how far up
    /// the family the roll reaches.
    /// </param>
    /// <remarks>
    /// It has to be passed in, and the event has to carry it, or the roll would stop being
    /// reproducible the moment the run cleared another trial: the same seed and the same number
    /// would give a different Pokémon, and an audit would call an honest roll a lie. It is the one
    /// input to a pull that is not derivable from the seed.
    /// </remarks>
    public GachaPull? Preview(GachaBanner banner, ulong runSeed, int number, int cleared = 0)
    {
        var source = new SeededRandomSource(runSeed).Derive(GachaSalt).Derive($"roll-{number}");
        return Generate(banner, source, number, cleared);
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
    /// <param name="cleared">
    /// Stages the run has cleared. Decides how far up an evolution family the roll can reach, and
    /// travels in the event so the roll can still be recomputed years later. Required and not
    /// defaulted to zero on purpose: a caller that forgot it would hand out first stages for ever
    /// and nothing would look wrong.
    /// </param>
    public async Task<GachaRollResult> RollAsync(Run run, string bannerId, int cleared,
        bool free = false, CancellationToken ct = default)
    {
        var balance = await points.GetBalanceAsync(run.Id, ct).ConfigureAwait(false);

        if (Find(bannerId) is not { } banner)
        {
            return new GachaRollResult(false, balance, Error: $"No existe el banner «{bannerId}».");
        }

        if (banner.TotalWeight <= 0)
        {
            return new GachaRollResult(false, balance,
                Error: $"«{banner.Name}» no está disponible.");
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

        if (Preview(banner, run.Seed, number, cleared) is not { } pull)
        {
            return new GachaRollResult(false, balance,
                Error: $"«{banner.Name}» no está disponible.");
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
            SpeciesName = pull.DisplayName,
            Level = pull.Level,
            IsShiny = pull.IsShiny,
            Origin = PokemonOrigin.Gacha,
            EncounterType = EncounterType.Special,
            ObtainedAt = clock.Now,

            // Una tirada no gasta el encuentro de ninguna zona: no viene de ninguna.
            ConsumedZoneEncounter = false,
            Form = pull.Form
        };

        await pokemon.SaveAsync(entry, ct).ConfigureAwait(false);
        await RecordAsync(run, banner, pull, entry, free, cleared, ct).ConfigureAwait(false);

        return new GachaRollResult(true, spent.NewBalance, pull, entry);
    }

    private Task RecordAsync(Run run, GachaBanner banner, GachaPull pull, PokemonEntry entry,
        bool free, int cleared, CancellationToken ct) =>
        events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.GachaRoll,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Gacha «{banner.Name}»: {pull.DisplayName} Nv.{pull.Level}"
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

                // Las etapas superadas EN ESE MOMENTO. Sin esto la tirada dejaria de poder
                // recomputarse en cuanto la run superara otra prueba: misma seed, mismo numero y
                // otro Pokemon. Es el unico dato de una tirada que no sale de la seed.
                ["etapas"] = cleared.ToString(),
                ["especie"] = pull.Species.ToString(),
                ["forma"] = pull.Form.ToString(),
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
    private GachaPull? Generate(GachaBanner banner, IRandomSource source, int number, int cleared)
    {
        if (PickTier(banner, source) is not { } tier)
        {
            return null;
        }

        var wantLegendary = source.Chance(tier.LegendaryChance);
        var lines = LinesOf(tier, wantLegendary);

        // Si no hay legendarios se cae a los normales, pero NUNCA al revés: un tier sin candidatos
        // normales es un fallo de configuración, y entregar un legendario para taparlo repartiría
        // legendarios por tiers donde el jugador pidió que no los hubiera.
        if (lines.Count == 0 && wantLegendary)
        {
            wantLegendary = false;
            lines = LinesOf(tier, false);
        }

        if (lines.Count == 0)
        {
            return null;
        }

        // Primero la FAMILIA, después la ETAPA. El tier dice en qué acaba la línea; por dónde vas
        // decide cuánto de esa línea te dan. Los dos dados se tiran siempre, aunque la familia sea
        // de una sola etapa, para que la corriente aleatoria no dependa de con qué línea tocó: si
        // no, cambiar la tabla de familias movería el resultado de todas las tiradas siguientes.
        var line = lines[source.Next(lines.Count)];
        var odds = OddsAt(cleared);
        var roll = source.Next(100);

        var stage = roll < odds.First ? 0 : roll < odds.First + odds.Second ? 1 : 2;

        var rung = line.StageAt(stage);
        var chosen = ById[rung[source.Next(rung.Count)]];

        var level = tier.MaxLevel > tier.MinLevel
            ? source.Next(tier.MinLevel, tier.MaxLevel + 1)
            : tier.MinLevel;

        // Habilidad al azar de entre TODAS las del juego, las del mod de gen 8-9 incluidas desde el
        // §136: antes se tiraba todo lo que pasara de la 233 creyendo que el campo era un byte, y
        // el mod guarda un noveno bit (§134). Qué se puede repartir lo decide AbilityDraw.
        var abilities = speciesStats.Abilities;
        var abilityId = AbilityDraw.Roll(source, abilities, speciesStats.BannedAbilities);

        var ability = abilityId > 0 && abilityId < abilities.Count ? abilities[abilityId] : string.Empty;

        var nature = source.Next(25);
        var natures = speciesStats.Natures;

        // La forma regional, de una fuente derivada: el resto de la tirada sale igual que antes (§139).
        var (form, formName) = FormDraw.Roll(source, chosen);

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
            number,
            form,
            formName);
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
