using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.Data;

public static class ServiceCollectionExtensions
{
    /// <param name="savesRoot">The <c>Saves/</c> folder: one run.json per run plus the shared event database.</param>
    public static IServiceCollection AddPermaLockeData(this IServiceCollection services, string savesRoot)
    {
        services.TryAddSingleton(sp => new RunBackup(savesRoot,
            sp.GetService<Microsoft.Extensions.Logging.ILogger<RunBackup>>()));

        services.TryAddSingleton<IEventStore>(_ =>
            new SqliteEventStore(Path.Combine(savesRoot, "permalocke.db")));

        services.TryAddSingleton<IPokemonRepository>(_ =>
            new SqlitePokemonRepository(Path.Combine(savesRoot, "permalocke.db")));

        services.TryAddSingleton<IRunRepository>(_ => new JsonRunRepository(savesRoot));

        return services;
    }
}
