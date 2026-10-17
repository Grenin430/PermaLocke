using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Rpc;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Probe;

/// <summary>
/// <c>--variocolor [poner]</c> (2026-10-17): the Poción Variocolor in the running game. Says whether its byte is set and whether the patch is in
/// the code the game runs (code.bin's file offsets plus 0x100000). «poner» sets the byte by hand, without the bag, so a wild encounter right
/// after tells whether the maker half works on its own.
/// </summary>
internal static class ShinyPotionProbe
{
    private const uint CodeBase = 0x100000;

    public static int Run(string mode)
    {
        using var client = new AzaharRpcClient();
        if (!client.TryReadMemory(RuleBlock.ShinyPotion, 1, out var flag))
        {
            Console.WriteLine("No llego a la memoria del juego. ¿Está el juego abierto en Azahar?");
            return 1;
        }

        Console.WriteLine($"Byte de la poción (0x{RuleBlock.ShinyPotion:X8}): {flag[0]}");
        Show(client, "lectura de tiradas", RulePatches.ShinyRollsSite, 1);
        Show(client, "primera mitad", RulePatches.ShinyCaveA, 5);
        Show(client, "segunda mitad", RulePatches.ShinyCaveB, 5);

        if (mode == "poner")
        {
            client.WriteMemory(RuleBlock.ShinyPotion, [1]);
            client.TryReadMemory(RuleBlock.ShinyPotion, 1, out flag);
            Console.WriteLine($"Puesto a mano: ahora vale {flag[0]}. Entra en la hierba: el siguiente salvaje debería salir variocolor.");
        }

        return 0;
    }

    private static void Show(AzaharRpcClient client, string what, int fileOffset, int words)
    {
        var at = CodeBase + (uint)fileOffset;
        if (!client.TryReadMemory(at, words * 4, out var bytes)) { Console.WriteLine($"  {what}: no se lee 0x{at:X8}"); return; }
        Console.WriteLine($"  {what} (0x{at:X8}): " + string.Join(" ", Enumerable.Range(0, words).Select(i => BitConverter.ToUInt32(bytes, i * 4).ToString("X8"))));
    }
}
