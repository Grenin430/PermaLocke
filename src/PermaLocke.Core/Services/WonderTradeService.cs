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

    public async Task<WonderTradeResult> TradeAsync(Run run, WonderTradeGift gift,
        CancellationToken ct = default)
    {
        if (BaseStatTotalOf(gift.Species) == 0)
        {
            return new WonderTradeResult(false,
                Error: $"No sé cuánto vale {gift.Name} en estadísticas base. ¿Está generado Data/species.json?");
        }

        var number = await CountTradesAsync(run.Id, ct).ConfigureAwait(false);

        if (Preview(gift, run.Seed, number) is not { } offer)
        {
            var (min, max) = catalog.Window.Band(BaseStatTotalOf(gift.Species));
            return new WonderTradeResult(false,
                Error: $"Ninguna especie cae entre {min} y {max} de total base, así que no hay nada "
                       + $"que ofrecer por {gift.Name}.");
        }

        var entry = new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Species = offer.Species,
            SpeciesName = offer.Name,
            Level = offer.Level,
            IsShiny = offer.IsShiny,
            Origin = PokemonOrigin.WonderTrade,
            EncounterType = EncounterType.Trade,
            ObtainedAt = clock.Now,

            // Un intercambio no gasta el encuentro de ninguna zona: no viene de ninguna.
            ConsumedZoneEncounter = false
        };

        await pokemon.SaveAsync(entry, ct).ConfigureAwait(false);
        await RecordAsync(run, offer, entry, ct).ConfigureAwait(false);

        return new WonderTradeResult(true, offer, entry);
    }

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

    private Task RecordAsync(Run run, WonderTradeOffer offer, PokemonEntry entry, CancellationToken ct) =>
        events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.WonderTrade,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Wonder trade: {offer.GivenName} ({offer.GivenBaseStatTotal}) "
                          + $"por {offer.Name} ({offer.BaseStatTotal})"
                          + (offer.Legendary ? " legendario" : string.Empty)
                          + (offer.IsShiny ? " shiny" : string.Empty),
            PokemonId = entry.Id,
            Seed = offer.Seed,
            Data = new Dictionary<string, string>
            {
                ["intercambio"] = offer.Number.ToString(),
                ["entregado"] = offer.GivenSpecies.ToString(),
                ["entregadoNombre"] = offer.GivenName,
                ["entregadoTotal"] = offer.GivenBaseStatTotal.ToString(),
                ["recibido"] = offer.Species.ToString(),
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

        // La habilidad sale de las que la especie declara, no de todas las del juego: esto es un
        // intercambio, no un gacha, y lo que llega tiene que poder existir.
        var abilities = chosen.Abilities.Count > 0 ? chosen.Abilities : [];
        var abilityIndex = abilities.Count > 0 ? source.Next(abilities.Count) : -1;
        var abilityName = abilityIndex >= 0 ? abilities[abilityIndex] : string.Empty;
        var abilityId = speciesStats.Abilities.ToList().IndexOf(abilityName);

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

        return new WonderTradeOffer(
            gift.Species,
            gift.Name,
            givenTotal,
            chosen.Id,
            chosen.Name,
            chosen.BaseStatTotal,
            Generations.Of(chosen.Id),
            types.GetTypes(chosen.Id),
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
            number);
    }
}
