using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Field;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// <c>--dinero</c> (singleplayer, 2026-10-12): the money of the game as the app reads it, from the save with the game closed or
/// from memory with it loaded, saying where. Only reads: nothing is paid. With the game loaded, right after saving, the two
/// numbers it prints must be equal; that is what proves the money was found in memory.
/// </summary>
internal static class MoneyProbe
{
    public static int Run()
    {
        using var client = new AzaharRpcClient();
        var save = new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client, AppContext.BaseDirectory);
        var live = new LiveSave(client, new SavedGameCache(save));
        var money = new GameMoney(save, client, live, Path.GetTempPath(), new ConsoleLogger<GameMoney>());

        Console.WriteLine($"Partida: {save.Find() ?? "no se encuentra"}");
        Console.WriteLine(save.IsGameLoaded() ? "Juego cargado: se lee de la memoria." : "Juego cerrado: se lee del fichero.");
        Console.WriteLine($"Dinero según la app: {money.Read()?.ToString() ?? "no se puede leer"} ₽");

        if (save.IsGameLoaded() && new SavedGameCache(save).Load() is { } file)
        {
            Console.WriteLine($"Dinero en el fichero (la última vez que guardaste): {file.Save.Money} ₽");
            Console.WriteLine("Si acabas de guardar y no has gastado nada, los dos números tienen que ser iguales.");

            // Where that number is in the save data the game holds (2026-10-16): the Misc block was looked for in a window that may not reach it.
            if (live.Base() is { } start)
            {
                Console.WriteLine($"Partida en memoria desde 0x{start:X8}. Busco {file.Save.Money} en los primeros 0x10000 bytes:");
                var needle = BitConverter.GetBytes(file.Save.Money);
                for (var at = 0u; at < 0x10000; at += 0x200)
                {
                    if (!client.TryReadMemory(start + at, 0x200, out var part)) { Console.WriteLine($"  no se puede leer +0x{at:X5}"); break; }
                    for (var i = 0; i + 4 <= part.Length; i += 4)
                    {
                        if (part.AsSpan(i, 4).SequenceEqual(needle)) Console.WriteLine($"  +0x{at + (uint)i:X5}");
                    }
                }
            }
            else Console.WriteLine("No llego a la partida en memoria (la cadena de punteros no lleva al montón).");
        }

        return 0;
    }

    private sealed class ConsoleLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) => Console.WriteLine($"  [{logLevel}] {formatter(state, exception)}");
    }
}
