using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;
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
    BallControlService ballControl,
    EncounterService encounters,
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

    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;

    /// <summary>Latest snapshot, or null before the first read completes.</summary>
    public GameSnapshot? Latest { get; private set; }

    public event EventHandler<GameSnapshot>? SnapshotChanged;

    /// <summary>Pokémon seen in the game that the run has not registered yet.</summary>
    public event EventHandler<IReadOnlyList<LivePartyMember>>? UnregisteredDetected;

    /// <summary>Raised when the whole party went down, so a screen can say so out loud.</summary>
    public event EventHandler<PenaltyResult>? TeamWiped;

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
    /// Turns the difference between game and run into action: deaths are recorded on the spot,
    /// while unregistered Pokémon are only announced, because registering one needs a zone and
    /// the zone is not readable from memory yet.
    /// </summary>
    private async Task InspectAsync(GameSnapshot snapshot)
    {
        if (runContext.Current is not { } run)
        {
            return;
        }

        var findings = await watcher.InspectAsync(run.Id, snapshot, _stopping.Token);

        foreach (var dead in findings.Fainted)
        {
            logger.LogWarning("Muerte detectada: {Pokemon}", dead.Nickname ?? dead.SpeciesName);
            await watcher.RecordDeathAsync(dead, run.PlayerName, _stopping.Token);
            ApplyDeathInGame(snapshot, dead);
        }


        if (findings.NewMembers.Count > 0)
        {
            UnregisteredDetected?.Invoke(this, findings.NewMembers);
        }

        await CheckWipeAsync(run, snapshot);
        await EnforceLevelCapAsync(run, snapshot);
        await ApplyBallRuleAsync(run);
    }

    /// <summary>
    /// Charges the extra penalty when the whole party goes down at once.
    /// </summary>
    /// <remarks>
    /// After the individual deaths, not before: each of them costs on its own, and the wipe is
    /// charged on top for the party falling as a whole.
    /// </remarks>
    private async Task CheckWipeAsync(Run run, GameSnapshot snapshot)
    {
        var result = await watcher.CheckWipeAsync(run.Id, run.PlayerName, snapshot, _stopping.Token);

        if (result is null)
        {
            return;
        }

        logger.LogWarning("Equipo caído. Penalización: {Points} puntos{Capped}. Saldo: {Balance}",
            result.Points, result.Capped ? " (tope alcanzado)" : string.Empty, result.NewBalance);

        TeamWiped?.Invoke(this, result);
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
    /// Brings anything above the cap back down, in the game and in the history.
    /// </summary>
    /// <remarks>
    /// Applied to every copy of the party for the same reason as the death transform. The level
    /// on screen does not change until the next battle starts: it is a derived field the game
    /// recomputes then, which is exactly the moment the run's rules describe.
    /// </remarks>
    private async Task EnforceLevelCapAsync(Run run, GameSnapshot snapshot)
    {
        if (await progress.CurrentCapAsync(run, _stopping.Token) is not { } cap || provider.AllLayouts.Count == 0)
        {
            return;
        }

        foreach (var member in snapshot.Party.Where(m => m.Level > cap))
        {
            var applied = provider.AllLayouts
                .Count(layout => writer.EnforceLevelCap(layout.SlotAddress(member.Slot), cap));

            if (applied == 0)
            {
                continue;
            }

            logger.LogWarning("{Pokemon} estaba a nivel {Level}, cap {Cap}. Corregido en {Copies} copias",
                member.SpeciesName, member.Level, cap, applied);

            await events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = clock.Now,
                Type = GameEventType.LevelCapEnforced,
                Source = EventSource.AutoDetect,
                Actor = run.PlayerName,
                Description = $"{member.SpeciesName} superaba el cap ({member.Level} > {cap}). "
                              + "Devuelto al cap; se verá al entrar en el siguiente combate.",
                Data = new Dictionary<string, string>
                {
                    ["nivel"] = member.Level.ToString(),
                    ["cap"] = cap.ToString(),
                    ["hueco"] = member.Slot.ToString()
                }
            }, _stopping.Token);
        }
    }

    /// <summary>
    /// Turns the dead Pokémon into the run's marker inside the game itself.
    /// </summary>
    /// <remarks>
    /// Matching is by PID so the right slot is hit even after nicknames or evolutions. If the
    /// party has not been located yet, or the PID is not in it, nothing is written: a wrong
    /// slot would destroy a living Pokémon.
    /// </remarks>
    private void ApplyDeathInGame(GameSnapshot snapshot, PokemonEntry dead)
    {
        if (provider.AllLayouts.Count == 0 || dead.Pid is not { } pid)
        {
            return;
        }

        var member = snapshot.Party.FirstOrDefault(m => m.Pid == pid);

        if (member is null)
        {
            logger.LogWarning("No encuentro en el equipo el PID {Pid} para transformarlo", pid);
            return;
        }

        try
        {
            // Written into every copy: the one the game reads is among them, and the rest are
            // refreshed from it anyway, so hitting all of them is both safe and sufficient.
            var applied = provider.AllLayouts
                .Count(layout => writer.ApplyDeath(layout.SlotAddress(member.Slot), new DeathTransform()));

            logger.LogInformation("{Pokemon} transformado en el juego (hueco {Slot}, {Applied} copias)",
                dead.SpeciesName, member.Slot, applied);
        }

        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo transformar a {Pokemon} en el juego", dead.SpeciesName);
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
    }
}
