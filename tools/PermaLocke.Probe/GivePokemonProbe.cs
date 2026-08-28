using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;
using PermaLocke.Infrastructure;

namespace PermaLocke.Probe;

/// <summary>
/// Puts one specific Pokémon into the player's PC, for measuring things that need a species the
/// run does not happen to have.
/// </summary>
/// <remarks>
/// <para>
/// A development tool and nothing else: the competition hands Pokémon over through the gacha, the
/// wonder trade and the roulette, all of which decide <em>what</em> you get. This decides it for
/// you, which is exactly why it lives in the probe and not in the application.
/// </para>
/// <para>
/// It goes through <see cref="SaveBoxDelivery"/>, the same writer the gacha uses, so it inherits
/// the guards that matter: the game has to be closed, the whole save is copied first, and the
/// delivery is read back before it is reported.
/// </para>
/// </remarks>
public static class GivePokemonProbe
{
    public static int Run(int species, int level)
    {
        var root = Root();
        var paths = new AppPaths(root);

        using var client = new AzaharRpcClient();

        var save = new PlayerSave(
            new AzaharInstallation(NullLogger<AzaharInstallation>.Instance), client,
            AppContext.BaseDirectory);

        var delivery = new SaveBoxDelivery(save, paths.SaveBackups,
            NullLogger<SaveBoxDelivery>.Instance);

        if (!delivery.CanDeliverNow(out var reason))
        {
            Console.WriteLine(reason);
            return 1;
        }

        var names = new PkhexSpeciesLookup();
        Console.WriteLine($"Entregando {names.GetName(species)} (especie {species}) de nivel {level}...");

        // Naturaleza, habilidad e IV fijos: esto existe para medir, no para jugar, y un Pokémon
        // reproducible es más útil que uno bonito.
        var pull = new GachaPull(
            BannerId: "sonda", TierId: "sonda", Species: species, SpeciesName: names.GetName(species),
            Legendary: false, BaseStatTotal: 0, Level: level, IsShiny: false,
            Ivs: [31, 31, 31, 31, 31, 31], Nature: 0, NatureName: "Fuerte",
            AbilityId: 0, Ability: string.Empty, Seed: 0, Number: 0);

        var result = delivery.DeliverAsync(pull, null!).GetAwaiter().GetResult();

        Console.WriteLine(result.Message);
        return result.Delivered ? 0 : 1;
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
