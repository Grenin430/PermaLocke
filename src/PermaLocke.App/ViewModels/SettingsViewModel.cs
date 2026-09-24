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
    private readonly DiscordLogin _discord;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _loading;

    public SettingsViewModel(AppSettings settings, WindowSizeService windowSizes, Notifier notifier, AppPaths paths, PokemonSpriteService sprites, DiscordLogin discord,
        ILogger<SettingsViewModel> logger)
        : base("CONFIGURACIÓN", "Ajustes de la aplicación")
    {
        _settings = settings;
        _windowSizes = windowSizes;
        _notifier = notifier;
        _paths = paths;
        _sprites = sprites;
        _discord = discord;
        ShowAccount(discord.Saved);
        _logger = logger;

        _loading = true;
        var current = settings.Current;
        Notifications = current.Notifications;
        StepAside = current.StepAside;
        DeathScene = current.DeathScene;
        Killcam = current.Killcam;
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

    partial void OnNotificationsChanged(bool value) => Save();
    partial void OnStepAsideChanged(bool value) => Save();
    partial void OnDeathSceneChanged(bool value) => Save();
    partial void OnKillcamChanged(bool value) => Save();

    private void Save()
    {
        if (_loading)
        {
            return;
        }

        _settings.Update(new AppSettingsData(Notifications, StepAside, DeathScene, Killcam));
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

    // ================================================== TORNEO (inicio de sesión con Discord, fase 1)

    [ObservableProperty]
    private string _discordName = string.Empty;

    [ObservableProperty]
    private string? _discordAvatar;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private bool _signingIn;

    public bool IsSignedIn => DiscordName.Length > 0;

    partial void OnDiscordNameChanged(string value) => OnPropertyChanged(nameof(IsSignedIn));

    private void ShowAccount(DiscordAccount? account)
    {
        DiscordName = account?.Name ?? string.Empty;
        DiscordAvatar = account?.AvatarUrl;
    }

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        SigningIn = true;
        Status = "Termina de entrar en el navegador...";

        try
        {
            ShowAccount(await _discord.SignInAsync());
            Status = $"Has entrado como {DiscordName}.";
        }
        catch (OperationCanceledException)
        {
            Status = "Se ha cansado de esperar al navegador. Vuelve a intentarlo.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el inicio de sesión con Discord");
            Status = "No se ha podido entrar con Discord.";
        }
        finally
        {
            SigningIn = false;
        }
    }

    private bool CanSignIn => !SigningIn;

    [RelayCommand]
    private void SignOut()
    {
        _discord.SignOut();
        ShowAccount(null);
        Status = "Sesión cerrada en este PC.";
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
