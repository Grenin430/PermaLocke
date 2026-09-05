using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;
using PKHeX.Core;

namespace PermaLocke.Probe;

/// <summary>
/// Compares the party entry the <b>save</b> holds with a 260-byte entry captured before a write,
/// and prints the ranges where the two differ.
/// </summary>
/// <remarks>
/// Both sides are decrypted first. Gen 7 seeds its cipher with the encryption constant, which is
/// the one field kept in the clear, so decryption works whether or not the checksum matches — that
/// is what makes it possible to look inside a Bad Egg at all, and why the ranges below are the
/// real content and not an artefact of a failed checksum.
/// </remarks>
public static class EggCompare
{
    public static int Run(string backupPath, int slot)
    {
        if (!File.Exists(backupPath))
        {
            Console.WriteLine($"No existe: {backupPath}");
            return 1;
        }

        var save = new PlayerSave(
            new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
            new AzaharRpcClient(),
            AppContext.BaseDirectory);

        if (save.Find() is not { } path
            || !SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            Console.WriteLine("No se encuentra o no se reconoce la partida.");
            return 1;
        }

        var before = new PK7(File.ReadAllBytes(backupPath)).Data.ToArray();
        var after = ((PK7)game.GetPartySlotAtIndex(slot)).Data.ToArray();

        Console.WriteLine($"antes: {backupPath}");
        Console.WriteLine($"ahora: {path}, hueco {slot + 1}");
        Console.WriteLine();

        var length = Math.Min(before.Length, after.Length);
        var runStart = -1;

        for (var i = 0; i <= length; i++)
        {
            var differs = i < length && before[i] != after[i];

            if (differs && runStart < 0)
            {
                runStart = i;
            }
            else if (!differs && runStart >= 0)
            {
                Console.WriteLine($"  0x{runStart:X3}..0x{i - 1:X3}  ({i - runStart} bytes)  {Block(runStart)}");
                if (i - runStart >= 16)
                {
                    Console.WriteLine("      antes " + Hex(before, runStart, i - runStart));
                    Console.WriteLine("      ahora " + Hex(after, runStart, i - runStart));
                }
                runStart = -1;
            }
        }

        Console.WriteLine();
        Console.WriteLine("Los cuatro bloques de 56 bytes van barajados según EC % 24, así que un");
        Console.WriteLine("bloque entero cambiado de sitio sale aquí como un tramo de 56.");

        return 0;
    }

    private static string Hex(byte[] data, int start, int count) =>
        Convert.ToHexString(data, start, Math.Min(count, 64));

    private static string Block(int offset) => offset switch
    {
        < 0x08 => "cabecera (EC, sanity, checksum)",
        < 0x40 => "bloque A (especie, PID, experiencia, objeto)",
        < 0x78 => "bloque B (movimientos, IV, mote)",
        < 0xB0 => "bloque C (nombre y país del EO, cintas)",
        < 0xE8 => "bloque D (encuentro, entrenador, nivel de encuentro)",
        _ => "cola de equipo (estadísticas de combate)"
    };
}

/// <summary>
/// Lists every party entry PermaLocke kept before a write, decoded.
/// </summary>
/// <remarks>
/// Repairing the slot means putting a real Pokémon back into it, and the only honest source for
/// that is a capture of the slot from before it broke. Which of those captures is usable is a
/// question with a measurable answer — the checksum — so it gets measured instead of assumed.
/// </remarks>
public static class EggBackups
{
    public static int Run(string folder)
    {
        var files = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.bin").OrderByDescending(File.GetLastWriteTime).ToArray()
            : [];

        if (files.Length == 0)
        {
            Console.WriteLine($"No hay copias de entradas de equipo en {folder}");
            return 1;
        }

        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            var name = Path.GetFileName(file);

            if (bytes.Length < new PK7().SIZE_STORED)
            {
                Console.WriteLine($"{name}  mide {bytes.Length} bytes, no es una entrada");
                continue;
            }

            var pokemon = new PK7(bytes);

            Console.WriteLine(
                $"{name}  #{pokemon.Species,-4} PID {pokemon.PID:X8}  Nv {pokemon.CurrentLevel,3}  " +
                $"{(pokemon.ChecksumValid ? "firma OK " : "FIRMA MAL")}  «{pokemon.Nickname}»");
        }

        return 0;
    }
}
