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
        // ANTES de GameLink, que registra el suyo con TryAdd: asi una MT dice que movimiento
        // ensena en ESTE mundo en vez de solo su numero, que con las maquinas randomizadas no
        // significa nada. Va aqui porque necesita leer el code.bin del mod instalado, y eso es
        // cosa de la aplicacion: GameLink no conoce al randomizador.
        collection.AddSingleton<IItemLookup>(sp => new Services.MachineItemLookup(
            new PermaLocke.GameLink.Data.PkhexItemLookup(),
            sp.GetRequiredService<AzaharInstallation>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<Services.MachineItemLookup>()));

        collection.AddPermaLockeGameLink(paths.SaveBackups);

        // EL ALMACEN DE EVENTOS, ENVUELTO para que avise cuando la run cambia. Se hace aqui y
        // despues de registrarlo porque la envoltura es cosa de la aplicacion que dibuja los
        // numeros, no del almacen: sin esto, comprar algo cambiaba el saldo en la base de datos y
        // la cifra de la cabecera se quedaba igual hasta cambiar de seccion.
        collection.AddSingleton<RunActivity>();
        Decorate(collection);

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
        collection.AddSingleton<ZoneOutcomeService>();
        collection.AddSingleton<IPenaltyCatalog>(_ =>
            JsonPenaltyCatalog.Load(Path.Combine(paths.Data, "penalties.json")));
        collection.AddSingleton<PenaltyService>();
        collection.AddSingleton<GameWatcher>();

        collection.AddSingleton<AzaharInstallation>();
        collection.AddSingleton<InstalledWorld>();
        collection.AddSingleton<IAppDialogs, AppDialogs>();
        collection.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        collection.AddSingleton<GameLinkMonitor>();

        // LOS AVISOS ENCIMA DEL JUEGO. Su propia ventana, para que salgan con PermaLocke
        // minimizado, que es como se juega. PlayNotifications solo se suscribe: hay que pedirlo
        // una vez para que exista, y eso se hace al arrancar el vigilante.
        collection.AddSingleton<Notifier>();
        collection.AddSingleton<PlayNotifications>();
        collection.AddSingleton<DeathCeremony>();

        // Y la pestaña del borde, que es la misma ventana flotante SIN la bandera que deja pasar
        // los clics: esta existe para que se pulse.
        collection.AddSingleton<EdgeTab>();
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
        // El reparto de zonas por isla, medido del cartucho con «RomTool mundos». Si falta el
        // fichero, el mapa lo dice en vez de dibujar media Alola.
        collection.AddSingleton(_ =>
            JsonIslandMap.Load(Path.Combine(paths.Data, "islas.json")));
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
        collection.AddSingleton<IslandMapService>();
        collection.AddSingleton<ZonePhotoService>();
        collection.AddSingleton<WindowSizeService>();
        collection.AddSingleton<MapViewModel>();
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

        // Antes de que el enlace con el juego empiece a leer: fija el techo de especies segun el
        // mundo que Azahar va a cargar de verdad. Ver InstalledWorld.
        _services.GetRequiredService<InstalledWorld>().Apply(AppContext.BaseDirectory);

        var main = _services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main };
        window.Resize(_services.GetRequiredService<WindowSizeService>().Current);
        MainWindow = window;
        window.Show();

        var run = await _services.GetRequiredService<RunService>().LoadMostRecentAsync();
        logger.LogInformation("Run cargada al inicio: {Run}", run?.Name ?? "ninguna");

        await main.InitialiseAsync();

        // Started last: it polls the emulator, and there is no point doing that before the
        // run it belongs to has been loaded.
        // Antes de arrancar el vigilante, para no perderse lo que pase en su primer ciclo.
        _services.GetRequiredService<PlayNotifications>();
        _services.GetRequiredService<EdgeTab>().Attach(window);

        _services.GetRequiredService<GameLinkMonitor>().Start();

        if (e.Args.Contains("--ensayar-muerte", StringComparer.OrdinalIgnoreCase))
        {
            await RehearseDeathsAsync(logger);
        }
    }

    /// <summary>
    /// Replays the ceremony of the last three deaths already in the run, recording nothing.
    /// </summary>
    /// <remarks>
    /// The animation can only be seen when something dies, and killing a Pokémon to look at a
    /// storyboard is not an option. These are deaths that DID happen, shown again, with what each
    /// one cost read from its own penalty event: nothing is recorded and nothing is charged.
    /// </remarks>
    private async Task RehearseDeathsAsync(ILogger logger)
    {
        var fallen = await _services!.GetRequiredService<MaintenanceService>().FallenAsync();
        var ceremony = _services!.GetRequiredService<DeathCeremony>();
        var run = _services!.GetRequiredService<IRunContext>().Current;
        var history = run is null
            ? []
            : await _services!.GetRequiredService<IEventStore>().GetAllAsync(run.Id);

        logger.LogInformation("Ensayo de la animación de muerte con {Count} caídos ya registrados; no se escribe nada",
            Math.Min(3, fallen.Count));

        foreach (var entry in fallen.Take(3))
        {
            var cost = -history
                .Where(e => e.Type == GameEventType.PointsPenalty && e.PokemonId == entry.Id)
                .Sum(e => e.PointsDelta);

            ceremony.Mourn(new DeathNotice(entry.Nickname ?? entry.SpeciesName, entry.Species, Math.Max(0, cost)));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
    /// <summary>
    /// Replaces the registered <see cref="IEventStore"/> with one that announces what it stores.
    /// </summary>
    /// <remarks>
    /// Written by hand because the container has no decoration of its own and pulling in a package
    /// for six lines would be worse. It takes the descriptor that is already there and builds the
    /// inner store from it, so whoever registered it stays the one who decides how it is made.
    /// </remarks>
    private static void Decorate(IServiceCollection collection)
    {
        var registered = collection.Last(service => service.ServiceType == typeof(IEventStore));

        collection.Remove(registered);

        collection.AddSingleton<IEventStore>(provider => new WatchedEventStore(
            (IEventStore)registered.ImplementationFactory!(provider),
            provider.GetRequiredService<RunActivity>()));
    }

}
