using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink;

/// <param name="Total">Pokémon looked at, boxes and party together.</param>
/// <param name="Nameless">How many of them carried no name at all.</param>
/// <param name="Named">One line per Pokémon fixed, for the player to read before believing it.</param>
/// <param name="Written">False for a dry run, or when there was nothing to do.</param>
public sealed record NameRepairReport(
    int Total,
    int Nameless,
    IReadOnlyList<string> Named,
    bool Written,
    string Message);

/// <summary>
/// Puts the species name back on the Pokémon PermaLocke delivered without one.
/// </summary>
/// <remarks>
/// <para>
/// The nickname is not something the game works out from the species: it is a field inside the
/// Pokémon, and whatever is in it is what gets shown. Everything PermaLocke handed over before
/// <see cref="Data.PokemonBuilder"/> learned to fill it in therefore sits in the boxes nameless,
/// and no future fix reaches them — they are already written.
/// </para>
/// <para>
/// Only a <b>blank</b> name is touched. A Pokémon the player named keeps its name, and one whose
/// name merely matches its species is left alone as well: this repairs what is missing, it does
/// not normalise what is there. As with every other write, the save is copied first and read back
/// afterwards, and nothing is reported as fixed that was not seen fixed on disk.
/// </para>
/// </remarks>
public sealed class SaveNameRepair(PlayerSave save, string backupFolder, ILogger<SaveNameRepair> logger)
{
    /// <summary>Looks at the player's save without writing to it.</summary>
    public NameRepairReport Inspect() => Run(write: false);

    /// <summary>Fixes the player's save. Refuses while the game is loaded, like every other write.</summary>
    public NameRepairReport Repair() => Run(write: true);

    private NameRepairReport Run(bool write)
    {
        var path = save.Find();

        if (path is null)
        {
            return new NameRepairReport(0, 0, [], false,
                "No se encuentra la partida de Ultra Luna. ¿Has jugado y guardado alguna vez con este emulador?");
        }

        if (write && save.IsGameLoaded())
        {
            return new NameRepairReport(0, 0, [], false,
                "El juego está abierto en el emulador. Guarda la partida y cierra Azahar: mientras "
                + "esté cargado, el emulador reescribiría el save y la reparación se perdería.");
        }

        return RepairIn(path, write);
    }

    /// <summary>
    /// Works on one specific file, so the whole repair can be exercised against a copy with no
    /// emulator anywhere near it.
    /// </summary>
    public NameRepairReport RepairIn(string path, bool write)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
            {
                return new NameRepairReport(0, 0, [], false,
                    $"El fichero de partida no se ha podido leer como Ultra Luna: {path}");
            }

            var (total, named) = Apply(game);

            if (named.Count == 0)
            {
                return new NameRepairReport(total, 0, [], false,
                    $"Los {total} Pokémon de la partida tienen nombre. No hay nada que reparar.");
            }

            if (!write)
            {
                return new NameRepairReport(total, named.Count, named, false,
                    $"{named.Count} de {total} Pokémon no tienen nombre. Nada escrito todavía.");
            }

            Backup(path);
            File.WriteAllBytes(path, game.Write().ToArray());

            var left = CountNameless(path);
            if (left != 0)
            {
                return new NameRepairReport(total, named.Count, named, true,
                    $"Se escribió la partida pero al releerla siguen {left} sin nombre. "
                    + $"La copia de seguridad está en {backupFolder}.");
            }

            logger.LogInformation("Nombres reparados: {Count} de {Total} Pokémon", named.Count, total);

            return new NameRepairReport(total, named.Count, named, true,
                $"{named.Count} Pokémon recuperan su nombre, y al releer la partida no queda ninguno sin él.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la reparación de nombres de {Path}", path);

            return new NameRepairReport(0, 0, [], false,
                "No se pudo reparar la partida. El detalle está en la carpeta Logs.");
        }
    }

    /// <summary>
    /// Put a Pokémon back exactly where it was, touching nothing else.
    /// </summary>
    /// <remarks>
    /// PKHeX treats putting a Pokémon in a box as <em>acquiring</em> it: by default it registers
    /// the Pokédex entry and bumps the trainer card counters. That is right when a Pokémon
    /// arrives, and wrong here — these were already in the box. Left at the default, renaming the
    /// hundred and fifty of a real run added a hundred and fifty captures, a hundred and fifty
    /// Poké Balls used and a hundred and fifty wild battles that never happened.
    /// </remarks>
    private static readonly EntityImportSettings PutBack = PokemonBuilder.InPlace;

    /// <summary>
    /// Names everything nameless in an already-loaded save, and says what it named.
    /// </summary>
    /// <remarks>
    /// Separate from the file so the repair itself can be tested: PKHeX does not recognise a save
    /// built here and written to disk, so a test can only reach this far — the same limit the box
    /// reader has, and for the same reason.
    /// </remarks>
    public static (int Total, IReadOnlyList<string> Named) Apply(SAV7USUM game)
    {
        var total = 0;
        var named = new List<string>();

        // Solo para lo que se le enseña al jugador; el nombre que se escribe dentro del Pokémon
        // sale del idioma de su propia partida, no de este.
        var names = GameInfo.GetStrings("es").specieslist;

        for (var box = 0; box < game.BoxCount; box++)
        {
            for (var slot = 0; slot < game.BoxSlotCount; slot++)
            {
                if (game.GetBoxSlotAtIndex(box, slot) is not PK7 { Species: > 0 } pokemon)
                {
                    continue;
                }

                total++;
                if (!NameIt(pokemon))
                {
                    continue;
                }

                named.Add($"caja {box + 1}, hueco {slot + 1}: {Label(names, pokemon)}");
                game.SetBoxSlotAtIndex(pokemon, box, slot, PutBack);
            }
        }

        for (var slot = 0; slot < game.PartyCount; slot++)
        {
            if (game.GetPartySlotAtIndex(slot) is not PK7 { Species: > 0 } pokemon)
            {
                continue;
            }

            total++;
            if (!NameIt(pokemon))
            {
                continue;
            }

            named.Add($"equipo, puesto {slot + 1}: {Label(names, pokemon)}");
            game.SetPartySlotAtIndex(pokemon, slot, PutBack);
        }

        return (total, named);
    }

    /// <summary>
    /// Fills in the species name, and says whether it had to. A name that is already there is
    /// left exactly as it is, nickname or not.
    /// </summary>
    private static bool NameIt(PK7 pokemon)
    {
        if (!string.IsNullOrWhiteSpace(pokemon.Nickname))
        {
            return false;
        }

        var name = Data.PokemonBuilder.SpeciesNameFor(pokemon);

        // Si ni así hay nombre, se deja como está: mejor un hueco sin nombre que decir que se
        // arregló algo que sigue en blanco.
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        pokemon.Nickname = name;
        pokemon.IsNicknamed = false;
        pokemon.RefreshChecksum();
        return true;
    }

    private static string Label(IReadOnlyList<string> names, PK7 pokemon) =>
        $"{(pokemon.Species < names.Count ? names[pokemon.Species] : pokemon.Species.ToString())} Nv.{pokemon.CurrentLevel}";

    private static int CountNameless(string path)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return -1;
        }

        var left = 0;

        for (var box = 0; box < game.BoxCount; box++)
        {
            for (var slot = 0; slot < game.BoxSlotCount; slot++)
            {
                if (game.GetBoxSlotAtIndex(box, slot) is PK7 { Species: > 0 } pokemon &&
                    string.IsNullOrWhiteSpace(pokemon.Nickname))
                {
                    left++;
                }
            }
        }

        for (var slot = 0; slot < game.PartyCount; slot++)
        {
            if (game.GetPartySlotAtIndex(slot) is PK7 { Species: > 0 } pokemon &&
                string.IsNullOrWhiteSpace(pokemon.Nickname))
            {
                left++;
            }
        }

        return left;
    }

    /// <summary>
    /// Copies the whole save before touching it. No backup, no repair: a partida that cannot be
    /// put back does not get written to.
    /// </summary>
    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);
        var name = $"main-{DateTime.Now:yyyyMMdd-HHmmss}-antes-de-nombres";
        File.Copy(path, Path.Combine(backupFolder, name), overwrite: false);
    }
}
