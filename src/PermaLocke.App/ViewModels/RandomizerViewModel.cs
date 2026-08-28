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
    private readonly ILogger<RandomizerViewModel> _logger;

    public RandomizerViewModel(IRunContext runContext, IEventStore events, IClock clock,
        IAppDialogs dialogs, AzaharInstallation azahar, AppPaths paths, IRunRoles roles,
        IRoleCatalog roleCatalog, ILogger<RandomizerViewModel> logger)
        : base("RANDOMIZADOR", "Genera la capa del mod desde tu ROM, sin tocar el original")
    {
        _runContext = runContext;
        _events = events;
        _clock = clock;
        _dialogs = dialogs;
        _azahar = azahar;
        _paths = paths;
        _roles = roles;
        _roleCatalog = roleCatalog;
        _logger = logger;

        _runContext.CurrentChanged += (_, _) => Refresh();
        Refresh();
    }

    [ObservableProperty]
    private string _seedLabel = "—";
    [ObservableProperty]
    private string _romText = "Sin comprobar";

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
            ? $"No hay ninguna ROM compatible y desencriptada en {_paths.Rom}"
            : $"{rom.FileName}  ·  {rom.Game}  ·  TitleID {rom.TitleId}";

        // Prefiere el emulador que PermaLocke trae consigo, y de paso le enciende el RPC: es el
        // ajuste que el jugador solo puede cambiar con la emulación parada.
        Azahar = _azahar.Locate(AppContext.BaseDirectory);
        _azahar.EnsureRpcEnabled(Azahar);
        AzaharText = Azahar.IsPortable
            ? $"Emulador propio, ya configurado: {Azahar.UserDirectory}"
            : $"Azahar instalado: {Azahar.UserDirectory}";

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
        Status = "Generando. La ROM no se toca: se lee y se escribe aparte.";

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
                };
            }

            var work = Path.Combine(Path.GetTempPath(), "permalocke-randomizer");
            var report = await new RandomizerService(options)
                .RandomizeAsync(RomPath, work, OutputDirectory, run.Seed);

            Status = $"Listo en {report.Elapsed.TotalSeconds:F1} s · "
                     + $"{report.Files.Count} ficheros · {report.TotalBytes / 1024.0 / 1024.0:F0} MB";

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
                }
            });

            _logger.LogInformation("Randomización generada en {Output} con seed {Seed}",
                OutputDirectory, run.Seed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la randomización");
            Status = "La randomización ha fallado. El detalle está en la carpeta Logs.";
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
                "Instalar la randomización en Azahar",
                "Esto cambia el mundo del juego: encuentros, entrenadores, tiendas y objetos.\n\n"
                + "Si ya tienes una partida empezada, se verá afectada. Lo normal es instalarlo "
                + "antes de empezar.\n\n¿Instalar?"))
        {
            return;
        }

        IsBusy = true;
        NotifyCommands();
        Status = "Copiando a Azahar...";

        try
        {
            var destination = Path.Combine(ModDirectory, "romfs");
            var source = Path.Combine(OutputDirectory, "romfs");
            await Task.Run(() => CopyTree(source, destination));

            Status = "Instalado. Cierra Azahar del todo y vuelve a abrirlo: los mods se leen al cargar el juego.";
            _logger.LogInformation("Randomización instalada en {Destination}", destination);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la instalación de la randomización");
            Status = "No se ha podido instalar. El detalle está en la carpeta Logs.";
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
                "Quitar la randomización",
                "El juego volverá a ser el original. La randomización generada se conserva y "
                + "puedes volver a instalarla.\n\n¿Quitar?"))
        {
            return;
        }

        try
        {
            if (Directory.Exists(ModDirectory))
            {
                Directory.Delete(ModDirectory, recursive: true);
            }
            Status = "Quitada. Al reiniciar Azahar el juego vuelve a ser el original.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo quitar la randomización");
            Status = "No se ha podido quitar. El detalle está en la carpeta Logs.";
        }
        finally
        {
            Refresh();
        }
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private void NotifyCommands()
    {
        GenerateCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }
}
