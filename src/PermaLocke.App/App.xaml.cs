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

    /// <summary>Held for the life of the process: while it exists, another PermaLocke does not start.</summary>
    private static Mutex? _onlyOne;

    /// <summary>
    /// One PermaLocke at a time, from any folder: there is one emulator to talk to, and two of them share it badly.
    /// </summary>
    /// <remarks>
    /// Found on 2026-09-21 in the test folder's log, where every line from 20:41:33 came out twice: the player had
    /// opened PermaLocke again with the first one still running. Both watched the same game and both wrote into it —
    /// balls taken and given back twice over, the same battle placed in Ruta 2 by one and in Playa Big Wave by the
    /// other, two searches of memory for every one — and the emulator went down three minutes later in the middle of
    /// one of them. <c>--sin-juego</c> is exempt: it is the read-only copy made precisely to look at screens while the
    /// real one runs, and it opens no link to the game.
    /// </remarks>
    /// <summary>
    /// Says once what the transfer from the old folder did (§195), and writes it to the log, which did not exist yet when
    /// it ran.
    /// </summary>
    private static void TellTransfer(AppPaths paths, ILogger logger)
    {
        var path = Path.Combine(paths.Config, FolderTransfer.DoneFile);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<TransferResult>(File.ReadAllText(path));
            File.Delete(path);
            if (result is null)
            {
                return;
            }

            foreach (var line in result.Log)
            {
                logger.LogInformation("Traspaso: {Line}", line);
            }

            MessageBox.Show(MainWindowOrNull(),
                result.Done
                    ? $"Tu partida está aquí: {result.Files} ficheros copiados. La carpeta de antes sigue como estaba."
                      + (result.SetAside is { } aside ? $"\n\nLo que había aquí antes se ha apartado en «{aside}»." : string.Empty)
                    : "No se ha podido traer tu partida:\n\n" + string.Join("\n", result.Log),
                "Traer mi partida", MessageBoxButton.OK, result.Done ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo leer el resultado del traspaso");
        }
    }

    private static Window MainWindowOrNull() => Current.MainWindow;

    private static bool AnotherIsRunning(StartupEventArgs e)
    {
        if (e.Args.Contains("--sin-juego", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        _onlyOne = new Mutex(initiallyOwned: true, @"Local\PermaLocke.App", out var createdNew);

        // Reiniciado para el traspaso (§195): la de antes se está cerrando, se le da un momento.
        if (!createdNew && e.Args.Contains(Services.TransferOffer.RestartArgument, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                createdNew = _onlyOne.WaitOne(TimeSpan.FromSeconds(15));
            }
            catch (AbandonedMutexException)
            {
                createdNew = true;
            }
        }

        return !createdNew;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Lo primero de todo, antes de copiar la base de datos o de tocar nada.
        if (AnotherIsRunning(e))
        {
            MessageBox.Show(
                "PermaLocke ya está abierto. Usa esa ventana: dos a la vez vigilan y escriben en el mismo juego y se pisan.",
                "PermaLocke", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var paths = new AppPaths();
        paths.EnsureCreated();

        // EL TRASPASO desde la carpeta vieja, si el jugador lo pidió (§195): antes que la copia de la base de datos y que
        // nada la abra. Lo que hizo queda en Config/traspaso-hecho.json y se dice al entrar.
        if (!e.Args.Contains("--sin-juego", StringComparer.OrdinalIgnoreCase))
        {
            FolderTransfer.RunPending(paths.Root, paths.Config,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        }

        // ANTES de que nada abra la base de datos, y por eso está aquí arriba y no dentro de un
        // servicio: es el único momento en el que el fichero está garantizadamente en reposo.
        // Copiar un SQLite que otro está escribiendo puede capturar una página a medias, y una
        // copia que quizá esté corrupta es peor que ninguna, porque en ella se confía.
        //
        // No se espera a que falle nada ni se comprueba el resultado: Run() no lanza nunca, y un
        // arranque no se detiene porque una copia no haya salido. Lo que pasó queda en el log.
        // Menos con --sin-juego: esa copia se abre justo cuando la aplicación de verdad está en marcha y
        // tiene la base de datos abierta, que es el caso que la frase de arriba prohíbe.
        if (!e.Args.Contains("--sin-juego", StringComparer.OrdinalIgnoreCase))
        {
            new RunBackup(paths.Saves).Run(DateTimeOffset.Now);
        }

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
        // Las habilidades que no se reparten salen de la MISMA lista que usa el randomizador: dos
        // listas de «qué no se puede dar» acabarían discrepando (§136).
        collection.AddSingleton<ISpeciesStatsCatalog>(_ =>
            JsonSpeciesStatsCatalog.Load(Path.Combine(paths.Data, "species.json"),
                [.. PermaLocke.Randomizer.RandomizerOptionsLoader
                    .Load(Path.Combine(paths.Data, "randomizer.json")).BannedAbilities]));
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

        // LA REGLA DE PRIMER ENCUENTRO (§117). Los mapas del cartucho para saber dónde está el jugador, y las
        // líneas evolutivas del mundo instalado para los duplicados: esta segunda sustituye a la vacía que
        // registra Rules, porque es la última que se registra la que se sirve.
        collection.AddSingleton(_ => JsonMapTable.Load(Path.Combine(paths.Data, "mapas.json")));
        collection.AddSingleton<WorldEvolutionLines>();
        collection.AddSingleton<PermaLocke.Rules.IEvolutionLineProvider>(sp => sp.GetRequiredService<WorldEvolutionLines>());
        collection.AddSingleton<WorldAllowedStatics>();
        collection.AddSingleton<PermaLocke.Rules.Services.TrialZoneService>();
        collection.AddSingleton<EncounterGuard>();
        collection.AddSingleton<IAppDialogs, AppDialogs>();
        collection.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        collection.AddSingleton<GameLinkMonitor>();

        // LOS AVISOS ENCIMA DEL JUEGO. Su propia ventana, para que salgan con PermaLocke
        // minimizado, que es como se juega. PlayNotifications solo se suscribe: hay que pedirlo
        // una vez para que exista, y eso se hace al arrancar el vigilante.
        collection.AddSingleton<Notifier>();
        collection.AddSingleton<PlayNotifications>();
        collection.AddSingleton<DeathCeremony>();
        collection.AddSingleton<KillcamRecorder>();
        collection.AddSingleton<IKillcamRecorder>(sp => sp.GetRequiredService<KillcamRecorder>());

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
        collection.AddSingleton<MoveReminderService>();
        collection.AddSingleton<RenameService>();
        collection.AddSingleton<IntegrityService>();
        collection.AddSingleton<IntegrityGuard>();
        collection.AddSingleton<OrderService>();
        collection.AddSingleton<RulesSync>();
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
        // ÁLBUM (§186): las cajas como carpeta de cartas, solo para mirar.
        collection.AddSingleton<TcgCardFactory>();
        collection.AddSingleton<CatchCeremony>();
        collection.AddSingleton<TransferOffer>();
        collection.AddSingleton<AlbumViewModel>();
        collection.AddSingleton<EvTrainingViewModel>();
        collection.AddSingleton<MoveReminderViewModel>();
        collection.AddSingleton<PokePasteViewModel>();
        collection.AddSingleton<IslandMapService>();
        collection.AddSingleton<ZonePhotoService>();
        collection.AddSingleton<WindowSizeService>();
        collection.AddSingleton<MapViewModel>();
        collection.AddSingleton<MiscellaneousViewModel>();
        collection.AddSingleton<TradedAwayReconciler>();
        collection.AddSingleton<BattleModeService>();

        // EL LANZADOR (§125): abrir, vigilar y cerrar el juego desde la aplicación, y el tiempo jugado por run.
        collection.AddSingleton<IPlaytimeStore>(_ => new JsonPlaytimeStore(paths.Saves));
        collection.AddSingleton<EmulatorCrashReport>();
        collection.AddSingleton<EmulatorLauncher>();
        collection.AddSingleton<LauncherViewModel>();
        collection.AddSingleton<BattleModeViewModel>();

        // EL JUGADOR DE ESTA MAQUINA (§123). En Config/ y no en Saves/: empezar de cero borra la run y
        // la partida, pero quien vuelve a empezar sigue siendo el mismo jugador.
        collection.AddSingleton<IPlayerProfileStore>(_ => new JsonPlayerProfileStore(paths.Config));
        collection.AddSingleton<SyncService>();

        // AMIGOS Y ACTIVIDAD de JUGAR (§126): presencia y logros de todos, por el servidor del torneo.
        collection.AddSingleton<CommunityService>();

        // Los fantasmas (§183): tus muertes a los demás, y las suyas encima de tu emulador.
        collection.AddSingleton<GhostService>();

        // «Jugando a PermaLocke» en el perfil de Discord mientras la app está abierta (§185).
        collection.AddSingleton<DiscordPresence>();

        // LA BANDEJA DE REGALOS (§129): lo que el organizador manda por el servidor, y recogerlo aquí.
        collection.AddSingleton<GiftService>();
        collection.AddSingleton<GiftInbox>();
        collection.AddSingleton<GiftInboxViewModel>();
        collection.AddSingleton<SyncViewModel>();
        collection.AddSingleton<InformationViewModel>();
        collection.AddSingleton<AlolaSky>();
        collection.AddSingleton<CemeteryService>();
        collection.AddSingleton<CemeteryViewModel>();
        collection.AddSingleton<MaintenanceService>();
        collection.AddSingleton<AppSettings>();
        collection.AddSingleton<DiscordLogin>();
        collection.AddSingleton<TournamentUpload>();
        collection.AddTransient<LoginViewModel>();
        collection.AddSingleton<SettingsViewModel>();
        collection.AddSingleton<MainViewModel>();

        _services = collection.BuildServiceProvider();

        var logger = _services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("PermaLocke iniciado. Raíz de datos: {Root}", paths.Root);

        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError(args.Exception, "Excepción no controlada en la interfaz");
            MessageBox.Show(
                "Ha ocurrido un error inesperado.",
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

        // Las preferencias de CONFIGURACIÓN, antes de que el vigilante pueda avisar o grabar nada.
        _services.GetRequiredService<AppSettings>().Load();

        // --hora-alola HH:mm: el cielo a una hora concreta, para verlo sin esperar a que llegue. No toca nada más.
        var sky = _services.GetRequiredService<AlolaSky>();

        if (Array.FindIndex(e.Args, arg => string.Equals(arg, "--hora-alola", StringComparison.OrdinalIgnoreCase)) is var hourAt
            && hourAt >= 0 && hourAt + 1 < e.Args.Length && TimeOnly.TryParse(e.Args[hourAt + 1], out var rehearsed))
        {
            sky.Rehearsal = rehearsed;
        }

        sky.Start();

        // LA PUERTA DEL TORNEO: sin una cuenta de Discord de la whitelist no se abre nada. Mientras se enseña, la
        // aplicación no se cierra al cerrar una ventana: la de entrar sería la "principal" y se llevaría todo.
        var login = _services.GetRequiredService<LoginViewModel>();

        if (!await login.TryKeptSessionAsync())
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (new Views.LoginWindow(login).ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        var main = _services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = main };
        // --tamano grande|enorme abre a ese tamaño SIN guardarlo, para ver una pantalla a otro tamaño sin
        // cambiar el que eligió el jugador (§143). Como --hora-alola: un ensayo, no una preferencia.
        var rehearsedSize = Array.FindIndex(e.Args, arg => string.Equals(arg, "--tamano", StringComparison.OrdinalIgnoreCase))
            is var sizeAt and >= 0 && sizeAt + 1 < e.Args.Length
                ? WindowSizeService.Sizes.FirstOrDefault(size => size.Key == e.Args[sizeAt + 1])
                : null;

        window.Resize(rehearsedSize ?? _services.GetRequiredService<WindowSizeService>().Current);
        MainWindow = window;
        window.Show();

        TellTransfer(paths, logger);

        var run = await _services.GetRequiredService<RunService>().LoadMostRecentAsync();
        logger.LogInformation("Run cargada al inicio: {Run}", run?.Name ?? "ninguna");

        // Una run de antes de los perfiles se vincula aqui, una vez y con su evento. Un fallo no
        // detiene el arranque: sin perfil la aplicacion funciona igual, solo no puede publicar.
        try
        {
            var ownership = await _services.GetRequiredService<PlayerProfileService>().LinkCurrentRunAsync();
            logger.LogInformation("Run y jugador: {Ownership}", ownership);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se ha podido vincular la run al perfil del jugador");
        }

        // El lanzador mira si el juego está abierto una vez por segundo. Con la run ya cargada, para que la
        // sesión que encuentre en marcha se apunte a la run que toca.
        // Antitrampas de recarga (2026-09-26), nunca en una copia para mirar pantallas: antes del lanzador, que ata el
        // juego a PermaLocke en cuanto lo ve. Si el juego ya estaba abierto, nadie lo vigilaba.
        var integrity = _services.GetRequiredService<IntegrityGuard>();
        integrity.Enabled = !e.Args.Contains("--sin-juego", StringComparer.OrdinalIgnoreCase);

        if (integrity.Enabled && run is not null)
        {
            try
            {
                await integrity.CheckPlaytimeAsync(run.Id, gameAlreadyOpen: EmulatorLauncher.IsOpen());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se ha podido comprobar la partida al abrir");
            }
        }

        // Cerrar PermaLocke con el juego abierto dejaría la partida sin vigilar: la ventana no se deja.
        var launcher = _services.GetRequiredService<EmulatorLauncher>();
        window.Closing += (_, closing) =>
        {
            if (integrity.Enabled && EmulatorLauncher.IsOpen())
            {
                closing.Cancel = true;
                MessageBox.Show(window, "Cierra primero el juego. PermaLocke no se puede cerrar mientras Ultra Luna está abierto.",
                    "PermaLocke", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };

        launcher.Start();

        await main.InitialiseAsync();

        // Started last: it polls the emulator, and there is no point doing that before the
        // run it belongs to has been loaded.
        // Antes de arrancar el vigilante, para no perderse lo que pase en su primer ciclo.
        _services.GetRequiredService<PlayNotifications>();

        // --sin-juego: una copia para mirar pantallas SIN vigilante ni pestaña. Con la aplicación de
        // verdad y el emulador abiertos, dos vigilantes verían la misma muerte y la cobrarían dos
        // veces -cada uno tiene su propia puerta-, así que las pruebas visuales van siempre con esto.
        var withoutGame = e.Args.Contains("--sin-juego", StringComparer.OrdinalIgnoreCase);

        // Una copia para mirar pantallas lee a los amigos, pero no dice que está aquí ni publica la run.
        // Amigos y actividad van por el servidor del torneo, también en la distribución local.
        _services.GetRequiredService<CommunityService>().Start(writes: !withoutGame);
        _services.GetRequiredService<GhostService>().Start(writes: !withoutGame);
        _services.GetRequiredService<DiscordPresence>().Start();

        // Los regalos del organizador llegan por el servidor del torneo, también en la distribución local.
        var inbox = _services.GetRequiredService<GiftInbox>();
        inbox.OrdersApplied += (_, _) => _ = launcher.RefreshLockAsync();
        inbox.Start();

        // Las reglas oficiales del organizador, una vez al abrir; nunca en una copia para mirar pantallas.
        if (!e.Args.Contains("--sin-juego", StringComparer.OrdinalIgnoreCase))
        {
            _ = _services.GetRequiredService<RulesSync>().SyncAsync();
        }

        if (!withoutGame)
        {
            _services.GetRequiredService<EdgeTab>().Attach(window);
            _services.GetRequiredService<TournamentUpload>().Start();
            _services.GetRequiredService<GameLinkMonitor>().Start();

            // Las líneas evolutivas se leen ya, en segundo plano: la primera vez puede tocar sacar el fichero de
            // la ROM, y eso no debe caer en el segundo en que empieza un combate.
            var evolutions = _services.GetRequiredService<WorldEvolutionLines>();
            _ = Task.Run(evolutions.Warm);

            // Y las capturas estáticas permitidas, por lo mismo: la tabla del cartucho puede tocar sacarla de la ROM.
            var allowedStatics = _services.GetRequiredService<WorldAllowedStatics>();
            _ = Task.Run(allowedStatics.Warm);
        }

        if (Array.FindIndex(e.Args, arg => string.Equals(arg, "--seccion", StringComparison.OrdinalIgnoreCase)) is var at
            && at >= 0 && at + 1 < e.Args.Length)
        {
            main.Navigate(e.Args[at + 1]);
        }

        if (e.Args.Contains("--ensayar-muerte", StringComparer.OrdinalIgnoreCase))
        {
            await RehearseDeathsAsync(logger);
        }

        if (e.Args.Contains("--ensayar-fantasma", StringComparer.OrdinalIgnoreCase))
        {
            // El fantasma de un amigo tal como lo vería él (§183), con el último caído de esta run.
            if ((await _services!.GetRequiredService<MaintenanceService>().FallenAsync()).FirstOrDefault() is { } ghost)
            {
                await _services!.GetRequiredService<GhostService>().RehearseAsync("Ensayo",
                    new DeathNotice(ghost.Nickname ?? ghost.SpeciesName, ghost.Species, 0, ghost.Form, ghost.IsShiny, ghost.Level));
            }
        }

        if (e.Args.Contains("--ensayar-lluvia", StringComparer.OrdinalIgnoreCase))
        {
            // La lluvia de sangre de un amigo que pierde el equipo (§184), sin servidor ni juego.
            await _services!.GetRequiredService<GhostService>().RehearseRainAsync("Ensayo");
        }

        if (e.Args.Contains("--ensayar-captura", StringComparer.OrdinalIgnoreCase))
        {
            // La carta de una captura volando al álbum (§190), con el primero del equipo de la partida.
            var snapshot = await _services!.GetRequiredService<IBoxReader>().ReadAsync();
            if (snapshot.Boxes.SelectMany(box => box.Pokemon).FirstOrDefault(p => !p.IsEgg) is { } caught)
            {
                await _services!.GetRequiredService<CatchCeremony>().RehearseAsync(caught);
            }
        }

        if (e.Args.Contains("--ensayar-killcam", StringComparer.OrdinalIgnoreCase))
        {
            await RehearseKillcamAsync(paths, logger);
        }
    }

    /// <summary>
    /// Records a few seconds of the emulator's top screen, writes them as a killcam outside the run and
    /// reads them back, saving three frames as pictures to look at.
    /// </summary>
    /// <remarks>
    /// The killcam only records during a battle and only saves on a death. This proves the capture, the
    /// file and the reading without either: the clip goes to <c>Logs/</c>, never next to a Pokémon of
    /// the run, because a replay attached to a death it does not show would be a false record.
    /// </remarks>
    private async Task RehearseKillcamAsync(AppPaths paths, ILogger logger)
    {
        var recorder = _services!.GetRequiredService<KillcamRecorder>();
        var path = Path.Combine(paths.Logs, "killcam-ensayo.killcam");

        // La captura lee la pantalla, no la ventana: la propia aplicación no puede estar delante.
        MainWindow!.WindowState = WindowState.Minimized;

        recorder.Recording = true;
        await Task.Delay(TimeSpan.FromSeconds(5));

        var written = await recorder.SaveAsync(path, recorder.Mark());
        recorder.Recording = false;
        MainWindow.WindowState = WindowState.Normal;

        var frames = written is null ? [] : KillcamClip.Read(written);
        logger.LogInformation("Ensayo de killcam: {Frames} fotogramas leídos de {Path}", frames.Count, path);

        foreach (var (frame, name) in new[] { (0, "primero"), (frames.Count / 2, "medio"), (frames.Count - 1, "ultimo") })
        {
            if (frame < 0 || frame >= frames.Count)
            {
                continue;
            }

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frames[frame].Image));

            await using var file = File.Create(Path.Combine(paths.Logs, $"killcam-ensayo-{name}.png"));
            encoder.Save(file);
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

            ceremony.Mourn(new DeathNotice(entry.Nickname ?? entry.SpeciesName, entry.Species, Math.Max(0, cost), entry.Form, entry.IsShiny));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Antes de soltar los servicios: los amigos ven «desconectado» ahora y no dentro de tres minutos.
        _services?.GetService<CommunityService>()?.SignOff();
        _services?.GetService<TournamentUpload>()?.Flush();
        _services?.GetService<DiscordPresence>()?.Stop();
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
