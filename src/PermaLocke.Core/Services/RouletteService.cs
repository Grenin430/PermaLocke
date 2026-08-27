using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// The wheel of the LUDÓPATA role: what it owes, what it shows, and what it does.
/// </summary>
/// <remarks>
/// <para>
/// Spins are <b>owed</b>, not offered: one per trial cleared, three for the league and two more for
/// the rematch. Both halves of that sum come from something real — the achievements, which work
/// themselves out from the cartridge, and the history, which knows what has already been spun — so
/// there is nothing to mark by hand and nothing to trust.
/// </para>
/// <para>
/// Every spin is computed from the run seed and the spin number, exactly like a gacha roll, so any
/// of them can be recomputed and checked afterwards. That also makes a failed spin safe to retry:
/// the same number gives the same wheel, so nobody can re-roll a death away.
/// </para>
/// <para>
/// The order is deliver-then-record, as everywhere else here. What is different is that a spin
/// touches several things at once — Pokémon, bag, points — so the whole action is decided first,
/// written in one go by <see cref="IRouletteWorldPort"/>, and recorded with the list of what
/// actually landed. If nothing landed, the spin is not consumed.
/// </para>
/// </remarks>
public sealed class RouletteService(
    IRouletteCatalog catalog,
    AchievementService achievements,
    IRouletteWorldPort world,
    IRunRoles roles,
    GachaService gacha,
    IPokemonDelivery delivery,
    PokemonIdentityService identities,
    IPokemonRepository pokemon,
    IEventStore events,
    IClock clock)
{
    /// <summary>Salt for this random stream, so other modules do not shift it.</summary>
    private const string RouletteSalt = "ruleta";

    /// <summary>How many faces the wheel shows at once.</summary>
    public const int FacesOnTheWheel = 6;

    public IReadOnlyList<RouletteFace> Faces => catalog.Faces;

    /// <summary>True when this run plays with the wheel at all.</summary>
    public bool PlaysWithTheWheel(Run run) => roles.Of(run.Id)?.Roulette ?? false;

    /// <summary>Spins the milestones have paid for, whether or not they have been used.</summary>
    public async Task<int> EarnedAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var progress = await achievements.GetProgressAsync(run.Id, ct).ConfigureAwait(false);

        var unlocked = progress
            .Where(p => p.Unlocked)
            .Select(p => p.Achievement.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var trials = catalog.TrialAchievements.Count(unlocked.Contains) * catalog.SpinsPerTrial;
        var league = unlocked.Contains(catalog.LeagueAchievement) ? catalog.SpinsForLeague : 0;
        var rematch = unlocked.Contains(catalog.RematchAchievement) ? catalog.SpinsForRematch : 0;

        return trials + league + rematch;
    }

    /// <summary>How many spins the history says have already been taken.</summary>
    public async Task<int> SpunAsync(Guid runId, CancellationToken ct = default)
    {
        var history = await events.GetAllAsync(runId, ct).ConfigureAwait(false);
        return history.Count(e => e.Type == GameEventType.RouletteSpun);
    }

    /// <summary>Spins still owed. Never negative: a wheel cannot be un-spun.</summary>
    public async Task<int> OwedAsync(Run run, CancellationToken ct = default) =>
        Math.Max(0, await EarnedAsync(run, ct).ConfigureAwait(false)
                    - await SpunAsync(run.Id, ct).ConfigureAwait(false));

    /// <summary>
    /// The wheel a given spin number produces, without spinning it.
    /// </summary>
    /// <remarks>
    /// Pure, so it can be recomputed from the run seed alone. The six faces are drawn from all
    /// sixteen <b>without repeating</b> — a wheel with the same face twice would be a wheel that
    /// lies about the odds — and nothing keeps the good and the bad balanced.
    /// </remarks>
    public RouletteWheel? Preview(ulong runSeed, int number)
    {
        if (catalog.Faces.Count == 0)
        {
            return null;
        }

        var source = new SeededRandomSource(runSeed).Derive(RouletteSalt).Derive($"tirada-{number}");
        var faces = Draw(source, catalog.Faces, Math.Min(FacesOnTheWheel, catalog.Faces.Count));

        return new RouletteWheel(faces, source.Next(faces.Count), number, source.Seed);
    }

    /// <summary>
    /// Turns the wheel once: decides, writes and records.
    /// </summary>
    public async Task<RouletteSpinResult> SpinAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!PlaysWithTheWheel(run))
        {
            return new RouletteSpinResult(RouletteOutcome.NotThisRole, null, [], 0,
                "Esta run no juega con la ruleta.");
        }

        var owed = await OwedAsync(run, ct).ConfigureAwait(false);

        if (owed <= 0)
        {
            return new RouletteSpinResult(RouletteOutcome.NothingOwed, null, [], 0,
                "No debes ninguna tirada. La ruleta se gira después de cada prueba, tres veces "
                + "por la liga y dos más por el rematch.");
        }

        if (!world.CanActNow(out var reason))
        {
            return new RouletteSpinResult(RouletteOutcome.GameUnreachable, null, [], owed, reason);
        }

        var number = await SpunAsync(run.Id, ct).ConfigureAwait(false);

        if (Preview(run.Seed, number) is not { } wheel)
        {
            return new RouletteSpinResult(RouletteOutcome.Failed, null, [], owed,
                "La ruleta no tiene ninguna cara configurada. ¿Está Data/roulette.json?");
        }

        var seen = await world.ReadAsync(ct).ConfigureAwait(false);
        var source = new SeededRandomSource(wheel.Seed).Derive("efecto");
        var action = Decide(wheel.Winner, seen, source);

        var lines = new List<string>();

        switch (wheel.Winner.Effect)
        {
            // Los puntos no pasan por el mundo del juego: son de la run. Van en el delta del propio
            // evento de la tirada, que es de donde sale el saldo, así que la ruleta mueve puntos con
            // un solo apunte y el historial dice que fue ella.
            case RouletteEffect.Puntos:
                lines.Add($"{wheel.Winner.Amount:+#;-#;0} puntos.");
                break;

            case RouletteEffect.Gacha:
                lines.AddRange(await RollAsync(run, wheel.Winner, ct).ConfigureAwait(false));
                break;

            default:
            {
                if (action.IsEmpty)
                {
                    lines.Add("No había nada sobre lo que aplicarlo.");
                    break;
                }

                var applied = await world.ApplyAsync(action, ct).ConfigureAwait(false);
                lines.AddRange(applied.Lines);

                if (!applied.Applied && applied.Lines.Count == 0)
                {
                    // Nada ha llegado, así que la tirada no se gasta: la seed manda, y volver a
                    // tirar con el mismo número da exactamente la misma cara.
                    return new RouletteSpinResult(RouletteOutcome.Failed, wheel, [], owed, applied.Message);
                }

                if (applied.Applied && wheel.Winner.Effect == RouletteEffect.Muerte)
                {
                    await BuryAsync(run, action, ct).ConfigureAwait(false);
                }

                break;
            }
        }

        await RecordAsync(run, wheel, action, lines, ct).ConfigureAwait(false);

        return new RouletteSpinResult(RouletteOutcome.Spun, wheel, lines, owed - 1,
            $"«{wheel.Winner.Name}». {wheel.Winner.Detail}");
    }

    /// <summary>
    /// Turns a winning face into the concrete thing it does to <em>this</em> game.
    /// </summary>
    /// <remarks>
    /// Everything random happens here, from the seeded source, so a spin can be recomputed down to
    /// which Pokémon it hit. When there is less than the face asks for — two in the party instead
    /// of three, no TMs to take — it does what it can and no more, which is the rule the whole
    /// wheel is written to (see <c>Data/roulette.json</c>).
    /// </remarks>
    public RouletteAction Decide(RouletteFace face, RouletteWorld seen, IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(face);
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(source);

        switch (face.Effect)
        {
            case RouletteEffect.Puntos:
            case RouletteEffect.Gacha:
                return RouletteAction.Nothing(face.Effect);

            case RouletteEffect.HabilidadBuena:
            case RouletteEffect.HabilidadMala:
            {
                var targets = Draw(source, seen.Party, face.Amount);
                var pool = face.Effect == RouletteEffect.HabilidadBuena
                    ? catalog.GoodAbilities
                    : catalog.BadAbilities;

                var abilities = pool.Count == 0
                    ? []
                    : targets.Select(_ => pool[source.Next(pool.Count)]).ToList();

                return new RouletteAction(face.Effect, abilities.Count == 0 ? [] : targets,
                    abilities, [], 0);
            }

            case RouletteEffect.IvPerfectos:
            case RouletteEffect.IvCero:
            case RouletteEffect.Muerte:
                return new RouletteAction(face.Effect, Draw(source, seen.Party, face.Amount), [], [], 0);

            case RouletteEffect.DarCurativos:
            {
                var kinds = Draw(source, catalog.HealingItems, face.Amount);
                return new RouletteAction(face.Effect, [], [],
                    [.. kinds.Select(id => new RouletteItemChange(id, face.Each))], 0);
            }

            case RouletteEffect.QuitarCurativos:
            {
                // Solo se puede quitar de lo que hay, y nunca más de lo que hay de cada uno.
                var carried = catalog.HealingItems
                    .Where(id => seen.HealingHeld.TryGetValue(id, out var count) && count > 0)
                    .ToList();

                var kinds = Draw(source, carried, face.Amount);

                return new RouletteAction(face.Effect, [], [],
                    [.. kinds.Select(id => new RouletteItemChange(
                        id, -Math.Min(face.Each, seen.HealingHeld[id])))], 0);
            }

            case RouletteEffect.DarMt:
            {
                var missing = seen.TmsAll.Except(seen.TmsHeld).ToList();
                return new RouletteAction(face.Effect, [], [],
                    [.. Draw(source, missing, face.Amount).Select(id => new RouletteItemChange(id, 1))], 0);
            }

            case RouletteEffect.QuitarMt:
                return new RouletteAction(face.Effect, [], [],
                    [.. Draw(source, seen.TmsHeld, face.Amount).Select(id => new RouletteItemChange(id, -1))], 0);

            default:
                return RouletteAction.Nothing(face.Effect);
        }
    }

    /// <summary>
    /// Takes <paramref name="count"/> different items at random, or everything when there is less.
    /// </summary>
    /// <remarks>
    /// A partial Fisher-Yates over a copy: it draws without repeating and does not care how long
    /// the list is, which is what "tres del equipo" has to mean when the equipo has two.
    /// </remarks>
    private static List<T> Draw<T>(IRandomSource source, IReadOnlyList<T> from, int count)
    {
        var pool = from.ToList();
        var take = Math.Clamp(count, 0, pool.Count);
        var drawn = new List<T>(take);

        for (var i = 0; i < take; i++)
        {
            var pick = source.Next(pool.Count);
            drawn.Add(pool[pick]);
            pool.RemoveAt(pick);
        }

        return drawn;
    }

    /// <summary>
    /// The free gacha rolls a face promises, one per banner named.
    /// </summary>
    /// <remarks>
    /// The real gacha, not a copy of it: same seed stream, same roll numbers, same delivery into
    /// the save and the same <c>GachaRoll</c> event, so a roll the wheel gave is auditable exactly
    /// like one the player bought. The only difference is that it is not charged.
    /// </remarks>
    private async Task<List<string>> RollAsync(Run run, RouletteFace face, CancellationToken ct)
    {
        var lines = new List<string>();

        foreach (var bannerId in face.BannerIds)
        {
            var rolled = await gacha.RollAsync(run, bannerId, free: true, ct).ConfigureAwait(false);

            if (!rolled.Success || rolled.Pull is not { } pull)
            {
                lines.Add($"El banner «{bannerId}» no ha podido tirar: {rolled.Error}");
                continue;
            }

            var delivered = await delivery.DeliverAsync(pull, run, ct).ConfigureAwait(false);

            if (delivered.Delivered && rolled.Pokemon is { } entry)
            {
                await identities.RememberDeliveryAsync(run, entry, delivered.Pid,
                    delivered.Box, delivered.Slot, ct).ConfigureAwait(false);
            }

            lines.Add($"{pull.SpeciesName} Nv.{pull.Level} de «{bannerId}». {delivered.Message}");
        }

        return lines;
    }

    /// <summary>
    /// Writes the deaths the wheel caused into the run itself.
    /// </summary>
    /// <remarks>
    /// <b>Without charging for them.</b> Every other death in this competition costs 25 points and
    /// goes through the penalties; this one is the wheel's doing, and the face says so out loud.
    /// The event is still a <c>PokemonDied</c>, because it is one — a run that hid roulette deaths
    /// under another name would be a run whose death count lies.
    /// </remarks>
    private async Task BuryAsync(Run run, RouletteAction action, CancellationToken ct)
    {
        var registered = await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false);

        foreach (var target in action.Pokemon)
        {
            var entry = registered.FirstOrDefault(p => p.Pid == target.Pid && p.Status == PokemonStatus.Alive);

            if (entry is null)
            {
                // No estaba registrado en la run -o ya constaba muerto-. Se ha muerto igual en el
                // juego; lo que no se hace es inventar una entrada para él.
                continue;
            }

            var died = await events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = clock.Now,
                Type = GameEventType.PokemonDied,
                Source = EventSource.System,
                Actor = run.PlayerName,
                Description = $"{entry.Nickname ?? entry.SpeciesName} ha muerto en la ruleta.",
                Reason = "Ruleta del rol LUDÓPATA. No resta puntos.",
                PokemonId = entry.Id,
                LocationId = entry.LocationId,
                Data = new Dictionary<string, string>
                {
                    ["especie"] = entry.Species.ToString(),
                    ["pid"] = target.Pid.ToString("X8"),
                    ["deteccion"] = "ruleta"
                }
            }, ct).ConfigureAwait(false);

            await pokemon.SaveAsync(entry with
            {
                Status = PokemonStatus.Dead,
                DiedAt = died.Timestamp
            }, ct).ConfigureAwait(false);
        }
    }

    private Task RecordAsync(Run run, RouletteWheel wheel, RouletteAction action,
        IReadOnlyList<string> lines, CancellationToken ct) =>
        events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.RouletteSpun,
            Source = EventSource.System,
            Actor = run.PlayerName,
            Description = $"Ruleta #{wheel.Number + 1}: «{wheel.Winner.Name}»."
                          + (lines.Count > 0 ? " " + string.Join(" ", lines) : string.Empty),
            Seed = wheel.Seed,

            // Los puntos de la ruleta van aquí y no en un evento aparte: el saldo es la suma de
            // los deltas del historial, así que un solo apunte deja el saldo bien y dice quién lo
            // movió. Y no lo multiplica el rol: la ruleta dice doscientos y son doscientos.
            PointsDelta = wheel.Winner.Effect == RouletteEffect.Puntos ? wheel.Winner.Amount : 0,
            Data = new Dictionary<string, string>
            {
                ["tirada"] = wheel.Number.ToString(),
                ["cara"] = wheel.Winner.Id,
                ["buena"] = wheel.Winner.Good.ToString(),
                ["efecto"] = action.Effect.ToString(),
                ["ruedaCompleta"] = string.Join(", ", wheel.Faces.Select(f => f.Id)),
                ["hecho"] = string.Join(" | ", lines)
            }
        }, ct);
}
