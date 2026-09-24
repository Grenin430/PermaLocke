using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Trades a Pokémon away for another of comparable strength.
/// </summary>
/// <remarks>
/// <para>
/// The band is the whole mechanic: what comes back has a base stat total within a few points of
/// what went in, so a wonder trade is a gamble on <em>which</em> Pokémon rather than a way of
/// upgrading one. The percentages live in <c>Data/wondertrade.json</c>.
/// </para>
/// <para>
/// The received Pokémon keeps the level of the one handed over. Otherwise the trade would be a
/// laundry: hand over a level 1 from the gacha, get a level 50 back.
/// </para>
/// <para>
/// Every trade is reproducible from the run seed and the trade number, exactly like a gacha roll,
/// so any of them can be recomputed and checked later.
/// </para>
/// </remarks>
public sealed class WonderTradeService(
    IWonderTradeCatalog catalog,
    ISpeciesStatsCatalog speciesStats,
    ITypeLookup types,
    IEventStore events,
    IPokemonRepository pokemon,
    IClock clock)
{
    /// <summary>Salt for this random stream, so other modules do not shift it.</summary>
    private const string TradeSalt = "wondertrade";

    public WonderTradeWindow Window => catalog.Window;

    /// <summary>What a Pokémon with this base stat total could come back as.</summary>
    public IReadOnlyList<SpeciesStats> PoolFor(int baseStatTotal)
    {
        var (min, max) = catalog.Window.Band(baseStatTotal);

        return [.. speciesStats.All.Where(s =>
            s.BaseStatTotal >= min
            && s.BaseStatTotal <= max
            && (catalog.Window.AllowLegendaries || !s.Legendary))];
    }

    /// <summary>Base stat total of a species, or zero when the catalogue does not know it.</summary>
    public int BaseStatTotalOf(int species) =>
        speciesStats.All.FirstOrDefault(s => s.Id == species)?.BaseStatTotal ?? 0;

    /// <summary>How many wonder trades this run has already made. Also the next trade number.</summary>
    public async Task<int> CountTradesAsync(Guid runId, CancellationToken ct = default)
    {
        var all = await events.GetAllAsync(runId, ct).ConfigureAwait(false);
        return all.Count(e => e.Type == GameEventType.WonderTrade);
    }

    /// <summary>
    /// Recomputes a trade from the run seed without touching anything, which is what makes the
    /// result auditable rather than merely recorded.
    /// </summary>
    public WonderTradeOffer? Preview(WonderTradeGift gift, ulong runSeed, int number)
    {
        var source = new SeededRandomSource(runSeed).Derive(TradeSalt).Derive($"trade-{number}");
        return Generate(gift, source, number);
    }

    /// <param name="free">
    /// True when a credit is paying for it, which is what marks the event as spent.
    /// </param>
    /// <remarks>
    /// Whether a credit is <em>needed</em> is not decided here: this only records how it was paid.
    /// The competition rule -- that a wonder trade needs one at all -- lives with the credits,
    /// because it is a rule and not a fact about trading.
    /// </remarks>
    public async Task<WonderTradeResult> TradeAsync(Run run, WonderTradeGift gift, bool free = false,
        CancellationToken ct = default)
    {
        if (await IsFallenAsync(run.Id, gift.Pid, ct).ConfigureAwait(false))
        {
            return new WonderTradeResult(false, Error: FallenMessage(gift.Name));
        }

        if (BaseStatTotalOf(gift.Species) == 0)
        {
            return new WonderTradeResult(false,
                Error: $"{gift.Name} no se puede intercambiar.");
        }

        var number = await CountTradesAsync(run.Id, ct).ConfigureAwait(false);

        if (Preview(gift, run.Seed, number) is not { } offer)
        {
            var (min, max) = catalog.Window.Band(BaseStatTotalOf(gift.Species));
            return new WonderTradeResult(false,
                Error: $"No hay nada que ofrecer por {gift.Name}.");
        }

        var entry = new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Species = offer.Species,
            SpeciesName = offer.DisplayName,
            Level = offer.Level,
            IsShiny = offer.IsShiny,
            Origin = PokemonOrigin.WonderTrade,
            EncounterType = EncounterType.Trade,
            ObtainedAt = clock.Now,

            // Un intercambio no gasta el encuentro de ninguna zona: no viene de ninguna.
            ConsumedZoneEncounter = false,
            Form = offer.Form
        };

        await pokemon.SaveAsync(entry, ct).ConfigureAwait(false);
        await RecordAsync(run, offer, entry, free, ct).ConfigureAwait(false);

        return new WonderTradeResult(true, offer, entry);
    }

    /// <summary>
    /// Whether the run has this Pokémon down as dead. A wonder trade only takes the living.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A competition rule the player added on 2026-09-14: a fallen Pokémon cannot be traded away. Otherwise a death
    /// would stop being a loss — hand the corpse over and something alive, of the same strength, comes back.
    /// </para>
    /// <para>
    /// The run is the authority, by PID, which is how every death is recorded (§56): the save cannot say it, because a
    /// Pokémon in a box carries no HP at all. Here and not only on the screen, so no other caller can trade one.
    /// A PID of zero identifies nobody and blocks nothing; since §56 every Pokémon of the save has a real one.
    /// </para>
    /// </remarks>
    public async Task<bool> IsFallenAsync(Guid runId, uint pid, CancellationToken ct = default)
    {
        if (pid == 0)
        {
            return false;
        }

        var all = await pokemon.GetAllAsync(runId, ct).ConfigureAwait(false);
        return all.Any(p => p.Pid == pid && p.Status == PokemonStatus.Dead);
    }

    /// <summary>What the player is told when they pick a fallen Pokémon.</summary>
    public static string FallenMessage(string name) =>
        $"{name} está muerto.";

    /// <summary>
    /// Marks the Pokémon that was handed over as gone, once the trade has really been written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A trade adds one Pokémon and removes another, and only the arrival used to be recorded: the
    /// one that left stayed <see cref="PokemonStatus.Alive"/> for ever. In the run that had already
    /// done it twenty-nine times, HOME was counting twenty-eight Pokémon that are not in the game.
    /// That is the same class of error as a death that goes unrecorded — a number describing
    /// something other than what it says.
    /// </para>
    /// <para>
    /// It runs <b>after</b> the save is written and not with the rest of the trade, because until
    /// then nothing has left anywhere. And it matches by PID, which only became possible once
    /// deliveries started recording one; without it there is no way to tell which of the player's
    /// three Vanilluxe went.
    /// </para>
    /// </remarks>
    public async Task<PokemonEntry?> MarkGivenAsTradedAsync(Run run, uint pid, string receivedName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (pid == 0)
        {
            // Cero no identifica a nadie. Antes que marcar al Pokémon equivocado, no se marca.
            return null;
        }

        var all = await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false);
        var given = all.FirstOrDefault(p => p.Pid == pid && p.Status == PokemonStatus.Alive);

        if (given is null)
        {
            return null;
        }

        var updated = given with { Status = PokemonStatus.Traded };
        await pokemon.SaveAsync(updated, ct).ConfigureAwait(false);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.PokemonTraded,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"{given.Nickname ?? given.SpeciesName} se ha ido en el wonder trade "
                          + $"a cambio de {receivedName}.",
            PokemonId = given.Id,
            Data = new Dictionary<string, string>
            {
                ["especie"] = given.Species.ToString(),
                ["pid"] = pid.ToString("X8"),
                ["recibido"] = receivedName
            }
        }, ct).ConfigureAwait(false);

        return updated;
    }

    private Task RecordAsync(Run run, WonderTradeOffer offer, PokemonEntry entry, bool free,
        CancellationToken ct) =>
        events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.WonderTrade,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Wonder trade: {offer.GivenName} ({offer.GivenBaseStatTotal}) "
                          + $"por {offer.DisplayName} ({offer.BaseStatTotal})"
                          + (offer.Legendary ? " legendario" : string.Empty)
                          + (offer.IsShiny ? " shiny" : string.Empty),
            PokemonId = entry.Id,
            Seed = offer.Seed,
            Data = new Dictionary<string, string>
            {
                ["intercambio"] = offer.Number.ToString(),
                ["gratis"] = free.ToString(),
                ["entregado"] = offer.GivenSpecies.ToString(),
                ["entregadoNombre"] = offer.GivenName,
                ["entregadoTotal"] = offer.GivenBaseStatTotal.ToString(),
                ["recibido"] = offer.Species.ToString(),
                ["forma"] = offer.Form.ToString(),
                ["recibidoTotal"] = offer.BaseStatTotal.ToString(),
                ["banda"] = $"{offer.MinBaseStatTotal}-{offer.MaxBaseStatTotal}",
                ["generacion"] = offer.Generation.ToString(),
                ["tipos"] = offer.Types.Label,
                ["legendario"] = offer.Legendary.ToString(),
                ["nivel"] = offer.Level.ToString(),
                ["shiny"] = offer.IsShiny.ToString(),
                ["ivs"] = string.Join('/', offer.Ivs),
                ["naturaleza"] = offer.NatureName,
                ["habilidad"] = offer.Ability
            }
        }, ct);

    /// <summary>
    /// The trade itself. Pure: same source, same result, on any machine and any build.
    /// </summary>
    private WonderTradeOffer? Generate(WonderTradeGift gift, IRandomSource source, int number)
    {
        var givenTotal = BaseStatTotalOf(gift.Species);
        var (min, max) = catalog.Window.Band(givenTotal);
        var pool = PoolFor(givenTotal);

        if (pool.Count == 0)
        {
            return null;
        }

        var chosen = pool[source.Next(pool.Count)];

        // La habilidad se sortea entre TODAS las del juego, igual que en el gacha.
        //
        // Antes salía de las que la especie declara, con el argumento de que un intercambio no es
        // un gacha y lo que llega tiene que poder existir. El jugador lo tumbó jugando, y tenía
        // razón: en un randomlocke con `randomizeAbilities` apagado en la ROM -- y está apagado a
        // propósito, porque encenderlo desincroniza los combates por link (§80) -- las habilidades
        // son las del cartucho en todas partes, así que un Victini llegaba con Tinovictoria y un
        // Togekiss con Afortunado. Cada intercambio devolvía exactamente lo que ese Pokémon es.
        // Ser legal no era una virtud aquí, era la ausencia de lo que se venía a buscar.
        //
        // Que el juego respeta la habilidad escrita y no la recalcula por el número de ranura está
        // medido sin querer: llegó un Heracross con Autoestima, que es su habilidad OCULTA, y el
        // constructor escribe siempre ranura 1. Si el juego recalculara, habría salido Enjambre.
        //
        // Las del mod de gen 8-9 entran desde el §136, con las mismas excepciones que el gacha:
        // qué se puede repartir lo decide AbilityDraw, compartido para que no discrepen.
        var abilities = speciesStats.Abilities;
        var abilityId = AbilityDraw.Roll(source, abilities, speciesStats.BannedAbilities);

        var abilityName = abilityId > 0 && abilityId < abilities.Count
            ? abilities[abilityId]
            : string.Empty;

        var ivs = new int[6];
        for (var stat = 0; stat < ivs.Length; stat++)
        {
            ivs[stat] = source.Next(32);
        }

        var nature = source.Next(25);
        var natures = speciesStats.Natures;

        // Misma probabilidad que la carta más cara del gacha. Un shiny por intercambio es raro,
        // y tiene que seguir siéndolo.
        var shiny = source.Chance(0.01);

        // La forma regional, de una fuente derivada: el resto del intercambio sale igual que antes, y
        // los tipos que se anuncian son los de esa forma (§139).
        var (form, formName) = FormDraw.Roll(source, chosen);

        return new WonderTradeOffer(
            gift.Species,
            gift.Name,
            givenTotal,
            chosen.Id,
            chosen.Name,
            chosen.BaseStatTotal,
            Generations.Of(chosen.Id),
            types.GetTypes(chosen.Id, form),
            chosen.Legendary,
            gift.Level,
            shiny,
            ivs,
            nature,
            nature < natures.Count ? natures[nature] : string.Empty,
            abilityId > 0 ? abilityId : 0,
            abilityName,
            min,
            max,
            source.Seed,
            number,
            form,
            formName);
    }
}
