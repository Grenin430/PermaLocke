using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// Puts back the ability PermaLocke gave a Pokémon when the game has recomputed it (§237).
/// </summary>
/// <remarks>
/// <para>
/// The game recomputes a Pokémon's ability from its species' table whenever its form changes (<c>ChangeFormNo</c>: Mega
/// Evolution reverting after a battle, Aegislash, Darmanitan...) and whenever its species does (<c>ChangeMonsNo</c>:
/// evolution). A gacha Heracross with Espada Indómita left a Mega Evolution with Gran Encanto, and a Greninja would lose
/// the ability its Frogadier was given. <see cref="AbilityLedger"/> remembers what PermaLocke wrote; this runs when the save
/// can be written, with the game closed: when the game closes, and before it opens. The history fills the ledger for what was
/// dealt before it existed (<see cref="AbilityBackfill"/>).
/// </para>
/// <para>
/// Only what PermaLocke itself dealt (gacha, wonder trade, nursery, the roulette's ability faces) is ever put back: an
/// ability the game gave on its own is the game's.
/// </para>
/// </remarks>
public sealed class AbilityKeeper(IRunContext runs, IPokemonRepository pokemon, IEventStore events,
    ISpeciesStatsCatalog stats, PlayerSave save, AppPaths paths, IClock clock, ILogger<AbilityKeeper> logger)
{
    private bool _backfilled;

    /// <summary>The game has closed or is about to open: restores what it recomputed. Returns what was put back.</summary>
    public async Task<IReadOnlyList<SaveAbilityKeeper.Fix>> RestoreAsync(CancellationToken ct = default)
    {
        try
        {
            if (runs.Current is not { } run || save.Find() is not { } path || save.IsGameLoaded())
            {
                return [];
            }

            var entries = await pokemon.GetAllAsync(run.Id, ct);
            var history = await events.GetAllAsync(run.Id, ct);

            // La historia solo se lee una vez por sesión: lo que reparte la app después ya lo apunta el libro al escribirlo.
            var learned = _backfilled ? 0 : AbilityLedger.Merge(AbilityBackfill.Intended(history, entries, stats.Abilities));
            _backfilled = true;
            if (learned > 0) logger.LogInformation("Habilidades repartidas antes del libro: {Count} apuntadas desde la historia", learned);

            var fixes = SaveAbilityKeeper.Restore(path, paths.SaveBackups, AbilityLedger.Snapshot());

            foreach (var fix in fixes)
            {
                logger.LogWarning("{Name} ({Pid:X8}): el juego le había puesto la habilidad {Was}; se le devuelve la {Should}",
                    fix.Name, fix.Pid, fix.Was, fix.Should);

                var entry = entries.FirstOrDefault(e => e.Pid == fix.Pid);
                await events.AppendAsync(new GameEvent
                {
                    Id = Guid.NewGuid(),
                    RunId = run.Id,
                    Timestamp = clock.Now,
                    Type = GameEventType.AbilityRestored,
                    Source = EventSource.AutoDetect,
                    Actor = run.PlayerName,
                    Description = $"{(entry?.Nickname ?? entry?.SpeciesName ?? fix.Name)}: el juego había cambiado su habilidad al evolucionar o cambiar de forma; se le devuelve la que se le dio.",
                    PokemonId = entry?.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["pid"] = fix.Pid.ToString("X8"),
                        ["antes"] = fix.Was.ToString(),
                        ["despues"] = fix.Should.ToString()
                    }
                }, ct);
            }

            return fixes;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nunca impide cerrar ni abrir el juego: la próxima vez se vuelve a intentar.
            logger.LogWarning(ex, "No se han podido restaurar las habilidades en la partida");
            return [];
        }
    }

    /// <summary>The same, for a caller that cannot wait (the launch is synchronous).</summary>
    public int RestoreNow() => Task.Run(() => RestoreAsync()).GetAwaiter().GetResult().Count;
}
