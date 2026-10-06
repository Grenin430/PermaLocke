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
/// Written when the level cap was enforced by writing into the party in memory (removed in 1.0.9). Which copy the game
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

        if (cap is not null)
        {
            // Desde la 1.0.9 el cap lo pone el juego parcheado (RulePatches); la escritura en memoria se quitó.
            Console.WriteLine("--cap ya no escribe: el cap lo pone el propio juego (gameRulePatches).");
        }

        return 0;
    }
}
