using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// The NURSERY of the MONOTYPE roles (§221): spins they are owed, and the eggs those spins turn into.
/// </summary>
/// <remarks>
/// <para>
/// A MONOTYPE run can only keep Pokémon of its type, and the wild ones are of any: the nursery is how it gets its own. Each
/// spin is one <b>egg</b> of a species of the type (never a legendary), at level 1, written into a box of the save, and it
/// hatches in the game as any other egg does. What grows with the run is the strength of the species: the target base stat
/// total starts at the weakest of the type and rises a twelfth of the way to the strongest for every trial cleared (the
/// roulette's schedule gives the spins; the numbers are in <c>Data/guarderia.json</c>).
/// </para>
/// <para>
/// Reproducible from the run seed and the egg number, like a gacha roll, and spent one egg at a time: an egg counts as spent
/// only once it is really in the save, so a delivery that fails (game open, boxes full) leaves the spin owed.
/// </para>
/// </remarks>
public sealed class NurseryService(
    INurseryCatalog catalog,
    ISpeciesStatsCatalog speciesStats,
    AchievementService achievements,
    MonotypeRule monotype,
    IEventStore events,
    IPokemonRepository pokemon,
    IClock clock)
{
    /// <summary>Salt of this random stream, so no other module shifts it.</summary>
    private const string EggSalt = "guarderia";

    /// <summary>The id the egg carries as banner and as tier, so a screen can tell it from a gacha roll.</summary>
    public const string Banner = "guarderia";

    /// <summary>What an unhatched egg is called everywhere.</summary>
    public const string EggName = "Huevo";

    public INurseryCatalog Catalog => catalog;

    /// <summary>True when the run's role has a nursery: the MONOTYPE ones.</summary>
    public bool PlaysWithTheNursery(Run run) => monotype.TypeOf(run) is not null;

    /// <summary>The run's role, for a screen that names it.</summary>
    public Role? RoleOf(Run run) => monotype.RoleOf(run);

    /// <summary>The total the next egg of this run aims for, or 0 when it has no nursery.</summary>
    public int TargetFor(Run run, int progress) => monotype.TypeOf(run) is { } type ? TargetFor(type, progress) : 0;

    private async Task<HashSet<string>> UnlockedAsync(Run run, CancellationToken ct)
    {
        var progress = await achievements.GetProgressAsync(run.Id, ct).ConfigureAwait(false);

        return progress
            .Where(p => p.Unlocked)
            .Select(p => p.Achievement.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Trials cleared: what makes the eggs stronger.</summary>
    public async Task<int> ProgressAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        return catalog.TrialAchievements.Count((await UnlockedAsync(run, ct).ConfigureAwait(false)).Contains);
    }

    /// <summary>Spins the milestones have paid for, whether or not they have been used.</summary>
    public async Task<int> EarnedAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var unlocked = await UnlockedAsync(run, ct).ConfigureAwait(false);

        return (catalog.TrialAchievements.Count(unlocked.Contains) * catalog.SpinsPerTrial)
               + (unlocked.Contains(catalog.LeagueAchievement) ? catalog.SpinsForLeague : 0)
               + (unlocked.Contains(catalog.RematchAchievement) ? catalog.SpinsForRematch : 0)
               + await GrantedAsync(run.Id, ct).ConfigureAwait(false);
    }

    /// <summary>Every milestone that pays eggs, in the order the competition lists them, with how many and whether it has been reached.</summary>
    public async Task<IReadOnlyList<NurseryMilestone>> MilestonesAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var progress = (await achievements.GetProgressAsync(run.Id, ct).ConfigureAwait(false))
            .ToDictionary(p => p.Achievement.Id, StringComparer.OrdinalIgnoreCase);

        NurseryMilestone Of(string id, int eggs) => progress.TryGetValue(id, out var found)
            ? new NurseryMilestone(found.Achievement.Name, eggs, found.Unlocked)
            : new NurseryMilestone(id, eggs, false);

        return
        [
            .. catalog.TrialAchievements.Select(id => Of(id, catalog.SpinsPerTrial)),
            Of(catalog.LeagueAchievement, catalog.SpinsForLeague),
            Of(catalog.RematchAchievement, catalog.SpinsForRematch)
        ];
    }

    /// <summary>Spins a testing tool handed over (§227), straight from the history.</summary>
    private async Task<int> GrantedAsync(Guid runId, CancellationToken ct) =>
        (await events.GetAllAsync(runId, ct).ConfigureAwait(false))
        .Where(e => e.Type == GameEventType.NurseryGrant)
        .Sum(e => e.Data is not null && e.Data.TryGetValue("tiradas", out var n) && int.TryParse(n, out var count) ? count : 0);

    /// <summary>Hands over spins for testing: an event, like everything that changes the run, and they are owed from then on.</summary>
    public async Task GrantAsync(Run run, int count, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.NurseryGrant,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Herramienta de pruebas: {count} tiradas de huevo de la guardería.",
            Data = new Dictionary<string, string> { ["tiradas"] = count.ToString() }
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Eggs already written into the save, straight from the history.</summary>
    public async Task<int> SpentAsync(Guid runId, CancellationToken ct = default) =>
        (await events.GetAllAsync(runId, ct).ConfigureAwait(false)).Count(e => e.Type == GameEventType.NurseryEgg);

    /// <summary>Spins still owed. Never negative.</summary>
    public async Task<int> OwedAsync(Run run, CancellationToken ct = default) =>
        Math.Max(0, await EarnedAsync(run, ct).ConfigureAwait(false) - await SpentAsync(run.Id, ct).ConfigureAwait(false));

    /// <summary>
    /// The species an egg can be in this run: those with the type, no legendaries, and a known total.
    /// </summary>
    public IReadOnlyList<SpeciesStats> SpeciesFor(int type)
    {
        var admitted = monotype.SpeciesOf(type);

        return [.. speciesStats.All.Where(s => s.BaseStatTotal > 0 && !s.Legendary && admitted.Contains(s.Id))];
    }

    /// <summary>The weakest and the strongest total among the species an egg can be, for the strength bar.</summary>
    public (int Low, int High) RangeFor(int type)
    {
        var pool = SpeciesFor(type);

        return pool.Count == 0 ? (0, 0) : (pool.Min(s => s.BaseStatTotal), Math.Min(catalog.TopBaseStatTotal, pool.Max(s => s.BaseStatTotal)));
    }

    /// <summary>
    /// The total an egg aims for after <paramref name="progress"/> trials: from the weakest species of the type, a
    /// <see cref="INurseryCatalog.Divisions"/>th of the way to the strongest per trial, never past the catalogue's top.
    /// </summary>
    public int TargetFor(int type, int progress)
    {
        var pool = SpeciesFor(type);

        if (pool.Count == 0)
        {
            return 0;
        }

        var low = pool.Min(s => s.BaseStatTotal);
        var high = pool.Max(s => s.BaseStatTotal);
        var step = (high - low) / catalog.Divisions;

        return Math.Min(catalog.TopBaseStatTotal, low + (step * Math.Max(0, progress)));
    }

    /// <summary>
    /// The egg of this number, recomputed from the run seed without touching anything.
    /// </summary>
    /// <param name="progress">Trials cleared when it was drawn: the one input that is not derivable from the seed.</param>
    public GachaPull? Preview(ulong runSeed, int number, int progress, int type)
    {
        var pool = SpeciesFor(type);

        if (pool.Count == 0)
        {
            return null;
        }

        var source = new SeededRandomSource(runSeed).Derive(EggSalt).Derive($"egg-{number}");
        var target = TargetFor(type, progress);
        var chosen = Pick(pool, target, source);

        var abilities = speciesStats.Abilities;
        var abilityId = AbilityDraw.Roll(source, abilities, speciesStats.BannedAbilities);
        var ability = abilityId > 0 && abilityId < abilities.Count ? abilities[abilityId] : string.Empty;

        var nature = source.Next(25);
        var natures = speciesStats.Natures;

        var ivs = new int[6];
        for (var stat = 0; stat < ivs.Length; stat++)
        {
            ivs[stat] = source.Next(32);
        }

        var (form, formName) = FormDraw.Roll(source, chosen);
        (form, formName) = monotype.FormFor(type, chosen, form, formName);

        return new GachaPull(Banner, Banner, chosen.Id, chosen.Name, false, chosen.BaseStatTotal, 1, false, ivs, nature,
            nature < natures.Count ? natures[nature] : string.Empty, abilityId, ability, source.Seed, number, form, formName);
    }

    /// <summary>
    /// A species among the ones whose total falls inside a band around the target, opened one percent at a time until
    /// <see cref="INurseryCatalog.MinCandidates"/> do (or the band is as wide as the target). Nothing inside: any of the type.
    /// </summary>
    private SpeciesStats Pick(IReadOnlyList<SpeciesStats> pool, int target, IRandomSource source)
    {
        for (var percent = 1; percent <= 100; percent++)
        {
            var margin = target * percent / 100.0;
            var inside = pool.Where(s => s.BaseStatTotal > target - margin && s.BaseStatTotal < target + margin).ToList();

            if (inside.Count >= catalog.MinCandidates)
            {
                return inside[source.Next(inside.Count)];
            }
        }

        return pool[source.Next(pool.Count)];
    }

    /// <summary>
    /// The next <paramref name="count"/> eggs this run would get, numbered after the ones already written. Pure: nothing is
    /// recorded until each one is in the save (<see cref="RecordAsync"/>).
    /// </summary>
    public async Task<IReadOnlyList<GachaPull>> PrepareAsync(Run run, int count, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (monotype.TypeOf(run) is not { } type || count <= 0)
        {
            return [];
        }

        var first = await SpentAsync(run.Id, ct).ConfigureAwait(false);
        var progress = await ProgressAsync(run, ct).ConfigureAwait(false);
        var eggs = new List<GachaPull>();

        for (var i = 0; i < count; i++)
        {
            if (Preview(run.Seed, first + i, progress, type) is { } egg)
            {
                eggs.Add(egg);
            }
        }

        return eggs;
    }

    /// <summary>
    /// Writes an egg down, once it is really in the save: the Pokémon of the run, with the PID the game gave it, and the
    /// event that spends the spin.
    /// </summary>
    public async Task<PokemonEntry> RecordAsync(Run run, GachaPull egg, DeliveryResult delivery, int progress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(egg);
        ArgumentNullException.ThrowIfNull(delivery);

        if (!delivery.Delivered || delivery.Pid == 0)
        {
            throw new InvalidOperationException("Un huevo solo se apunta cuando está en la partida y se sabe su PID.");
        }

        var entry = new PokemonEntry
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            // Un huevo no dice qué hay dentro (§230): sin especie hasta que eclosiona (HatchedAsync).
            Species = 0,
            SpeciesName = EggName,
            Level = egg.Level,
            Origin = PokemonOrigin.Nursery,
            EncounterType = EncounterType.Special,
            ObtainedAt = clock.Now,

            // Un huevo de la guardería no sale de ninguna zona.
            ConsumedZoneEncounter = false,
            Pid = delivery.Pid
        };

        await pokemon.SaveAsync(entry, ct).ConfigureAwait(false);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.NurseryEgg,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            // Sin la especie: lo que hay dentro de un huevo es una sorpresa (§228). Sale de la semilla y el número, si alguien tiene que comprobarlo.
            Description = $"Guardería: huevo n.º {egg.Number + 1} "
                          + (delivery.Box == 0 ? "en el equipo." : $"en la caja {delivery.Box}, hueco {delivery.Slot}."),
            PokemonId = entry.Id,
            Seed = egg.Seed,
            Data = new Dictionary<string, string>
            {
                ["huevo"] = egg.Number.ToString(),
                ["pruebas"] = progress.ToString(),
                ["monotipo"] = monotype.TypeOf(run)?.ToString() ?? string.Empty,
                ["pid"] = delivery.Pid.ToString("X8"),
                ["caja"] = delivery.Box.ToString(),
                ["hueco"] = delivery.Slot.ToString()
            }
        }, ct).ConfigureAwait(false);

        return entry;
    }

    /// <summary>True for an egg of the nursery that has not hatched: it has no species yet.</summary>
    public static bool IsUnhatched(PokemonEntry entry) => entry is { Origin: PokemonOrigin.Nursery, Species: 0 };

    /// <summary>
    /// The egg of this PID has hatched: the entry takes the species it turned out to be, with an event. Null when the
    /// PID is not an unhatched egg of the run.
    /// </summary>
    public async Task<PokemonEntry?> HatchedAsync(Run run, uint pid, int species, string speciesName, int form, int level,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var entry = (await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false))
            .FirstOrDefault(p => p.Pid == pid && IsUnhatched(p));
        if (entry is null) return null;

        var hatched = entry with { Species = species, SpeciesName = speciesName, Form = form, Level = level };
        await pokemon.SaveAsync(hatched, ct).ConfigureAwait(false);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.NurseryHatch,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"Guardería: el huevo ha eclosionado y es {speciesName}.",
            PokemonId = hatched.Id,
            Data = new Dictionary<string, string> { ["pid"] = pid.ToString("X8"), ["especie"] = species.ToString() }
        }, ct).ConfigureAwait(false);

        return hatched;
    }
}

/// <summary>One milestone of the competition and the eggs it pays: «Prueba 3», 1, reached.</summary>
public sealed record NurseryMilestone(string Name, int Eggs, bool Reached);
