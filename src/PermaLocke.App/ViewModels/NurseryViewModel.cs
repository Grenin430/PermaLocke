using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The NURSERY of the MONOTYPE roles (§221, §228): the spins the run is owed, spent <b>one at a time</b>, each one an egg that goes
/// into a box of the save with its own animation.
/// </summary>
/// <remarks>
/// <para>
/// The egg is decided, written into the save and recorded before the animation starts, and nothing on this screen says what is
/// inside it: the surprise is when it hatches. What the player sees afterwards is the egg and where it went.
/// </para>
/// <para>
/// Needs the game <b>closed</b>, like everything that writes the save. An egg only counts as spent once it is in the file,
/// so a refusal (game open, boxes full) leaves the spin owed and says why.
/// </para>
/// </remarks>
public sealed partial class NurseryViewModel : SectionViewModel
{
    private readonly NurseryService _nursery;
    private readonly IEggDelivery _delivery;
    private readonly IRunContext _runContext;
    private readonly PokemonSpriteService _sprites;
    private readonly ILogger<NurseryViewModel> _logger;

    public NurseryViewModel(NurseryService nursery, IEggDelivery delivery, IRunContext runContext,
        PokemonSpriteService sprites, ILogger<NurseryViewModel> logger)
        : base("GUARDERÍA", "Un huevo de tu tipo por cada tirada que te deben")
    {
        Fleeting.Fade(this, nameof(Status));

        _nursery = nursery;
        _delivery = delivery;
        _runContext = runContext;
        _sprites = sprites;
        _logger = logger;
    }

    public override string IconKey => "IconGacha";

    public override GameNeed Needs => GameNeed.Closed;

    /// <summary>The eggs asked for since this screen opened, newest first: where each one went, and nothing about what is in it.</summary>
    public ObservableCollection<ReceivedEggViewModel> Received { get; } = [];

    public bool HasReceived => Received.Count > 0;

    /// <summary>The eggs still owed, drawn small: up to a dozen, so the player sees how many there are to ask for.</summary>
    public ObservableCollection<OwedEggViewModel> OwedEggs { get; } = [];

    /// <summary>One lit square per trial of the run: cleared or not.</summary>
    public ObservableCollection<TrialPipViewModel> Trials { get; } = [];

    /// <summary>What each milestone of the competition pays in eggs, reached or not, like the gacha's table of what each trial gives.</summary>
    public ObservableCollection<MilestoneEggsViewModel> Milestones { get; } = [];

    [ObservableProperty]
    private string _milestonesSummary = string.Empty;

    /// <summary>Spins still owed: eggs still to ask for.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetEggCommand))]
    private int _owed;

    [ObservableProperty]
    private string _typeText = string.Empty;

    /// <summary>How strong the eggs are right now, in words.</summary>
    [ObservableProperty]
    private string _strengthText = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _statusIsWarning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetEggCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private double _strengthShare;

    [ObservableProperty]
    private string _strengthLabel = string.Empty;

    [ObservableProperty]
    private string _progressLabel = string.Empty;

    [ObservableProperty]
    private string _roleName = string.Empty;

    [ObservableProperty]
    private System.Windows.Media.Color _typeColour = System.Windows.Media.Colors.Gray;

    [ObservableProperty]
    private string _owedMore = string.Empty;

    /// <summary>What the button says: how many are left after this one, or that there are none.</summary>
    [ObservableProperty]
    private string _buttonText = "PEDIR UN HUEVO";

    /// <summary>What the arrival animation plays; null when nothing is playing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying))]
    private NurseryPlay? _play;

    public bool IsPlaying => Play is not null;

    /// <summary>The animation is over: the place and CONTINUAR come up.</summary>
    [ObservableProperty]
    private bool _playDone;

    /// <summary>Where the egg that just arrived went, for the end of the animation.</summary>
    [ObservableProperty]
    private string _arrivedPlace = string.Empty;

    public override async Task ActivateAsync() => await RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            if (_runContext.Current is not { } run || !_nursery.PlaysWithTheNursery(run) || _nursery.RoleOf(run)?.MonoType is not { } type)
            {
                Owed = 0;
                TypeText = "Esta run no tiene guardería: es solo de los roles MONOTYPE.";
                StrengthText = string.Empty;
                OwedEggs.Clear();
                Milestones.Clear();
                MilestonesSummary = string.Empty;
                Trials.Clear();
                OwedMore = string.Empty;
                StrengthShare = 0;
                StrengthLabel = string.Empty;
                ProgressLabel = string.Empty;
                RoleName = string.Empty;
                ButtonText = "PEDIR UN HUEVO";
                return;
            }

            Owed = await _nursery.OwedAsync(run);
            var progress = await _nursery.ProgressAsync(run);
            RoleName = (_nursery.RoleOf(run)?.Name ?? run.RoleId).ToUpperInvariant();
            TypeColour = TypePalette.ColourOf(type);
            TypeText = "Solo salen especies de tu tipo, sin legendarios. Cada tirada es un huevo a nivel 1 que va a una caja de tu PC: "
                       + "qué lleva dentro lo descubres cuando eclosiona.";

            var target = _nursery.TargetFor(run, progress);
            var (low, high) = _nursery.RangeFor(type);
            var total = _nursery.Catalog.TrialAchievements.Count;

            StrengthShare = high > low ? Math.Clamp((target - low) / (double)(high - low), 0, 1) : 0;
            StrengthLabel = target > 0 ? $"~{target} PTS" : string.Empty;
            ProgressLabel = $"PRUEBAS {Math.Min(progress, total)}/{total}";
            StrengthText = "Los huevos apuntan a especies más fuertes con cada prueba que superas.";

            Trials.Clear();
            for (var i = 0; i < total; i++)
            {
                Trials.Add(new TrialPipViewModel(i < progress));
            }

            Milestones.Clear();
            var milestones = await _nursery.MilestonesAsync(run);
            foreach (var milestone in milestones)
            {
                Milestones.Add(new MilestoneEggsViewModel(milestone.Name, milestone.Eggs, milestone.Reached));
            }

            var won = milestones.Where(m => m.Reached).Sum(m => m.Eggs);
            var all = milestones.Sum(m => m.Eggs);
            MilestonesSummary = $"{milestones.Count(m => m.Reached)} de {milestones.Count} conseguidos: {won} de {all} huevos.";

            // Los huevos que se deben, dibujados: hasta doce, y un «+N» si hay más.
            OwedEggs.Clear();
            var egg = _sprites.GetEgg();
            for (var i = 0; i < Math.Min(Owed, 12); i++)
            {
                OwedEggs.Add(new OwedEggViewModel(egg));
            }

            OwedMore = Owed > 12 ? $"+{Owed - 12}" : string.Empty;
            ButtonText = Owed switch
            {
                0 => "SIN HUEVOS QUE PEDIR",
                1 => "PEDIR EL ÚLTIMO HUEVO",
                _ => $"PEDIR UN HUEVO ({Owed})"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo leer lo que debe la guardería");
        }
    }

    private bool CanGetEgg => !IsBusy && Owed > 0 && _runContext.Current is not null && !IsPlaying;

    /// <summary>Asks for the next egg: one spin, one egg, one animation.</summary>
    [RelayCommand(CanExecute = nameof(CanGetEgg))]
    private async Task GetEggAsync()
    {
        if (_runContext.Current is not { } run)
        {
            return;
        }

        IsBusy = true;
        StatusIsWarning = false;
        Status = string.Empty;

        try
        {
            // Primero se mira que se pueda escribir: con el juego abierto no se gasta ni una tirada.
            if (!_delivery.CanDeliverNow(out var reason))
            {
                Status = reason;
                StatusIsWarning = true;
                return;
            }

            var progress = await _nursery.ProgressAsync(run);
            var eggs = await _nursery.PrepareAsync(run, 1);

            if (eggs.Count == 0)
            {
                Status = "No hay nada que dar.";
                return;
            }

            var egg = eggs[0];
            var results = await _delivery.DeliverEggsAsync(eggs);

            if (!results[0].Delivered)
            {
                Status = results[0].Message;
                StatusIsWarning = true;
                return;
            }

            // El huevo se apunta cuando ya está en el archivo, y solo entonces cuenta como tirada gastada.
            await _nursery.RecordAsync(run, egg, results[0], progress);
            _logger.LogInformation("Guardería: huevo {Number} entregado (caja {Box}, hueco {Slot})", egg.Number + 1, results[0].Box, results[0].Slot);

            ArrivedPlace = PlaceOf(results[0]);
            _arrived = new ReceivedEggViewModel(egg.Number + 1, ArrivedPlace, _sprites.GetEgg());

            // La animación: el huevo cae, se carga, estalla la luz. Al acabar sale dónde ha ido, y CONTINUAR.
            PlayDone = false;
            Play = new NurseryPlay(1, TypeColours.Of(_nursery.RoleOf(run)?.MonoType ?? 0), (uint)(egg.Seed ^ (egg.Seed >> 32)));
            GetEggCommand.NotifyCanExecuteChanged();
            await Task.Delay(TimeSpan.FromSeconds(NurseryTimeline.Rest(1) + 0.3));
            PlayDone = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la guardería");
            Status = "Ha fallado.";
            StatusIsWarning = true;
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }

    private static string PlaceOf(DeliveryResult result) => result.Box == 0 ? "EN TU EQUIPO" : $"CAJA {result.Box} · HUECO {result.Slot}";

    /// <summary>The egg that just arrived, until CONTINUAR puts it in the list.</summary>
    private ReceivedEggViewModel? _arrived;

    /// <summary>After the animation: the scene goes and the egg joins the list of the ones asked for.</summary>
    [RelayCommand]
    private void Continue()
    {
        PlayDone = false;
        Play = null;
        GetEggCommand.NotifyCanExecuteChanged();

        if (_arrived is { } egg)
        {
            Received.Insert(0, egg);
            OnPropertyChanged(nameof(HasReceived));
            Status = Owed > 0
                ? $"Huevo en {egg.Place.ToLowerInvariant()}. Todavía te quedan {Owed}."
                : $"Huevo en {egg.Place.ToLowerInvariant()}. Llévalo al equipo y camina para que eclosione.";
        }

        _arrived = null;
    }

    public override void ResetState()
    {
        // Una animación a medias no es trabajo del jugador: al volver, la pantalla abre limpia.
        Play = null;
        PlayDone = false;
        _arrived = null;
        Received.Clear();
        OnPropertyChanged(nameof(HasReceived));
    }
}

/// <summary>One egg of the row of what the nursery still owes.</summary>
public sealed record OwedEggViewModel(BitmapSource? Sprite);

/// <summary>One square of the row of trials: lit when cleared.</summary>
public sealed record TrialPipViewModel(bool Cleared);

/// <summary>An egg already asked for: its number, where it went and the egg's own picture. Never what is inside.</summary>
public sealed record ReceivedEggViewModel(int Number, string Place, BitmapSource? Sprite)
{
    public string Title => $"HUEVO N.º {Number}";
}

/// <summary>A milestone of the competition and the eggs it pays, for the table of what each one gives.</summary>
public sealed record MilestoneEggsViewModel(string Name, int Eggs, bool Reached)
{
    public string EggsText => Eggs == 1 ? "1 HUEVO" : $"{Eggs} HUEVOS";
}
