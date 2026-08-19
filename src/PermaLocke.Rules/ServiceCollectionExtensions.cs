using Microsoft.Extensions.DependencyInjection;
using PermaLocke.Rules.Rules;

namespace PermaLocke.Rules;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the engine and every rule. Adding a rule means adding one line here and one
    /// entry in Data/rules.json; nothing else in the application changes.
    /// </summary>
    public static IServiceCollection AddPermaLockeRules(this IServiceCollection services, string rulesJsonPath)
    {
        services.AddSingleton<IRule, FirstEncounterRule>();
        services.AddSingleton<IRule, ShinyClauseRule>();
        services.AddSingleton<IRule, DupesClauseRule>();
        services.AddSingleton<IRule, SpeciesClauseRule>();
        services.AddSingleton<IRule, GiftPokemonRule>();
        services.AddSingleton<IRule, StaticEncounterRule>();
        services.AddSingleton<IRule, LevelCapRule>();

        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton(_ => RulesConfigurationLoader.Load(rulesJsonPath));
        services.AddSingleton(_ => LevelCapTable.Load(
            Path.Combine(Path.GetDirectoryName(rulesJsonPath)!, "levelcaps.json")));
        services.AddSingleton(_ => ZoneTable.Load(
            Path.Combine(Path.GetDirectoryName(rulesJsonPath)!, "zones.json")));
        services.AddSingleton<IEvolutionLineProvider, NullEvolutionLineProvider>();
        services.AddSingleton<Services.ProgressService>();

        // La regla de las balls lee su lista de objetos de la configuración: sin lista, se apaga
        // sola en vez de decidir por su cuenta qué quitarle al jugador de la mochila.
        services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<RulesConfiguration>();

            return new Services.BallControlService(
                provider.GetRequiredService<Core.Abstractions.IZoneProvider>(),
                provider.GetRequiredService<Core.Abstractions.IItemWithholder>(),
                provider.GetRequiredService<ZoneTable>(),
                provider.GetRequiredService<Core.Abstractions.IEventStore>(),
                provider.GetRequiredService<Core.Abstractions.IClock>())
            {
                Enabled = configuration.BallControl.Enabled,
                BallItemIds = configuration.BallControl.ItemIds
            };
        });

        return services;
    }
}
