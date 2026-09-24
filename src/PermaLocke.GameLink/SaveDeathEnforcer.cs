using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Data;
using PKHeX.Core;

namespace PermaLocke.GameLink;

/// <param name="Alive">Registros caídos que siguen en pie en el equipo de la partida.</param>
/// <param name="AlreadyMarked">Caídos del equipo que ya están a 0 PS.</param>
/// <param name="Missing">Caídos que no están en la partida: intercambiados, liberados o soltados.</param>
/// <param name="Boxed">
/// Caídos que están en una caja. No se pueden marcar y se dicen aparte: un Pokémon en caja no lleva
/// estadísticas de combate, así que escribirle cero no cambiaría nada que el juego lea.
/// </param>
/// <param name="Marked">Cuántos se han tumbado en esta pasada. Cero al solo inspeccionar.</param>
/// <param name="Message">Una línea para la pantalla.</param>
public sealed record DeathEnforcementReport(
    int Alive, int AlreadyMarked, int Missing, int Boxed, int Marked, string Message);

/// <summary>
/// Leaves the run's dead at zero HP inside the save file, as themselves.
/// </summary>
/// <remarks>
/// <para>
/// This is the only thing that marks a death now. It used to be a backstop behind a marker the
/// application wrote into the running game, and that marker is gone: the player asked for the
/// Shedinja to go, and §98 measured that HP written into memory never reaches the game anyway —
/// 120 written into the party mirror, the screen still saying 128. The save is the door that works,
/// and it was checked against the screen: 55 HP written into the file came up as 55 on load.
/// </para>
/// <para>
/// So the game has to be shut, which means marking happens when a session ends rather than at the
/// moment somebody falls. It is <b>idempotent</b>: one already down is skipped, so running it twice
/// costs nothing.
/// </para>
/// <para>
/// <b>Only the party can be marked</b>, and the ones in boxes are counted and named rather than
/// quietly skipped. A boxed Pokémon carries no battle stats (§32), so a zero written there changes
/// nothing and it would come out at full health — a marker that pretends to work is worse than an
/// absent one.
/// </para>
/// <para>
/// Matching is by <b>PID</b>, never by species or nickname. This run holds a real Shedinja called
/// MUERTO that was never a death (§59), which is exactly what matching on appearance would hit.
/// </para>
/// </remarks>
public sealed class SaveDeathEnforcer(
    PlayerSave save, string backupFolder, ILogger<SaveDeathEnforcer> logger)
{
    /// <summary>Counts what would change, writing nothing.</summary>
    public DeathEnforcementReport Inspect(IReadOnlyList<PokemonEntry> registered) =>
        Run(registered, write: false);

    /// <summary>Marks them, after copying the save and re-reading what was written.</summary>
    public DeathEnforcementReport Apply(IReadOnlyList<PokemonEntry> registered) =>
        Run(registered, write: true);

    private DeathEnforcementReport Run(IReadOnlyList<PokemonEntry> registered, bool write)
    {
        var wanted = registered
            .Where(p => p.Status == PokemonStatus.Dead && p.Pid is not null and not 0)
            .Select(p => p.Pid!.Value)
            .ToHashSet();

        if (wanted.Count == 0)
        {
            return new DeathEnforcementReport(0, 0, 0, 0, 0,
                "No hay caídos que marcar.");
        }

        if (save.Find() is not { } path)
        {
            return new DeathEnforcementReport(0, 0, 0, 0, 0,
                "No se encuentra tu partida de Ultra Luna.");
        }

        // Con el juego abierto no se escribe, y la comprobación va AQUÍ y no en quien llama: desde
        // que esto se hace solo al cerrar el emulador, el que llama es un temporizador y no una
        // persona leyendo un aviso. El emulador reescribiría el fichero al guardar y se perdería.
        if (write && save.IsGameLoaded())
        {
            return new DeathEnforcementReport(0, 0, 0, 0, 0,
                "El juego está abierto. Guarda y cierra Azahar.");
        }

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM file)
        {
            throw new InvalidDataException($"{path} no es una partida de Ultra Sol/Ultra Luna.");
        }

        var found = new List<(PK7 Pokemon, int Box, int Slot)>();

        foreach (var (pokemon, box, slot) in Everything(file))
        {
            if (wanted.Contains(pokemon.PID))
            {
                found.Add((pokemon, box, slot));
            }
        }

        // Los de caja se cuentan y se dicen, no se marcan: ahí un cero no significa nada.
        var boxed = found.Count(f => !DeathMark.WorksIn(f.Box));
        var inParty = found.Where(f => DeathMark.WorksIn(f.Box)).ToList();

        var alreadyMarked = inParty.Count(f => DeathMark.IsMarked(f.Pokemon));
        var pending = inParty.Where(f => !DeathMark.IsMarked(f.Pokemon)).ToList();
        var missing = wanted.Count - found.Count;

        if (!write || pending.Count == 0)
        {
            return new DeathEnforcementReport(pending.Count, alreadyMarked, missing, boxed, 0,
                pending.Count == 0
                    ? "No hay nada que hacer."
                    : $"Hay {pending.Count} caído(s) por dejar a 0 PS.");
        }

        Backup(path);

        foreach (var (pokemon, _, slot) in pending)
        {
            DeathMark.Apply(pokemon);
            file.SetPartySlotAtIndex(pokemon, slot, PokemonBuilder.InPlace);
        }

        File.WriteAllBytes(path, file.Write().ToArray());

        // Se relee, que es lo único que convierte «escrito» en «hecho».
        if (!SaveUtil.TryGetSaveFile(path, out var reread) || reread is not SAV7USUM back)
        {
            throw new InvalidDataException("La partida dejó de leerse tras escribirla.");
        }

        // Solo el equipo, que es lo único que se ha escrito: contar las cajas aquí haría fallar
        // una escritura correcta por algo que nunca se intentó.
        var still = Everything(back)
            .Count(e => wanted.Contains(e.Pokemon.PID)
                        && DeathMark.WorksIn(e.Box)
                        && !DeathMark.IsMarked(e.Pokemon));

        if (still > 0)
        {
            throw new InvalidDataException(
                $"{still} caído(s) siguen en pie después de escribir. La copia previa está en "
                + backupFolder + ".");
        }

        logger.LogInformation("{Count} caídos a 0 PS en la partida", pending.Count);

        return new DeathEnforcementReport(0, alreadyMarked + pending.Count, missing, boxed, pending.Count,
            $"{pending.Count} caído(s) dejados a 0 PS.");
    }

    /// <summary>Party and every box, so a dead Pokémon is found wherever it was left.</summary>
    private static IEnumerable<(PK7 Pokemon, int Box, int Slot)> Everything(SAV7USUM file)
    {
        for (var slot = 0; slot < 6; slot++)
        {
            if (file.GetPartySlotAtIndex(slot) is PK7 { Species: > 0 } member)
            {
                yield return (member, BoxedPokemon.PartyBox, slot);
            }
        }

        for (var box = 0; box < file.BoxCount; box++)
        {
            for (var slot = 0; slot < file.BoxSlotCount; slot++)
            {
                if (file.GetBoxSlotAtIndex(box, slot) is PK7 { Species: > 0 } stored)
                {
                    yield return (stored, box, slot);
                }
            }
        }
    }

    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);
        var to = Path.Combine(backupFolder,
            $"main-antes-de-marcar-caidos-{DateTimeOffset.Now:yyyyMMdd-HHmmss}");
        File.Copy(path, to, overwrite: true);
        logger.LogInformation("Copia de la partida antes de marcar caídos: {Path}", to);
    }
}
