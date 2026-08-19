using System.Diagnostics;
using System.Text;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Checks the fork's native memory search against a result we already know: the trainer name
/// appears ten times in memory, per docs/ARCHITECTURE.md §6.
/// </summary>
public static class SearchProbe
{
    public static void Run(string trainerName)
    {
        using var client = new AzaharRpcClient();

        if (!client.TryPing(out var message))
        {
            Console.WriteLine($"El servidor RPC no responde: {message}");
            Console.WriteLine("Abre Azahar (el fork) con Ultra Luna cargado y vuelve a intentarlo.");
            return;
        }

        // Trampa documentada: sin fijar el proceso, el servidor contesta con datos que no son
        // del juego y solo se ve en el log del emulador.
        var process = client.AttachTo(0x00040000001B5100);
        Console.WriteLine($"Proceso {process.ProcessId} '{process.Name}' fijado.\n");

        var needle = Encoding.Unicode.GetBytes(trainerName);
        var mask = Enumerable.Repeat((byte)0xFF, needle.Length).ToArray();

        (string Name, uint Start, uint Size)[] regions =
        [
            ("heap", 0x08000000, 0x02000000),
            ("linear", 0x30000000, 0x04000000),
        ];

        var total = 0;
        var stopwatch = Stopwatch.StartNew();

        foreach (var (name, start, size) in regions)
        {
            var at = Stopwatch.StartNew();
            var hits = client.SearchMemory(start, size, needle, mask, stride: 2);
            Console.WriteLine($"{name,-8} 0x{start:X8}+{size / 1024 / 1024,3} MB -> {hits.Count,3} aciertos en {at.ElapsedMilliseconds,5} ms");
            foreach (var hit in hits.Take(12))
            {
                Console.WriteLine($"           0x{hit:X8}");
            }
            total += hits.Count;
        }

        Console.WriteLine($"\nTotal {total} aciertos en {stopwatch.ElapsedMilliseconds} ms.");
        Console.WriteLine(total == 0
            ? "Cero aciertos. O el emulador no es el fork, o el nombre no es ese."
            : "El parche responde.");
    }
}
