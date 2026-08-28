using PKHeX.Core;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;
using Microsoft.Extensions.Logging.Abstractions;

namespace PermaLocke.Probe;

/// <summary>
/// What the saved game says about mega evolution, and what each party member is holding.
/// </summary>
/// <remarks>
/// Written because the Key Stone in the bag turned out not to be enough. Before deciding that a
/// story flag gates it, the mundane explanations have to be ruled out with a measurement rather
/// than with a question: whether the Pokémon is actually holding its stone, and whether it is in
/// the party at all. Only looks; writes nothing.
/// </remarks>
public static class MegaProbe
{
    /// <summary>
    /// Turns the unlock on, once it has been found.
    /// </summary>
    /// <remarks>
    /// A separate switch from looking, because this writes the partida. It refuses while the game
    /// is loaded — Azahar would overwrite it on the next save — copies the whole file first, and
    /// reads it back before saying it worked. The same three guards every write in this project
    /// has, for the same reason.
    /// </remarks>
    public static int Enable()
    {
        using var client = new AzaharRpcClient();

        var save = new PlayerSave(
            new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client,
            AppContext.BaseDirectory);

        if (save.IsGameLoaded())
        {
            Console.WriteLine("El juego está cargado en Azahar. Guarda, ciérralo del todo y vuelve.");
            return 1;
        }

        if (save.Find() is not { } path)
        {
            Console.WriteLine("No se encuentra la partida.");
            return 1;
        }

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            Console.WriteLine("La partida no se lee como Ultra Luna.");
            return 1;
        }

        if (game.Blocks.MyStatus.MegaUnlocked)
        {
            Console.WriteLine("Ya estaba activado. No se toca nada.");
            return 0;
        }

        // A Saves/backup, NUNCA al lado de la partida: esa carpeta es el archivo de datos del
        // juego, y el emulador lleva la cuenta de lo que hay dentro (§67). Dejar ahí un fichero
        // suelto es meter algo en una estructura que no es nuestra.
        var backups = Path.Combine(Root(), "Saves", "backup");
        Directory.CreateDirectory(backups);

        var backup = Path.Combine(backups,
            $"main-antes-de-la-mega-{DateTime.Now:yyyyMMdd-HHmmss}.sav");

        File.Copy(path, backup, overwrite: false);
        Console.WriteLine($"Copia de la partida en {backup}");

        game.Blocks.MyStatus.MegaUnlocked = true;
        File.WriteAllBytes(path, game.Write().ToArray());

        // Releído del fichero, no del objeto que acabamos de escribir: comprobar lo que tienes en
        // la mano no demuestra nada.
        if (!SaveUtil.TryGetSaveFile(path, out var again) || again is not SAV7USUM written
            || !written.Blocks.MyStatus.MegaUnlocked)
        {
            Console.WriteLine("Se escribió pero al releer sigue desactivado. La copia está al lado.");
            return 1;
        }

        Console.WriteLine("MegaUnlocked = True, escrito y releído.");
        Console.WriteLine("Abre Azahar y entra en un combate con el Swampert llevando la Swampertita.");
        return 0;
    }

    public static int Run()
    {
        using var client = new AzaharRpcClient();

        var save = new PlayerSave(
            new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client,
            AppContext.BaseDirectory);

        if (save.Find() is not { } path)
        {
            Console.WriteLine("No se encuentra la partida.");
            return 1;
        }

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            Console.WriteLine("La partida no se lee como Ultra Luna.");
            return 1;
        }

        Console.WriteLine($"Partida: {path}");
        Console.WriteLine($"(lo ultimo guardado: {File.GetLastWriteTime(path):HH:mm:ss})");
        Console.WriteLine();

        var items = GameInfo.GetStrings("es").itemlist;

        Console.WriteLine("EQUIPO Y LO QUE LLEVA PUESTO");
        for (var slot = 0; slot < game.PartyCount; slot++)
        {
            if (game.GetPartySlotAtIndex(slot) is not PK7 pokemon)
            {
                continue;
            }

            var held = pokemon.HeldItem == 0
                ? "nada"
                : $"{items[pokemon.HeldItem]} ({pokemon.HeldItem})";

            Console.WriteLine($"  {slot + 1}. {GameInfo.GetStrings("es").specieslist[pokemon.Species],-14} "
                              + $"Nv.{pokemon.CurrentLevel,-3} lleva: {held}");
        }

        Console.WriteLine();
        Console.WriteLine("MOCHILA: objetos clave");
        foreach (var pouch in game.Inventory.Pouches.Where(p => p.Type == InventoryType.KeyItems))
        {
            foreach (var item in pouch.Items.Where(i => i.Index != 0))
            {
                Console.WriteLine($"  {items[item.Index]} ({item.Index})");
            }
        }

        // Lo que PKHeX sepa del desbloqueo, si es que sabe algo. Se lista por reflexion para no
        // suponer un nombre de propiedad que quiza no existe en esta version.
        Console.WriteLine();
        Console.WriteLine("LO QUE PKHeX EXPONE Y SUENA A MEGA / DESBLOQUEO");

        var found = 0;

        // La raiz Y los bloques colgados de ella: PKHeX guarda el desbloqueo en un sub-bloque, no
        // en el objeto de partida, asi que mirar solo arriba no encuentra nada.
        found += Scan("SAV7USUM", game);

        foreach (var holder in game.GetType().GetProperties())
        {
            object? block;

            try
            {
                block = holder.GetValue(game);
            }
            catch (Exception)
            {
                continue;
            }

            if (block is null || block is string || holder.PropertyType.IsPrimitive)
            {
                continue;
            }

            found += Scan(holder.Name, block);

            foreach (var inner in block.GetType().GetProperties())
            {
                try
                {
                    if (inner.GetValue(block) is { } deep && deep is not string
                        && !inner.PropertyType.IsPrimitive)
                    {
                        found += Scan($"{holder.Name}.{inner.Name}", deep);
                    }
                }
                catch (Exception)
                {
                    // Un bloque que no se deja leer no dice nada, y no debe parar el barrido.
                }
            }
        }

        if (found == 0)
        {
            Console.WriteLine("  nada. El desbloqueo no esta expuesto: si existe, es una bandera de evento.");
        }

        return 0;
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>Lists anything on one object whose name sounds like the unlock. Returns how many.</summary>
    private static int Scan(string where, object target)
    {
        var found = 0;

        foreach (var property in target.GetType().GetProperties())
        {
            if (!property.Name.Contains("Mega", StringComparison.OrdinalIgnoreCase)
                && !property.Name.Contains("Unlock", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Console.WriteLine($"  {where}.{property.Name} = {Safe(property, target)}");
            found++;
        }

        return found;
    }

    private static string Safe(System.Reflection.PropertyInfo property, object target)
    {
        try
        {
            return property.GetValue(target)?.ToString() ?? "null";
        }
        catch (Exception ex)
        {
            return $"(no se pudo leer: {ex.GetType().Name})";
        }
    }
}
