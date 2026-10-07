using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// Generates the LayeredFS mod for the current run and, as a separate and explicit step,
/// installs it into Azahar.
/// <para>
/// Generating and installing are deliberately two buttons. Installing replaces the world of a
/// game that may already be in progress, so it is never a side effect of pressing something
/// else.
/// </para>
/// </summary>
public sealed partial class RandomizerViewModel : SectionViewModel
{
    private readonly IRunContext _runContext;
    private readonly IEventStore _events;
    private readonly IClock _clock;
    private readonly IAppDialogs _dialogs;
    private readonly AzaharInstallation _azahar;
    private readonly AppPaths _paths;
    private readonly IRunRoles _roles;
    private readonly IRoleCatalog _roleCatalog;
    private readonly InstalledWorld _installedWorld;
    private readonly WorldAllowedStatics _allowedStatics;
    private readonly WorldEvolutionLines _evolutionLines;
    private readonly ILogger<RandomizerViewModel> _logger;

    public RandomizerViewModel(IRunContext runContext, IEventStore events, IClock clock,
        IAppDialogs dialogs, AzaharInstallation azahar, AppPaths paths, IRunRoles roles,
        IRoleCatalog roleCatalog, InstalledWorld installedWorld, WorldAllowedStatics allowedStatics,
        WorldEvolutionLines evolutionLines, ILogger<RandomizerViewModel> logger, IUiDispatcher ui)
        : base("RANDOMIZADOR", "Crea e instala tu mundo")
    {
        _installedWorld = installedWorld;
        _allowedStatics = allowedStatics;
        _evolutionLines = evolutionLines;
        _runContext = runContext;
        _events = events;
        _clock = clock;
        _dialogs = dialogs;
        _azahar = azahar;
        _paths = paths;
        _roles = roles;
        _roleCatalog = roleCatalog;
        _logger = logger;

        // Borrar o crear la run lo avisa desde otro hilo; los botones solo se tocan desde el de la ventana (1.0.7.6).
        _runContext.CurrentChanged += (_, _) => _ = ui.InvokeAsync(() => { Refresh(); return Task.CompletedTask; });
        Refresh();
    }

    [ObservableProperty]
    private string _seedLabel = "—";
    [ObservableProperty]
    private string _romText = "Sin comprobar";

    /// <summary>What the screen says about a base mod, which is either there or it is not.</summary>
    [ObservableProperty]
    private string _expansionText = "Sin comprobar";

    [ObservableProperty]
    private string _azaharText = "Sin comprobar";

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isGenerated;

    [ObservableProperty]
    private bool _isInstalled;

    private bool CanGenerate => !IsBusy && _runContext.Current is not null && RomPath is not null;

    private bool CanInstall => !IsBusy && IsGenerated && Azahar is not null;

    private bool CanRemove => !IsBusy && IsInstalled;

    private string? RomPath { get; set; }

    private AzaharLocation? Azahar { get; set; }

    private string OutputDirectory =>
        Path.Combine(_paths.Randomized, $"seed-{_runContext.Current?.Seed ?? 0}");

    /// <summary>
    /// The romfs of another mod to randomize on top of, or null to use the cartridge.
    /// </summary>
    /// <remarks>
    /// Detected by the folder simply being there, the same way the cartridge is. There is no
    /// switch for it because a switch could disagree with the folder, and the screen would then be
    /// claiming to have randomized a world it did not read.
    /// </remarks>
    private string? BaseLayer { get; set; }

    public override string IconKey => "IconRandomizer";

    public override GameNeed Needs => GameNeed.Closed;

    public override Task ActivateAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Re-reads everything the screen depends on: the run, the cartridge and Azahar. Nothing is
    /// assumed to still be where it was the last time the section was opened.
    /// </summary>
    private void Refresh()
    {
        var run = _runContext.Current;
        SeedLabel = run?.SeedLabel ?? "—";

        var rom = RomInspector.ScanFolder(_paths.Rom).FirstOrDefault(r => r.IsSupported);
        RomPath = rom?.Path;
        RomText = rom is null
            ? "No se encuentra tu ROM de Ultra Luna."
            : rom.FileName;

        // Prefiere el emulador que PermaLocke trae consigo, y de paso le enciende el RPC: es el
        // ajuste que el jugador solo puede cambiar con la emulación parada.
        Azahar = _azahar.Locate(AppContext.BaseDirectory);
        _azahar.EnsureRpcEnabled(Azahar);
        AzaharText = Azahar.IsPortable
            ? "Azahar listo."
            : "Azahar listo.";

        var expansion = Path.Combine(_paths.Expansion, "romfs");
        BaseLayer = Directory.Exists(expansion) ? expansion : null;
        ExpansionText = BaseLayer is null
            ? "Juego original."
            : "Con las generaciones 8 y 9.";

        IsGenerated = Directory.Exists(Path.Combine(OutputDirectory, "romfs"));
        IsInstalled = Azahar is not null && Directory.Exists(Path.Combine(ModDirectory, "romfs"));

        NotifyCommands();
    }

    /// <summary>Where this installation reads its mods from.</summary>
    private string ModDirectory =>
        AzaharInstallation.ModDirectory(Azahar!, LayeredFsMod.UltraMoonProgramId);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        var run = _runContext.Current;
        if (run is null || RomPath is null)
        {
            return;
        }

        IsBusy = true;
        NotifyCommands();
        Status = "Generando...";

        try
        {
            var options = RandomizerOptionsLoader.Load(Path.Combine(_paths.Data, "randomizer.json"));

            // El ROL manda sobre el fichero de opciones: parte de la dificultad se cuece en la
            // ROM, y quien decide cuánto suben los entrenadores es el rol de la run, no una
            // configuración que el jugador pudiera cambiar después de haber empezado.
            if (_roles.Of(run.Id) is { } role)
            {
                options = options with
                {
                    EnemyLevelPercent = role.EnemyLevelPercent,
                    ExtraTrainerPokemon = role.ExtraTrainerPokemon,
                    ImportantTrainerClasses = _roleCatalog.ImportantTrainerClasses,
                    MonoType = role.MonoType,
                };
            }

            var work = Path.Combine(Path.GetTempPath(), "permalocke-randomizer");
            var report = await new RandomizerService(options)
                .RandomizeAsync(RomPath, work, OutputDirectory, run.Seed, BaseLayer);

            Status = "Mundo generado. Ya puedes instalarlo.";

            await _events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = _clock.Now,
                Type = GameEventType.RomRandomized,
                Source = EventSource.Player,
                Actor = run.PlayerName,
                Description = $"Randomización generada con seed {run.SeedLabel}: "
                              + string.Join("; ", report.Steps.Select(s => s.Module)),
                Seed = run.Seed,
                Data = new Dictionary<string, string>
                {
                    ["output"] = OutputDirectory,
                    ["files"] = report.Files.Count.ToString(),
                    ["bytes"] = report.TotalBytes.ToString(),
                    ["modules"] = string.Join(",", report.Steps.Select(s => s.Module)),

                    // Las dos unicas opciones que cambian datos que el juego lee DURANTE un
                    // combate por link. Se guardan AQUI, en el evento, y no se leen de
                    // randomizer.json cuando hacen falta: el fichero dice como esta configurado
                    // HOY y el evento dice con que se genero el mundo que estas jugando. Si
                    // alguien cambia el JSON y no regenera, son cosas distintas, y la que manda
                    // para saber si puedes combatir es la segunda.
                    ["shuffleBaseStats"] = options.ShuffleBaseStats.ToString(),
                    ["randomizeAbilities"] = options.RandomizeAbilities.ToString(),

                    // Con que mundo se genero esto. Va aqui por lo mismo que las dos de arriba: la
                    // carpeta Expansion puede vaciarse o llenarse despues, y entonces mirarla no
                    // diria con que se genero LO QUE SE ESTA JUGANDO. Y el numero de especies es
                    // lo que permite ver de un vistazo si el mod se leyo de verdad: 807 con un mod
                    // base declarado significa que algo no se aplico.
                    ["baseLayer"] = (report.BaseLayerFiles?.Count ?? 0).ToString(),
                    ["baseLayerFiles"] = string.Join(",", report.BaseLayerFiles ?? []),
                    ["maxSpecies"] = report.MaxSpecies.ToString(),
                }
            });

            _logger.LogInformation("Randomización generada en {Output} con seed {Seed}",
                OutputDirectory, run.Seed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la randomización");
            Status = "La randomización ha fallado.";
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        if (Azahar is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                "Instalar tu mundo",
                "Cambia los encuentros, entrenadores, tiendas y objetos del juego. "
                + "Lo normal es instalarlo antes de empezar a jugar.\n\n¿Instalar?"))
        {
            return;
        }

        IsBusy = true;
        NotifyCommands();
        Status = "Copiando a Azahar...";

        try
        {
            var destination = Path.Combine(ModDirectory, "romfs");

            // La capa base ENTERA primero y lo nuestro encima. Sin esto, Azahar recibia nuestros
            // siete ficheros y el cartucho para todo lo demas: los datos de especie, los
            // aprendizajes y los 2,5 GB de modelos se quedaban fuera, asi que los Pokemon nuevos
            // aparecian en las tablas de encuentro y el juego no tenia con que dibujarlos.
            await Task.Run(() => ModInstaller.Install(
                OutputDirectory, ModDirectory, BaseLayer,
                Path.Combine(_paths.Expansion, "exefs"),
                message => Status = message));

            Status = "Instalado. Si Azahar estaba abierto, ciérralo y vuelve a abrirlo.";
            _logger.LogInformation("Randomización instalada en {Destination}", destination);
            ReloadWorld();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la instalación de la randomización");
            Status = "No se ha podido instalar.";
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        if (Azahar is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                "Quitar tu mundo",
                "El juego volverá a ser el original. Podrás volver a instalar tu mundo.\n\n¿Quitar?"))
        {
            return;
        }

        try
        {
            if (Directory.Exists(ModDirectory))
            {
                Directory.Delete(ModDirectory, recursive: true);
            }
            Status = "Quitado.";
            ReloadWorld();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo quitar la randomización");
            Status = "No se ha podido quitar.";
        }
        finally
        {
            Refresh();
        }
    }


    /// <summary>
    /// Reads the world just installed or removed, instead of the one there was when the app started.
    /// </summary>
    /// <remarks>
    /// Found getting the friends' folder ready on 2026-09-21. The installed world was read once, at startup, and a
    /// fresh copy has no world then: create the run, generate, install, all with the app open, and it stayed on the
    /// cartridge's 807 species until restarted, so a gen 8-9 starter in the party was not a species the live readers
    /// knew and the party was never found. Reinstalling had the milder version of the same thing: the previous world's
    /// learnsets in MOVIMIENTOS and its allowed statics in the ball rule. Never throws: a world that cannot be read now
    /// is read at the next start, as before.
    /// </remarks>
    private void ReloadWorld()
    {
        try
        {
            _installedWorld.Apply(AppContext.BaseDirectory);
            _allowedStatics.Forget();
            _evolutionLines.Forget();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo releer el mundo instalado; se leerá al reiniciar PermaLocke");
        }
    }

    private void NotifyCommands()
    {
        GenerateCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }
}
