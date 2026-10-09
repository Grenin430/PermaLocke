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
    /// <param name="hurry">Asked every frame: while it says yes the animation plays three times as fast, so that an item
    /// does not hold up the ones behind it (the long ones, the Mega Stones, can be cut that way as the player goes on).</param>
    /// <param name="leave">Asked every frame: the first time it says yes the player has carried on, and the scene goes out
    /// through the dither in <see cref="ItemScene.ExitSeconds"/> instead of waiting for its end.</param>
    /// <param name="speed">1 is real time; the rehearsal slows it down to be able to look at it.</param>
    public Task PlayAsync(ItemScene.Item item, (int Left, int Top) corner, double pixel, Func<bool>? hurry = null,
        Func<bool>? leave = null, double speed = 1)
    {
        var scene = new ItemScene(pixel, ItemScene.HeightFor(item));
        var box = (corner.Left, corner.Top, scene.Width, scene.Height);
        return Run(box, scene.Width, scene.Height, (t, fade) => scene.Render(item, t, fade), () => scene.Pixels, scene.LengthFor(item),
            hurry, leave, speed);
    }

    private Task Run((int Left, int Top, int Width, int Height) box, int width, int height, Action<double, double> render,
        Func<byte[]> pixels, double length, Func<bool>? hurry = null, Func<bool>? leave = null, double speed = 1)
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
        var played = 0.0;
        var seen = 0.0;
        double? left = null;

        void Frame(object? sender, EventArgs e)
        {
            // Real time passes at the frame rate there is; what the scene sees is time, never a count of frames.
            var now = clock.Elapsed.TotalSeconds;
            played += (now - seen) * speed * (hurry?.Invoke() == true ? 3 : 1);
            seen = now;
            var t = played;

            if (left is null && leave?.Invoke() == true) left = clock.Elapsed.TotalSeconds;
            var fade = left is { } since ? 1 - ((now - since) / ItemScene.ExitSeconds) : 1;

            try
            {
                render(t, fade);
                bitmap.WritePixels(whole, pixels(), width * 4, 0);
            }
            catch (Exception ex)
            {
                CompositionTarget.Rendering -= Frame;
                done.TrySetException(ex);
                return;
            }

            if (t >= length || fade <= 0)
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
        return Run(box, scene.Width, scene.Height, (t, _) => scene.Render(front, back, card, t), () => scene.Pixels, CatchTimeline.Length);
    }
}
