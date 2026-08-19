using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Infrastructure.Logging;

namespace PermaLocke.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}

public static class ServiceCollectionExtensions
{
    /// <summary>Folder layout, clock and file logging. Shared by both applications.</summary>
    public static IServiceCollection AddPermaLockeInfrastructure(
        this IServiceCollection services,
        AppPaths paths,
        string logFilePrefix,
        LogLevel minimumLevel = LogLevel.Information)
    {
        services.TryAddSingleton(paths);
        services.TryAddSingleton<IClock, SystemClock>();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddProvider(new FileLoggerProvider(paths.Logs, logFilePrefix, minimumLevel));
        });

        return services;
    }
}
