using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>One of the six wedges, with the shape it occupies on the wheel.</summary>
/// <remarks>
/// The geometry is worked out here and not in XAML because a wedge is an arc, and an arc written
/// by hand six times is six chances to get one wrong. The label starts as a question mark: what
/// each wedge holds is revealed one at a time before the wheel turns.
/// </remarks>
public sealed partial class RouletteSlotViewModel(int index, Geometry wedge, Brush colour) : ObservableObject
{
    /// <summary>Where the wheel sits, in pixels. Everything else is derived from it.</summary>
    public const double Size = 340;

    private const double Radius = 158;

    private const double LabelRadius = 104;

    public int Index { get; } = index;

    public Geometry Wedge { get; } = wedge;

    public Brush Colour { get; } = colour;

    /// <summary>Middle of the wedge, where its label goes.</summary>
    public double LabelX { get; } = (Size / 2) + (LabelRadius * Math.Cos(Middle(index)));

    public double LabelY { get; } = (Size / 2) + (LabelRadius * Math.Sin(Middle(index)));

    [ObservableProperty]
    private string _text = "?";

    [ObservableProperty]
    private bool _revealed;

    /// <summary>True only for the wedge the wheel stopped on.</summary>
    [ObservableProperty]
    private bool _isWinner;

    /// <summary>Radians of the middle of wedge <paramref name="index"/>, with zero at twelve.</summary>
    private static double Middle(int index) => ((index * 60) + 30 - 90) * Math.PI / 180;

    /// <summary>The wedge as a filled arc: a line out, sixty degrees round, and back to the middle.</summary>
    public static Geometry Slice(int index)
    {
        var centre = new Point(Size / 2, Size / 2);
        var from = Edge(index * 60);
        var to = Edge((index + 1) * 60);

        var figure = new PathFigure { StartPoint = centre, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment(from, isStroked: true));
        figure.Segments.Add(new ArcSegment(to, new Size(Radius, Radius), 0,
            isLargeArc: false, SweepDirection.Clockwise, isStroked: true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();
        return geometry;
    }

    private static Point Edge(double degrees)
    {
        var radians = (degrees - 90) * Math.PI / 180;
        return new Point((Size / 2) + (Radius * Math.Cos(radians)), (Size / 2) + (Radius * Math.Sin(radians)));
    }
}

/// <param name="Turns">How many whole turns before it settles, so the stop is not instant.</param>
/// <param name="FinalAngle">Where the wheel ends up, with the winning wedge under the marker.</param>
/// <param name="Stopped">Called when the wheel has really stopped, which is what starts the result.</param>
public sealed record SpinTheWheel(int Turns, double FinalAngle, TimeSpan Duration, Action Stopped);

/// <summary>
/// The LUDÓPATA wheel.
/// </summary>
/// <remarks>
/// <para>
/// Like the gacha, and for the same reason: the spin is <b>decided, applied and recorded before a
/// single frame plays</b>. The animation shows a result that already exists in the save and in the
/// history; it never stands in for a pending computation, and closing the window mid-spin cannot
/// change what happened.
/// </para>
/// <para>
/// The wheel only turns when a milestone has paid for it. That number is not kept here: it is the
/// achievements minus the history, asked for fresh every time this screen opens.
/// </para>
/// </remarks>
public sealed partial class RouletteViewModel : SectionViewModel
{
    /// <summary>One colour per wedge. Six that tell each other apart at a glance.</summary>
    private static readonly string[] WedgeColours =
    [
        "#E0553F", "#E8A13A", "#57B45F", "#2FA5C0", "#6E63C6", "#C74E93"
    ];

    private readonly RouletteService _roulette;
    private readonly IRunContext _runContext;
    private readonly PokemonSpriteService _sprites;
    private readonly ILogger<RouletteViewModel> _logger;

    public RouletteViewModel(RouletteService roulette, IRunContext runContext,
        PokemonSpriteService sprites, ILogger<RouletteViewModel> logger)
        : base("RULETA", "Lo que la ruleta diga, va")
    {
        _roulette = roulette;
        _runContext = runContext;
        _sprites = sprites;
        _logger = logger;

        for (var index = 0; index < RouletteService.FacesOnTheWheel; index++)
        {
            var colour = (SolidColorBrush)new BrushConverter().ConvertFromString(WedgeColours[index])!;
            colour.Freeze();
            Slots.Add(new RouletteSlotViewModel(index, RouletteSlotViewModel.Slice(index), colour));
        }
    }

    /// <summary>Raised when the wheel should turn. The view animates; this owns the plan.</summary>
    public event EventHandler<SpinTheWheel>? SpinRequested;

    public ObservableCollection<RouletteSlotViewModel> Slots { get; } = [];

    /// <summary>Everything the wheel can land on, for the screen to list before anyone spins.</summary>
    public ObservableCollection<RouletteFace> Pool { get; } = [];

    /// <summary>What came out, line by line: who died, what changed, what arrived.</summary>
    public ObservableCollection<string> Lines { get; } = [];

    [ObservableProperty]
    private int _owed;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isSpinning;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _resultName = string.Empty;

    [ObservableProperty]
    private string _resultDetail = string.Empty;

    /// <summary>True when the face that won was one of the good ones, so the card can say so.</summary>
    [ObservableProperty]
    private bool _resultIsGood;

    /// <summary>The Poké Ball at the centre, out of the player's own cartridge.</summary>
    [ObservableProperty]
    private System.Windows.Media.Imaging.BitmapSource? _hub;

    public bool CanSpin => Owed > 0 && !IsSpinning;

    partial void OnOwedChanged(int value) => SpinCommand.NotifyCanExecuteChanged();

    partial void OnIsSpinningChanged(bool value) => SpinCommand.NotifyCanExecuteChanged();

    public override GameNeed Needs => GameNeed.Closed;

    public override async Task ActivateAsync()
    {
        await _sprites.PrepareAsync();
        Hub = _sprites.GetBall();

        Pool.Clear();
        foreach (var face in _roulette.Faces)
        {
            Pool.Add(face);
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_runContext.Current is not { } run)
        {
            Owed = 0;
            Status = "No hay ninguna run abierta.";
            return;
        }

        try
        {
            var earned = await _roulette.EarnedAsync(run);
            var spun = await _roulette.SpunAsync(run.Id);
            Owed = Math.Max(0, earned - spun);

            Status = Owed > 0
                ? $"Debes {Owed} tirada{(Owed == 1 ? string.Empty : "s")}. "
                  + "Guarda y cierra el juego: la ruleta escribe en la partida."
                : $"No debes ninguna tirada. Llevas {spun} de {earned}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al contar las tiradas de la ruleta");
            Status = "No se han podido contar las tiradas. El detalle está en la carpeta Logs.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSpin))]
    private async Task SpinAsync()
    {
        if (_runContext.Current is not { } run)
        {
            return;
        }

        IsSpinning = true;
        HasResult = false;
        Lines.Clear();
        Reset();

        try
        {
            // Se decide, se escribe y se registra ANTES de animar nada. Lo que gire después es
            // presentación de algo que ya ha pasado.
            var result = await _roulette.SpinAsync(run);

            if (!result.Succeeded || result.Wheel is not { } wheel)
            {
                Status = result.Message;
                await RefreshAsync();
                return;
            }

            await PlayAsync(wheel);

            ResultName = wheel.Winner.Name;
            ResultDetail = wheel.Winner.Detail;
            ResultIsGood = wheel.Winner.Good;

            foreach (var line in result.Lines)
            {
                Lines.Add(line);
            }

            HasResult = true;
            Owed = result.Owed;
            Status = result.Owed > 0
                ? $"Te quedan {result.Owed} tirada{(result.Owed == 1 ? string.Empty : "s")}."
                : "No debes ninguna tirada más.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la tirada de la ruleta");
            Status = "Ha fallado la tirada. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsSpinning = false;
        }
    }

    /// <summary>
    /// The show: the six question marks turn over one by one, and then the wheel spins.
    /// </summary>
    /// <remarks>
    /// Revealing before spinning is the point of it — the player finds out what is on the wheel,
    /// and only then what it lands on. The six can be six good ones or six bad ones; nothing here
    /// arranges them, they are the six the seed drew.
    /// </remarks>
    private async Task PlayAsync(RouletteWheel wheel)
    {
        for (var index = 0; index < Slots.Count && index < wheel.Faces.Count; index++)
        {
            Slots[index].Text = wheel.Faces[index].Name;
            Slots[index].Revealed = true;
            await Task.Delay(TimeSpan.FromMilliseconds(420));
        }

        await Task.Delay(TimeSpan.FromMilliseconds(350));

        // La marca está arriba, así que la cuña ganadora tiene que acabar debajo de ella: se gira
        // hacia atrás el centro de esa cuña, más unas cuantas vueltas enteras para que frene.
        var target = (360 * 6) - ((wheel.WinningIndex * 60) + 30);

        var stopped = new TaskCompletionSource();
        SpinRequested?.Invoke(this, new SpinTheWheel(6, target, TimeSpan.FromSeconds(4.5),
            () => stopped.TrySetResult()));

        // Se espera a que pare de verdad, no a que pase el tiempo. La red de seguridad existe por
        // si la vista nunca llegó a arrancar: una pantalla que no se cuelga.
        await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(8)));

        Slots[wheel.WinningIndex].IsWinner = true;
    }

    private void Reset()
    {
        foreach (var slot in Slots)
        {
            slot.Text = "?";
            slot.Revealed = false;
            slot.IsWinner = false;
        }
    }
}
