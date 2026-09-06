using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.GameLink;
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
    BallControlService ballControl,
    EncounterService encounters,
    RewardService rewards,
    MaintenanceService maintenance,
    IEventStore events,
    IClock clock,
    ILogger<GameLinkMonitor> logger) : IDisposable
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
    private DateTimeOffset _lastRewardCheck = DateTimeOffset.MinValue;

    /// <summary>Latest snapshot, or null before the first read completes.</summary>
    public GameSnapshot? Latest { get; private set; }

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

    /// <summary>A Pokémon of the run has just been recorded as fallen. The name it was known by.</summary>
    /// <remarks>
    /// Separate from <see cref="DeathMarked"/>, which is about the marker written into the game.
    /// This one is the death itself, and it is the one worth shouting: the marker may fail -the
    /// emulator may not be ours, the party may have moved- and the death is recorded either way.
    /// </remarks>
    public event EventHandler<string>? PokemonDied;

    public void Start()
    {
        _loop ??= Task.Run(RunAsync);
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

    /// <summary>
    /// Turns the difference between game and run into action: new party members are registered,
    /// deaths are recorded, and both leave their event behind.
    /// </summary>
    private async Task InspectAsync(GameSnapshot snapshot)
    {
        if (runContext.Current is not { } run)
        {
            return;
        }

        var findings = await watcher.InspectAsync(run.Id, snapshot, _stopping.Token);
        var changed = false;

        // Antes que las muertes: el vigilante empareja por PID contra lo registrado, asi que un
        // Pokemon sin registrar es invisible y no puede morirse. Registrando primero, uno que
        // aparece ya caido se cuenta en el mismo ciclo en vez de no contarse nunca. Solo se vuelve
        // a preguntar si de verdad se registro algo.
        if (findings.NewMembers.Count > 0
            && await RegisterNewMembersAsync(run, findings.NewMembers) > 0)
        {
            changed = true;
            findings = await watcher.InspectAsync(run.Id, snapshot, _stopping.Token);
        }

        foreach (var dead in findings.Fainted)
        {
            var name = dead.Nickname ?? dead.SpeciesName;

            logger.LogWarning("Muerte detectada: {Pokemon}", name);
            await watcher.RecordDeathAsync(dead, run.PlayerName, ct: _stopping.Token);
            Announce(() => PokemonDied?.Invoke(this, name));
            changed = true;
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
        await ApplyBallRuleAsync(run);
        changed |= await ClaimAutomaticRewardsAsync(run);

        // Una sola vez por ciclo, y solo si de verdad cambio algo: las pantallas se refrescan
        // porque el juego se movio, no porque el reloj haya dado otra vuelta.
        if (changed)
        {
            Announce(() => RunDataChanged?.Invoke(this, EventArgs.Empty));
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
    private async Task<int> RegisterNewMembersAsync(Run run, IReadOnlyList<LivePartyMember> members)
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
                        Pid: member.Pid),
                    run.PlayerName, _stopping.Token, EventSource.AutoDetect);

                if (result.Registered)
                {
                    registered++;
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
    /// Keeps the bag in line with the zone: no Poké Balls while the zone has spent its
    /// encounter, and back again on leaving.
    /// </summary>
    /// <remarks>
    /// Runs on every tick because it is cheap — the zone is 48 bytes read from a cached anchor —
    /// and it only writes when the answer changes. Anything uncertain leaves the bag alone.
    /// </remarks>
    private async Task ApplyBallRuleAsync(Run run)
    {
        var spent = await encounters.GetSpentZonesAsync(run.Id, _stopping.Token);
        var result = await ballControl.ApplyAsync(run, spent, _stopping.Token);

        if (result.Acted)
        {
            logger.LogInformation("Regla de bolas en {Zone}: {Outcome}, {Count} objetos",
                result.LocationName ?? "una zona sin identificar", result.Outcome, result.ItemsAffected);
        }
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
            CapProblem = "No sé dónde está el equipo en memoria, así que el cap de nivel no se está aplicando.";
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
                ? $"{who} pasa del cap, que es {cap}. Se le está bajando: entra en un combate y "
                  + "sal, que el juego no repinta el nivel hasta que recarga el equipo."
                : $"{who} pasa del cap de nivel, que es {cap}. Bájalo tú: PermaLocke está puesto "
                  + "para avisar y no para corregir.";
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
                    ? $"{member.SpeciesName} está a nivel {member.Level} y el cap es {cap}, pero el juego "
                      + "no acepta la corrección. Hace falta el fork propio de Azahar."
                    : $"{member.SpeciesName} está a nivel {member.Level} y el cap es {cap}, pero no "
                      + "encuentro su hueco en la memoria del juego.";

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
                CapProblem = $"{member.SpeciesName} vuelve a estar por encima del cap. El juego lo "
                             + $"está deshaciendo desde una copia que no conozco (tengo {applied} "
                             + "de las que hay): se ha vuelto a aplicar y se está buscando el resto.";
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

        try
        {
            var fallen = await watcher.FallenPidsAsync(run.Id, _stopping.Token);
            var here = snapshot.Party.Where(m => fallen.Contains(m.Pid)).ToList();

            foreach (var member in here)
            {
                var name = string.IsNullOrWhiteSpace(member.Nickname) ? member.SpeciesName : member.Nickname;
                var applied = 0;

                foreach (var layout in provider.AllLayouts
                             .Where(l => l.Stride == PartyLayoutLocator.AuthoritativeStride))
                {
                    // El PID por delante y en CADA copia, no el número de hueco: las estructuras
                    // guardan el equipo en órdenes distintos y escribir por posición manda la
                    // corrección al Pokémon de al lado (§96).
                    for (var slot = 0; slot < PartySlots; slot++)
                    {
                        var at = layout.SlotAddress(slot);

                        if (writer.ReadAuthoritative(at) is { ChecksumValid: true } found
                            && found.PID == member.Pid
                            && found.Stat_HPCurrent > 0
                            && writer.SetLiveHp(at, 0, member.Pid).Applied)
                        {
                            applied++;
                        }
                    }
                }

                if (applied == 0)
                {
                    continue;
                }

                logger.LogWarning("{Pokemon} está caído y le habían devuelto los PS: al suelo otra vez"
                                  + " ({Copias} copias)", name, applied);

                Announce(() => DeathMarked?.Invoke(this, $"{name} está caído: se le quitan los PS otra vez."));
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
                $"{report.Marked} caído(s) se han quedado a 0 PS en la partida. Se verá al cargar."
                + (report.Boxed > 0
                    ? $" {report.Boxed} está(n) en una caja y ahí no se puede marcar."
                    : string.Empty)));

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
