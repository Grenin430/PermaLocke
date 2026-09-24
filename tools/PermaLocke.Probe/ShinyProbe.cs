using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Probe;

/// <summary>
/// <c>--shiny-siempre [quitar]</c>: puts or takes away the test patch that makes every generated Pokémon shiny.
/// </summary>
/// <remarks>
/// A file beside the installed mod's <c>code.bin</c>, never the <c>code.bin</c> itself (<see cref="AlwaysShinyPatch"/>).
/// Refuses while Azahar is running — the patch is read at boot, and nothing in the installed mod is written with the
/// emulator open — and only ever deletes a patch whose bytes are exactly the ones it writes.
/// </remarks>
internal static class ShinyProbe
{
    public static int Run(string mode)
    {
        var location = new AzaharInstallation(NullLogger<AzaharInstallation>.Instance).Locate(AppContext.BaseDirectory);
        var exefs = Path.Combine(AzaharInstallation.ModDirectory(location, LayeredFsMod.UltraMoonProgramId), "exefs");
        var codeBin = Path.Combine(exefs, "code.bin");
        var patch = Path.Combine(exefs, "code.ips");

        if (Process.GetProcessesByName("azahar").Length > 0)
        {
            Console.WriteLine("Azahar está abierto. Ciérralo: el parche se lee al arrancar el juego y no se toca el mod con el emulador abierto.");
            return 1;
        }

        if (mode == "quitar")
        {
            return Remove(codeBin, patch);
        }

        if (!File.Exists(codeBin))
        {
            Console.WriteLine($"No hay {codeBin}. Sin un code.bin en el mod no hay dónde medir el sitio del parche.");
            return 1;
        }

        var code = File.ReadAllBytes(codeBin);

        if (AlwaysShinyPatch.Find(code) is not { } offset)
        {
            Console.WriteLine("El patrón no aparece exactamente una vez en el code.bin del mod: no se parchea nada.");
            return 1;
        }

        if (code[offset] != AlwaysShinyPatch.Original)
        {
            Console.WriteLine($"En 0x{offset:X6} hay 0x{code[offset]:X2} y no 0x{AlwaysShinyPatch.Original:X2}: alguien ya ha tocado ese salto.");
            return 1;
        }

        if (File.Exists(patch) && !File.ReadAllBytes(patch).SequenceEqual(AlwaysShinyPatch.Ips(offset)))
        {
            Console.WriteLine($"Ya hay un {patch} que no es este. No se sobrescribe.");
            return 1;
        }

        var ips = AlwaysShinyPatch.Ips(offset);
        File.WriteAllBytes(patch, ips);

        if (!File.ReadAllBytes(patch).SequenceEqual(ips))
        {
            Console.WriteLine("El parche no se ha escrito como debía.");
            return 1;
        }

        Console.WriteLine($"Todo shiny: {patch}");
        Console.WriteLine($"  0x{offset:X6}: 0x{AlwaysShinyPatch.Original:X2} -> 0x{AlwaysShinyPatch.Always:X2} al arrancar. El code.bin no se ha tocado.");
        Console.WriteLine("  Para quitarlo: --shiny-siempre quitar (con Azahar cerrado).");
        return 0;
    }

    private static int Remove(string codeBin, string patch)
    {
        if (!File.Exists(patch))
        {
            Console.WriteLine("No hay parche que quitar.");
            return 0;
        }

        var ours = File.Exists(codeBin) && AlwaysShinyPatch.Find(File.ReadAllBytes(codeBin)) is { } offset
                   && File.ReadAllBytes(patch).SequenceEqual(AlwaysShinyPatch.Ips(offset));

        if (!ours)
        {
            Console.WriteLine($"{patch} no es el parche de shiny de PermaLocke. No se borra.");
            return 1;
        }

        File.Delete(patch);
        Console.WriteLine("Parche de shiny quitado: el juego vuelve a su probabilidad normal al arrancar.");
        return 0;
    }
}
