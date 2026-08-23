using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <param name="Where">Where it lives, for the player to check before believing any of this.</param>
/// <param name="Species">Species name, in Spanish, for the same reason.</param>
/// <param name="Pid">The personality value it now has.</param>
public sealed record PidAssignment(string Where, string Species, uint Pid);

/// <param name="Total">Pokémon looked at, boxes and party together.</param>
/// <param name="WithoutPid">How many of them carried no personality value at all.</param>
/// <param name="Given">One line per Pokémon fixed.</param>
/// <param name="Written">False for a dry run, or when there was nothing to do.</param>
public sealed record PidRepairReport(
    int Total,
    int WithoutPid,
    IReadOnlyList<PidAssignment> Given,
    bool Written,
    string Message);

/// <summary>
/// Gives a personality value to the Pokémon PermaLocke delivered without one.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Data.PokemonBuilder"/> never set the PID, and a fresh <see cref="PK7"/> starts at
/// zero, so everything the gacha and the wonder trade had ever handed over shared the same one.
/// The PID is what identifies a Pokémon for life — it survives nicknames, levels and evolutions —
/// and it is the only thing <c>GameWatcher</c> matches the live party against. A party of zeros is
/// a party the run cannot recognise, which is why a run with a hundred and eighty Pokémon had
/// never recorded a single death.
/// </para>
/// <para>
/// The builder fills it in from now on, but that reaches nothing already written: those Pokémon
/// are in the boxes. This is for them.
/// </para>
/// <para>
/// Shininess is preserved exactly. A random PID comes out shiny about one time in four thousand,
/// so it is rolled first and corrected afterwards, and a Pokémon that was shiny stays shiny. Every
/// other visible thing about a gen 7 Pokémon — species, nature, ability, IVs, gender, name — lives
/// in its own field and is not touched.
/// </para>
/// </remarks>
public sealed class SavePidRepair(PlayerSave save, string backupFolder, ILogger<SavePidRepair> logger)
{
    /// <summary>Looks at the player's save without writing to it.</summary>
    public PidRepairReport Inspect() => Run(write: false);

    /// <summary>Fixes the player's save. Refuses while the game is loaded, like every other write.</summary>
    public PidRepairReport Repair() => Run(write: true);

    private PidRepairReport Run(bool write)
    {
        var path = save.Find();

        if (path is null)
        {
            return new PidRepairReport(0, 0, [], false,
                "No se encuentra la partida de Ultra Luna. ¿Has jugado y guardado alguna vez con este emulador?");
        }

        if (write && save.IsGameLoaded())
        {
            return new PidRepairReport(0, 0, [], false,
                "El juego está abierto en el emulador. Guarda la partida y cierra Azahar: mientras "
                + "esté cargado, el emulador reescribiría el save y la reparación se perdería.");
        }

        return RepairIn(path, write);
    }

    /// <summary>
    /// Works on one specific file, so the whole repair can be exercised against a copy with no
    /// emulator anywhere near it.
    /// </summary>
    public PidRepairReport RepairIn(string path, bool write)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
            {
                return new PidRepairReport(0, 0, [], false,
                    $"El fichero de partida no se ha podido leer como Ultra Luna: {path}");
            }

            var (total, given) = Apply(game);

            if (given.Count == 0)
            {
                return new PidRepairReport(total, 0, [], false,
                    $"Los {total} Pokémon de la partida ya tienen PID. No hay nada que reparar.");
            }

            if (!write)
            {
                return new PidRepairReport(total, given.Count, given, false,
                    $"{given.Count} de {total} Pokémon no tienen PID. Nada escrito todavía.");
            }

            Backup(path);
            File.WriteAllBytes(path, game.Write().ToArray());

            // Se relee del disco: ni un Pokémon queda a cero, y ninguno comparte PID con otro.
            var (left, repeated) = Check(path);

            if (left != 0 || repeated != 0)
            {
                return new PidRepairReport(total, given.Count, given, true,
                    $"Se escribió la partida pero al releerla quedan {left} sin PID y {repeated} "
                    + $"repetidos. La copia de seguridad está en {backupFolder}.");
            }

            logger.LogInformation("PID repartidos: {Count} de {Total} Pokémon", given.Count, total);

            return new PidRepairReport(total, given.Count, given, true,
                $"{given.Count} Pokémon reciben un PID propio, y al releer la partida no queda "
                + "ninguno a cero ni repetido.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la reparación de PID de {Path}", path);

            return new PidRepairReport(0, 0, [], false,
                "No se pudo reparar la partida. El detalle está en la carpeta Logs.");
        }
    }

    /// <summary>
    /// Gives a PID to everything without one in an already-loaded save, and says what it gave.
    /// </summary>
    /// <remarks>
    /// Separate from the file so the repair itself can be tested, the same split the name repair
    /// uses: PKHeX does not recognise a save built in a test and written to disk.
    /// </remarks>
    public static (int Total, IReadOnlyList<PidAssignment> Given) Apply(SAV7USUM game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var names = GameInfo.GetStrings("es").specieslist;
        var given = new List<PidAssignment>();
        var total = 0;

        // Lo ya usado, para no repartir dos veces el mismo número: un PID repetido reproduce el
        // problema que esto viene a arreglar, solo que con dos Pokémon en vez de con ciento
        // cuarenta y nueve.
        var used = Everything(game).Select(slot => slot.Pokemon.PID).Where(pid => pid != 0).ToHashSet();

        foreach (var (pokemon, where, put) in Everything(game))
        {
            total++;

            if (pokemon.PID != 0)
            {
                continue;
            }

            Assign(pokemon, used);
            used.Add(pokemon.PID);

            given.Add(new PidAssignment(where, Label(names, pokemon), pokemon.PID));
            put(pokemon);
        }

        return (total, given);
    }

    /// <summary>Rolls a PID that nobody else has, keeping the Pokémon as shiny as it was.</summary>
    private static void Assign(PK7 pokemon, HashSet<uint> used)
    {
        var shiny = pokemon.IsShiny;

        do
        {
            pokemon.PID = Random32();
            pokemon.SetIsShiny(shiny);
        }
        while (pokemon.PID == 0 || used.Contains(pokemon.PID));

        if (pokemon.EncryptionConstant == 0)
        {
            pokemon.EncryptionConstant = Random32();
        }

        pokemon.RefreshChecksum();
    }

    private static uint Random32() => (uint)Random.Shared.NextInt64(0, uint.MaxValue + 1L);

    /// <summary>
    /// Every Pokémon in the save, with the way to put each one back where it came from.
    /// </summary>
    /// <remarks>
    /// Boxes and party are different stores, and a Pokémon goes back to the store it came out of.
    /// It goes back with <see cref="PokemonBuilder.InPlace"/>: PKHeX treats putting a Pokémon in a
    /// box as <em>acquiring</em> it, and left at the default this would add a hundred and fifty
    /// captures, Poké Balls used and wild battles that never happened (§42).
    /// </remarks>
    private static IEnumerable<(PK7 Pokemon, string Where, Action<PK7> Put)> Everything(SAV7USUM game)
    {
        for (var box = 0; box < game.BoxCount; box++)
        {
            for (var slot = 0; slot < game.BoxSlotCount; slot++)
            {
                if (game.GetBoxSlotAtIndex(box, slot) is not PK7 { Species: > 0 } pokemon)
                {
                    continue;
                }

                var (b, s) = (box, slot);
                yield return (pokemon, $"caja {b + 1}, hueco {s + 1}",
                    p => game.SetBoxSlotAtIndex(p, b, s, PokemonBuilder.InPlace));
            }
        }

        for (var slot = 0; slot < game.PartyCount; slot++)
        {
            if (game.GetPartySlotAtIndex(slot) is not PK7 { Species: > 0 } pokemon)
            {
                continue;
            }

            var s = slot;
            yield return (pokemon, $"equipo, hueco {s + 1}",
                p => game.SetPartySlotAtIndex(p, s, PokemonBuilder.InPlace));
        }
    }

    private static (int Zero, int Repeated) Check(string path)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return (int.MaxValue, int.MaxValue);
        }

        var pids = Everything(game).Select(slot => slot.Pokemon.PID).ToList();

        return (pids.Count(pid => pid == 0), pids.Count - pids.Distinct().Count());
    }

    private static string Label(ReadOnlySpan<string> names, PK7 pokemon) =>
        pokemon.Species < names.Length ? names[pokemon.Species] : $"#{pokemon.Species}";

    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);

        var name = $"{DateTime.Now:yyyyMMdd-HHmmss}-antes-de-repartir-pid.sav";
        File.Copy(path, Path.Combine(backupFolder, name), overwrite: true);

        logger.LogInformation("Copia de la partida antes de repartir PID: {Name}", name);
    }
}
