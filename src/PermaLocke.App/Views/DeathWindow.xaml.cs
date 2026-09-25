using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>
/// Draws the death ceremony over the emulator: opens the scene, plays each death, closes it.
/// </summary>
/// <remarks>
/// <para>
/// Presentation only: what to show comes resolved in the <see cref="DeathCard"/>. The code-behind
/// exists for what XAML cannot do — the timelines, the blood, freezing the game's frame and keeping
/// the window glued to another program's window.
/// </para>
/// <para>
/// <b>Every animated property is a track that starts at time zero with its own starting value</b>,
/// and nothing is ever removed or stopped. That is not style, it is the fix of what the rehearsal
/// showed: with <c>Storyboard.Remove</c> between deaths only the first one was drawn. The removal is
/// applied on the next tick, after the next death's storyboard had already begun, and it took that
/// one's animations off the properties too — its clock ran, the scene closed on time, and nothing
/// moved. A track that begins at zero replaces the previous one in the same instant.
/// </para>
/// <para>
/// <b>One grid for the Pokémon and its blood.</b> The sprite is drawn at exactly <see cref="Cell"/>
/// design units per pixel, at positions that are multiples of it, and the blood is a bitmap of one
/// cell per pixel laid over the same grid (<see cref="PixelBlood"/>). Its shake moves one cell and
/// its sinking goes down cell by cell, so the two never slide out of step.
/// </para>
/// </remarks>
public partial class DeathWindow : Window
{
    private const string ScaleX = "(UIElement.RenderTransform).(ScaleTransform.ScaleX)";
    private const string ScaleY = "(UIElement.RenderTransform).(ScaleTransform.ScaleY)";
    private const string MoveX = "(UIElement.RenderTransform).(TranslateTransform.X)";
    private const string MoveY = "(UIElement.RenderTransform).(TranslateTransform.Y)";

    /// <summary>Design units per pixel of the sprite, and per cell of blood.</summary>
    private const int Cell = 7;

    /// <summary>The stage in cells: the biggest box icon in the cartridge is 40×30.</summary>
    private const int StageColumns = 40, StageRows = 30;

    /// <summary>Where the stage starts, in cells of the 1000×560 centre (357 and 35 design units).</summary>
    private const int StageLeft = 51, StageTop = 5;

    /// <summary>When the hit lands, and when the Pokémon starts and ends sinking, from the start of a card.</summary>
    private const int Hit = 300, Sinks = 1250, Sunk = 1710;

    private readonly DispatcherTimer _follow;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private PixelBlood? _blood;

    /// <summary>How many whole cells the blood bitmap reaches beyond the centre on the left and on top.</summary>
    private (int X, int Y) _margin;

    public DeathWindow()
    {
        InitializeComponent();

        // Antes de enseñarse por primera vez: una ventana que se muestra y DESPUÉS se vuelve
        // intocable puede llevarse el foco en ese instante, y eso es soltar el mando en pleno
        // combate.
        OverlayWindows.MakeUntouchable(this);

        // Se recoloca mientras se ve: si el jugador mueve o redimensiona el emulador, la escena va
        // con él en vez de quedarse tapando el escritorio.
        _follow = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Render,
            (_, _) => OverlayWindows.PlaceOver(this, Target()), Dispatcher);
    }

    private double Now => _clock.Elapsed.TotalMilliseconds;

    /// <summary>Freezes the game's frame, drains its colour, brings the bars in and lets them bleed.</summary>
    public Task OpenAsync()
    {
        var box = Target();

        // La foto ANTES de enseñarse: después saldría la propia escena dentro de la foto. Es lo que
        // hay en pantalla en ese sitio, así que aparecer encima no se nota.
        var frame = OverlayWindows.Capture(box);

        FrozenColour.Source = frame;
        FrozenGrey.Source = frame is null ? null : Grey(frame);

        OverlayWindows.PlaceOver(this, box);

        if (!IsVisible)
        {
            Show();
        }

        UpdateLayout();
        LayBlood();

        _follow.Start();
        CompositionTarget.Rendering -= OnFrame;
        CompositionTarget.Rendering += OnFrame;

        var story = new Storyboard();

        Add(story, Track(1), Root, "Opacity");

        // Sin foto -si no se pudo leer la pantalla- no hay nada que desaturar: un velo oscuro hace
        // de fondo y el resto de la escena sigue igual.
        Add(story, Track(0, new Step(0, 650, frame is null ? 0 : 1)), FrozenGrey, "Opacity");
        Add(story, Track(0, new Step(0, 650, frame is null ? 0.85 : 0.45)), Dim, "Opacity");
        Add(story, Track(0), Hurt, "Opacity");

        Add(story, Track(0, new Step(200, 480, 1, Out())), TopBar, ScaleY);
        Add(story, Track(0, new Step(200, 480, 1, Out())), BottomBar, ScaleY);

        // Lo de la tarjeta, apagado hasta que llegue la primera.
        Add(story, Track(0), Sprite, "Opacity");
        Add(story, Track(0), Headline, "Opacity");
        Add(story, Track(0), Band, "Opacity");
        Add(story, Track(0), PointsText, "Opacity");

        return Run(story, 700);
    }

    /// <summary>Plays one death and completes when it has gone.</summary>
    public Task PlayAsync(DeathCard card)
    {
        FlashSprite.Source = card.Flash;
        ColourSprite.Source = card.Sprite;
        HeadlineText.Text = Spaced(card.Title);
        PointsText.Text = Spaced(card.Points);

        var hasSprite = !card.IsWipe && card.Sprite is not null;
        SpriteStage.Visibility = hasSprite ? Visibility.Visible : Visibility.Hidden;

        var named = hasSprite ? 1600 : 350;
        var leaves = named + 3600;

        var body = hasSprite ? PlaceSprite(card.Sprite!) : [];
        _blood?.StartCard(Now, body, _margin.Y + StageTop + StageRows, Hit, Sinks, Sunk, leaves);

        var story = new Storyboard();

        if (hasSprite)
        {
            // Aparece, y enseguida el golpe: tres destellos en blanco con su temblor de una celda y la
            // pantalla teñida de rojo un instante. La sangre la lleva PixelBlood con el mismo reloj.
            Add(story, Track(0, new Step(0, 180, 1)), Sprite, "Opacity");

            var flashes = new List<Step>();
            var shakes = new List<Step>();

            for (var i = 0; i < 6; i++)
            {
                flashes.Add(new Step(Hit + (i * 70), 0, i % 2 == 0 ? 1 : 0));
                shakes.Add(new Step(Hit + (i * 70), 0, i % 2 == 0 ? Cell : -Cell));
            }

            flashes.Add(new Step(Hit + 420, 0, 0));
            shakes.Add(new Step(Hit + 420, 0, 0));

            Add(story, Track(0, [.. flashes]), FlashSprite, "Opacity");
            Add(story, Track(0, [.. shakes]), Sprite, MoveX);
            Add(story, Track(0, new Step(Hit, 50, 0.42), new Step(Hit + 50, 520, 0, Out())), Hurt, "Opacity");

            // Se hunde en su charco celda a celda, acelerando: la posición crece con el cuadrado del
            // tiempo, así que la celda k llega en la raíz de k.
            var sinking = new List<Step>();

            for (var k = 1; k <= StageRows; k++)
            {
                var at = Sinks + (int)((Sunk - Sinks) * Math.Sqrt(k / (double)StageRows));
                sinking.Add(new Step(at, 0, k * Cell));
            }

            Add(story, Track(0, [.. sinking]), Sprite, MoveY);
        }

        if (hasSprite && card.Ghost is { } ghost)
        {
            AddGhost(story, ghost, card.Sprite!, leaves);
        }
        else
        {
            Add(story, Track(0), GhostSprite, "Opacity");
        }

        // EL NOMBRE, a lo Souls: la banda primero, el texto entra despacio y sigue creciendo un poco
        // mientras se lee. Y se va todo junto.
        Add(story, Track(0, new Step(named - 150, 800, 1), new Step(leaves, 500, 0)), Band, "Opacity");
        Add(story, Track(0, new Step(named, 1500, 1, InOut()), new Step(leaves, 500, 0)), Headline, "Opacity");
        Add(story, Track(0.9, new Step(named, 3400, 1, Out())), Headline, ScaleX);
        Add(story, Track(0.9, new Step(named, 3400, 1, Out())), Headline, ScaleY);
        Add(story, Track(0, new Step(named + 1300, 600, 1), new Step(leaves, 500, 0)), PointsText, "Opacity");

        return Run(story, leaves + 650);
    }

    /// <summary>
    /// The ghost (§183): it comes out where the Pokémon sank, rises, and drifts off by the left edge of the screen, on
    /// its way to the others' screens.
    /// </summary>
    /// <remarks>
    /// On the same grid as the sprite and the blood, and it moves only in whole cells, a step at a time: the pixel art
    /// stays pixel art. It has to be gone before the title leaves, so its steps are sized from the real distance to
    /// the left edge, which depends on how wide the emulator is.
    /// </remarks>
    private void AddGhost(Storyboard story, BitmapSource ghost, BitmapSource sprite, int leaves)
    {
        var width = Math.Min(sprite.PixelWidth, StageColumns);
        var height = Math.Min(sprite.PixelHeight, StageRows);
        var left = 357 + ((StageColumns - width) / 2 * Cell);
        var top = 35 + ((StageRows - height) * Cell);

        GhostSprite.Source = ghost;
        GhostSprite.Width = width * Cell;
        GhostSprite.Height = height * Cell;
        GhostSprite.Margin = new Thickness(left, top, 0, 0);

        // Hasta el borde izquierdo de la ventana, no del diseño: el centro de 1000 se escala y se centra.
        var scale = Math.Min(Middle.ActualWidth / 1000, Middle.ActualHeight / 560);
        var outside = scale > 0 ? (Middle.ActualWidth / scale - 1000) / 2 : 0;
        var distance = left + outside + (width * Cell) + Cell;

        const int appears = Sunk + 140, rise = 8, riseEvery = 90;
        var drifts = appears + (rise * riseEvery) + 80;
        var gone = leaves - 250;
        const int stepEvery = 45;
        var steps = Math.Max(1, (gone - drifts) / stepEvery);
        var stride = Math.Max(Cell, (int)Math.Ceiling(distance / steps / Cell) * Cell);

        Add(story, Track(0, new Step(appears, 0, 0.5), new Step(appears + 80, 0, 1), new Step(leaves, 0, 0)),
            GhostSprite, "Opacity");

        var up = new List<Step>();
        for (var k = 1; k <= rise; k++)
        {
            up.Add(new Step(appears + (k * riseEvery), 0, -k * Cell));
        }

        // Flota: una celda arriba y abajo mientras se va.
        for (var at = drifts; at < gone; at += 300)
        {
            up.Add(new Step(at, 0, -(rise + ((at - drifts) / 300 % 2)) * Cell));
        }

        Add(story, Track(0, [.. up]), GhostSprite, MoveY);

        var away = new List<Step>();
        for (var k = 1; k <= steps; k++)
        {
            away.Add(new Step(drifts + (k * stepEvery), 0, -k * stride));
        }

        Add(story, Track(0, [.. away]), GhostSprite, MoveX);
    }

    /// <summary>Takes the bars away and gives the game back.</summary>
    public Task CloseAsync()
    {
        var story = new Storyboard();

        Add(story, Track(1, new Step(0, 420, 0, In())), TopBar, ScaleY);
        Add(story, Track(1, new Step(0, 420, 0, In())), BottomBar, ScaleY);
        Add(story, Track(1, new Step(250, 550, 0)), Root, "Opacity");

        return Run(story, 820, then: () =>
        {
            _follow.Stop();
            CompositionTarget.Rendering -= OnFrame;
        });
    }

    private void OnFrame(object? sender, EventArgs e) => _blood?.Tick(Now);

    /// <summary>
    /// Sizes the blood bitmap so its cells are exactly the sprite's pixels on screen, lined up with
    /// the centre and reaching out to the edges of the row.
    /// </summary>
    /// <remarks>
    /// The centre is a 1000×560 design scaled to fit and centred, so a cell measures
    /// <see cref="Cell"/> times that scale. The bitmap starts a whole number of cells before the
    /// centre's corner — which is how its grid and the sprite's coincide — and whatever pokes out
    /// of the row is cut by its clip.
    /// </remarks>
    private void LayBlood()
    {
        var width = Middle.ActualWidth;
        var height = Middle.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            _blood = null;
            BloodLayer.Source = null;
            return;
        }

        var scale = Math.Min(width / 1000, height / 560);
        var cell = Cell * scale;
        var left = (width - (1000 * scale)) / 2;
        var top = (height - (560 * scale)) / 2;

        _margin = ((int)Math.Ceiling(left / cell), (int)Math.Ceiling(top / cell));

        var originX = left - (_margin.X * cell);
        var originY = top - (_margin.Y * cell);
        var columns = (int)Math.Ceiling((width - originX) / cell);
        var rows = (int)Math.Ceiling((height - originY) / cell);

        _blood = new PixelBlood(columns, rows);

        BloodLayer.Source = _blood.Bitmap;
        BloodLayer.Margin = new Thickness(originX, originY, 0, 0);
        BloodLayer.Width = columns * cell;
        BloodLayer.Height = rows * cell;

        // Los hilos no bajan más allá de unas celdas por encima del suelo del Pokémon.
        _blood.OpenScene(Now, _margin.Y + StageTop + StageRows - 6);
    }

    /// <summary>
    /// Puts the sprite on the grid at its real pixel size, centred and standing on the floor, and
    /// returns its opaque pixels in blood cells — where the blood will come out of.
    /// </summary>
    private IReadOnlyList<(int X, int Y)> PlaceSprite(BitmapSource sprite)
    {
        var width = Math.Min(sprite.PixelWidth, StageColumns);
        var height = Math.Min(sprite.PixelHeight, StageRows);
        var fromLeft = (StageColumns - width) / 2;
        var fromTop = StageRows - height;

        Sprite.Width = width * Cell;
        Sprite.Height = height * Cell;
        Sprite.Margin = new Thickness(fromLeft * Cell, fromTop * Cell, 0, 0);

        var bgra = new FormatConvertedBitmap(sprite, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
        bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);

        var body = new List<(int X, int Y)>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[(((y * bgra.PixelWidth) + x) * 4) + 3] > 0)
                {
                    body.Add((_margin.X + StageLeft + fromLeft + x, _margin.Y + StageTop + fromTop + y));
                }
            }
        }

        return body;
    }

    /// <summary>
    /// Over the emulator's picture if it is there; over PermaLocke if it is not; the screen if
    /// neither can be seen.
    /// </summary>
    /// <remarks>
    /// Does not depend on PermaLocke's own window being open: with the app tucked into the edge
    /// tab its window is minimised, and the scene still goes over the emulator (measured, §113).
    /// </remarks>
    private static (int Left, int Top, int Width, int Height) Target() =>
        GameWindow.ClientBox(GameWindow.Handle())
        ?? (Application.Current?.MainWindow is { } main
            ? GameWindow.ClientBox(new WindowInteropHelper(main).Handle)
            : null)
        ?? OverlayWindows.WorkArea();

    /// <summary>
    /// Letters pulled apart with thin spaces, words with a wider gap: the tracking of an engraved
    /// title, which WPF's text has no property for.
    /// </summary>
    private static string Spaced(string text) =>
        string.Join((char)0x2009, text.Select(letter => letter == ' ' ? ((char)0x2002).ToString() : letter.ToString()));

    private Task Run(Storyboard story, int lengthMs, Action? then = null)
    {
        story.Duration = Ms(lengthMs);

        // Continuaciones fuera del evento: lo siguiente -otra muerte, o cerrar- no arranca dentro del
        // Completed del storyboard que acaba de terminar.
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        story.Completed += (_, _) =>
        {
            then?.Invoke();
            done.TrySetResult();
        };

        story.Begin(this);

        return done.Task;
    }

    /// <summary>One stretch of a track: from <paramref name="Begin"/> it goes to the value in
    /// <paramref name="Length"/> milliseconds — zero for a jump.</summary>
    private readonly record struct Step(int Begin, int Length, double To, IEasingFunction? Ease = null);

    /// <summary>
    /// A property's whole life on one timeline: its starting value at zero, and each step after,
    /// holding the last value in between.
    /// </summary>
    private static DoubleAnimationUsingKeyFrames Track(double start, params Step[] steps)
    {
        var track = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
        var current = start;

        track.KeyFrames.Add(new DiscreteDoubleKeyFrame(start, KeyTime.FromTimeSpan(TimeSpan.Zero)));

        foreach (var step in steps)
        {
            track.KeyFrames.Add(new DiscreteDoubleKeyFrame(current, KeyTime.FromTimeSpan(Ms(step.Begin))));

            track.KeyFrames.Add(step.Length == 0
                ? new DiscreteDoubleKeyFrame(step.To, KeyTime.FromTimeSpan(Ms(step.Begin)))
                : new EasingDoubleKeyFrame(step.To, KeyTime.FromTimeSpan(Ms(step.Begin + step.Length)), step.Ease));

            current = step.To;
        }

        return track;
    }

    /// <summary>The frozen frame in greys, for the colour to drain into.</summary>
    private static BitmapSource Grey(BitmapSource frame)
    {
        var grey = new FormatConvertedBitmap(frame, PixelFormats.Gray8, null, 0);
        grey.Freeze();
        return grey;
    }

    private static IEasingFunction Out() => new CubicEase { EasingMode = EasingMode.EaseOut };

    private static IEasingFunction In() => new QuadraticEase { EasingMode = EasingMode.EaseIn };

    private static IEasingFunction InOut() => new SineEase { EasingMode = EasingMode.EaseInOut };

    /// <summary>
    /// Always by the ELEMENT and a property path, never with a transform as the target: animating a
    /// <see cref="Transform"/> set directly with <c>SetTarget</c> did not apply, and the title stayed
    /// at its starting scale (measured on a capture, §113).
    /// </summary>
    private static void Add(Storyboard story, Timeline animation, DependencyObject target, string property) =>
        Add(story, animation, target, new PropertyPath(property));

    private static void Add(Storyboard story, Timeline animation, DependencyObject target, PropertyPath property)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        story.Children.Add(animation);
    }

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);
}
