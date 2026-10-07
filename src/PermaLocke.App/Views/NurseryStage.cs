using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>What the nursery shows: how many eggs come, in the colour of the run's type, from the run's seed.</summary>
public sealed record NurseryPlay(int Eggs, PixelColour Type, uint Seed);

/// <summary>
/// Puts <see cref="NurseryScene"/> on screen over the nursery: a bitmap at the screen's own resolution, painted from the
/// moment the play starts. All the drawing is in the scene; this only keeps time.
/// </summary>
public sealed class NurseryStage : ContentControl
{
    public static readonly DependencyProperty PlayProperty = DependencyProperty.Register(
        nameof(Play), typeof(NurseryPlay), typeof(NurseryStage),
        new PropertyMetadata(null, (d, _) => ((NurseryStage)d).OnPlayChanged()));

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly Stopwatch _clock = new();

    private NurseryScene? _scene;
    private WriteableBitmap? _bitmap;
    private long _lastStep = -1;
    private bool _broken;

    public NurseryStage()
    {
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        Content = new Grid { UseLayoutRounding = true, Background = Brushes.Transparent, Children = { _image } };
        SizeChanged += (_, _) => Reshape();

        // Quitar antes de poner: WPF puede lanzar Loaded dos veces (§210).
        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFrame;
            CompositionTarget.Rendering += OnFrame;
        };
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    public NurseryPlay? Play
    {
        get => (NurseryPlay?)GetValue(PlayProperty);
        set => SetValue(PlayProperty, value);
    }

    /// <summary>The scene could not be drawn: the nursery shows the eggs without the animation.</summary>
    public event Action<Exception>? RenderFailed;

    private void OnPlayChanged()
    {
        _broken = false;

        if (Play is not null)
        {
            _clock.Restart();
        }
        else
        {
            _clock.Reset();
        }

        Paint();
    }

    private void Reshape()
    {
        if (ActualWidth < 1 || ActualHeight < 1)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Floor(ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Floor(ActualHeight * dpi.DpiScaleY);

        if (_scene is null || _scene.Width != width || _scene.Height != height)
        {
            _scene = new NurseryScene(width, height);
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            _image.Source = _bitmap;
            _image.Width = ActualWidth;
            _image.Height = ActualHeight;
        }

        Paint();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        // 30 imágenes por segundo: la escena pinta la pantalla entera en cada una.
        var step = _clock.ElapsedMilliseconds / 33;
        if (IsVisible && Play is not null && step != _lastStep)
        {
            _lastStep = step;
            Paint();
        }
    }

    private void Paint()
    {
        if (_broken || _scene is null || _bitmap is null || Play is not { } play)
        {
            return;
        }

        try
        {
            _scene.Render(play.Eggs, play.Type, play.Seed, _clock.Elapsed.TotalSeconds);
            _bitmap.WritePixels(new Int32Rect(0, 0, _scene.Width, _scene.Height), _scene.Pixels, _scene.Width * 4, 0);
        }
        catch (Exception ex)
        {
            _broken = true;
            RenderFailed?.Invoke(ex);
        }
    }
}
