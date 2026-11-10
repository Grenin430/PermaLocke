using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Games;

/// <summary>
/// Ultra Luna, the launcher's first game (2026-11-10): everything of PermaLocke that belongs to it and not to the app. The 3DS emulator
/// (Azahar) and its launcher, the save and the live memory (GameLink), the installed world made with pk3DS, Alola's maps and trials, the
/// cap panel over the game and what rides on it (the Pokévial, the field items, Alola's sky).
/// </summary>
/// <remarks>
/// Registered after the common services, because it replaces one of them: the empty evolution
/// lines of Rules become the installed world's. The app's screens still ask for these services directly; a second game will have to give
/// its own of each, or the screens learn to do without.
/// </remarks>
public static class UltraMoonModule
{
    public static IServiceCollection AddUltraMoon(this IServiceCollection collection, AppPaths paths)
    {
        // ANTES de GameLink, que registra el suyo con TryAdd: asi una MT dice que movimiento
        // ensena en ESTE mundo en vez de solo su numero, que con las maquinas randomizadas no
        // significa nada. Va aqui porque necesita leer el code.bin del mod instalado, y eso es
        // cosa de la aplicacion: GameLink no conoce al randomizador.
        collection.AddSingleton<IItemLookup>(sp => new MachineItemLookup(
            new PermaLocke.GameLink.Data.PkhexItemLookup(),
            sp.GetRequiredService<AzaharInstallation>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<MachineItemLookup>()));

        collection.AddPermaLockeGameLink(paths.SaveBackups);


        collection.AddSingleton<AzaharInstallation>();
        collection.AddSingleton<InstalledWorld>();

        // LA REGLA DE PRIMER ENCUENTRO (§117). Los mapas del cartucho para saber dónde está el jugador, y las
        // líneas evolutivas del mundo instalado para los duplicados: esta segunda sustituye a la vacía que
        // registra Rules, porque es la última que se registra la que se sirve.
        collection.AddSingleton(_ => JsonMapTable.Load(paths.DataFile("mapas.json")));
        collection.AddSingleton<WorldEvolutionLines>();
        collection.AddSingleton<WorldItemCatalog>();
        collection.AddSingleton<PermaLocke.Rules.IEvolutionLineProvider>(sp => sp.GetRequiredService<WorldEvolutionLines>());
        collection.AddSingleton<WorldAllowedStatics>();
        collection.AddSingleton<PermaLocke.Rules.Services.TrialZoneService>();

        // La memoria en vivo del juego y lo que el panel del cap dibuja encima.
        collection.AddSingleton<VitalsPump>();
        collection.AddSingleton<BarSync>();
        collection.AddSingleton<ActiveBar>();
        collection.AddSingleton<CapBadge>();
        collection.AddSingleton<PokeVialService>();
        collection.AddSingleton<FieldItemsService>();
        collection.AddSingleton<AlolaSky>();

        // Las habilidades y el mundo que la ruleta toca, de la partida de Ultra Luna.
        collection.AddSingleton<IAbilityLookup>(_ => new PermaLocke.GameLink.Data.PkhexAbilityLookup());
        collection.AddSingleton<IRouletteWorldPort>(sp => new SaveRouletteWorld(
            sp.GetRequiredService<PlayerSave>(), paths.SaveBackups,
            sp.GetRequiredService<ISpeciesLookup>(), sp.GetRequiredService<IItemLookup>(),
            sp.GetRequiredService<IAbilityLookup>(),
            sp.GetRequiredService<ILogger<SaveRouletteWorld>>()));

        // EL LANZADOR (§125): abrir, vigilar y cerrar Azahar.
        collection.AddSingleton<EmulatorCrashReport>();
        collection.AddSingleton<EmulatorLauncher>();

        return collection;
    }
}
