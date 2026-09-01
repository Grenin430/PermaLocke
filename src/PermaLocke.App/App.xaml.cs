using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.App.ViewModels;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.Data;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;
using PermaLocke.Rules;
using PermaLocke.Rules.Services;

namespace PermaLocke.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new AppPaths();
        paths.EnsureCreated();

        // ANTES de que nada abra la base de datos, y por eso está aquí arriba y no dentro de un
        // servicio: es el único momento en el que el fichero está garantizadamente en reposo.
        // Copiar un SQLite que otro está escribiendo puede capturar una página a medias, y una
        // copia que quizá esté corrupta es peor que ninguna, porque en ella se confía.
        //
        // No se espera a que falle nada ni se comprueba el resultado: Run() no lanza nunca, y un
        // arranque no se detiene porque una copia no haya salido. Lo que pasó queda en el log.
        new RunBackup(paths.Saves).Run(DateTimeOffset.Now);

        var collection = new ServiceCollection();
        collection.AddPermaLockeInfrastructure(paths, "permalocke");
        collection.AddPermaLockeData(paths.Saves);
        collection.AddPermaLockeCore();
        collection.AddPermaLockeRules(Path.Combine(paths.Data, "rules.json"));
        collection.AddPermaLockeGameLink(paths.SaveBackups);

        // Gacha: los banners y la tabla de especies son configuración; sin ellas la sección lo
        // dice en pantalla en vez de tirar con datos inventados.
        collection.AddSingleton<IGachaCatalog>(_ =>
            JsonGachaCatalog.Load(Path.Combine(paths.Data, "gacha.json")));
        collection.AddSingleton<ISpeciesStatsCatalog>(_ =>
            JsonSpeciesStatsCatalog.Load(Path.Combine(paths.Data, "species.json")));
        collection.AddSingleton<GachaService>();
        collection.AddSingleton<IWonderTradeCatalog>(_ =>
            JsonWonderTradeCatalog.Load(Path.Combine(paths.Data, "wondertrade.json")));
        collection.AddSingleton<WonderTradeService>();
        collection.AddSingleton<EncounterService>();
        collection.AddSingleton<IPenaltyCatalog>(_ =>
            JsonPenaltyCatalog.Load(Path.Combine(paths.Data, "penalties.json")));
        collection.AddSingleton<PenaltyService>();
        collection.AddSingleton<GameWatcher>();

        collection.AddSingleton<AzaharInstallation>();
        collection.AddSingleton<IAppDialogs, AppDialogs>();
        collection.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        collection.AddSingleton<GameLinkMonitor>();
        collection.AddSingleton<PokemonSpriteService>();
        collection.AddTransient<CreateRunViewModel>();
        collection.AddTransient<RegisterCaptureViewModel>();
        collection.AddTransient<ChangeRoleViewModel>();
        collection.AddSingleton<HomeViewModel>();
        collection.AddSingleton<RandomizerViewModel>();
        collection.AddSingleton<GachaViewModel>();
        collection.AddSingleton<WonderTradeViewModel>();
        collection.AddSingleton<IAchievementCatalog>(_ =>
            JsonAchievementCatalog.Load(Path.Combine(paths.Data, "achievements.json")));
        collection.AddSingleton<AchievementService>();
        collection.AddSingleton<AchievementsViewModel>();
        collection.AddSingleton<IRoleCatalog>(_ =>
            JsonRoleCatalog.Load(Path.Combine(paths.Data, "roles.json")));
        collection.AddSingleton<IRunRoles, RunRoles>();
        collection.AddSingleton<IShopCatalog>(_ =>
            JsonShopCatalog.Load(Path.Combine(paths.Data, "shop.json")));
        collection.AddSingleton<IItemDelivery, BagItemDelivery>();
        collection.AddSingleton<ShopService>();
        collection.AddSingleton<EvTrainingService>();
        collection.AddSingleton<PokemonIdentityService>();
        collection.AddSingleton<IRewardCatalog>(_ =>
            JsonRewardCatalog.Load(Path.Combine(paths.Data, "rewards.json")));
        collection.AddSingleton<RewardService>();
        collection.AddSingleton<IAbilityLookup>(_ => new PermaLocke.GameLink.Data.PkhexAbilityLookup());
        collection.AddSingleton<IRouletteCatalog>(sp =>
            JsonRouletteCatalog.Load(Path.Combine(paths.Data, "roulette.json"),
                sp.GetRequiredService<IAbilityLookup>()));
        collection.AddSingleton<IRouletteWorldPort>(sp => new SaveRouletteWorld(
            sp.GetRequiredService<PlayerSave>(), paths.SaveBackups,
            sp.GetRequiredService<ISpeciesLookup>(), sp.GetRequiredService<IItemLookup>(),
            sp.GetRequiredService<IAbilityLookup>(),
            sp.GetRequiredService<ILogger<SaveRouletteWorld>>()));
        collection.AddSingleton<ICreditCatalog>(_ =>
            JsonCreditCatalog.Load(Path.Combine(paths.Data, "grants.json")));
        collection.AddSingleton<CreditService>();
        collection.AddSingleton<RouletteService>();
        collection.AddSingleton<RouletteViewModel>();
        collection.AddSingleton<ShopViewModel>();
        collection.AddSingleton<PokemonViewerViewModel>();
        collection.AddSingleton<PokePasteViewModel>();
        collection.AddSingleton<MiscellaneousViewModel>();
        collection.AddSingleton<TradedAwayReconciler>();
        collection.AddSingleton<BattleModeService>();
        collection.AddSingleton<BattleModeViewModel>();
        collection.AddSingleton<SnapshotStore>();
        collection.AddSingleton<SyncService>();
        collection.AddSingleton<SyncViewModel>();
        collection.AddSingleton<StatisticsService>();
        collection.AddSingleton<StatisticsViewModel>();
        collection.AddSingleton<MaintenanceService>();
        collection.AddSingleton<MaintenanceViewModel>();
        collection.AddSingleton<MainViewModel>();

        _services = collection.BuildServiceProvider();

        var logger = _services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("PermaLocke iniciado. Raíz de datos: {Root}", paths.Root);

        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError(args.Exception, "Excepción no controlada en la interfaz");
            MessageBox.Show(
                "Ha ocurrido un error inesperado. El detalle técnico se ha escrito en la carpeta Logs.",
                "PermaLocke", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // The dispatcher handler above only sees UI thread failures. These two catch the rest,
        // so a crash on a background thread can never again disappear without a trace.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            logger.LogCritical(args.ExceptionObject as Exception,
                "Excepción no controlada fuera del hilo de interfaz. Terminando: {Terminating}",
                args.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogError(args.Exception, "Excepción de tarea sin observar");
            args.SetObserved();
        };

        var main = _services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main };
        MainWindow = window;
        window.Show();

        var run = await _services.GetRequiredService<RunService>().LoadMostRecentAsync();
        logger.LogInformation("Run cargada al inicio: {Run}", run?.Name ?? "ninguna");

        await main.InitialiseAsync();

        // Started last: it polls the emulator, and there is no point doing that before the
        // run it belongs to has been loaded.
        _services.GetRequiredService<GameLinkMonitor>().Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
