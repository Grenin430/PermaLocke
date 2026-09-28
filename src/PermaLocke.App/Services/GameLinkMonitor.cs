using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Data;
using PermaLocke.Rules;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <summary>
/// Polls the running game and publishes what it sees. One poller for the whole application,
/// so every screen shows the same snapshot and the emulator is asked once per cycle.
/// </summary>
/// <remarks>
/// Deliberately conservative: a failed read is published as a disconnected snapshot with an
/// explanation, never swallowed, and never retried in a tight loop. The emulator has already
/// been taken down once by an unthrottled burst of requests.
/// </remarks>
public sealed class GameLinkMonitor(
    AzaharGameStateProvider provider,
    IRunContext runContext,
    GameWatcher watcher,
    AzaharGameWriter writer,
    ProgressService progress,
    LevelCapTable caps,
    EncounterGuard encounterGuard,
    EncounterService encounters,
    RewardService rewards,
    MaintenanceService maintenance,
    IEventStore events,
    IClock clock,
    BattleTableReader battleTables,
    IKillcamRecorder killcam,
    PermaLocke.Infrastructure.AppPaths paths,
    ILogger<GameLinkMonitor> logger,
    PermaLocke.Rules.Services.BallControlService balls,
    IntegrityService integrity,
    RulesConfiguration rules,
    PermaLocke.GameLink.Field.BerryPileKeeper berries,
    PermaLocke.GameLink.BagService bag) : IDisposable
{
    /// <summary>
    /// How often the game is polled.
    /// </summary>
    /// <remarks>
    /// One second, not three. Three was the original figure, chosen when the monitor only read
    /// the party, and it turned out to be too slow once the ball rule arrived: the player could
    /// walk into a spent zone and throw a ball before the bag was touched. A cycle costs about
    /// two kilobytes of reads — the party, the bag pointer table and the zone — so polling three
    /// times as often is nothing next to a sweep, which is what actually strains the emulator.
    /// </remarks>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How often the automatic prizes are looked at.
    /// </summary>
    /// <remarks>
    /// Five seconds, because the answer now comes from the live bag rather than the save file and
    /// the check short-circuits on the history once everything automatic is taken. It was thirty
    /// while it parsed the whole partida every time, and thirty seconds is a long time to stand
    /// there wondering whether it worked.
    /// </remarks>
    private static readonly TimeSpan RewardInterval = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private Task? _battleLoop;

    /// <summary>Who is standing and who has fallen in the battle in progress, reading by reading.</summary>
    private readonly BattleFaintTracker _faints = new();

    /// <summary>Fallen party members no authoritative copy holds, already reported once.</summary>
    private readonly HashSet<uint> _unreachableFallen = [];

    /// <summary>
    /// One death recorded at a time, whoever sees it first.
    /// </summary>
    /// <remarks>
    /// Two paths see deaths now: the battle loop, the moment the bar empties, and the party check,
    /// when the battle ends and the party structure gets its zero. They must not both charge the same
    /// one, so each looks the Pokémon up as alive again inside this gate before recording anything.
    /// </remarks>
    private readonly SemaphoreSlim _deathGate = new(1, 1);
    private DateTimeOffset _lastRewardCheck = DateTimeOffset.MinValue;

    /// <summary>
    /// When the game was last read in the middle of a battle, or null when never (2026-09-26).
    /// </summary>
    /// <remarks>Not cleared when the emulator goes: that is when <see cref="IntegrityGuard"/> asks.</remarks>
    public DateTimeOffset? LastReadingInBattle { get; private set; }

    /// <summary>The tables of the battle in progress as last read, or empty outside one (1.0.4.9). Read-only, for the cap panel.</summary>
    public IReadOnlyList<BattleTable> BattleNow { get; private set; } = [];

    /// <summary>Latest snapshot, or null before the first read completes.</summary>
    public GameSnapshot? Latest { get; private set; }

    /// <summary>
    /// Writes a nickname into the party with the game open (2026-09-28): every copy that holds that PID. True when at
    /// least one took it and read back; false when the Pokémon is not in the party or the game is closed.
    /// </summary>
    public bool RenameLive(uint pid, string nickname)
    {
        if (Latest is not { Connected: true } snapshot || snapshot.Party.FirstOrDefault(m => m.Pid == pid) is not { } member)
        {
            return false;
        }

        var applied = provider.AllLayouts
            .Select(layout => writer.SetNickname(layout.SlotAddress(member.Slot), nickname, pid))
            .Count(result => result.Applied);

        logger.LogInformation("Mote «{Nickname}» a {Pokemon} ({Pid:X8}) con el juego abierto: {Applied} copias", nickname,
            member.SpeciesName, pid, applied);
        return applied > 0;
    }
    public string EncounterProblem => Latest?.Connected == true && runContext.Current is not null
        ? encounterGuard.Problem ?? string.Empty : string.Empty;

    public event EventHandler<GameSnapshot>? SnapshotChanged;

    /// <summary>Pokémon seen in the game that the run has not registered yet.</summary>
    public event EventHandler<IReadOnlyList<LivePartyMember>>? UnregisteredDetected;

    /// <summary>Raised when the whole party went down, so a screen can say so out loud.</summary>
    public event EventHandler<PenaltyResult>? TeamWiped;

    /// <summary>Raised when a prize was handed over without anybody asking for it.</summary>
    public event EventHandler<RewardResult>? RewardGiven;

    /// <summary>
    /// Raised whenever the monitor changed the run: a registration, a death, a wipe, a prize.
    /// </summary>
    /// <remarks>
    /// So the screens follow the game instead of the player's navigation. Without it a death was
    /// recorded and charged correctly and HOME kept showing the old balance until somebody left
    /// the section and came back — the work was done and invisible, which reads exactly like the
    /// work not being done.
    /// </remarks>
    public event EventHandler? RunDataChanged;

    /// <summary>
    /// Raised when the run's dead have been left at zero HP in the <b>saved game</b>.
    /// </summary>
    /// <remarks>
    /// It carries a message because the timing is the part worth saying: this happens when the game
    /// is closed, not when somebody falls, and there is no way around that. The mark lives in the
    /// save file, the save file can only be written with the emulator shut, and §98 measured that
    /// writing it into memory instead never reaches the game — 120 HP written into the party
    /// mirror and the screen still saying 128.
    /// </remarks>
    public event EventHandler<string>? DeathMarked;

    /// <summary>A Pokémon of the run has just been recorded as fallen: who, at what level, and what it cost.</summary>
    /// <remarks>
    /// Separate from <see cref="DeathMarked"/>, which is about the marker written into the game.
    /// This one is the death itself, and it is the one worth shouting: the marker may fail -the
    /// emulator may not be ours, the party may have moved- and the death is recorded either way.
    /// </remarks>
    public event EventHandler<DeathNotice>? PokemonDied;

    /// <summary>
    /// A Pokémon that turned up in the party and was registered on its own (2026-09-28): an egg that hatched, a fossil,
    /// a gift. The card goes to the album for it too, not only for wild captures.
    /// </summary>
    public event EventHandler<PKHeX.Core.PK7>? NewcomerArrived;

    public void Start()
    {
        _loop ??= Task.Run(RunAsync);
        _battleLoop ??= Task.Run(RunBattleAsync);
    }

    private async Task RunAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                provider.TrainerName = runContext.Current?.PlayerName ?? string.Empty;

                var snapshot = await provider.ReadAsync(_stopping.Token);

                // Only transitions are logged. Polling every three seconds would otherwise
                // fill the log with the same line forever.
                if (Latest?.Connected != snapshot.Connected)
                {
                    if (snapshot.Connected)
                    {
                        logger.LogInformation("Conectado al juego. Equipo: {Count} Pokémon",
                            snapshot.Party.Count);
                    }
                    else
                    {
                        logger.LogInformation("Sin conexión con el juego: {Problem}", snapshot.Problem);
                    }
                }

                // El juego se ha cerrado: es el único momento en que se puede escribir la partida,
                // y por tanto el único en que la marca de muerte se puede poner.
                if (Latest?.Connected == true && !snapshot.Connected)
                {
                    await MarkFallenAsync();
                }

                Latest = snapshot;
                SnapshotChanged?.Invoke(this, snapshot);

                await InspectAsync(snapshot);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo al consultar el juego");
            }

            try
            {
                await Task.Delay(Interval, _stopping.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>What each party Pokémon was last seen as, and the change it went through, by PID (2026-09-28).</summary>
    private readonly Dictionary<uint, (int Ability, int Nature, (int Ability, int Nature, DateTimeOffset At)? Before)> _seen = [];
    private DateTimeOffset _rerollCheckedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _bagCheckedAt = DateTimeOffset.MinValue;

    /// <summary>An item the player got in the game (floor, shop, gift) and how many: id, amount.</summary>
    public event EventHandler<(int ItemId, int Amount)>? ItemGained;

    /// <summary>What the bag held at the last look, by item; null until it is read (and after a failed read).</summary>
    private Dictionary<int, int>? _carried;

    /// <summary>
    /// Compares the bag with the last look (2026-09-28, the item animation). Only the bag already located, never a sweep.
    /// What PermaLocke wrote itself (balls given back, the shop, gifts from Admin) and balls held back are left out, and
    /// so is a jump of many items at once: that is a reload or another save, not something picked up.
    /// </summary>
    private void WatchBag(Run run)
    {
        if (bag.ReadKnown() is not { } slots)
        {
            _carried = null;
            return;
        }

        var now = slots.Where(slot => slot.Entry.ItemId > 0 && slot.Entry.Count > 0)
            .GroupBy(slot => slot.Entry.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(slot => slot.Entry.Count));

        if (_carried is { } before)
        {
            var gained = now.Select(pair => (ItemId: pair.Key, Amount: pair.Value - before.GetValueOrDefault(pair.Key)))
                .Where(item => item.Amount > 0
                    && DateTime.UtcNow - bag.LastOwnWrite(item.ItemId) > TimeSpan.FromSeconds(15)
                    && bag.Owed(run.Id, item.ItemId) == 0)
                .ToList();

            if (gained.Count is > 0 and <= 4)
            {
                foreach (var item in gained)
                {
                    logger.LogInformation("Objeto nuevo en la mochila: {Item} ×{Amount}", item.ItemId, item.Amount);
                    Announce(() => ItemGained?.Invoke(this, item));
                }
            }
        }

        _carried = now;
    }

    /// <summary>Party members seen as eggs, by PID, to tell when one hatches.</summary>
    private readonly HashSet<uint> _eggs = [];
    private readonly HashSet<uint> _starters = [];

    /// <summary>Whether a Pokémon was registered as the starter: alone in the party when it was registered.</summary>
    public bool IsStarter(uint pid) => _starters.Contains(pid);

    /// <summary>
    /// Flags an Ability Capsule (or anything else) used and undone by reloading without saving (2026-09-28): a party
    /// Pokémon whose ability or nature changes and, within six hours, is back to what it was. Only a note for Admin.
    /// </summary>
    /// <remarks>
    /// Every five seconds, one read per party member through the layouts the game already uses, matched by PID. A change
    /// that stays is fine (the player saved); only the return is flagged. Kept in memory: closing PermaLocke forgets it.
    /// </remarks>
    private async Task WatchRerollsAsync(Run run, GameSnapshot snapshot)
    {
        if (clock.Now - _rerollCheckedAt < TimeSpan.FromSeconds(5))
        {
            return;
        }

        _rerollCheckedAt = clock.Now;

        try
        {
            berries.Keep(run.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudieron mirar los montones de bayas");
        }

        foreach (var member in snapshot.Party)
        {
            var pokemon = provider.AllLayouts.Select(layout => writer.Read(layout.SlotAddress(member.Slot)))
                .FirstOrDefault(pk => pk is { ChecksumValid: true } && pk.PID == member.Pid);
            if (pokemon is null) continue;

            // Un huevo que eclosiona (1.0.7.4): llegó como huevo, sin carta ni mote posibles; ahora sí, como un regalo.
            if (pokemon.IsEgg)
            {
                _eggs.Add(member.Pid);
                continue;
            }

            if (_eggs.Remove(member.Pid))
            {
                var hatched = pokemon;
                logger.LogInformation("{Pokemon} ({Pid:X8}) ha salido del huevo", member.SpeciesName, member.Pid);
                Announce(() => NewcomerArrived?.Invoke(this, hatched));
            }

            var now = ((int)pokemon.Ability, (int)pokemon.Nature);
            if (!_seen.TryGetValue(member.Pid, out var seen))
            {
                _seen[member.Pid] = (now.Item1, now.Item2, null);
                continue;
            }

            if ((seen.Ability, seen.Nature) == now) continue;

            if (seen.Before is { } before && (before.Ability, before.Nature) == now && clock.Now - before.At < TimeSpan.FromHours(6))
            {
                await integrity.FlagAsync(run.Id, IntegrityKinds.Reroll,
                    $"{member.SpeciesName} cambió de habilidad o naturaleza y ha vuelto a la de antes: se usó algo y se recargó sin guardar.",
                    new Dictionary<string, string>
                    {
                        ["pid"] = member.Pid.ToString("X8"),
                        ["habilidad"] = $"{before.Ability} > {seen.Ability} > {now.Item1}",
                        ["naturaleza"] = $"{before.Nature} > {seen.Nature} > {now.Item2}"
                    }, _stopping.Token);
                logger.LogWarning("{Pokemon} ({Pid:X8}) volvió a su habilidad/naturaleza de antes: posible recarga", member.SpeciesName, member.Pid);
                _seen[member.Pid] = (now.Item1, now.Item2, null);
                continue;
            }

            _seen[member.Pid] = (now.Item1, now.Item2, (seen.Ability, seen.Nature, clock.Now));
        }
    }

    /// <summary>
    /// Turns the difference between game and run into action: new party members are registered,
    /// deaths are recorded, and both leave their event behind.
    /// </summary>
    private async Task InspectAsync(GameSnapshot snapshot)
    {
        // No bag scans, rewards or writes until the provider has selected and validated the game.
        if (!snapshot.Connected || runContext.Current is not { } run)
        {
            return;
        }

        // La mochila, también antes de la primera Poké Ball: los primeros objetos del suelo llegan antes (1.0.7.8).
        if (clock.Now - _bagCheckedAt >= TimeSpan.FromSeconds(5))
        {
            _bagCheckedAt = clock.Now;
            try
            {
                WatchBag(run);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo mirar la mochila");
            }
        }

        // Hasta la primera Poke Ball no se registra ni se cuenta nada: ni capturas ni muertes (§150). La regla
        // de las rutas ya espera a esa misma ball (§149), y las dos miran el mismo evento.
        if (!await balls.HasHadBallsAsync(run, _stopping.Token))
        {
            return;
        }

        await WatchRerollsAsync(run, snapshot);

        var findings = await watcher.InspectAsync(run.Id, snapshot, _stopping.Token);
        var changed = false;

        // Antes que las muertes: el vigilante empareja por PID contra lo registrado, asi que un
        // Pokemon sin registrar es invisible y no puede morirse. Registrando primero, uno que
        // aparece ya caido se cuenta en el mismo ciclo en vez de no contarse nunca. Solo se vuelve
        // a preguntar si de verdad se registro algo.
        if (findings.NewMembers.Count > 0
            && await RegisterNewMembersAsync(run, findings.NewMembers, snapshot.Party.Count) > 0)
        {
            changed = true;
            findings = await watcher.InspectAsync(run.Id, snapshot, _stopping.Token);
        }

        foreach (var dead in findings.Fainted)
        {
            // Normalmente el combate ya la ha registrado en el momento, y esto no hace nada: queda
            // para lo que el combate no ve -la app abierta a mitad de un combate, un combate cuyas
            // tablas no se encontraron- y para las muertes fuera de combate.
            if (dead.Pid is not { } pid)
            {
                continue;
            }

            var live = snapshot.Party.FirstOrDefault(member => member.Pid == pid);
            changed |= await RecordDeathOnceAsync(run, pid, live, "memoria del juego") is not null;
        }

        // Aquí no se escribe nada en el juego, y ese es el cambio. La marca vivía en memoria: la
        // aplicación convertía al caído en un Shedinja y le pedía al emulador que lo mantuviera. Ya
        // no. El §98 midió que los PS escritos en memoria no llegan al juego, y el jugador no
        // quería el Shedinja, así que la marca es «sin PS» y va en el FICHERO de partida, que sí
        // manda —comprobado contra la pantalla— y que solo se puede escribir con el juego cerrado.
        // Lo hace MarkFallenAsync cuando se pierde el enlace.

        // Y en vivo, cada vuelta: un caído al que le devuelven los PS vuelve al suelo en menos de
        // un segundo. Esto no se podía hacer hasta el §99, porque hasta entonces PermaLocke solo
        // sabía escribir en el espejo y el juego no lo lee.
        await KeepFallenDownAsync(run, snapshot);

        if (findings.NewMembers.Count > 0)
        {
            Announce(() => UnregisteredDetected?.Invoke(this, findings.NewMembers));
        }

        changed |= await CheckWipeAsync(run, snapshot);
        await EnforceLevelCapAsync(run, snapshot);
        changed |= await ClaimAutomaticRewardsAsync(run);

        // Una sola vez por ciclo, y solo si de verdad cambio algo: las pantallas se refrescan
        // porque el juego se movio, no porque el reloj haya dado otra vuelta.
        if (changed)
        {
            Announce(() => RunDataChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    /// <summary>
    /// Watches the battle in progress and records a death the moment the HP bar empties.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A second loop, because a second a reading is too coarse for a moment the player is looking at:
    /// four readings a second during a battle. Outside one it only looks for a battle every few
    /// seconds, with the limits <see cref="BattleTableReader"/> explains — the research that found
    /// these tables froze the emulator by searching in a loop (§114).
    /// </para>
    /// <para>
    /// It also closes the case §111 left open: a story battle that heals the party before handing
    /// control back hid its deaths from the party check, and the battle tables see the zero first.
    /// </para>
    /// </remarks>
    private async Task RunBattleAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                if (Latest is { Connected: true } snapshot && runContext.Current is { } run)
                {
                    var tables = battleTables.Read(clock.Now);
                    var faints = _faints.Observe(tables);

                    // Los caídos, también desde este bucle, que da vueltas cada medio segundo fuera de combate (1.0.4.4):
                    // el juego cura antes de un combate de la historia y lo monta en menos de un segundo, y el ciclo de un
                    // segundo llegaba tarde (SaintPablo, 27/09: cura a las 02:27:30.058, combate a las .867).
                    if (!_faints.InBattle)
                    {
                        _fallenCheckedThisBattle = false;
                        await KeepFallenDownAsync(run, snapshot);
                    }
                    else if (!_fallenCheckedThisBattle)
                    {
                        _fallenCheckedThisBattle = true;
                        await FlagFallenInBattleAsync(run, tables);
                    }

                    // La última lectura dentro de un combate: si el emulador se va justo después, se dejó a medias. No se
                    // borra al salir de él: una lectura vacía mientras el emulador se cierra no puede tapar el combate.
                    if (_faints.InBattle) LastReadingInBattle = clock.Now;

                    // Para el panel del cap (1.0.4.9): los PS del combate en curso, de la misma lectura, sin leer nada más.
                    // Una referencia que se sustituye entera: el panel la lee desde otro hilo sin cerrojos.
                    BattleNow = _faints.InBattle ? tables : [];

                    // La killcam graba mientras dura el combate y solo entonces.
                    killcam.Recording = _faints.InBattle;

                    foreach (var faint in faints)
                    {
                        await OnBattleFaintAsync(run, snapshot, faint, tables);
                    }

                    // La regla de primer encuentro vive aquí y no en el ciclo de un segundo: el contador de
                    // combates salvajes sube al empezar el combate, y un duplicado no puede tener Poké Balls
                    // durante el segundo que tardaría el otro ciclo (§117).
                    try
                    {
                        if (await encounterGuard.TickAsync(run, tables, faints, _faints.InBattle, _stopping.Token,
                                [.. snapshot.Party.Select(member => member.Species)]))
                        {
                            Announce(() => RunDataChanged?.Invoke(this, EventArgs.Empty));
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Una regla que falla no se lleva por delante la detección de muertes del mismo bucle.
                        logger.LogWarning(ex, "Fallo en la regla de primer encuentro");
                    }
                }
                else
                {
                    _faints.Observe([]);
                    BattleNow = [];
                    killcam.Recording = false;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Una lectura rota de combate cuesta la detección en el momento, nada más: la
                // muerte la sigue viendo la comprobación del equipo al acabar el combate.
                logger.LogWarning(ex, "Fallo al leer el combate en curso");
            }

            try
            {
                // Medio segundo fuera de combate y no uno: es lo que tarda en verse que ha empezado uno salvaje.
                await Task.Delay(_faints.InBattle ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromMilliseconds(500),
                    _stopping.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task OnBattleFaintAsync(Run run, GameSnapshot snapshot, BattleFaint faint,
        IReadOnlyList<BattleTable> tables)
    {
        if (!faint.IsPlayers)
        {
            // Los rivales no son de la run. Se apuntan porque son la manera de comprobar que la
            // detección funciona sin que se muera nadie del jugador.
            logger.LogInformation("Rival debilitado en combate: especie {Species} (posición {Id})",
                faint.Species, faint.BattleId);
            return;
        }

        // The report of 20/09 showed a Wooloo at battle position 0 while the party's position 0
        // held another species. Prefer the checksum-validated identity behind the battle blocks.
        var copies = tables.Select(table => table.Block(faint.BattleId))
            .OfType<BattleBlock>().Where(block => block.IsPlayers && block.Species == faint.Species)
            .Select(battleTables.ReadPokemon);
        var member = BattlePokemon.MatchPlayer(faint, snapshot.Party, copies);

        if (member is null || member.Species != faint.Species)
        {
            logger.LogWarning("Caída en combate en la posición {Id} (especie {Species}) que no cuadra con el equipo: "
                              + "no se registra en el momento", faint.BattleId, faint.Species);
            return;
        }
        if (member.Slot != faint.BattleId)
            logger.LogInformation("Caída identificada por PID {Pid:X8}: posición de combate {Battle}, hueco del equipo {Party}",
                member.Pid, faint.BattleId, member.Slot);

        // Las tablas lo saben antes de que se vea: la segunda cambia con el mensaje «¡X ha usado Y!»,
        // antes de la animación del ataque y de la barra. La escena tiene que saltar cuando la barra
        // llega a cero, así que se espera a verla -o como mucho unos segundos- (§114 ter).
        var bar = await HpBarWatcher.WaitUntilEmptyAsync(_stopping.Token);
        var mark = killcam.Mark();
        logger.LogInformation("Caída en combate de la posición {Id}: {Bar}", faint.BattleId, bar);

        if (await RecordDeathOnceAsync(run, member.Pid, member, "combate, en el momento", Rivals(tables), mark) is { } fallen)
        {
            Announce(() => RunDataChanged?.Invoke(this, EventArgs.Empty));

        }
    }

    /// <summary>
    /// The rivals of the battle a Pokémon fell in, for the cemetery: every opponent the tables hold,
    /// in battle order, by species.
    /// </summary>
    /// <remarks>
    /// All of them and not «the one that killed it», because which opponent is on the field is not
    /// something the tables have been measured to say. In a wild battle there is only one, and that one
    /// is the killer; against a trainer the cemetery says the battle was against this team, which is
    /// true, instead of naming one of them, which would be a guess.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> Rivals(IReadOnlyList<BattleTable> tables)
    {
        var rivals = tables.Count == 0
            ? []
            : tables[0].Blocks.Where(block => !block.IsPlayers).OrderBy(block => block.BattleId).Select(block => block.Species).ToList();

        return rivals.Count == 0
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["rivales"] = string.Join(",", rivals) };
    }

    internal async Task SaveKillcamAsync(Guid runId, Guid pokemonId, double mark)
    {
        try
        {
            if (await killcam.SaveAsync(KillcamClip.PathFor(paths.Saves, runId, pokemonId), mark, _stopping.Token) is not null)
                Announce(() => RunDataChanged?.Invoke(this, EventArgs.Empty));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sin killcam la muerte sigue registrada; solo le falta la repetición.
            logger.LogWarning(ex, "No se ha podido guardar la killcam");
        }
    }

    /// <summary>
    /// Records a death unless it is already recorded, and announces it.
    /// </summary>
    /// <returns>Whether it recorded something.</returns>
    private async Task<PokemonEntry?> RecordDeathOnceAsync(Run run, uint pid, LivePartyMember? live, string detection,
        IReadOnlyDictionary<string, string>? details = null, double? clipMark = null)
    {
        // Antes de la primera Poke Ball una muerte no cuenta, tampoco la del combate (§150).
        if (!await balls.HasHadBallsAsync(run, _stopping.Token))
        {
            return null;
        }

        await _deathGate.WaitAsync(_stopping.Token);

        try
        {
            // Vivo en la run AHORA, dentro de la puerta: si el combate ya la registró, aquí no está.
            var entry = (await maintenance.AliveAsync(_stopping.Token)).FirstOrDefault(p => p.Pid == pid);

            if (entry is null)
            {
                return null;
            }

            // El nombre y el nivel de AHORA, leídos del juego, y no los de cuando se registró: un
            // Pokémon capturado a nivel 5 que cae a nivel 59 no murió a nivel 5, y el mote puede
            // habérselo puesto después.
            var name = !string.IsNullOrWhiteSpace(live?.Nickname) ? live.Nickname
                : entry.Nickname ?? entry.SpeciesName;

            logger.LogWarning("Muerte detectada ({Detection}): {Pokemon}", detection, name);
            var mark = clipMark ?? killcam.Mark();
            var charged = await watcher.RecordDeathAsync(entry, run.PlayerName, detection: detection, ct: _stopping.Token,
                details: details);

            var notice = new DeathNotice(name, live?.Species ?? entry.Species, charged.Points, live?.Form ?? entry.Form,
                live?.IsShiny ?? entry.IsShiny, live?.Level ?? entry.Level);
            // Both monitors can win the death gate. Whichever records it also saves the available
            // recording, so the party fallback cannot silently skip a clip the battle loop captured.
            _ = SaveKillcamAsync(run.Id, entry.Id, mark);
            Announce(() => PokemonDied?.Invoke(this, notice));
            return entry;
        }
        finally
        {
            _deathGate.Release();
        }
    }

    /// <summary>
    /// Registers whatever turned up in the party that the run does not know about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to be an announcement and a button, and that turned out to be a hole rather than
    /// a choice: the whole penalty system hangs off the run knowing a Pokémon exists, so a player
    /// who did not press the button had deaths that were never counted and never told. Measured on
    /// a real run -- one starter in the party, zero registered, and therefore zero deaths possible.
    /// A rule that only applies when somebody remembers to press something is not a rule.
    /// </para>
    /// <para>
    /// What it registers is only what the game actually says: species, level, nickname, shiny, PID
    /// and the met location the Pokémon carries. The <b>encounter type it does not know</b>, and it
    /// says so with <see cref="EncounterType.Unknown"/> instead of assuming wild -- assuming would
    /// spend the zone's one encounter on the player's behalf. Registering is not adjudicating.
    /// </para>
    /// <para>
    /// A capture a rule blocks is <b>not</b> forced through: it stays unregistered and goes back to
    /// being announced, because overriding a rule is the player's call and theirs alone.
    /// </para>
    /// </remarks>
    /// <returns>How many were actually registered.</returns>
    private async Task<int> RegisterNewMembersAsync(Run run, IReadOnlyList<LivePartyMember> members, int partySize)
    {
        var registered = 0;

        foreach (var member in members)
        {
            try
            {
                var result = await encounters.RegisterAsync(run.Id, new RegisterCaptureRequest(
                        member.Species,
                        member.SpeciesName,
                        member.MetLocationName,
                        EncounterType.Unknown,
                        member.IsShiny,
                        member.Level,
                        string.IsNullOrWhiteSpace(member.Nickname) ? null : member.Nickname,
                        Force: false,
                        Pid: member.Pid,
                        Form: member.Form,
                        Moves: member.Moves),
                    run.PlayerName, _stopping.Token, EventSource.AutoDetect);

                if (result.Registered)
                {
                    registered++;

                    // Solo en el equipo cuando se registra = el inicial: se registra con la primera Poké Ball y no lleva
                    // votación de mote (2026-09-28, lo pidió el organizador).
                    if (partySize == 1) _starters.Add(member.Pid);

                    // La carta al álbum, con el Pokémon leído del equipo en vivo por su PID.
                    var read = provider.AllLayouts.Select(layout => writer.Read(layout.SlotAddress(member.Slot)))
                        .FirstOrDefault(pk => pk is { ChecksumValid: true } && pk.PID == member.Pid);
                    if (read is not null) Announce(() => NewcomerArrived?.Invoke(this, read));
                    logger.LogInformation(
                        "{Pokemon} Nv.{Level} registrado solo (PID {Pid:X8}, encontrado en {Where})",
                        member.SpeciesName, member.Level, member.Pid, member.MetLocationName);
                }
                else
                {
                    logger.LogWarning("{Pokemon} no se registra solo: {Why}", member.SpeciesName,
                        result.Evaluation.Primary?.Message ?? "una regla lo impide");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falló el registro automático de {Pokemon}", member.SpeciesName);
            }
        }

        return registered;
    }

    /// <summary>
    /// Charges the extra penalty when the whole party goes down at once.
    /// </summary>
    /// <remarks>
    /// After the individual deaths, not before: each of them costs on its own, and the wipe is
    /// charged on top for the party falling as a whole.
    /// </remarks>
    /// <returns>True when the wipe was charged, so the screens know to catch up.</returns>
    private async Task<bool> CheckWipeAsync(Run run, GameSnapshot snapshot)
    {
        var result = await watcher.CheckWipeAsync(run.Id, run.PlayerName, snapshot, _stopping.Token);

        if (result is null)
        {
            return false;
        }

        logger.LogWarning("Equipo caído. Penalización: {Points} puntos{Capped}. Saldo: {Balance}",
            result.Points, result.Capped ? " (tope alcanzado)" : string.Empty, result.NewBalance);

        Announce(() => TeamWiped?.Invoke(this, result));
        return true;
    }

    /// <summary>
    /// The Pokémon already brought down to the cap, so a correction the game undoes can be told
    /// apart from a first one.
    /// </summary>
    /// <remarks>
    /// Keyed by PID, which survives nicknames and evolutions. Without it every revert looks like a
    /// fresh problem, and the log fills with identical lines that never say the one thing worth
    /// knowing: that the correction is not holding.
    /// </remarks>
    private readonly Dictionary<uint, int> _cappedAt = [];

    /// <summary>Polls in a row with nobody over the cap. The record of who has been corrected
    /// is only forgotten after a good few, never on the first quiet one.</summary>
    private int _calmPolls;

    /// <summary>Roughly a minute at the monitor's cadence.</summary>
    private const int CalmPollsBeforeForgetting = 20;

    /// <summary>Why the cap is not being applied, or empty when it is. Shown on HOME.</summary>
    public string CapProblem { get; private set; } = string.Empty;

    /// <summary>
    /// Brings anything above the cap back down, in the game and in the history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applied to every copy of the party for the same reason as the death transform: the one the
    /// game reads is among them.
    /// </para>
    /// <para>
    /// Nothing is recorded that has not been read back. The writer verifies the bytes and then the
    /// level itself, so a correction the emulator swallowed is a warning here and not an event
    /// claiming the run enforced something it did not.
    /// </para>
    /// </remarks>
    private async Task EnforceLevelCapAsync(Run run, GameSnapshot snapshot)
    {
        if (!snapshot.Connected)
        {
            // Sin juego no hay nada que corregir, y decir que el cap «no se está aplicando» con
            // Azahar cerrado es alarmar por lo evidente: de eso ya avisa la tira del enlace. El
            // aviso rojo tiene que significar que el juego está delante y aun así no se aplica.
            CapProblem = string.Empty;
            return;
        }

        if (await progress.CurrentCapAsync(run, _stopping.Token) is not { } cap)
        {
            return;
        }

        if (provider.AllLayouts.Count == 0)
        {
            CapProblem = "El nivel máximo no se está vigilando ahora mismo.";
            return;
        }

        var over = snapshot.Party.Where(m => m.Level > cap).ToList();

        // Aviso primero, y sin escribir nada, que es como lo hace la competición.
        //
        // Medido sobre los binarios de la referencia: NO accede a la memoria del emulador por
        // ningún sitio -- ni WriteMemory, ni RPC, ni el puerto 45987 -- y del cap tiene un único
        // símbolo, el getter de la lista. Allí el cap es una regla que el jugador cumple y la
        // aplicación le enseña. Forzarlo reescribiendo el juego en marcha es una pelea que no se
        // gana: el equipo vive en veinticinco sitios de la memoria a la vez, el juego lo restaura
        // desde el que quiere, y una escritura que entra y se relee bien pierde igual. Y cuando
        // sale mal no falla, CAMBIA el Pokémon de alguien: el §53 evolucionó un Ledyba así.
        if (over.Count > 0)
        {
            var who = string.Join(", ",
                over.Select(m => $"{m.SpeciesName} (Nv.{m.Level})"));

            CapProblem = caps.CorrectInMemory
                ? $"{who} pasa del nivel máximo ({cap}). Se está corrigiendo: vuelve a abrir el menú para verlo."
                : $"{who} pasa del nivel máximo ({cap}).";
        }

        if (!caps.CorrectInMemory)
        {
            if (over.Count == 0)
            {
                CapProblem = string.Empty;
            }

            return;
        }

        if (over.Count == 0)
        {
            CapProblem = string.Empty;

            // No se olvida a la primera lectura buena, y ese detalle es el que ocultó el fallo.
            // El equipo vive en varias copias y no todas se corrigen a la vez, así que en cuanto
            // una lectura caía por debajo del cap el registro se borraba entero -- y la siguiente
            // corrección del MISMO Pokémon volvía a parecer la primera. Tres seguidas en un
            // minuto, ninguna marcada como repetición, y el aviso rojo nunca llegó a salir.
            if (++_calmPolls >= CalmPollsBeforeForgetting)
            {
                _cappedAt.Clear();
            }

            return;
        }

        _calmPolls = 0;

        foreach (var member in over)
        {
            // Con el PID por delante: el hueco tiene que contener a ESE Pokémon. El equipo vive en
            // varias estructuras y no todas se leen igual, así que sin esta comprobación una copia
            // desalineada se corrige igual y lo que se corrige es el de al lado.
            var results = provider.AllLayouts
                .Select(layout => writer.EnforceLevelCap(layout.SlotAddress(member.Slot), cap, member.Pid))
                .ToList();

            var applied = results.Count(r => r.Applied);
            var rejected = results.Count(r => r.Rejected);

            if (applied == 0)
            {
                CapProblem = rejected > 0
                    ? $"{member.SpeciesName} pasa del nivel máximo ({cap}) y no se ha podido corregir. Usa el Azahar que viene con PermaLocke."
                    : $"{member.SpeciesName} pasa del nivel máximo ({cap}) y no se ha podido corregir.";

                logger.LogWarning("{Pokemon} a nivel {Level} con cap {Cap}: {Rejected} copias rechazaron "
                                  + "la escritura y ninguna la aceptó", member.SpeciesName, member.Level,
                    cap, rejected);
                continue;
            }

            // Corregirlo otra vez significa que algo lo deshizo entre medias. Eso es información,
            // no ruido: es la diferencia entre "el cap funciona" y "el cap se pelea y pierde".
            var again = _cappedAt.ContainsKey(member.Pid);
            _cappedAt[member.Pid] = cap;

            // Se vuelve a barrer SIEMPRE que hay que corregir, no solo cuando se repite.
            //
            // Esperar a la repetición no valía, y el motivo es que el monitor lee y escribe LA
            // MISMA copia: corrige la que lee, la relee correcta, y la copia desde la que el juego
            // restaura el nivel le es invisible. La repetición que dispararía el barrido no puede
            // llegar a detectarse. Medido en la run real: cinco estructuras del equipo en memoria
            // y la lista de escritura tenía UNA.
            //
            // Y ahora se puede pagar. El barrido costaba diez minutos cuando se midió, pero
            // aquello fue antes de que el §54 arreglase el cliente RPC, que perdía respuestas y
            // reintentaba: hoy son unos cinco segundos, cronometrados. Corregir el cap es raro
            // -- solo pasa al pasarse de nivel --, así que cinco segundos por acertar es barato.
            provider.SweepAgain();

            if (again)
            {
                CapProblem = $"{member.SpeciesName} vuelve a pasar del nivel máximo. Se está corrigiendo.";
            }

            logger.LogWarning(
                "{Pokemon} estaba a nivel {Level}, cap {Cap}. Corregido y releído en {Copies} copias{Again}",
                member.SpeciesName, member.Level, cap, applied,
                again ? " (el juego lo había deshecho)" : string.Empty);

            await events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = clock.Now,
                Type = GameEventType.LevelCapEnforced,
                Source = EventSource.AutoDetect,
                Actor = run.PlayerName,
                Description = $"{member.SpeciesName} superaba el cap ({member.Level} > {cap}). "
                              + "Devuelto al cap y comprobado en la memoria del juego"
                              + (again ? ", después de que el juego lo deshiciera." : "."),
                Data = new Dictionary<string, string>
                {
                    ["nivel"] = member.Level.ToString(),
                    ["cap"] = cap.ToString(),
                    ["hueco"] = member.Slot.ToString(),
                    ["copias"] = applied.ToString(),
                    ["rechazadas"] = rejected.ToString(),
                    ["repetida"] = again.ToString()
                }
            }, _stopping.Token);
        }
    }

    /// <summary>
    /// Hands over the prizes that are meant to arrive on their own.
    /// </summary>
    /// <remarks>
    /// Throttled, and not for tidiness: working out whether a prize is earned reads the whole save
    /// file through PKHeX, and doing that once a second next to the party poll would be a real
    /// cost for an answer that changes about twice a run.
    /// </remarks>
    /// <returns>True when something was actually handed over.</returns>
    private async Task<bool> ClaimAutomaticRewardsAsync(Run run)
    {
        if (clock.Now - _lastRewardCheck < RewardInterval)
        {
            return false;
        }

        _lastRewardCheck = clock.Now;
        var handed = false;

        try
        {
            foreach (var given in await rewards.ClaimAutomaticAsync(run, _stopping.Token))
            {
                if (given.Succeeded)
                {
                    handed = true;
                    logger.LogInformation("Premio automático entregado: {Message}", given.Message);
                    Announce(() => RewardGiven?.Invoke(this, given));
                }
                else
                {
                    logger.LogWarning("Premio automático no entregado: {Message}", given.Message);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la entrega automática de premios");
        }

        return handed;
    }

    /// <summary>
    /// Turns the dead Pokémon into the run's marker inside the game itself.
    /// </summary>
    /// <remarks>
    /// Matching is by PID so the right slot is hit even after nicknames or evolutions. If the
    /// party has not been located yet, or the PID is not in it, nothing is written: a wrong
    /// slot would destroy a living Pokémon.
    /// </remarks>
    /// <summary>Most Pokemon a party can hold, which is how far a slot search goes.</summary>
    private const int PartySlots = 6;

    /// <summary>
    /// Raises an event without letting a listener's failure take the inspection down with it.
    /// </summary>
    /// <remarks>
    /// The third time this lesson is paid for, and this time it cost a Pokémon: a notice read the
    /// main window's state from <b>this</b> thread, WPF threw, and the exception climbed all the
    /// way out of the inspection — so the death was recorded and charged, and then the rest of the
    /// cycle never ran because it was already gone.
    /// <para>
    /// Whoever listens is a spectator. The work is registering the death and charging for it;
    /// telling somebody about it is a courtesy, and a courtesy that throws must cost the courtesy
    /// and nothing else.
    /// </para>
    /// </remarks>
    private void Announce(Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Un oyente falló al recibir un aviso del vigilante");
        }
    }

    /// <summary>
    /// Leaves the run's dead at zero HP in the save, now that the game has let go of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole death marker, and it can only happen here. What used to run at the moment of death
    /// wrote into the running game's memory, and §98 measured that the game never reads it back —
    /// so the file is the only door, and the file can only be written with the emulator shut.
    /// </para>
    /// <para>
    /// It goes through the same service as the button in MANTENIMIENTO rather than repeating it:
    /// copy first, write, re-read, and one already down is skipped, so a session that killed nobody
    /// costs a read and nothing else. If the emulator is still holding the save it does nothing and
    /// the next disconnection tries again — never a write into a file another program owns.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Puts back to zero any Pokémon the run says is dead and the game has healed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes «dead» mean dead while you play, and it only became possible once §99
    /// found where the game keeps HP: <c>0x158</c> into the entries of the authoritative structure.
    /// Everything PermaLocke wrote before that went into the save-block mirror, which the game
    /// fills and never reads — measured three times, and it is why the marker had to be a Shedinja.
    /// </para>
    /// <para>
    /// Every poll rather than only when the party changes: a Pokémon Centre heals without the
    /// roster moving at all, so a pass keyed on it would never notice.
    /// </para>
    /// <para>
    /// It reads the HP from every authoritative copy instead of trusting the snapshot's, and that
    /// is not caution, it is a measurement. There are <b>three</b> structures at that stride and
    /// they disagree: read in the same second, one said the Gyarados was at 128, another at 103 and
    /// the third at 131 — two photographs and the live one. The application had cached the stalest
    /// from a previous session and revalidated it «without sweeping», so the first version of this
    /// pass looked at a healed Pokémon, read zero, and concluded there was nothing to do. Nothing
    /// tells the three apart by shape, so none of them gets to be trusted: every copy is asked, by
    /// PID, and whichever still has HP is put down.
    /// </para>
    /// <para>
    /// It is <b>not</b> handed to the emulator's block watcher, and that is a decision and not an
    /// oversight. The watcher takes the first four bytes of what it guards as the identity, and at
    /// <c>0x158</c> those are the status condition, not the encryption constant; guarding from the
    /// start of the entry instead would mean holding the bytes between <c>0xE8</c> and <c>0x158</c>
    /// as well, which nobody has identified and the game moves on its own. That is exactly what
    /// corrupted a real save into a Bad Egg in §97. Once a second from here cannot corrupt
    /// anything.
    /// </para>
    /// </remarks>
    private async Task KeepFallenDownAsync(Run run, GameSnapshot snapshot)
    {
        if (!snapshot.Connected || snapshot.Party.Count == 0 || provider.AllLayouts.Count == 0)
        {
            return;
        }

        // Lo llaman dos bucles (el de un segundo y el de combate); si ya está en marcha, esta vuelta sobra.
        if (!_keepingDown.Wait(0))
        {
            return;
        }

        try
        {
            await KeepFallenDownCoreAsync(run, snapshot);
        }
        finally
        {
            _keepingDown.Release();
        }
    }

    private readonly SemaphoreSlim _keepingDown = new(1, 1);

    /// <summary>Whether this battle has already been looked at for a fallen Pokémon fighting.</summary>
    private bool _fallenCheckedThisBattle;

    /// <summary>
    /// Once per battle: a Pokémon the run holds as fallen that entered it with HP (1.0.4.4). The game healed it right
    /// before a story battle, faster than PermaLocke could put it down again. Not a punishment, and nothing is written into
    /// the battle (that structure is only read, §53): the organiser is told in Admin and decides.
    /// </summary>
    private async Task FlagFallenInBattleAsync(Run run, IReadOnlyList<BattleTable> tables)
    {
        try
        {
            var fallen = await watcher.FallenPidsAsync(run.Id, _stopping.Token);

            if (fallen.Count == 0 || tables.Count == 0)
            {
                return;
            }

            foreach (var block in tables[0].Blocks.Where(b => b.IsPlayers && b.CurrentHp > 0))
            {
                if (battleTables.ReadPokemon(block) is { } pokemon && fallen.Contains(pokemon.PID))
                {
                    logger.LogWarning("Un caído ha entrado vivo en un combate: especie {Species}, PID {Pid:X8}", block.Species, pokemon.PID);

                    // En el banquillo se le deja a 0 PS dentro del propio combate (1.0.4.10), en las dos tablas: para el
                    // juego es un Pokémon debilitado que no se puede sacar. En el campo (posiciones 0 y 1) no se toca.
                    var benched = block.BattleId >= 2;
                    var fixedIt = benched && rules.KnockDownFallenInBattle && tables
                        .Select(table => table.Block(block.BattleId))
                        .OfType<BattleBlock>()
                        .Where(b => b.Species == block.Species && b.MaxHp == block.MaxHp && b.CurrentHp > 0)
                        .Select(b => battleTables.KnockDownOnBench(b, pokemon.PID))
                        .ToList() is { Count: > 0 } results && results.All(done => done);

                    await integrity.FlagAsync(run.Id, IntegrityKinds.FallenInBattle,
                        $"Un Pokémon caído (especie {block.Species}) ha entrado en un combate con {block.CurrentHp} PS: el juego lo curó justo antes."
                        + (fixedIt ? " Estaba en el banquillo: PermaLocke lo ha dejado debilitado en el combate."
                            : benched && rules.KnockDownFallenInBattle ? " Estaba en el banquillo, pero no se ha podido debilitar."
                            : benched ? " Estaba en el banquillo (corrección apagada en las reglas)." : " Salió al campo: no se ha tocado."),
                        new Dictionary<string, string> { ["pid"] = pokemon.PID.ToString("X8"), ["especie"] = block.Species.ToString() },
                        _stopping.Token);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se ha podido mirar si un caído ha entrado en el combate");
        }
    }

    private async Task KeepFallenDownCoreAsync(Run run, GameSnapshot snapshot)
    {

        // Durante un combate no: sus bloques apuntan a estas mismas estructuras, un caído en combate
        // tiene aquí todavía los PS de antes de empezar, y el juego le copia el cero al terminar. No
        // hay nada que corregir y sí algo que el combate está usando.
        if (_faints.InBattle)
        {
            return;
        }

        try
        {
            var fallen = await watcher.FallenPidsAsync(run.Id, _stopping.Token);
            var here = snapshot.Party.Where(m => fallen.Contains(m.Pid)).ToList();

            foreach (var member in here)
            {
                var name = string.IsNullOrWhiteSpace(member.Nickname) ? member.SpeciesName : member.Nickname;
                var applied = 0;
                var seen = false;

                foreach (var layout in provider.AllLayouts
                             .Where(l => l.Stride == PartyLayoutLocator.AuthoritativeStride))
                {
                    // El PID por delante y en CADA copia, no el número de hueco: las estructuras
                    // guardan el equipo en órdenes distintos y escribir por posición manda la
                    // corrección al Pokémon de al lado (§96).
                    for (var slot = 0; slot < PartySlots; slot++)
                    {
                        var at = layout.SlotAddress(slot);

                        if (writer.ReadAuthoritative(at) is not { ChecksumValid: true } found
                            || found.PID != member.Pid)
                        {
                            continue;
                        }

                        seen = true;

                        if (found.Stat_HPCurrent > 0 && writer.SetLiveHp(at, 0, member.Pid).Applied)
                        {
                            applied++;
                        }
                    }
                }

                // Un caído que no está en ninguna copia de las que lee el juego no se puede tumbar, y
                // hasta el §135 eso pasaba sin decir nada: la lista de direcciones era de otra sesión.
                // Se dice una vez por Pokémon y se pide buscar el equipo de nuevo.
                if (!seen)
                {
                    if (_unreachableFallen.Add(member.Pid))
                    {
                        logger.LogWarning("{Pokemon} está caído y no aparece en ninguna copia que lea el juego:"
                                          + " se busca el equipo de nuevo", name);
                        provider.SweepAgain();
                    }

                    continue;
                }

                _unreachableFallen.Remove(member.Pid);

                if (applied == 0)
                {
                    continue;
                }

                logger.LogWarning("{Pokemon} está caído y le habían devuelto los PS: al suelo otra vez"
                                  + " ({Copias} copias)", name, applied);

                Announce(() => DeathMarked?.Invoke(this, $"{name} sigue caído."));
            }
        }
        catch (Exception ex)
        {
            // Sin reintento inmediato: la vuelta siguiente lo mira otra vez, y el historial de la
            // run ya tiene la muerte pase lo que pase.
            logger.LogWarning(ex, "No se ha podido comprobar si algún caído ha recuperado PS");
        }
    }

    private async Task MarkFallenAsync()
    {
        if (runContext.Current is null)
        {
            return;
        }

        try
        {
            var report = await maintenance.EnforceDeathsAsync(_stopping.Token);

            if (report.Marked == 0)
            {
                return;
            }

            logger.LogInformation("{Count} caído(s) a 0 PS en la partida al cerrar el juego",
                report.Marked);

            Announce(() => DeathMarked?.Invoke(this,
                $"{report.Marked} caído(s) dejados a 0 PS."));

            Announce(() => RunDataChanged?.Invoke(this, EventArgs.Empty));
        }
        catch (Exception ex)
        {
            // Sin reintento inmediato: la proxima vez que se cierre el juego se vuelve a intentar,
            // y el historial de la run ya tiene la muerte pase lo que pase.
            logger.LogWarning(ex, "No se ha podido dejar a los caídos a 0 PS en la partida");
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
    }
}
