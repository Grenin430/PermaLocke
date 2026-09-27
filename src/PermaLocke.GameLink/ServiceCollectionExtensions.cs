using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Battle;
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
        services.TryAddSingleton<ITypeLookup>(_ => new PkhexTypeLookup(language));
        services.TryAddSingleton<AzaharRpcClient>();
        services.TryAddSingleton<BattleTableReader>();
        services.TryAddSingleton(sp => new BagService(
            sp.GetRequiredService<AzaharRpcClient>(),
            sp.GetRequiredService<AzaharGameWriter>(),
            Path.Combine(backupFolder, "objetos-retirados.txt"),
            Path.Combine(backupFolder, "mochila.txt"),
            sp.GetRequiredService<ILogger<BagService>>(),
            sp.GetRequiredService<Field.SavedGameCache>()));

        services.TryAddSingleton(sp => new AzaharGameWriter(
            sp.GetRequiredService<AzaharRpcClient>(),
            backupFolder,
            sp.GetRequiredService<ILogger<AzaharGameWriter>>()));
        services.TryAddSingleton(sp => new PlayerSave(
            sp.GetRequiredService<AzaharInstallation>(),
            sp.GetRequiredService<AzaharRpcClient>(),
            AppContext.BaseDirectory));
        services.TryAddSingleton<IGameUnlocks>(sp => new SaveGameUnlocks(
            sp.GetRequiredService<PlayerSave>(),
            backupFolder,
            sp.GetRequiredService<ILogger<SaveGameUnlocks>>()));
        services.TryAddSingleton(sp => new SaveEraser(
            sp.GetRequiredService<PlayerSave>(),
            backupFolder,
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<ILogger<SaveEraser>>()));
        services.TryAddSingleton<SaveBoxDelivery>(sp => new SaveBoxDelivery(
            sp.GetRequiredService<PlayerSave>(),
            backupFolder,
            sp.GetRequiredService<ILogger<SaveBoxDelivery>>()));
        services.TryAddSingleton<IPokemonDelivery>(sp => sp.GetRequiredService<SaveBoxDelivery>());
        services.TryAddSingleton<SaveBoxReader>(sp => new SaveBoxReader(
            sp.GetRequiredService<PlayerSave>(),
            sp.GetRequiredService<ILocationLookup>(),
            language,
            sp.GetRequiredService<ILogger<SaveBoxReader>>()));
        services.TryAddSingleton<IBoxReader>(sp => sp.GetRequiredService<SaveBoxReader>());
        services.TryAddSingleton<SaveRecordReader>(sp => new SaveRecordReader(
            sp.GetRequiredService<PlayerSave>(),
            sp.GetRequiredService<ILogger<SaveRecordReader>>()));
        services.TryAddSingleton<IGameRecords>(sp => sp.GetRequiredService<SaveRecordReader>());
        services.TryAddSingleton<SaveBoxSwap>(sp => new SaveBoxSwap(
            sp.GetRequiredService<PlayerSave>(),
            backupFolder,
            sp.GetRequiredService<ILogger<SaveBoxSwap>>()));
        services.TryAddSingleton<IPokemonSwap>(sp => sp.GetRequiredService<SaveBoxSwap>());
        services.TryAddSingleton<SaveEvTrainer>(sp => new SaveEvTrainer(
            sp.GetRequiredService<PlayerSave>(),
            backupFolder,
            sp.GetRequiredService<ILogger<SaveEvTrainer>>()));
        services.TryAddSingleton<IEvTrainer>(sp => sp.GetRequiredService<SaveEvTrainer>());
        // El recuerda-movimientos (§142): mismas guardas que los EV, y lo que se puede recordar sale del mundo
        // instalado, no de las tablas del cartucho.
        services.TryAddSingleton<SaveMoveTeacher>(sp => new SaveMoveTeacher(
            sp.GetRequiredService<PlayerSave>(),
            backupFolder,
            sp.GetRequiredService<ILogger<SaveMoveTeacher>>()));
        services.TryAddSingleton<IMoveTeacher>(sp => sp.GetRequiredService<SaveMoveTeacher>());
        // El mote desde el VISOR (2026-09-26): mismas guardas, y se relee lo escrito.
        services.TryAddSingleton<IPokemonRenamer>(sp => new SaveRenamer(
            sp.GetRequiredService<PlayerSave>(),
            backupFolder,
            sp.GetRequiredService<ILogger<SaveRenamer>>()));
        // Las hierbas de naturaleza de la TIENDA (2026-09-27): las mismas guardas que el mote.
        services.TryAddSingleton<INatureChanger>(sp => new SaveNatureChanger(
            sp.GetRequiredService<PlayerSave>(),
            backupFolder,
            sp.GetRequiredService<ILogger<SaveNatureChanger>>()));
        services.TryAddSingleton<IMoveCatalog>(_ => new WorldMoveCatalog(language));
        services.TryAddSingleton<IStatForecast, WorldStatForecast>();
        // La zona, los contadores del juego y la Pokédex (§117). FieldZoneReader necesita la MapTable, que
        // registra quien sabe dónde está Data/mapas.json.
        services.TryAddSingleton<Field.SavedGameCache>();
        services.TryAddSingleton(sp => new Field.FieldZoneReader(
            sp.GetRequiredService<AzaharRpcClient>(),
            sp.GetRequiredService<Field.SavedGameCache>(),
            sp.GetRequiredService<Core.Domain.MapTable>(),
            Path.Combine(backupFolder, "registros-de-posicion.txt"),
            sp.GetRequiredService<ILogger<Field.FieldZoneReader>>()));
        services.TryAddSingleton<IZoneProvider>(sp => sp.GetRequiredService<Field.FieldZoneReader>());
        services.TryAddSingleton<Field.BattleCounterReader>();
        services.TryAddSingleton<IBattleCounters>(sp => sp.GetRequiredService<Field.BattleCounterReader>());
        services.TryAddSingleton<IOwnedSpecies, Field.SaveDex>();
        services.TryAddSingleton<IItemWithholder>(sp => sp.GetRequiredService<BagService>());
        services.TryAddSingleton(sp => new AzaharGameStateProvider(
            sp.GetRequiredService<AzaharRpcClient>(),
            sp.GetRequiredService<ISpeciesLookup>(),
            sp.GetRequiredService<ILocationLookup>(),
            Path.Combine(backupFolder, "equipo.txt"),
            sp.GetRequiredService<ILogger<AzaharGameStateProvider>>(),
            savedPartyKeys: SavedPartyKeys(sp.GetRequiredService<Field.SavedGameCache>())));
        services.TryAddSingleton<IGameStateProvider>(sp => sp.GetRequiredService<AzaharGameStateProvider>());

        return services;
    }

    /// <summary>
    /// The encryption constants of the party in the last save, or null when there is no save: what tells the provider
    /// whether a full sweep has anything to find.
    /// </summary>
    private static Func<IReadOnlyList<uint>?> SavedPartyKeys(Field.SavedGameCache saved) => () =>
        saved.Load() is { } game
            ? [.. game.Save.PartyData.Select(pokemon => pokemon.EncryptionConstant).Where(key => key != 0)]
            : null;
}
