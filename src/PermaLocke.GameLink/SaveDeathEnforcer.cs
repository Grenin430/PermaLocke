using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Data;
using PKHeX.Core;

namespace PermaLocke.GameLink;

/// <param name="Alive">Registros caídos que siguen sin marcar en la partida.</param>
/// <param name="AlreadyMarked">Caídos que ya son Shedinja.</param>
/// <param name="Missing">Caídos que no están en la partida: intercambiados, liberados o soltados.</param>
/// <param name="Marked">Cuántos se han transformado en esta pasada. Cero al solo inspeccionar.</param>
/// <param name="Message">Una línea para la pantalla.</param>
public sealed record DeathEnforcementReport(
    int Alive, int AlreadyMarked, int Missing, int Marked, string Message);

/// <summary>
/// Makes the run's dead <b>permanently</b> dead inside the save file.
/// </summary>
/// <remarks>
/// <para>
/// The watcher already writes the marker into the running game the moment it sees a Pokémon at
/// zero HP, but that is a write into <em>memory</em>: it survives only if the player saves
/// afterwards, and it never happens at all for a death the watcher could not see — the application
/// closed, the link down, or the game healing the party before the next read, which is what a Totem
/// trial does.
/// </para>
/// <para>
/// This closes all of those. It works on the save file, so the game has to be shut, and what it
/// writes is on disk for good. It is <b>idempotent</b>: a Pokémon already marked is skipped, so
/// running it twice costs nothing and running it after every session is the sane habit.
/// </para>
/// <para>
/// Matching is by <b>PID</b>, never by species or nickname. A dead Pokémon has usually already been
/// transformed into something else, and a run holds several Shedinja called MUERTO — matching by
/// what it looks like would hit the wrong one.
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
            return new DeathEnforcementReport(0, 0, 0, 0,
                "No hay ningún caído con PID que marcar.");
        }

        if (save.Find() is not { } path)
        {
            return new DeathEnforcementReport(0, 0, 0, 0,
                "No encuentro la partida. ¿Has jugado alguna vez con este Azahar?");
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

        var alreadyMarked = found.Count(f => DeathMark.IsMarked(f.Pokemon));
        var pending = found.Where(f => !DeathMark.IsMarked(f.Pokemon)).ToList();
        var missing = wanted.Count - found.Count;

        if (!write || pending.Count == 0)
        {
            return new DeathEnforcementReport(pending.Count, alreadyMarked, missing, 0,
                pending.Count == 0
                    ? $"Nada que hacer: los {alreadyMarked} caídos que están en la partida ya son Shedinja."
                    : $"{pending.Count} caído(s) siguen enteros en la partida y se pueden marcar.");
        }

        Backup(path);

        foreach (var (pokemon, box, slot) in pending)
        {
            DeathMark.Apply(pokemon);

            if (box == BoxedPokemon.PartyBox)
            {
                file.SetPartySlotAtIndex(pokemon, slot, PokemonBuilder.InPlace);
            }
            else
            {
                file.SetBoxSlotAtIndex(pokemon, box, slot, PokemonBuilder.InPlace);
            }
        }

        File.WriteAllBytes(path, file.Write().ToArray());

        // Se relee, que es lo único que convierte «escrito» en «hecho».
        if (!SaveUtil.TryGetSaveFile(path, out var reread) || reread is not SAV7USUM back)
        {
            throw new InvalidDataException("La partida dejó de leerse tras escribirla.");
        }

        var still = Everything(back)
            .Count(e => wanted.Contains(e.Pokemon.PID) && !DeathMark.IsMarked(e.Pokemon));

        if (still > 0)
        {
            throw new InvalidDataException(
                $"{still} caído(s) siguen sin marcar después de escribir. La copia previa está en "
                + backupFolder + ".");
        }

        logger.LogInformation("{Count} caídos marcados como Shedinja en la partida", pending.Count);

        return new DeathEnforcementReport(0, alreadyMarked + pending.Count, missing, pending.Count,
            $"{pending.Count} marcado(s) como Shedinja. Es permanente: está escrito en la partida.");
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
