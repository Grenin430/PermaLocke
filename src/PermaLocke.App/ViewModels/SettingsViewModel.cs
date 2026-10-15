using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// CONFIGURACIÓN: the window size, the notices over the game, the death scene, the killcam and the folders.
/// </summary>
/// <remarks>
/// Replaces MANTENIMIENTO, which the player asked to remove on 2026-09-24. Of what it held only the window size stays
/// here. The repair tools are still available as <c>tools/PermaLocke.Probe</c> commands, and
/// <see cref="MaintenanceService"/> stays because the monitor uses it. Everything here is a preference, stored by
/// <see cref="AppSettings"/>. No rule or point depends on it.
/// </remarks>
public sealed partial class SettingsViewModel : SectionViewModel
{
    private readonly AppSettings _settings;
    private readonly WindowSizeService _windowSizes;
    private readonly Notifier _notifier;
    private readonly AppPaths _paths;
    private readonly PokemonSpriteService _sprites;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _loading;

    /// <summary>The version running, to see at a glance whether an update (§196) went in.</summary>
    public string VersionText { get; } = $"PERMALOCKE {AppUpdate.Display(typeof(SettingsViewModel).Assembly.GetName().Version ?? new Version(0, 0))} · UN JUGADOR";

    public SettingsViewModel(AppSettings settings, WindowSizeService windowSizes, Notifier notifier, AppPaths paths, PokemonSpriteService sprites,
        ILogger<SettingsViewModel> logger)
        : base("CONFIGURACIÓN", "Ajustes de la aplicación")
    {
        Fleeting.Fade(this, nameof(Status));

        _settings = settings;
        _windowSizes = windowSizes;
        _notifier = notifier;
        _paths = paths;
        _sprites = sprites;
        _logger = logger;

        _loading = true;
        var current = settings.Current;
        Notifications = current.Notifications;
        StepAside = current.StepAside;
        DeathScene = current.DeathScene;
        Killcam = current.Killcam;
        Follower = current.Follower;
        Ghosts = current.Ghosts;
        CatchCard = current.CatchCard;
        Nicknames = current.Nicknames;
        CapPanel = current.CapPanel;
        Background = current.Background;
        Resolution = current.Resolution;
        Vulkan = current.Vulkan;
        CapSize = current.CapSize;
        CapMaxCell = current.CapMaxCell;
        CapMemorial = current.CapMemorial;
        CapDetails = current.CapDetails;
        CapAnimations = current.CapAnimations;
        CapKey = current.CapKey;
        CapHpSync = current.CapHpSync;
        CapPinned = current.CapPinned;
        _loading = false;
    }

    public override string IconKey => "IconTools";

    /// <summary>The sample notices carry sprites, which are taken from the ROM the first time.</summary>
    public override Task ActivateAsync() => _sprites.PrepareAsync();

    public IReadOnlyList<WindowSize> WindowSizes { get; } = WindowSizeService.Sizes;

    public string CurrentSizeName => _windowSizes.Current.Name;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _notifications;

    [ObservableProperty]
    private bool _stepAside;

    [ObservableProperty]
    private bool _deathScene;

    [ObservableProperty]
    private bool _killcam;

    [ObservableProperty]
    private bool _follower;

    [ObservableProperty]
    private bool _ghosts;

    [ObservableProperty]
    private bool _catchCard;

    [ObservableProperty]
    private bool _nicknames;

    [ObservableProperty]
    private bool _capPanel;

    [ObservableProperty]
    private bool _background;

    [ObservableProperty]
    private int _resolution;

    [ObservableProperty]
    private bool _vulkan;

    /// <summary>0 automatic, 1 compact, 2 slim, 3 the whole showcase: the richest mode the panel may use.</summary>
    [ObservableProperty]
    private int _capSize;

    /// <summary>The biggest cell in screen pixels the panel may use; 0 is no limit.</summary>
    [ObservableProperty]
    private int _capMaxCell;

    [ObservableProperty]
    private bool _capMemorial;

    [ObservableProperty]
    private bool _capDetails;

    [ObservableProperty]
    private bool _capAnimations;

    [ObservableProperty]
    private bool _capPinned;

    /// <summary>The key that folds the panel (a virtual-key code), or 0 for none.</summary>
    [ObservableProperty]
    private int _capKey;

    /// <summary>When the panel shows a change of life: 0 with the game (default), 1 as soon as it is read, 2 three seconds after.</summary>
    [ObservableProperty]
    private int _capHpSync;

    public string CapSizeText => CapSize switch { 1 => "Tamaño del panel: reducido", 2 => "Tamaño del panel: estrecho", 3 => "Tamaño del panel: completo", _ => "Tamaño del panel: el que quepa" };

    public string CapMaxCellText => CapMaxCell == 0 ? "Escala del panel: la mayor que quepa" : $"Escala del panel: hasta x{CapMaxCell}";

    public string CapHpSyncText => CapHpSync switch { 1 => "Vida del panel: instantánea (se adelanta al juego)", 2 => "Vida del panel: con 3 s de retraso", _ => "Vida del panel: sincronizada con el juego" };

    public string CapKeyText => CapKey switch { 0x13 => "Plegar el panel con: Pausa", 0x2D => "Plegar el panel con: Insert", 0x91 => "Plegar el panel con: Bloq Despl", _ => "Plegar el panel con: ninguna tecla" };

    public string ResolutionText => Resolution == 0 ? "Resolución: la que tenga el emulador" : $"Resolución x{Resolution}";

    partial void OnNotificationsChanged(bool value) => Save();
    partial void OnStepAsideChanged(bool value) => Save();
    partial void OnDeathSceneChanged(bool value) => Save();
    partial void OnKillcamChanged(bool value) => Save();
    partial void OnFollowerChanged(bool value) => Save();
    partial void OnGhostsChanged(bool value) => Save();
    partial void OnCatchCardChanged(bool value) => Save();

    partial void OnNicknamesChanged(bool value) => Save();
    partial void OnCapPanelChanged(bool value) => Save();
    partial void OnBackgroundChanged(bool value) => Save();
    partial void OnVulkanChanged(bool value) => Save();
    partial void OnCapMemorialChanged(bool value) => Save();
    partial void OnCapDetailsChanged(bool value) => Save();
    partial void OnCapAnimationsChanged(bool value) => Save();
    partial void OnCapPinnedChanged(bool value) => Save();

    partial void OnCapSizeChanged(int value)
    {
        OnPropertyChanged(nameof(CapSizeText));
        Save();
    }

    partial void OnCapMaxCellChanged(int value)
    {
        OnPropertyChanged(nameof(CapMaxCellText));
        Save();
    }

    partial void OnCapHpSyncChanged(int value)
    {
        OnPropertyChanged(nameof(CapHpSyncText));
        Save();
    }

    [RelayCommand]
    private void SetCapHpSync(string? mode)
    {
        if (int.TryParse(mode, out var value)) CapHpSync = Math.Clamp(value, 0, 2);
    }

    partial void OnCapKeyChanged(int value)
    {
        OnPropertyChanged(nameof(CapKeyText));
        Save();
    }

    [RelayCommand]
    private void SetCapSize(string? size)
    {
        if (int.TryParse(size, out var value)) CapSize = Math.Clamp(value, 0, 3);
    }

    [RelayCommand]
    private void SetCapMaxCell(string? cell)
    {
        if (int.TryParse(cell, out var value)) CapMaxCell = value == 0 ? 0 : Math.Clamp(value, 2, 8);
    }

    [RelayCommand]
    private void SetCapKey(string? key)
    {
        if (int.TryParse(key, out var value)) CapKey = value;
    }

    partial void OnResolutionChanged(int value)
    {
        OnPropertyChanged(nameof(ResolutionText));
        Save();
    }

    [RelayCommand]
    private void SetResolution(string? factor)
    {
        if (int.TryParse(factor, out var value)) Resolution = value == Resolution ? 0 : Math.Clamp(value, 1, 4);
    }

    private void Save()
    {
        if (_loading)
        {
            return;
        }

        _settings.Update(_settings.Current with
        {
            Notifications = Notifications, StepAside = StepAside, DeathScene = DeathScene, Killcam = Killcam,
            Follower = Follower, Ghosts = Ghosts, CatchCard = CatchCard, Nicknames = Nicknames, CapPanel = CapPanel,
            Background = Background, Resolution = Resolution, Vulkan = Vulkan, CapSize = CapSize, CapMaxCell = CapMaxCell,
            CapMemorial = CapMemorial, CapDetails = CapDetails, CapAnimations = CapAnimations, CapKey = CapKey, CapHpSync = CapHpSync,
            CapPinned = CapPinned
        });
        Status = "Guardado.";
    }

    [RelayCommand]
    private void SetWindowSize(WindowSize? size)
    {
        if (size is null)
        {
            return;
        }

        try
        {
            _windowSizes.Set(size);
            (Application.Current?.MainWindow as MainWindow)?.Resize(size);
            OnPropertyChanged(nameof(CurrentSizeName));
            Status = $"Ventana en {size.Name.ToLowerInvariant()}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cambiar el tamaño de ventana");
            Status = "No se pudo guardar el tamaño.";
        }
    }

    [RelayCommand]
    private void TestNotification()
    {
        if (!Notifications)
        {
            Status = "Los avisos están desactivados.";
            return;
        }

        var random = new Random();
        _notifier.Say(ToastKind.Info, "Así se ven los avisos", "Aparecen aquí mientras juegas.");
        _notifier.Say(ToastKind.Shiny, "Prueba: variocolor", "Un variocolor se puede capturar siempre.", _sprites.GetRandom(random));
        _notifier.Say(ToastKind.BallsTaken, "Prueba: Poké Balls retiradas", "Esta ruta ya ha gastado su encuentro.", _sprites.GetBall());
        _notifier.Say(ToastKind.Death, "Prueba: baja", "Esto es solo una prueba.", _sprites.GetRandom(random));
        Status = "Aviso enviado.";
    }

    [RelayCommand]
    private void OpenFolder(string which)
    {
        var folder = which switch
        {
            "logs" => _paths.Logs,
            "saves" => _paths.Saves,
            _ => _paths.Root
        };

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo abrir la carpeta {Folder}", folder);
            Status = "No se pudo abrir la carpeta.";
        }
    }
}
