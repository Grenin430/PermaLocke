using System.Buffers.Binary;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Copies the game's working save out of the emulator's memory into a separate file, when the
/// emulator can no longer save by itself.
/// </summary>
/// <remarks>
/// <para>
/// The game keeps its whole save in memory while it runs and only copies it to the file when the
/// player saves. If the emulator hangs before that, the progress is still sitting in RAM. This finds
/// that buffer by searching for a block of the <b>last saved file</b> that does not change while
/// playing, reads the whole save length from there, and writes it <b>to a new file</b>. The real save
/// is never touched; putting the rescued one in its place is a separate decision for the player.
/// </para>
/// <para>
/// Read-only against the emulator, and the search is confined to one megabyte that is known to be
/// mapped (the bag and the party mirror live there): searching unmapped memory logs an error per
/// page, and that flood is what froze the emulator in the first place.
/// </para>
/// </remarks>
public static class RescueProbe
{
    private const uint SearchStart = 0x33000000;
    private const uint SearchSize = 0x00100000;

    public static int Run()
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("No se puede enganchar al juego: " + ex.Message);
            return 1;
        }

        var save = new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client,
            AppContext.BaseDirectory);

        if (save.Find() is not { } path)
        {
            Console.WriteLine("No se encuentra la partida de Ultra Luna.");
            return 1;
        }

        var file = File.ReadAllBytes(path);

        if (!SaveUtil.TryGetSaveFile(file.ToArray(), out var loaded) || loaded is not SAV7USUM saved)
        {
            Console.WriteLine("PKHeX no reconoce la partida como Ultra Luna.");
            return 1;
        }

        Console.WriteLine($"Partida guardada: {path}");
        Console.WriteLine($"Escrita: {File.GetLastWriteTime(path):yyyy-MM-dd HH:mm:ss}, {file.Length} bytes");
        Console.WriteLine();

        var blocks = saved.AllBlocks;

        if (Locate(client, file, blocks) is not { } baseAddress)
        {
            Console.WriteLine("No se ha encontrado la copia de la partida en esa zona de memoria.");
            return 1;
        }

        Console.WriteLine($"Copia de trabajo de la partida en memoria: 0x{baseAddress:X8}");

        var ram = new byte[file.Length];
        var missing = 0;

        for (var offset = 0; offset < ram.Length; offset += 0x1000)
        {
            var size = Math.Min(0x1000, ram.Length - offset);

            if (client.TryReadMemory(baseAddress + (uint)offset, size, out var chunk))
            {
                chunk.CopyTo(ram, offset);
            }
            else
            {
                missing++;
            }

            Thread.Sleep(2);
        }

        if (missing > 0)
        {
            Console.WriteLine($"{missing} trozos no se pudieron leer: el rescate estaría incompleto y no se guarda.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("Bloque   offset    tamaño   bytes distintos");

        var changedBlocks = 0;

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var differ = 0;

            for (var b = 0; b < block.Length && block.Offset + b < file.Length; b++)
            {
                if (file[block.Offset + b] != ram[block.Offset + b])
                {
                    differ++;
                }
            }

            if (differ > 0)
            {
                changedBlocks++;
                Console.WriteLine($"  {i,3}   0x{block.Offset:X5}  {block.Length,7}   {differ}");
            }
        }

        Console.WriteLine($"{changedBlocks} de {blocks.Count} bloques han cambiado desde el último guardado.");

        var folder = Path.Combine(new PermaLocke.Infrastructure.AppPaths().Root, "Saves", "rescate");
        Directory.CreateDirectory(folder);
        var rescued = Path.Combine(folder, $"memoria-{DateTime.Now:yyyyMMdd-HHmmss}.bin");
        File.WriteAllBytes(rescued, ram);

        Console.WriteLine();
        Console.WriteLine($"Copia en crudo guardada en {rescued} (tu partida no se ha tocado).");
        Console.WriteLine();

        Compare(saved, ram);
        return 0;
    }

    /// <summary>
    /// Finds the save buffer by a block that should be identical in the file and in memory.
    /// </summary>
    /// <remarks>
    /// Several blocks are tried, and an address only counts if <b>most</b> blocks line up from it —
    /// one matching block could be a stray copy of that block alone.
    /// </remarks>
    private static uint? Locate(AzaharRpcClient client, byte[] file, IReadOnlyList<BlockInfo> blocks)
    {
        foreach (var block in blocks.Where(block => block.Length >= 128))
        {
            var pattern = file.AsSpan(block.Offset, 64).ToArray();

            // Un patrón casi todo ceros encuentra cualquier cosa.
            if (pattern.Distinct().Count() < 16)
            {
                continue;
            }

            var mask = Enumerable.Repeat((byte)0xFF, pattern.Length).ToArray();
            var hits = client.SearchMemory(SearchStart, SearchSize, pattern, mask, stride: 4);

            foreach (var hit in hits)
            {
                if (hit < block.Offset)
                {
                    continue;
                }

                var candidate = hit - (uint)block.Offset;
                var agree = blocks.Count(other => SameStart(client, file, candidate, other));

                Console.WriteLine($"Bloque de 0x{block.Offset:X5} hallado en 0x{hit:X8}: base 0x{candidate:X8}, "
                                  + $"{agree} de {blocks.Count} bloques empiezan igual");

                if (agree >= blocks.Count / 2)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool SameStart(AzaharRpcClient client, byte[] file, uint baseAddress, BlockInfo block)
    {
        var length = Math.Min(16, block.Length);

        return client.TryReadMemory(baseAddress + (uint)block.Offset, length, out var bytes)
               && bytes.AsSpan().SequenceEqual(file.AsSpan(block.Offset, length));
    }

    /// <summary>What the rescued copy holds that the saved file does not, in terms a player recognises.</summary>
    private static void Compare(SAV7USUM saved, byte[] ram)
    {
        if (!SaveUtil.TryGetSaveFile(ram.ToArray(), out var fromRam) || fromRam is not SAV7USUM memory)
        {
            Console.WriteLine("PKHeX no reconoce la copia de memoria como partida: no se puede comparar.");
            return;
        }

        Console.WriteLine("                     GUARDADA        EN MEMORIA");
        Console.WriteLine($"Tiempo de juego      {saved.PlayedHours}h {saved.PlayedMinutes:00}m {saved.PlayedSeconds:00}s"
                          + $"      {memory.PlayedHours}h {memory.PlayedMinutes:00}m {memory.PlayedSeconds:00}s");
        Console.WriteLine($"Dinero               {saved.Money,-15} {memory.Money}");
        Console.WriteLine($"Capturas (récord)    {saved.GetRecord(0),-15} {memory.GetRecord(0)}");
        Console.WriteLine($"Pokémon en cajas     {Boxed(saved),-15} {Boxed(memory)}");
        Console.WriteLine($"Zona                 {saved.Situation.M,-15} {memory.Situation.M}");

        var names = GameInfo.GetStrings("es").specieslist;

        for (var slot = 0; slot < 6; slot++)
        {
            var a = saved.GetPartySlotAtIndex(slot);
            var b = memory.GetPartySlotAtIndex(slot);

            Console.WriteLine($"Equipo {slot + 1}             {Describe(a, names),-15} {Describe(b, names)}");
        }
    }

    private static int Boxed(SAV7USUM game)
    {
        var count = 0;

        for (var box = 0; box < game.BoxCount; box++)
        {
            for (var slot = 0; slot < game.BoxSlotCount; slot++)
            {
                if (game.GetBoxSlotAtIndex(box, slot).Species > 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static string Describe(PKM pokemon, string[] names) =>
        pokemon.Species == 0
            ? "-"
            : $"{(pokemon.Species < names.Length ? names[pokemon.Species] : "#" + pokemon.Species)} Nv{pokemon.CurrentLevel}";
}
