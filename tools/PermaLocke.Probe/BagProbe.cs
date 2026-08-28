using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Exercises exactly what the MISCELÁNEA screen does, without the app: locates the bag block,
/// prints what the player is carrying, and optionally writes Rare Candies.
/// </summary>
/// <remarks>
/// The full sweep is behind a flag on purpose. It is 24.000 requests and the emulator logs a
/// line per reply, so it is not something to run out of habit; the ordinary path revalidates
/// the address of the last session with a single read.
/// </remarks>
public static class BagProbe
{
    public static void Run(int? candies, bool sweep, int itemId = BagService.RareCandyItemId)
    {
        using var client = new AzaharRpcClient();

        if (!client.TryPing(out var message))
        {
            Console.WriteLine($"El servidor RPC no responde: {message}");
            return;
        }

        // Trampa documentada: sin fijar el proceso, el servidor contesta con datos que no son
        // del juego y solo se ve en el log del emulador.
        var process = client.AttachTo(0x00040000001B5100);
        Console.WriteLine($"Proceso {process.ProcessId} '{process.Name}' fijado.\n");

        var layout = BagLayout.UltraSunMoon;
        Console.WriteLine($"Bloque de mochila: 0x{layout.BlockSize:X} bytes, "
                          + $"{layout.Pockets.Count} bolsillos, tabla de punteros de "
                          + $"{layout.PointerTableBytes} bytes.");

        var locator = new BagLocator(client);

        if (sweep)
        {
            var stopwatch = Stopwatch.StartNew();
            var blocks = locator.LocateAll();

            Console.WriteLine($"\nBarrido completo en {stopwatch.ElapsedMilliseconds} ms: "
                              + $"{blocks.Count} bloque(s).");

            foreach (var found in blocks)
            {
                Console.WriteLine($"  0x{found.BaseAddress:X8}, tabla en 0x{found.PointerTableAddress:X8}");
            }
        }

        var backups = Path.Combine(AppContext.BaseDirectory, "backup");
        var writer = new AzaharGameWriter(client, backups, NullLogger<AzaharGameWriter>.Instance);
        var bag = new BagService(client, writer, Path.Combine(backups, "objetos-retirados.txt"),
            Path.Combine(backups, "mochila.txt"), NullLogger<BagService>.Instance);

        var timer = Stopwatch.StartNew();
        var block = bag.Locate();

        Console.WriteLine($"\nBagService.Locate() en {timer.ElapsedMilliseconds} ms -> "
                          + (block is null ? "no encontrada" : $"0x{block.BaseAddress:X8}"));

        if (block is null)
        {
            Console.WriteLine("No se ha encontrado la mochila. ¿Está el juego cargado?");
            return;
        }

        var names = GameInfo.GetStrings("es").itemlist;

        Console.WriteLine();
        Show(locator, block, names);

        if (candies is null)
        {
            Console.WriteLine("\n(solo lectura; pasa una cantidad para escribir Caramelos Raros)");
            return;
        }

        var result = bag.SetCount(itemId, candies.Value);

        Console.WriteLine($"\nSetCount({names[itemId]}, {candies}) -> {result.Outcome}");
        Console.WriteLine($"  antes {result.Previous}, después {result.Applied}, en 0x{result.Address:X8}");
        Console.WriteLine(result.Outcome switch
        {
            BagWriteOutcome.Ok => "  Escrito y releído. Abre la mochila en el juego para verlo.",
            BagWriteOutcome.NotApplied => "  La escritura no ha cuajado: hace falta el fork de Azahar.",
            BagWriteOutcome.BagNotFound => "  No se encontró la mochila.",
            BagWriteOutcome.PocketFull => "  El bolsillo está lleno.",
            BagWriteOutcome.UnknownPocket => "  Ningún bolsillo admite ese objeto.",
            _ => "  No había nada que hacer."
        });

        Console.WriteLine();
        Show(locator, block, names);
    }

    private static void Show(BagLocator locator, BagBlock block, string[] names)
    {
        var contents = locator.ReadContents(block);

        foreach (var pocket in block.Layout.Pockets)
        {
            var slots = contents.Where(slot => slot.Pocket.Type == pocket.Type).ToList();

            Console.WriteLine($"  {pocket.Type,-12} +0x{pocket.Offset:X3} {slots.Count,3}/{pocket.Slots,3}  "
                              + string.Join(", ", slots.Select(slot =>
                                  $"{names[slot.Entry.ItemId]} x{slot.Entry.Count} [{slot.Index}]")));
        }
    }
}
