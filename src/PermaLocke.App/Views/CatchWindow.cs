using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>
/// The card of a wild Pokémon just caught going into the album, over the emulator's top screen (§190).
/// </summary>
/// <remarks>
/// Presentation only, like <see cref="GhostWindow"/>: transparent, never takes the mouse or the focus, and draws what
/// <see cref="CatchScene"/> paints, at the monitor's own pixels.
/// </remarks>
public sealed class CatchWindow : Window
{
    private readonly Image _image = new() { Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

    public CatchWindow()
    {
        Title = "PermaLocke";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        IsHitTestVisible = false;
        UseLayoutRounding = true;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        Content = new Canvas { Children = { _image } };
        OverlayWindows.MakeUntouchable(this);
    }

    /// <summary>An item going into the bag (2026-09-28), in the same window: one animation over the game at a time.</summary>
    public Task PlayAsync(ItemScene.Item item, (int Left, int Top) corner, double pixel)
    {
        var scene = new ItemScene(pixel);
        var box = (corner.Left, corner.Top, scene.Width, scene.Height);
        return Run(box, scene.Width, scene.Height, t => scene.Render(item, t), () => scene.Pixels, ItemTimeline.Length);
    }

    private Task Run((int Left, int Top, int Width, int Height) box, int width, int height, Action<double> render,
        Func<byte[]> pixels, double length)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
        var whole = new Int32Rect(0, 0, width, height);

        OverlayWindows.PlaceOver(this, box);
        if (!IsVisible) Show();
        OverlayWindows.PlaceOver(this, box);

        var dpi = VisualTreeHelper.GetDpi(this);
        _image.Source = bitmap;
        _image.Width = width / dpi.DpiScaleX;
        _image.Height = height / dpi.DpiScaleY;

        var clock = Stopwatch.StartNew();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Frame(object? sender, EventArgs e)
        {
            var t = clock.Elapsed.TotalSeconds;
            try
            {
                render(t);
                bitmap.WritePixels(whole, pixels(), width * 4, 0);
            }
            catch (Exception ex)
            {
                CompositionTarget.Rendering -= Frame;
                done.TrySetException(ex);
                return;
            }

            if (t >= length)
            {
                CompositionTarget.Rendering -= Frame;
                done.TrySetResult();
            }
        }

        CompositionTarget.Rendering += Frame;
        return done.Task;
    }

    /// <summary>Plays the whole thing over a box of monitor pixels. Completes when nothing is left on screen.</summary>
    /// <param name="pixel">Monitor pixels per pixel of the game.</param>
    public Task PlayAsync(TcgRender front, TcgRender back, TcgCard card, (int Left, int Top, int Width, int Height) box, double pixel)
    {
        var scene = new CatchScene(box.Width, box.Height, pixel);
        return Run(box, scene.Width, scene.Height, t => scene.Render(front, back, card, t), () => scene.Pixels, CatchTimeline.Length);
    }
}
