using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Services;

namespace PermaLocke.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Domain services shared by PermaLocke.App and PermaLocke.Admin. Neither application
    /// implements business logic of its own; both resolve these.
    /// </summary>
    public static IServiceCollection AddPermaLockeCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IRunContext, RunContext>();
        services.TryAddSingleton<IPointsService, PointsService>();
        services.TryAddSingleton<RunService>();
        services.TryAddSingleton<PlayerProfileService>();
        return services;
    }
}
