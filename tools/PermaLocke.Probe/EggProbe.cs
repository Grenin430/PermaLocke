using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;
using PermaLocke.GameLink.Data;
using PKHeX.Core;

namespace PermaLocke.Probe;

/// <summary>
/// Prints, for every party slot of the <b>saved</b> game, the handful of fields that decide
/// whether the game draws a Pokémon or an egg.
/// </summary>
/// <remarks>
/// <para>
/// It exists because "se ha convertido en un huevo" has two completely different causes and the
/// fix for one is the opposite of the fix for the other. A real egg is a Pokémon whose
/// <see cref="PKM.IsEgg"/> flag is set — bit 30 of the 32-bit word that holds the six IVs, which
/// is why a crooked write into the IVs can hatch a Pokémon by accident. A <b>Bad Egg</b> is not a
/// flag at all: it is what the game shows when the stored block fails its own checksum, and no
/// flag anywhere says so.
/// </para>
/// <para>
/// So the two are told apart by printing them side by side and letting the numbers say which it
/// is, rather than reasoning from the picture on screen.
/// </para>
/// </remarks>
public static class EggProbe
{
    public static int Run()
    {
        var save = new PlayerSave(
            new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
            new AzaharRpcClient(),
            AppContext.BaseDirectory);

        if (save.Find() is not { } path)
        {
            Console.WriteLine("No se encuentra la partida de Ultra Luna.");
            return 1;
        }

        Console.WriteLine($"Partida: {path}");
        Console.WriteLine($"Escrita:  {File.GetLastWriteTime(path):yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            Console.WriteLine("PKHeX no reconoce el fichero como una partida de Ultra Luna.");
            return 1;
        }

        for (var slot = 0; slot < 6; slot++)
        {
            if (game.GetPartySlotAtIndex(slot) is not PK7 pokemon || pokemon.Species == 0)
            {
                Console.WriteLine($"hueco {slot + 1}: vacío");
                continue;
            }

            // El checksum se recalcula sobre lo leído y se compara con el que el fichero guarda.
            // Si no cuadran, el juego dibuja un Huevo Malo: ningún campo lo anuncia.
            var ok = pokemon.ChecksumValid;

            Console.WriteLine($"hueco {slot + 1}: #{pokemon.Species} «{pokemon.Nickname}»");
            Console.WriteLine($"    EC={pokemon.EncryptionConstant:X8}  orden de bloques={pokemon.EncryptionConstant % 24}");
            Console.WriteLine($"    huevo={(pokemon.IsEgg ? "SÍ" : "no")}  mote={(pokemon.IsNicknamed ? "sí" : "no")}");
            Console.WriteLine($"    checksum guardado={pokemon.Checksum:X4} → {(ok ? "cuadra" : "NO CUADRA (Huevo Malo)")}");
            Console.WriteLine($"    exp={pokemon.EXP}  Nv(exp)={pokemon.CurrentLevel}  Stat_Level={pokemon.Stat_Level}");
            Console.WriteLine($"    IV={pokemon.IV_HP}/{pokemon.IV_ATK}/{pokemon.IV_DEF}/{pokemon.IV_SPA}/{pokemon.IV_SPD}/{pokemon.IV_SPE}");
            Console.WriteLine($"    PS={pokemon.Stat_HPCurrent}/{pokemon.Stat_HPMax}  PID={pokemon.PID:X8}  huevo recibido en={pokemon.EggLocation}");
            Console.WriteLine($"    movimientos={pokemon.Move1}/{pokemon.Move2}/{pokemon.Move3}/{pokemon.Move4}");
        }

        return 0;
    }
/// <summary>
/// Puts a real Pokémon back into a party slot the save holds as a Bad Egg.
/// </summary>
/// <remarks>
/// <para>
/// A Bad Egg is not repairable in place: its stored block failed its own checksum, so every field
/// inside it — species, level, PID — decrypts to noise and there is nothing in there to correct.
/// What makes the repair honest is that PermaLocke captures the slot's exact bytes before every
/// write, so the Pokémon that was there is on disk, intact and checksum-valid. The marker is then
/// re-applied to that capture with the same <see cref="DeathMark"/> everything else uses.
/// </para>
/// <para>
/// The identity guard is the <b>encryption constant</b> and not the PID, which is the one place
/// this differs from every other write in the project. The PID lives inside the block that is
/// broken; the encryption constant is the four plain bytes at the front and survives whatever
/// happened to the rest. It is the only field of a Bad Egg that still means something.
/// </para>
/// <para>
/// And it refuses a slot whose checksum is fine. A repair that can also overwrite a healthy
/// Pokémon is not a repair.
/// </para>
/// </remarks>
public static int Repair(string capturePath, int slot, bool onCopy)
{
    if (!File.Exists(capturePath))
    {
        Console.WriteLine($"No existe: {capturePath}");
        return 1;
    }

    var save = new PlayerSave(
        new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
        new AzaharRpcClient(),
        AppContext.BaseDirectory);

    if (save.Find() is not { } found)
    {
        Console.WriteLine("No se encuentra la partida de Ultra Luna.");
        return 1;
    }

    var path = found;

    if (!onCopy && save.IsGameLoaded())
    {
        Console.WriteLine("El juego está abierto en el emulador. Guarda dentro del juego y cierra "
                          + "Azahar: mientras esté abierto, lo que se escriba aquí lo pisa él.");
        return 1;
    }

    // La partida de verdad se deja fuera del ensayo por completo, no se confía en que el resto
    // del método se porte bien: §65.
    if (onCopy)
    {
        var copy = Path.Combine(Path.GetTempPath(), $"permalocke-ensayo-{Guid.NewGuid():N}.sav");
        File.Copy(path, copy);
        path = copy;
        Console.WriteLine("ENSAYO sobre una copia. La partida no se toca.");
    }

    if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
    {
        Console.WriteLine("PKHeX no reconoce el fichero como una partida de Ultra Luna.");
        return 1;
    }

    var broken = (PK7)game.GetPartySlotAtIndex(slot);
    var capture = new PK7(File.ReadAllBytes(capturePath));

    Console.WriteLine($"Partida: {path}");
    Console.WriteLine($"hueco {slot + 1}: EC {broken.EncryptionConstant:X8}, "
                      + $"firma {(broken.ChecksumValid ? "OK" : "MAL")}");
    Console.WriteLine($"captura: EC {capture.EncryptionConstant:X8}, "
                      + $"firma {(capture.ChecksumValid ? "OK" : "MAL")}, "
                      + $"#{capture.Species} PID {capture.PID:X8} «{capture.Nickname}»");
    Console.WriteLine();

    if (broken.ChecksumValid)
    {
        Console.WriteLine("Ese hueco está sano. No se toca.");
        return 1;
    }

    if (!capture.ChecksumValid)
    {
        Console.WriteLine("La captura tampoco cuadra consigo misma; no sirve para reconstruir.");
        return 1;
    }

    if (broken.EncryptionConstant != capture.EncryptionConstant)
    {
        Console.WriteLine("La captura es de OTRO Pokémon: las constantes de encriptación no "
                          + "coinciden. No se escribe.");
        return 1;
    }

    var pid = capture.PID;
    DeathMark.Apply(capture);

    var backup = Path.Combine(Path.GetDirectoryName(path)!,
        $"main-antes-de-arreglar-el-huevo-{DateTime.Now:yyyyMMdd-HHmmss}.sav");

    File.Copy(path, backup, overwrite: true);
    Console.WriteLine($"Copia previa: {backup}");

    game.SetPartySlotAtIndex(capture, slot, PokemonBuilder.InPlace);
    File.WriteAllBytes(path, game.Write().ToArray());

    // Releer desde el fichero, no fiarse de lo que se acaba de construir en memoria.
    if (!SaveUtil.TryGetSaveFile(path, out var again) || again is not SAV7USUM reread)
    {
        Console.WriteLine("Escrito, pero la partida ya no se deja leer. Restaura la copia previa.");
        return 1;
    }

    var now = (PK7)reread.GetPartySlotAtIndex(slot);

    Console.WriteLine();
    Console.WriteLine($"ahora: #{now.Species} «{now.Nickname}» Nv {now.CurrentLevel}  "
                      + $"PID {now.PID:X8}  firma {(now.ChecksumValid ? "OK" : "MAL")}");

    var good = now.ChecksumValid && now.PID == pid && DeathMark.IsMarked(now);

    Console.WriteLine(good
        ? "Arreglado: el hueco vuelve a tener un Pokémon, y es la marca de muerte del mismo PID."
        : "NO ha quedado bien. Restaura la copia previa.");

    return good ? 0 : 1;
}
}
