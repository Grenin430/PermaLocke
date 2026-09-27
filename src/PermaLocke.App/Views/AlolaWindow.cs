using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PermaLocke.App.Views.AlolaPalette;

namespace PermaLocke.App.Views;

/// <summary>
/// A small pixel-art view of the sea at the hour of the player's Alola: the sky's colours, the sun or the
/// moon on its arc, stars at night, and an island with a palm tree.
/// </summary>
/// <remarks>
/// <para>
/// Next to the time in words, so the hour is seen and not only read: the sun low and orange at seven, the moon
/// high at midnight. The sun crosses from 6:00 to 18:00 and the moon the other twelve hours; that arc is drawing, not
/// astronomy, and says only what hour it is.
/// </para>
/// <para>
/// Same technique as the cemetery (§115): a few hundred cells painted by hand with ordered dithering and shown
/// with nearest-neighbour scaling at a whole number of pixels. It animates slowly — waves and stars — at a
/// few frames per second, because it is on screen for hundreds of hours.
/// </para>
/// </remarks>
public sealed class AlolaWindow : ContentControl
{
    public static readonly DependencyProperty HourProperty = DependencyProperty.Register(
        nameof(Hour), typeof(double), typeof(AlolaWindow),
        new PropertyMetadata(0.0, (d, e) => ((AlolaWindow)d).Paint()));

    /// <summary>172 pixels at two per cell: the sidebar leaves 175 between its margins.</summary>
    private const int Columns = 86;
    private const int Rows = 48;
    private const int Horizon = 32;
    private const int Cell = 2;

    private static readonly (int X, int Y, double Phase)[] Stars = MakeStars();

    private readonly byte[] _pixels = new byte[Columns * Rows * 4];
    private readonly WriteableBitmap _bitmap = new(Columns, Rows, 96, 96, PixelFormats.Bgra32, null);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame = double.NegativeInfinity;

    public AlolaWindow()
    {
        var image = new Image
        {
            Source = _bitmap,
            Width = Columns * Cell,
            Height = Rows * Cell,
            Stretch = Stretch.Fill,
            Clip = new RectangleGeometry(new Rect(0, 0, Columns * Cell, Rows * Cell), 5, 5)
        };

        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        Content = image;
        UseLayoutRounding = true;

        // WPF puede lanzar Loaded otra vez sin Unloaded entre medias (al volver a la sección): sin quitarlo antes, el
        // fotograma se apuntaba dos o tres veces y la animación iba x2 o x3 (1.0.4.6).
        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnFrame;
            CompositionTarget.Rendering += OnFrame;
        };
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnFrame;
        Paint();
    }

    public double Hour
    {
        get => (double)GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        // Seis fotogramas por segundo: olas y estrellas, nada que pida más.
        var now = _clock.Elapsed.TotalMilliseconds;

        if (IsVisible && now - _lastFrame >= 166)
        {
            _lastFrame = now;
            Paint();
        }
    }

    private void Paint()
    {
        var hour = ((Hour % 24) + 24) % 24;
        var seconds = _clock.Elapsed.TotalSeconds;
        var (top, middle, low) = SkyAt(hour);

        // Cielo: cinco tonos entre arriba, en medio y el horizonte, a trama.
        Color[] ramp = [top, Lerp(top, middle, 0.5), middle, Lerp(middle, low, 0.5), low];

        for (var y = 0; y <= Horizon; y++)
        {
            var position = y / (double)Horizon * (ramp.Length - 1);
            var band = Math.Min((int)position, ramp.Length - 2);
            var mix = position - band;

            for (var x = 0; x < Columns; x++)
            {
                Put(x, y, Bayer[y & 3, x & 3] < mix * 16 ? ramp[band + 1] : ramp[band]);
            }
        }

        var night = NightAmount(hour);

        foreach (var (sx, sy, phase) in Stars)
        {
            var twinkle = 0.55 + (0.45 * Math.Sin((seconds * 1.1) + phase));

            if (night * twinkle > 0.35)
            {
                Mix(sx, sy, Star, Math.Min(1, night * twinkle));
            }
        }

        // El astro: el sol de 6 a 18 y la luna las otras doce horas, por el mismo arco.
        var (isDay, along) = Body(hour);
        var bodyX = (int)Math.Round(8 + (along * (Columns - 16)));
        var bodyY = (int)Math.Round(Horizon - (Math.Sin(Math.PI * along) * (Horizon - 7)));
        var sunColour = Lerp(SunLow, SunHigh, Math.Sin(Math.PI * along));

        if (isDay)
        {
            Disc(bodyX, bodyY, 3.5, sunColour, sunColour);
            Halo(bodyX, bodyY, sunColour);
        }
        else
        {
            Disc(bodyX, bodyY, 3.5, MoonLight, MoonShade);
        }

        // El mar: más oscuro que el horizonte y cada vez más hondo; olas que corren y el reflejo del astro.
        var reflection = isDay ? sunColour : MoonLight;

        for (var y = Horizon + 1; y < Rows; y++)
        {
            var depth = (y - Horizon) / (double)(Rows - Horizon);
            var water = Lerp(Lerp(low, middle, 0.5), Ink, 0.45 + (depth * 0.35));
            var crest = Lerp(water, low, 0.35);
            var drift = (int)(seconds * (1.5 + (depth * 2))) * ((y & 1) == 0 ? 1 : -1);

            for (var x = 0; x < Columns; x++)
            {
                var wave = (((x + drift + (y * 7)) % 13) + 13) % 13 < 2;
                Put(x, y, wave ? crest : water);

                var spread = 1 + (int)(depth * 5);

                if (bodyY <= Horizon && Math.Abs(x - bodyX) <= spread && ((x + y + (int)(seconds * 3)) % 3) == 0)
                {
                    Mix(x, y, reflection, 0.55 * (1 - depth));
                }
            }
        }

        Islands(top, seconds);

        _bitmap.WritePixels(new Int32Rect(0, 0, Columns, Rows), _pixels, Columns * 4, 0);
    }

    /// <summary>
    /// A near island with its palm tree, backlit, and a far flat one. Both in the middle stretch of the sea:
    /// the sun rises on the left edge and sets on the right, and the first version hid the sunset behind
    /// the palm.
    /// </summary>
    private void Islands(Color skyTop, double seconds)
    {
        var far = Lerp(skyTop, Ink, 0.35);
        var near = Lerp(skyTop, Ink, 0.72);

        for (var x = 50; x <= 64; x++)
        {
            var rise = x is >= 54 and <= 60 ? 2 : 1;

            for (var y = Horizon - rise + 1; y <= Horizon; y++)
            {
                Put(x, y, far);
            }
        }

        const int HillCentre = 34;

        for (var x = HillCentre - 17; x <= HillCentre + 17; x++)
        {
            var across = (x - HillCentre) / 17.0;
            var height = (int)Math.Round(Math.Sqrt(Math.Max(0, 1 - (across * across))) * 6);

            for (var y = Horizon - height + 1; y <= Horizon + 2; y++)
            {
                Put(x, y, near);
            }
        }

        // El tronco, curvado hacia la izquierda.
        const int Base = HillCentre + 3;
        var crownX = Base - 6;
        var crownY = Horizon - 24;

        for (var i = 0; i <= 20; i++)
        {
            var t = i / 20.0;
            var x = (int)Math.Round(Base - (6 * t * t));
            var y = Horizon - 4 - (int)Math.Round(20 * t);
            Put(x, y, near);

            if (i < 6)
            {
                Put(x + 1, y, near);
            }
        }

        // Las hojas, con la punta meciéndose un píxel.
        var sway = (int)Math.Round(Math.Sin(seconds * 0.9));
        (int X, int Y)[][] fronds =
        [
            [(-1, 0), (-2, 0), (-3, 0), (-4, 1), (-5, 1), (-6, 2), (-7, 3), (-7, 4)],
            [(1, 0), (2, -1), (3, -1), (4, 0), (5, 0), (6, 1), (7, 2), (8, 3)],
            [(-1, -1), (-2, -2), (-3, -2), (-4, -2), (-5, -1), (-6, 0)],
            [(1, -1), (2, -2), (3, -2), (4, -2), (5, -1), (6, 0)],
            [(-1, 1), (-2, 2), (-3, 3), (-3, 4)],
            [(1, 1), (2, 2), (3, 3), (4, 4)]
        ];

        foreach (var frond in fronds)
        {
            for (var i = 0; i < frond.Length; i++)
            {
                var tip = i >= frond.Length - 3 ? sway : 0;
                Put(crownX + frond[i].X + tip, crownY + frond[i].Y, near);
            }
        }

        Put(crownX, crownY, near);
    }

    private void Disc(int cx, int cy, double radius, Color light, Color shade)
    {
        var reach = (int)Math.Ceiling(radius);

        for (var y = cy - reach; y <= cy + reach; y++)
        {
            for (var x = cx - reach; x <= cx + reach; x++)
            {
                // El astro se esconde tras el horizonte en vez de pintarse encima del mar.
                if (y > Horizon)
                {
                    continue;
                }

                var dx = x - cx;
                var dy = y - cy;

                if ((dx * dx) + (dy * dy) <= radius * radius)
                {
                    // Sombra en diagonal por abajo a la derecha, y no por filas y columnas: así se lee redondo.
                    Put(x, y, dx + dy >= 3 ? shade : light);
                }
            }
        }
    }

    private void Halo(int cx, int cy, Color colour)
    {
        for (var y = cy - 7; y <= Math.Min(Horizon, cy + 7); y++)
        {
            for (var x = cx - 7; x <= cx + 7; x++)
            {
                var d = Math.Sqrt(((x - cx) * (x - cx)) + ((y - cy) * (y - cy)));

                if (d is > 3.6 and < 7 && Bayer[y & 3, x & 3] < (7 - d) * 3)
                {
                    Mix(x, y, colour, 0.28);
                }
            }
        }
    }

    private static (int X, int Y, double Phase)[] MakeStars()
    {
        var random = new Random(1985);
        return [.. Enumerable.Range(0, 26).Select(_ => (random.Next(Columns), random.Next(Horizon - 5), random.NextDouble() * Math.PI * 2))];
    }

    private void Put(int x, int y, Color colour)
    {
        if (x < 0 || x >= Columns || y < 0 || y >= Rows)
        {
            return;
        }

        var i = ((y * Columns) + x) * 4;
        _pixels[i] = colour.B;
        _pixels[i + 1] = colour.G;
        _pixels[i + 2] = colour.R;
        _pixels[i + 3] = 255;
    }

    private void Mix(int x, int y, Color colour, double amount)
    {
        if (x < 0 || x >= Columns || y < 0 || y >= Rows)
        {
            return;
        }

        var i = ((y * Columns) + x) * 4;
        _pixels[i] = (byte)(_pixels[i] + ((colour.B - _pixels[i]) * amount));
        _pixels[i + 1] = (byte)(_pixels[i + 1] + ((colour.G - _pixels[i + 1]) * amount));
        _pixels[i + 2] = (byte)(_pixels[i + 2] + ((colour.R - _pixels[i + 2]) * amount));
        _pixels[i + 3] = 255;
    }
}
