using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Services;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// Stops the reload tricks of a locke and writes down, for the organiser only, the ones it cannot stop (2026-09-26).
/// </summary>
/// <remarks>
/// <para>
/// A death and a spent route already survive a reload: both live in the run, the dead are written back at zero HP and
/// the balls withheld again. What was left: save states (their keys go and any state that appears is moved out and
/// flagged), playing or restoring the save without PermaLocke (the save's own play time against the last one watched),
/// and closing the emulator in the middle of a battle before the death is seen (the exit code tells a close from a
/// crash, and a crash is never flagged). Closing PermaLocke with the game open is refused by the window, and killing
/// PermaLocke takes the emulator with it (<see cref="EmulatorJob"/>).
/// </para>
/// <para>
/// Off with <c>--sin-juego</c>: a copy to look at screens must not flag the real game.
/// </para>
/// </remarks>
public sealed class IntegrityGuard(AzaharInstallation azahar, PlayerSave save, IntegrityService integrity,
    GameLinkMonitor monitor, AppPaths paths, ILogger<IntegrityGuard> logger)
{
    /// <summary>How close to the end of the emulator a battle reading has to be for the battle to have been left open.</summary>
    private static readonly TimeSpan BattleWindow = TimeSpan.FromSeconds(6);

    private const string SeenFile = "partida-vista.json";

    private sealed record Seen(double PlaySeconds, DateTimeOffset At);

    /// <summary>False in a copy started with <c>--sin-juego</c>.</summary>
    public bool Enabled { get; set; }

    private string StatesFolder => Path.Combine(paths.Saves, "estados-retirados");

    /// <summary>Before the emulator opens: no state keys and no states within reach.</summary>
    public void PrepareLaunch(AzaharLocation location)
    {
        if (!Enabled)
        {
            return;
        }

        azahar.DisableSaveStates(location);

        // Los que ya estaban son de antes de esta sesión: se retiran sin más, porque cargarlos es la trampa.
        foreach (var state in azahar.SetAsideSaveStates(location, StatesFolder))
        {
            logger.LogWarning("Estado guardado {State} retirado antes de abrir el juego", state);
        }
    }

    /// <summary>While playing: a state that appears is moved out before it can be loaded, and flagged.</summary>
    public async Task WatchStatesAsync(Guid run, AzaharLocation location)
    {
        if (!Enabled)
        {
            return;
        }

        foreach (var state in azahar.SetAsideSaveStates(location, StatesFolder))
        {
            logger.LogWarning("Estado guardado {State} creado jugando: retirado y anotado", state);
            await integrity.FlagAsync(run, IntegrityKinds.SaveState,
                "Ha guardado un estado del emulador mientras jugaba. PermaLocke lo ha retirado antes de que pudiera cargarlo.",
                new Dictionary<string, string> { ["fichero"] = state });
        }
    }

    /// <summary>
    /// Before a session, or when PermaLocke opens: the save's play time against the last one PermaLocke saw.
    /// </summary>
    /// <param name="gameAlreadyOpen">The emulator was running before PermaLocke started, so nobody was watching it.</param>
    public async Task CheckPlaytimeAsync(Guid run, bool gameAlreadyOpen = false)
    {
        if (!Enabled)
        {
            return;
        }

        if (gameAlreadyOpen)
        {
            await integrity.FlagAsync(run, IntegrityKinds.PlayedOutside,
                "El juego ya estaba abierto al abrir PermaLocke: se ha jugado sin que nadie vigilara.");
            return;
        }

        if (await Task.Run(save.PlayTime) is not { } now)
        {
            return;
        }

        var seen = await ReadSeenAsync(run);

        if (seen is not null && IntegrityService.ComparePlaytime(TimeSpan.FromSeconds(seen.PlaySeconds), now) is { } verdict)
        {
            var minutes = (int)Math.Round(verdict.Gap.TotalMinutes);
            var last = TimeSpan.FromSeconds(seen.PlaySeconds);

            logger.LogWarning("Partida {Kind}: {Last} vistas, {Now} ahora", verdict.Kind, last, now);
            await integrity.FlagAsync(run, verdict.Kind,
                verdict.Kind == IntegrityKinds.PlayedOutside
                    ? $"Su partida tiene {minutes} min más de juego que la última vez que PermaLocke la vio: ha jugado sin PermaLocke."
                    : $"Su partida tiene {minutes} min menos de juego que la última vez que PermaLocke la vio: la ha restaurado de una copia.",
                new Dictionary<string, string>
                {
                    ["antes"] = $"{(int)last.TotalHours}:{last:mm\\:ss}",
                    ["ahora"] = $"{(int)now.TotalHours}:{now:mm\\:ss}",
                    ["vista"] = seen.At.ToString("O")
                });
        }

        await WriteSeenAsync(run, now);
    }

    /// <summary>After a session PermaLocke watched: what the save says now is the new last seen.</summary>
    public async Task RememberPlaytimeAsync(Guid run)
    {
        if (Enabled && await Task.Run(save.PlayTime) is { } now)
        {
            await WriteSeenAsync(run, now);
        }
    }

    /// <summary>When the emulator has gone: flags it if it was closed, not crashed, with a battle still open.</summary>
    public async Task CheckAbandonAsync(Guid run, uint exitCode, bool askedByPermaLocke, DateTimeOffset endedBy)
    {
        if (!Enabled || monitor.LastReadingInBattle is not { } lastBattle || endedBy - lastBattle > BattleWindow)
        {
            return;
        }

        if (IntegrityService.HowClosed(exitCode, askedByPermaLocke) is not { } how)
        {
            logger.LogInformation("Azahar cayó en mitad de un combate (0x{Code:X8}): un fallo no se anota", exitCode);
            return;
        }

        logger.LogWarning("Azahar {How} en mitad de un combate", how);
        await integrity.FlagAsync(run, IntegrityKinds.BattleAbandoned,
            $"Emulador {how} en mitad de un combate.",
            new Dictionary<string, string> { ["salida"] = $"0x{exitCode:X8}", ["combate"] = lastBattle.ToString("O") });
    }

    private string SeenPath(Guid run) => Path.Combine(paths.Saves, run.ToString("N"), SeenFile);

    private async Task<Seen?> ReadSeenAsync(Guid run)
    {
        try
        {
            return File.Exists(SeenPath(run))
                ? JsonSerializer.Deserialize<Seen>(await File.ReadAllTextAsync(SeenPath(run)))
                : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "No se ha podido leer {Path}", SeenPath(run));
            return null;
        }
    }

    private async Task WriteSeenAsync(Guid run, TimeSpan playTime)
    {
        // Solo si la run tiene su carpeta, como las sesiones: no resucita la de una run borrada.
        if (!Directory.Exists(Path.GetDirectoryName(SeenPath(run))))
        {
            return;
        }

        await File.WriteAllTextAsync(SeenPath(run),
            JsonSerializer.Serialize(new Seen(playTime.TotalSeconds, DateTimeOffset.Now)));
    }
}
