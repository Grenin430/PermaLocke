using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Dumps the live party from <b>every</b> copy PermaLocke knows about, with the two fields that
/// decide a Pokémon's level side by side.
/// </summary>
/// <remarks>
/// The level cap writes into the party in memory and the game was undoing it. Which copy the game
/// actually reads is the question that decides whether the cap can work at all, and this is how it
/// gets answered: write to one, look at all of them, and see which ones moved.
/// </remarks>
public static class PartyLiveProbe
{
    public static int Run(string? trainer, int? cap)
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("No hay juego cargado en Azahar: " + ex.Message);
            return 1;
        }

        var layouts = PartyLayoutLocator.Distinct(
            new PartyLayoutLocator(client).LocateAll(trainer ?? string.Empty));

        if (layouts.Count == 0)
        {
            Console.WriteLine("No se ha encontrado ninguna copia del equipo.");
            return 1;
        }

        Console.WriteLine($"{layouts.Count} estructuras distintas del equipo:");
        Console.WriteLine();

        var names = GameInfo.GetStrings("es").specieslist;
        var writer = new AzaharGameWriter(client, Path.Combine(Path.GetTempPath(), "permalocke-probe"),
            NullLogger<AzaharGameWriter>.Instance);

        foreach (var layout in layouts)
        {
            Console.WriteLine($"0x{layout.Address:X8}  salto 0x{layout.Stride:X}  «{layout.TrainerName}»");

            for (var slot = 0; slot < 6; slot++)
            {
                var address = layout.SlotAddress(slot);

                if (writer.Read(address) is not { Species: > 0 } pokemon || !pokemon.ChecksumValid)
                {
                    continue;
                }

                var name = pokemon.Species < names.Length ? names[pokemon.Species] : "?";
                // Stat_Level solo significa algo en la estructura de equipo de verdad; en las
                // demás cae fuera de sitio y devuelve cualquier cosa. Se enseña para verlo, no
                // para creerselo.
                var mismatch = pokemon.Stat_Level != pokemon.CurrentLevel ? "  <-- ese campo no es el nivel aqui" : string.Empty;

                Console.WriteLine($"   hueco {slot} 0x{address:X8}  {name,-12} PID {pokemon.PID:X8}  "
                                  + $"nivel {pokemon.CurrentLevel,3}   [0xEC] {pokemon.Stat_Level,3}{mismatch}");
            }

            Console.WriteLine();
        }

        if (cap is not { } wanted)
        {
            Console.WriteLine("Para probar la escritura: Probe --equipo --cap 24");
            return 0;
        }

        Console.WriteLine($"Aplicando el cap {wanted} a todo lo que esté por encima, copia por copia:");
        Console.WriteLine();

        foreach (var layout in layouts)
        {
            for (var slot = 0; slot < 6; slot++)
            {
                var address = layout.SlotAddress(slot);

                if (writer.Read(address) is not { Species: > 0 } pokemon || !pokemon.ChecksumValid)
                {
                    continue;
                }

                if (pokemon.CurrentLevel <= wanted)
                {
                    continue;
                }

                var result = writer.EnforceLevelCap(address, wanted, pokemon.PID);
                var name = pokemon.Species < names.Length ? names[pokemon.Species] : "?";

                Console.WriteLine($"   0x{address:X8} {name,-12} nivel {pokemon.Stat_Level,3} -> "
                                  + (result.Applied ? "ESCRITO Y RELEIDO"
                                      : result.Rejected ? $"RECHAZADO ({result.Verified}/{result.Written} bytes)"
                                      : "no se ha tocado"));
            }
        }

        Console.WriteLine();
        Console.WriteLine("Vuelve a ejecutar Probe --equipo dentro del juego y despues de un combate:");
        Console.WriteLine("lo que vuelva a subir dice que copia manda de verdad.");

        return 0;
    }
}
