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
public sealed partial class RouletteSlotViewModel(int index, Geometry wedge) : ObservableObject
{
    /// <summary>Where the wheel sits, in pixels. Everything else is derived from it.</summary>
    /// <remarks>
    /// It used to be 460 because the screen gave 330 of its width to a list of the sixteen faces.
    /// That list is now a strip underneath, so the wheel gets the column to itself: 1180 of window
    /// minus the 216 of the sidebar leaves 964, and 700 sits inside that with room for the panel's
    /// own padding at the narrowest window on offer.
    /// </remarks>
    public const double Size = 700;

    /// <summary>The wedges, and the ring the labels sit on. Fractions of the size and not their
    /// own numbers: three constants that have to agree are two chances to make them disagree.</summary>
    private const double Radius = Size * 0.4647;

    private const double LabelRadius = Size * 0.3059;

    public int Index { get; } = index;

    public Geometry Wedge { get; } = wedge;

    /// <summary>Middle of the wedge, where its label goes.</summary>
    public double LabelX { get; } = (Size / 2) + (LabelRadius * Math.Cos(Middle(index)));

    public double LabelY { get; } = (Size / 2) + (LabelRadius * Math.Sin(Middle(index)));

    /// <summary>
    /// What this wedge is painted, which now depends on the FACE and not on where it sits.
    /// </summary>
    /// <remarks>
    /// It used to arrive in the constructor from a table of six rainbow colours indexed by
    /// position, so a face came out red or blue depending on where it landed and the colour could
    /// not carry meaning even in principle. Now green means it pays and red means it costs, which
    /// is the only thing anybody wants to know looking at a wheel — and it is not a spoiler,
    /// because the six faces are turned over one by one before it starts to move.
    /// </remarks>
    [ObservableProperty]
    private Brush _colour = Hidden;

    /// <summary>What the wedge says, in two or three words.</summary>
    [ObservableProperty]
    private string _label = "?";

    /// <summary>The number it shows big, or empty.</summary>
    [ObservableProperty]
    private string _figure = string.Empty;

    /// <summary>The face's picture out of the cartridge, or null when there is none.</summary>
    [ObservableProperty]
    private System.Windows.Media.Imaging.BitmapSource? _icon;

    /// <summary>Which half of the pool this wedge holds. Only meaningful once revealed.</summary>
    [ObservableProperty]
    private bool _good;

    [ObservableProperty]
    private bool _revealed;

    /// <summary>True only for the wedge the wheel stopped on.</summary>
    [ObservableProperty]
    private bool _isWinner;

    /// <summary>True for the five that did not win, so the winner is the only thing lit.</summary>
    [ObservableProperty]
    private bool _dimmed;

    private static Brush Hidden => Themed("WedgeHiddenBrush");

    /// <summary>
    /// A brush from the theme, or grey.
    /// </summary>
    /// <remarks>
    /// Grey rather than a guessed colour on purpose: a wheel of grey wedges is obviously missing
    /// its theme, while six invented colours look deliberate.
    /// </remarks>
    private static Brush Themed(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

    /// <summary>Turns this wedge over: what it holds, what it is worth and what it looks like.</summary>
    public void Reveal(string label, string figure, bool good,
        System.Windows.Media.Imaging.BitmapSource? icon)
    {
        Label = label;
        Figure = figure;
        Good = good;
        Icon = icon;

        // Dos tonos por bando, alternados por posicion. No dice nada de la cara -- eso seria una
        // escala de gravedad inventada -- y solo existe para que dos cunas seguidas del mismo
        // bando no se lean como una sola mancha.
        var alternate = Index % 2 == 1;

        Colour = Themed(good
            ? alternate ? "WedgeGoodAltBrush" : "WedgeGoodBrush"
            : alternate ? "WedgeBadAltBrush" : "WedgeBadBrush");

        Revealed = true;
    }

    /// <summary>Back to a question mark, for the next spin.</summary>
    public void Hide()
    {
        Label = "?";
        Figure = string.Empty;
        Icon = null;
        Colour = Hidden;
        Revealed = false;
        IsWinner = false;
        Dimmed = false;
    }

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

/// <summary>One of the sixteen faces in the side list, and whether this wheel drew it.</summary>
/// <remarks>
/// A wrapper and not the record itself because <see cref="OnTheWheel"/> changes while the screen is
/// open, and a record cannot tell the list it did. What it buys is the answer to the question the
/// list actually raises â Â«of all this, which six am I playing forÂ» â which was there in the wedges
/// and nowhere in the list of everything.
/// </remarks>
public sealed partial class RouletteFaceViewModel(RouletteFace face) : ObservableObject
{
    public string Id { get; } = face.Id;

    public string Name { get; } = face.Name;

    public string Detail { get; } = face.Detail;

    public bool Good { get; } = face.Good;

    /// <summary>The short form, for the strip: the long name does not fit a chip.</summary>
    public string Label { get; } = face.Label;

    public string Figure { get; } = face.Figure;

    /// <summary>Its picture out of the cartridge, filled in when the screen opens.</summary>
    [ObservableProperty]
    private System.Windows.Media.Imaging.BitmapSource? _icon;

    /// <summary>True while this face is one of the six drawn for the wheel on screen.</summary>
    [ObservableProperty]
    private bool _onTheWheel;

    /// <summary>True for the one it landed on.</summary>
    [ObservableProperty]
    private bool _won;
}

/// <param name="Turns">How many whole turns before it settles, so the stop is not instant.</param>
/// <param name="FinalAngle">Where the wheel ends up, with the winning wedge under the marker.</param>
/// <param name="Ending">How it comes to a stop. Drawn from its own stream, never from the result.</param>
/// <param name="Stopped">Called when the wheel has really stopped, which is what starts the result.</param>
public sealed record SpinTheWheel(int Turns, double FinalAngle, TimeSpan Duration,
    Views.WheelEnding Ending, Action Stopped);

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
            Slots.Add(new RouletteSlotViewModel(index, RouletteSlotViewModel.Slice(index)));
        }
    }

    /// <summary>
    /// The picture that stands for a face, out of the player's own cartridge.
    /// </summary>
    /// <remarks>
    /// Null when the face names no picture, or names an item whose icon nobody has measured. That
    /// is on purpose and it is why <c>ItemIconIndex</c> refuses to guess: a wedge with no drawing
    /// is obvious, a wedge with the wrong drawing is not. What keeps the shipped file honest is a
    /// test, not this method.
    /// </remarks>
    private System.Windows.Media.Imaging.BitmapSource? Sprite(RouletteFace face) =>
        face.SpeciesIcon > 0 ? _sprites.Get(face.SpeciesIcon)
        : face.ItemIcon > 0 ? _sprites.GetItem(face.ItemIcon)
        : null;

    /// <summary>Raised when the wheel should turn. The view animates; this owns the plan.</summary>
    /// <summary>Cuanto gira y cuanto dura. Doce segundos y once vueltas: la gracia esta en el
    /// final, cuando ya casi no se mueve y se puede leer cada cuna que pasa.</summary>
    private static readonly TimeSpan SpinTime = TimeSpan.FromSeconds(12);

    private const int Turns = 11;

    public event EventHandler<SpinTheWheel>? SpinRequested;

    /// <summary>Raised as each wedge is turned over, carrying which one.</summary>
    /// <remarks>
    /// An event and not a trigger on <c>Revealed</c>. The trigger version threw on the first wedge
    /// â a storyboard inside a DataTemplate cannot reach a transform by property path â and because
    /// the reveal happens inside the spin, it took the rest of the show down with it: the effect
    /// had already been written to the save and the player never found out what came out.
    /// </remarks>
    public event EventHandler<int>? RevealRequested;

    public ObservableCollection<RouletteSlotViewModel> Slots { get; } = [];

    /// <summary>Everything the wheel can land on, for the screen to list before anyone spins.</summary>
    public ObservableCollection<RouletteFaceViewModel> Pool { get; } = [];

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

    public override string IconKey => "IconWheel";

    public override GameNeed Needs => GameNeed.Closed;

    public override async Task ActivateAsync()
    {
        await _sprites.PrepareAsync();
        Hub = _sprites.GetBall();

        Pool.Clear();
        foreach (var face in _roulette.Faces)
        {
            Pool.Add(new RouletteFaceViewModel(face) { Icon = Sprite(face) });
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

            // El espectÃ¡culo va aparte. Lo de debajo ya ha pasado -- estÃ¡ en la partida y en el
            // historial -- asÃ­ que una animaciÃ³n rota puede costar la animaciÃ³n y nada mÃ¡s. CostÃ³
            // dos tiradas aprenderlo: una excepciÃ³n en el primer desvelado abortaba el mÃ©todo
            // entero y el jugador se quedaba sin saber quÃ© le habÃ­a tocado.
            try
            {
                await PlayAsync(wheel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FallÃ³ la animaciÃ³n de la ruleta; el resultado sÃ­ es vÃ¡lido");
            }

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
            var face = wheel.Faces[index];

            Slots[index].Reveal(face.Label, face.Figure, face.Good, Sprite(face));
            RevealRequested?.Invoke(this, index);

            // La lista de las diecisÃ©is marca la que se acaba de desvelar, al mismo tiempo que la
            // cuÃ±a: asÃ­ se ve cuÃ¡les de todas estÃ¡n en juego sin tener que ir leyendo la rueda.
            foreach (var entry in Pool.Where(entry => entry.Id == wheel.Faces[index].Id))
            {
                entry.OnTheWheel = true;
            }
            // Hay que poder LEER lo que va saliendo: son dieciseis caras posibles y seis en la
            // rueda, asi que verlas pasar sin tiempo de leerlas no es enterarse de nada.
            await Task.Delay(TimeSpan.FromMilliseconds(1500));
        }

        await Task.Delay(TimeSpan.FromMilliseconds(350));

        // Dónde tiene que quedarse la rueda, en absoluto: la marca está arriba, así que el centro
        // de la cuña ganadora -- que está a (índice*60 + 30) del origen -- tiene que acabar ahí.
        var target = RestingAngle(wheel.WinningIndex);

        var stopped = new TaskCompletionSource();

        SpinRequested?.Invoke(this, new SpinTheWheel(Turns, target, SpinTime,
            Views.WheelEnding.For(wheel.Seed, wheel.Number), () => stopped.TrySetResult()));

        // Se espera a que pare de verdad, no a que pase el tiempo. La red de seguridad existe por
        // si la vista nunca llegó a arrancar: una pantalla que no se cuelga.
        // La red se calcula desde la duracion y no se escribe aparte: alargar el giro y olvidarse
        // de esto haria que la pantalla se diera por vencida antes de que la rueda parase.
        await Task.WhenAny(stopped.Task, Task.Delay(SpinTime + TimeSpan.FromSeconds(3)));

        // El ganador se queda encendido y los otros cinco se apagan. Antes la rueda paraba con las
        // seis igual de vivas y el premio salia en el panel de al lado, asi que el final ocurria
        // en otro sitio distinto de donde estabas mirando.
        foreach (var slot in Slots)
        {
            slot.IsWinner = slot.Index == wheel.WinningIndex;
            slot.Dimmed = !slot.IsWinner;
        }

        foreach (var entry in Pool.Where(entry => entry.Id == wheel.Winner.Id))
        {
            entry.Won = true;
        }
    }

    /// <summary>Where the wheel has to come to rest for wedge <paramref name="index"/> to be under
    /// the marker, as an absolute angle in (0, 360].</summary>
    /// <remarks>
    /// A position and not a distance, which is the whole point. It used to hand the view the whole
    /// sweep -- eleven turns minus the offset -- and the view added it to wherever the wheel
    /// happened to be. The first spin of a session looked perfect because the wheel started at
    /// zero; every one after it carried the previous spin's offset, so the second landed half a
    /// wedge out, with the marker on a seam and the winning face a quarter turn away. Seen on the
    /// real run: the card said «Mueren 3 Pokémon» and the arrow pointed between two other wedges.
    /// </remarks>
    public static double RestingAngle(int index) => 360 - ((index * 60) + 30);

    private void Reset()
    {
        foreach (var slot in Slots)
        {
            slot.Hide();
        }

        foreach (var entry in Pool)
        {
            entry.OnTheWheel = false;
            entry.Won = false;
        }
    }
}
