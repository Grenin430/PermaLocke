using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PermaLocke.App.Games;

namespace PermaLocke.App.ViewModels;

/// <summary>The whole of HeartGold in PermaLocke for now (2026-11-10): randomize with the Universal Randomizer, and play.</summary>
public sealed partial class HeartGoldViewModel : ObservableObject
{
    private readonly HeartGold _game;

    public HeartGoldViewModel(HeartGold game)
    {
        _game = game;
        Refresh();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RandomizeCommand))]
    private string? _rom;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RandomizeCommand))]
    private string? _settings;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(AddRareCandiesCommand))]
    private string? _randomized;

    /// <summary>The Randomizer or melonDS are not there yet: the window offers to download them.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RandomizeCommand), nameof(PlayCommand), nameof(OpenRandomizerCommand))]
    private bool _toolsMissing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RandomizeCommand), nameof(PlayCommand), nameof(DownloadToolsCommand), nameof(AddRareCandiesCommand))]
    private bool _busy;

    [ObservableProperty]
    private string _status = string.Empty;

    public string RomText => Rom is null ? "No está. Pon tu ROM de HeartGold (.nds) en la carpeta ROM." : Path.GetFileName(Rom);

    public string SettingsText => Settings is null ? "No hay ajustes. Hazlos en el Randomizer, guárdalos y elígelos aquí."
        : Settings == _game.DefaultSettings ? "Los básicos de PermaLocke: iniciales de línea de tres, movimientos y habilidades al azar; salvajes de fuerza parecida y sin legendarios, entrenadores y estáticos al azar. Tipos, estadísticas y evoluciones como el cartucho."
        : Path.GetFileName(Settings);

    public string RandomizedText => Randomized is null ? "Todavía ninguna. Randomiza primero." : Path.GetFileName(Randomized);

    partial void OnRomChanged(string? value) => OnPropertyChanged(nameof(RomText));

    partial void OnSettingsChanged(string? value) => OnPropertyChanged(nameof(SettingsText));

    partial void OnRandomizedChanged(string? value) => OnPropertyChanged(nameof(RandomizedText));

    public void Refresh()
    {
        Rom = _game.FindRom();
        Settings = _game.Settings;
        Randomized = _game.LastRandomized;
        ToolsMissing = !_game.ToolsReady;
        if (ToolsMissing && !Busy) Status = "Faltan las herramientas de HeartGold (melonDS, el Randomizer y Java, unos 60 MB). Pulsa DESCARGAR.";
    }

    /// <summary>The settings file the player picked.</summary>
    public void UseSettings(string path)
    {
        _game.ChosenSettings = path;
        Settings = _game.Settings;
    }

    private bool CanDownload() => !Busy;

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadToolsAsync()
    {
        Busy = true;
        Status = "Descargando las herramientas… 0 %";
        try
        {
            await _game.DownloadToolsAsync(new Progress<double>(share => Status = $"Descargando las herramientas… {share:P0}"));
            Status = "Herramientas listas. Ya puedes randomizar.";
        }
        catch (Exception ex)
        {
            Status = $"No se han podido descargar: {ex.Message}";
        }
        finally
        {
            Busy = false;
            Refresh();
        }
    }

    private bool CanOpenRandomizer() => !ToolsMissing;

    [RelayCommand(CanExecute = nameof(CanOpenRandomizer))]
    private void OpenRandomizer()
    {
        try
        {
            _game.OpenRandomizer();
            Status = "Abriendo el Randomizer. Haz tus ajustes, guárdalos (Save Settings) y elígelos aquí con AJUSTES.";
        }
        catch (Exception ex)
        {
            Status = $"No se ha podido abrir el Randomizer: {ex.Message}";
        }
    }

    private bool CanRandomize() => !Busy && !ToolsMissing && Rom is not null && Settings is not null;

    [RelayCommand(CanExecute = nameof(CanRandomize))]
    private async Task RandomizeAsync()
    {
        Busy = true;
        Status = "Randomizando… puede tardar un poco.";
        try
        {
            var (done, output) = await _game.RandomizeAsync(Rom!, Settings!);
            Randomized = _game.LastRandomized;
            Status = done ? $"Hecho: {RandomizedText}" : $"El Randomizer no ha podido: {LastLines(output)}";
        }
        catch (Exception ex)
        {
            Status = $"No se ha podido randomizar: {ex.Message}";
        }
        finally
        {
            Busy = false;
        }
    }

    private bool CanPlay() => !Busy && !ToolsMissing && Randomized is not null;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play()
    {
        try
        {
            _game.Play(Randomized!);
            Status = $"Jugando a {RandomizedText}";
        }
        catch (Exception ex)
        {
            Status = $"No se ha podido abrir melonDS: {ex.Message}";
        }
    }

    /// <summary>How many Rare Candies each press puts in the bag.</summary>
    public const int CandiesPerPress = 10;

    private bool CanAddCandies() => !Busy && Randomized is not null;

    [RelayCommand(CanExecute = nameof(CanAddCandies))]
    private void AddRareCandies()
    {
        try
        {
            var now = HeartGold.AddRareCandies(Randomized!, CandiesPerPress);
            Status = $"+{CandiesPerPress} Caramelos Raros: ahora tienes {now}. Carga la partida guardada, no un estado guardado de melonDS.";
        }
        catch (Exception ex)
        {
            Status = $"No se han podido meter: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BackToMenu() => Services.GameSwitch.BackToMenu(gameOpen: false);

    private static string LastLines(string text) => string.Join(" ", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(3));
}
