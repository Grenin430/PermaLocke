using System.IO;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;
using PermaLocke.Randomizer.Output;

namespace PermaLocke.App.Services;

/// <summary>Where the world is right now.</summary>
public enum BattleModeState
{
    /// <summary>The randomized world is installed. Normal play.</summary>
    Playing,

    /// <summary>The world is set aside and the game is the cartridge. Ready to link-battle.</summary>
    InBattle,

    /// <summary>Nothing installed and nothing set aside: there is no randomization to swap.</summary>
    Nothing
}

/// <param name="Ok">Whether the swap actually happened.</param>
/// <param name="Message">What happened, or why it did not.</param>
public sealed record BattleModeResult(bool Ok, string Message);

/// <summary>
/// Sets the randomized world aside so the group can link-battle, and puts it back afterwards.
/// </summary>
/// <remarks>
/// <para>
/// A link battle is a lockstep simulation: both consoles compute the same battle and only the
/// orders travel. Two different randomizations therefore drift apart the moment a species has
/// different base stats or a different ability on each side — <b>measured, not assumed</b> (§80).
/// </para>
/// <para>
/// The alternative to this is turning off the two options that touch battle data for good, which
/// costs the shuffled stats and the random abilities for ever. This keeps the whole world
/// randomized and pays for battles with a swap instead.
/// </para>
/// <para>
/// <b>It MOVES the folder, it does not delete it.</b> The randomizer's own QUITAR deletes, which is
/// defensible because the world can be regenerated from <c>Randomized/</c> — but only while that
/// folder still exists, and 461 MB is exactly the kind of thing somebody clears out to make room.
/// A move is also instant, because it stays on the same volume: the world never travels.
/// </para>
/// <para>
/// It refuses while Azahar is answering. Mods are read when the game loads, so a swap under a
/// running emulator would either do nothing visible or move files out from under it.
/// </para>
/// </remarks>
public sealed class BattleModeService(
    AzaharInstallation azahar,
    IRunContext runContext,
    IEventStore events,
    IClock clock,
    PermaLocke.Infrastructure.AppPaths paths,
    ILogger<BattleModeService> logger)
{
    /// <summary>Where the world waits while the group is battling.</summary>
    /// <remarks>
    /// Inside Azahar's own <c>load\</c> so the move never crosses a volume, and named so nobody
    /// mistakes it for something the emulator loads: only <c>load\mods\&lt;titleid&gt;</c> is read.
    /// </remarks>
    private const string HoldingName = "permalocke-mundo-en-espera";

    /// <summary>Marca que dice que el mundo esta apartado, cuando hay un mod base debajo.</summary>
    /// <remarks>
    /// Con mod base la carpeta NO se mueve: se sobreescriben los ficheros randomizados con los del
    /// mod y luego al reves, asi que no hay ausencia que mirar. Las dos operaciones son
    /// idempotentes, de modo que una marca que se quedara colgada cuesta una copia de mas y nunca
    /// un mundo perdido.
    /// </remarks>
    private const string BattleMarker = "permalocke-en-combate.txt";

    /// <summary>El romfs del mod base, si el jugador tiene uno.</summary>
    private string? BaseRomfs =>
        Directory.Exists(Path.Combine(paths.Expansion, "romfs"))
            ? Path.Combine(paths.Expansion, "romfs")
            : null;

    private string? Generated => runContext.Current is { } run
        ? Path.Combine(paths.Randomized, $"seed-{run.Seed}")
        : null;

    private AzaharLocation? Where => azahar.Locate(AppContext.BaseDirectory);

    private string? ModFolder => Where is { } w
        ? AzaharInstallation.ModDirectory(w, LayeredFsMod.UltraMoonProgramId)
        : null;

    private string? Holding => Where is { } w
        ? Path.Combine(w.UserDirectory, "load", HoldingName)
        : null;

    public BattleModeState State
    {
        get
        {
            if (ModFolder is not { } mod || Holding is not { } held)
            {
                return BattleModeState.Nothing;
            }

            // Con mod base la carpeta nunca se va, asi que la ausencia no dice nada: lo dice la
            // marca, que solo existe mientras el mundo esta apartado.
            if (BaseRomfs is not null)
            {
                if (!Directory.Exists(Path.Combine(mod, "romfs")))
                {
                    return BattleModeState.Nothing;
                }

                return File.Exists(Path.Combine(mod, BattleMarker))
                    ? BattleModeState.InBattle
                    : BattleModeState.Playing;
            }

            if (Directory.Exists(Path.Combine(mod, "romfs")))
            {
                return BattleModeState.Playing;
            }

            return Directory.Exists(Path.Combine(held, "romfs"))
                ? BattleModeState.InBattle
                : BattleModeState.Nothing;
        }
    }

    /// <summary>Sets the world aside. The game becomes the cartridge until it is put back.</summary>
    public Task<BattleModeResult> PrepareAsync(CancellationToken ct = default) =>
        BaseRomfs is not null
            ? LayerAsync(toBattle: true, ct)
            : SwapAsync(ModFolder, Holding, toBattle: true, ct);

    /// <summary>Puts the world back exactly as it was.</summary>
    public Task<BattleModeResult> RestoreAsync(CancellationToken ct = default) =>
        BaseRomfs is not null
            ? LayerAsync(toBattle: false, ct)
            : SwapAsync(Holding, ModFolder, toBattle: false, ct);


    /// <summary>
    /// Con un mod base debajo, el mundo NO se aparta: se vuelve al mod SIN randomizar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es mejor que dejarlo en el cartucho, y la diferencia la decidió una medida. Ultra Luna
    /// original con un Pokémon de gen 8 o 9 encima no se cuelga, pero lo enseña como otra cosa:
    /// tipo Normal, sprite de Bulbasaur, estadísticas ajenas y «para subir de nivel: −985». Está
    /// calculando con una especie que no existe.
    /// </para>
    /// <para>
    /// Volviendo al mod en vez de al cartucho, los Pokémon nuevos siguen existiendo y los dos
    /// jugadores tienen exactamente los mismos datos de especie, que es lo único que el §80 midió
    /// como causa de la desincronización. La condición pasa de «los dos en vanilla» a <b>los dos
    /// con la misma versión del mod</b>, que es la que ya hacía falta para jugar.
    /// </para>
    /// <para>
    /// Se copia encima en vez de mover, así que no hay ausencia que mirar y hace falta una marca.
    /// Las dos direcciones son idempotentes: repetir una copia no rompe nada, de modo que una marca
    /// colgada cuesta una copia de más y nunca un mundo perdido.
    /// </para>
    /// </remarks>
    private async Task<BattleModeResult> LayerAsync(bool toBattle, CancellationToken ct)
    {
        if (ModFolder is not { } mod || BaseRomfs is not { } baseRomfs)
        {
            return new BattleModeResult(false, "No encuentro la carpeta de mods de Azahar.");
        }

        if (System.Diagnostics.Process.GetProcessesByName("azahar").Length > 0)
        {
            return new BattleModeResult(false,
                "Azahar está abierto. Ciérralo del todo antes: el juego lee los mods al arrancar, "
                + "así que un cambio con él abierto no serviría de nada.");
        }

        if (!Directory.Exists(Path.Combine(mod, "romfs")))
        {
            return new BattleModeResult(false, "No hay ningún mundo instalado.");
        }

        var marker = Path.Combine(mod, BattleMarker);

        if (!toBattle && Generated is not { } gen)
        {
            return new BattleModeResult(false,
                "No hay ninguna run cargada, así que no sé qué mundo devolver.");
        }

        try
        {
            if (toBattle)
            {
                // Los ficheros del mod ENCIMA de los randomizados: deshace lo nuestro sin tocar
                // los 2,5 GB de modelos, que son idénticos y el salto por fecha y tamaño evita.
                await Task.Run(() => ModInstaller.CopyTree(baseRomfs, Path.Combine(mod, "romfs"),
                    skipUnchanged: true), ct);

                var exefs = Path.Combine(paths.Expansion, "exefs");

                if (Directory.Exists(exefs))
                {
                    await Task.Run(() => ModInstaller.CopyTree(exefs, Path.Combine(mod, "exefs")), ct);
                }

                await File.WriteAllTextAsync(marker,
                    "El mundo randomizado está apartado para poder combatir. "
                    + "Devuélvelo desde PermaLocke, en COMBATES.", ct);
            }
            else
            {
                await Task.Run(() => ModInstaller.Install(Generated!, mod, baseRomfs,
                    Path.Combine(paths.Expansion, "exefs")), ct);

                File.Delete(marker);
            }

            // Se relee: no se da por hecho lo que no se ha vuelto a mirar.
            if (File.Exists(marker) != toBattle)
            {
                return new BattleModeResult(false,
                    "El cambio no ha quedado como debía. Mira la carpeta load de Azahar antes de "
                    + "volver a jugar.");
            }

            await RecordAsync(toBattle, ct);

            return new BattleModeResult(true, toBattle
                ? "Listo. Tu juego es ahora el mod SIN randomizar, así que los Pokémon de octava y "
                  + "novena siguen existiendo. Todos tenéis que llevar la misma versión del mod."
                : "Tu mundo ha vuelto. Los encuentros y los entrenadores son otra vez los tuyos.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el cambio de modo combate con mod base");
            return new BattleModeResult(false,
                "No se ha podido cambiar. El detalle está en la carpeta Logs.");
        }
    }
    private async Task<BattleModeResult> SwapAsync(string? from, string? to, bool toBattle,
        CancellationToken ct)
    {
        if (from is null || to is null)
        {
            return new BattleModeResult(false, "No encuentro la carpeta de mods de Azahar.");
        }

        // Los mods se leen al cargar el juego, asi que cambiarlos con el emulador en marcha no
        // haria nada visible; y mover ficheros por debajo de un proceso que los tiene abiertos es
        // pedir problemas.
        //
        // Se mira el PROCESO y no el RPC a proposito: el caso peligroso es justo Azahar abierto
        // con el servidor apagado, que por RPC se leeria como "no esta".
        if (System.Diagnostics.Process.GetProcessesByName("azahar").Length > 0)
        {
            return new BattleModeResult(false,
                "Azahar está abierto. Ciérralo del todo antes: el juego lee los mods al arrancar, "
                + "así que un cambio con él abierto no serviría de nada.");
        }

        if (!Directory.Exists(Path.Combine(from, "romfs")))
        {
            return new BattleModeResult(false, toBattle
                ? "No hay ninguna randomización instalada que apartar."
                : "No hay ningún mundo guardado que devolver.");
        }

        try
        {
            if (Directory.Exists(to))
            {
                Directory.Delete(to, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            Directory.Move(from, to);

            // Se relee: no se da por hecho un movimiento que no se ha vuelto a ver.
            if (!Directory.Exists(Path.Combine(to, "romfs")) || Directory.Exists(Path.Combine(from, "romfs")))
            {
                return new BattleModeResult(false,
                    "El cambio no ha quedado como debía. Mira la carpeta load de Azahar antes de "
                    + "volver a jugar.");
            }

            await RecordAsync(toBattle, ct);

            logger.LogInformation("Modo combate: {Mode}", toBattle ? "preparado" : "restaurado");

            return new BattleModeResult(true, toBattle
                ? "Listo. Tu juego es ahora el original, igual que el de los demás. Abre Azahar y a pelear."
                : "Tu mundo está de vuelta. Abre Azahar y sigue donde lo dejaste.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el cambio de modo combate");
            return new BattleModeResult(false,
                "No se ha podido cambiar. El detalle está en la carpeta Logs.");
        }
    }

    /// <summary>
    /// Leaves the swap in the history.
    /// </summary>
    /// <remarks>
    /// Rule 4: nothing about the run changes in silence. Which world a run was being played in when
    /// something happened is exactly the sort of question a competition ends up asking.
    /// </remarks>
    private async Task RecordAsync(bool toBattle, CancellationToken ct)
    {
        if (runContext.Current is not { } run)
        {
            return;
        }

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.BattleModeChanged,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = toBattle
                ? "Mundo apartado para combatir por link: el juego vuelve a ser el original."
                : "Mundo restaurado: se vuelve a jugar la versión randomizada.",
            Data = new Dictionary<string, string> { ["modo"] = toBattle ? "combate" : "normal" }
        }, ct);
    }
}
