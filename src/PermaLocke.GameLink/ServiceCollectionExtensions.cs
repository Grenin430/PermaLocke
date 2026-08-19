using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

public static class ServiceCollectionExtensions
{
    /// <param name="language">PKHeX language code for species names; "es" for Spanish.</param>
    /// <param name="backupFolder">Where the bytes replaced by every write are kept.</param>
    public static IServiceCollection AddPermaLockeGameLink(this IServiceCollection services,
        string backupFolder, string language = "es")
    {
        // Building the species table walks the whole Pokédex, so it is done once.
        services.TryAddSingleton<ISpeciesLookup>(_ => new PkhexSpeciesLookup(language));

        services.TryAddSingleton<ILocationLookup>(_ => new PkhexLocationLookup(language));
        services.TryAddSingleton<IItemLookup>(_ => new PkhexItemLookup(language));
        services.TryAddSingleton<AzaharRpcClient>();
        services.TryAddSingleton(sp => new BagService(
            sp.GetRequiredService<AzaharRpcClient>(),
            sp.GetRequiredService<AzaharGameWriter>(),
            Path.Combine(backupFolder, "objetos-retirados.txt"),
            Path.Combine(backupFolder, "mochila.txt"),
            sp.GetRequiredService<ILogger<BagService>>()));

        services.TryAddSingleton(sp => new AzaharGameWriter(
            sp.GetRequiredService<AzaharRpcClient>(),
            backupFolder,
            sp.GetRequiredService<ILogger<AzaharGameWriter>>()));
        services.TryAddSingleton<ZoneService>();
        services.TryAddSingleton<IZoneProvider>(sp => sp.GetRequiredService<ZoneService>());
        services.TryAddSingleton<IItemWithholder>(sp => sp.GetRequiredService<BagService>());
        services.TryAddSingleton(sp => new AzaharGameStateProvider(
            sp.GetRequiredService<AzaharRpcClient>(),
            sp.GetRequiredService<ISpeciesLookup>(),
            sp.GetRequiredService<ILocationLookup>(),
            Path.Combine(backupFolder, "equipo.txt"),
            sp.GetRequiredService<ILogger<AzaharGameStateProvider>>()));
        services.TryAddSingleton<IGameStateProvider>(sp => sp.GetRequiredService<AzaharGameStateProvider>());

        return services;
    }
}
