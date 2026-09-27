namespace PermaLocke.App.Views;

/// <summary>The moments of a friend's ghost crossing this emulator (1.0.5), in seconds.</summary>
public static class GhostTimeline
{
    /// <summary>The cold comes in: the screen dims at the edges and the mist rises.</summary>
    public const double Chill = 0.8;

    /// <summary>When the ghost reaches the middle, stops and looks out of the screen.</summary>
    public const double Pause = 3.1;

    /// <summary>How long it stays looking.</summary>
    public const double Look = 1.1;

    /// <summary>When it reaches the left edge and starts coming apart.</summary>
    public const double Leave = 6.4;

    public const double Length = 7.4;
}

/// <summary>
/// A friend's fallen Pokémon crossing the emulator as a ghost (1.0.5): pixel art on a grid of cells, drawn a frame at a
/// time from a moment, so it is tested without a window.
/// </summary>
/// <remarks>
/// <para>
/// The screen goes cold: its edges dim in steps of blue and a pale mist drifts along the bottom. The ghost comes in
/// from the right on a slow wave, see-through and flickering, its outline wobbling row by row like heat, its lower
/// half fraying into wisps that trail behind. Two will-o'-wisps circle it and it sheds motes that rise and fade. Halfway
/// it stops and turns to face the screen, the wisps flare, then it goes on and comes apart into sparks at the left
/// edge. The line under it says whose it was.
/// </para>
/// <para>
/// Everything is premultiplied BGRA with transparency: the game shows through wherever nothing is drawn.
/// </para>
/// </remarks>
public sealed class GhostScene
{
    private readonly byte[] _ghost;
    private readonly int _ghostWidth;
    private readonly int _ghostHeight;
    private readonly (double Phase, double Speed, double Height)[] _motes;

    /// <param name="columns">Width of the overlay, in cells.</param>
    /// <param name="rows">Its height, in cells.</param>
    /// <param name="ghost">The ghost's sprite, straight BGRA, <paramref name="ghostWidth"/> by <paramref name="ghostHeight"/>.</param>
    public GhostScene(int columns, int rows, byte[] ghost, int ghostWidth, int ghostHeight, int seed)
    {
        Columns = Math.Max(1, columns);
        Rows = Math.Max(1, rows);
        Pixels = new byte[Columns * Rows * 4];
        _ghost = ghost;
        _ghostWidth = ghostWidth;
        _ghostHeight = ghostHeight;

        var random = new Random(seed);
        _motes = new (double, double, double)[40];
        for (var i = 0; i < _motes.Length; i++)
        {
            _motes[i] = (random.NextDouble(), 0.5 + random.NextDouble(), 4 + (random.NextDouble() * 14));
        }
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>The frame, premultiplied BGRA, one pixel per cell.</summary>
    public byte[] Pixels { get; }

    /// <summary>Where the ghost's centre is at <paramref name="t"/>, in cells: right to left on a wave, stopping halfway.</summary>
    public (double X, double Y) Centre(double t)
    {
        var from = Columns + _ghostWidth;
        var to = -_ghostWidth;
        var middle = Columns * 0.5;

        double x;
        if (t < GhostTimeline.Pause)
        {
            var p = Ease(t / GhostTimeline.Pause, 0.4);
            x = from + ((middle - from) * p);
        }
        else if (t < GhostTimeline.Pause + GhostTimeline.Look)
        {
            x = middle;
        }
        else
        {
            var p = (t - GhostTimeline.Pause - GhostTimeline.Look) / (GhostTimeline.Leave + 0.6 - GhostTimeline.Pause - GhostTimeline.Look);
            x = middle + ((to - middle) * Ease(Math.Clamp(p, 0, 1), 0.6));
        }

        var y = (Rows * 0.42) + (Math.Sin(t * 2.1) * 3) + (Math.Sin(t * 5.3) * 0.8);
        return (x, y);
    }

    /// <summary>Draws the moment <paramref name="t"/> seconds after the notice has gone.</summary>
    public void Render(double t)
    {
        Array.Clear(Pixels);
        if (t < 0 || t >= GhostTimeline.Length)
        {
            return;
        }

        var chill = t < GhostTimeline.Chill ? t / GhostTimeline.Chill
            : t > GhostTimeline.Length - 0.8 ? (GhostTimeline.Length - t) / 0.8 : 1;

        Cold(chill);
        Mist(t, chill);

        var (cx, cy) = Centre(t);
        var looking = t >= GhostTimeline.Pause && t < GhostTimeline.Pause + GhostTimeline.Look;
        var leaving = t >= GhostTimeline.Leave ? (t - GhostTimeline.Leave) / (GhostTimeline.Length - GhostTimeline.Leave) : 0;

        Motes(t, cx, cy, leaving);
        Ghost(t, cx, cy, looking, leaving);
        Wisps(t, cx, cy, looking, leaving);
        Sparks(t, cx, cy, leaving);
    }

    // ======================================================================================================== COLD

    /// <summary>The edges of the screen dim towards blue, in four bands of cells.</summary>
    private void Cold(double chill)
    {
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                var edge = Math.Min(Math.Min(x, Columns - 1 - x) / (Columns * 0.18), Math.Min(y, Rows - 1 - y) / (Rows * 0.22));
                if (edge >= 1) continue;
                var band = Math.Floor((1 - edge) * 4) / 4;
                Blend(x, y, 0x08, 0x10, 0x28, band * 0.42 * chill);
            }
        }
    }

    /// <summary>A pale mist along the bottom: three layers of blobs drifting left at different speeds.</summary>
    private void Mist(double t, double chill)
    {
        for (var layer = 0; layer < 3; layer++)
        {
            var speed = 3 + (layer * 2.5);
            var top = Rows - 4 - (layer * 3);
            for (var x = 0; x < Columns; x++)
            {
                var wave = Math.Sin(((x + (t * speed)) * 0.21) + (layer * 1.7)) + Math.Sin(((x + (t * speed * 0.6)) * 0.07) + layer);
                var height = 2 + (int)Math.Round((wave + 2) * 1.2);
                for (var dy = 0; dy < height; dy++)
                {
                    var y = top + (layer * 2) + dy - height + 3;
                    if (y < 0 || y >= Rows) continue;
                    Blend(x, y, 0xE0, 0xEC, 0xFF, (0.10 + (layer * 0.03)) * chill * (dy == 0 ? 0.6 : 1));
                }
            }
        }
    }

    // ======================================================================================================= GHOST

    /// <summary>
    /// The ghost itself: each row shifted by a wave so the outline wobbles, flickering, and its lower third fraying into
    /// wisps that trail behind it. Facing left as it drifts, it turns to the screen while it looks.
    /// </summary>
    private void Ghost(double t, double cx, double cy, bool looking, double leaving)
    {
        var left = (int)Math.Round(cx - (_ghostWidth / 2.0));
        var top = (int)Math.Round(cy - (_ghostHeight / 2.0));
        var flicker = 0.78 + (0.14 * Math.Sin(t * 13)) + (0.08 * Math.Sin(t * 29));
        var fade = (1 - leaving) * flicker;
        var fray = _ghostHeight * 0.62;

        for (var y = 0; y < _ghostHeight; y++)
        {
            var wobble = (int)Math.Round(Math.Sin((t * 6) + (y * 0.55)) * (looking ? 0.5 : 1.0));
            var lower = y > fray ? (y - fray) / (_ghostHeight - fray) : 0;

            for (var x = 0; x < _ghostWidth; x++)
            {
                // Mirando al jugador se da la vuelta: el sprite se dibuja al revés mientras dura.
                var sx = looking ? _ghostWidth - 1 - x : x;
                var at = ((y * _ghostWidth) + sx) * 4;
                var a = _ghost[at + 3] / 255.0;
                if (a <= 0) continue;

                // La mitad de abajo se deshace: menos cuerpo cuanto más abajo, y se queda atrás.
                if (lower > 0 && Hash(x, y, (int)(t * 12)) < lower * 0.8) continue;
                var drag = (int)Math.Round(lower * lower * 6);

                Blend(left + x + wobble + drag, top + y, _ghost[at], _ghost[at + 1], _ghost[at + 2], a * fade * (1 - (lower * 0.55)));
            }
        }

        // Los jirones: hilos que cuelgan del borde de abajo y ondean hacia atrás.
        for (var strand = 0; strand < 5; strand++)
        {
            var sx = left + (_ghostWidth * (0.2 + (strand * 0.15)));
            for (var k = 0; k < 7; k++)
            {
                var x = sx + (k * 1.1) + (Math.Sin((t * 7) + strand + (k * 0.7)) * 1.4);
                var y = top + _ghostHeight - 1 + k;
                Blend((int)Math.Round(x), y, 0xFF, 0xF2, 0xE4, 0.5 * fade * (1 - (k / 7.0)));
            }
        }

        // Mientras mira, dos ojos que brillan un momento.
        if (looking)
        {
            var glow = Math.Sin((t - GhostTimeline.Pause) / GhostTimeline.Look * Math.PI);
            var ey = top + (int)(_ghostHeight * 0.36);
            foreach (var ex in new[] { left + (int)(_ghostWidth * 0.36), left + (int)(_ghostWidth * 0.62) })
            {
                Blend(ex, ey, 0xFF, 0xFF, 0xB0, glow);
                Blend(ex + 1, ey, 0xFF, 0xFF, 0xB0, glow * 0.6);
                Blend(ex, ey - 1, 0xFF, 0xF0, 0x80, glow * 0.4);
            }
        }
    }

    /// <summary>Two blue flames circling the ghost; they flare while it looks.</summary>
    private void Wisps(double t, double cx, double cy, bool looking, double leaving)
    {
        var radius = (_ghostWidth * 0.75) + (looking ? 2 : 0);
        for (var i = 0; i < 2; i++)
        {
            var angle = (t * 2.6) + (i * Math.PI);
            var x = cx + (Math.Cos(angle) * radius);
            var y = cy + (Math.Sin(angle) * radius * 0.45);
            var size = looking ? 2 : 1;
            var alpha = (1 - leaving) * (Math.Sin(angle) > 0 ? 1 : 0.55);

            for (var dy = -size; dy <= size; dy++)
            {
                for (var dx = -size; dx <= size; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > size) continue;
                    var core = dx == 0 && dy == 0;
                    Blend((int)Math.Round(x) + dx, (int)Math.Round(y) + dy, core ? (byte)0xFF : (byte)0xFF, core ? (byte)0xF6 : (byte)0xC8,
                        core ? (byte)0xE0 : (byte)0x70, alpha * (core ? 1 : 0.7));
                }
            }

            // La llama tira hacia arriba: una cola de dos celdas.
            Blend((int)Math.Round(x), (int)Math.Round(y) - size - 1, 0xFF, 0xB0, 0x50, alpha * 0.6);
            Blend((int)Math.Round(x), (int)Math.Round(y) - size - 2, 0xFF, 0x90, 0x40, alpha * 0.3);
        }
    }

    /// <summary>Motes the ghost sheds behind it: they rise from where it passed and fade.</summary>
    private void Motes(double t, double cx, double cy, double leaving)
    {
        foreach (var (phase, speed, height) in _motes)
        {
            var life = ((t * 0.45 * speed) + phase) % 1;
            var born = t - (life * 2.2);
            if (born < 0) continue;

            var (bx, by) = Centre(born);
            var x = bx + (_ghostWidth * (phase - 0.5) * 0.8) + (Math.Sin((t * 3) + (phase * 20)) * 1.2);
            var y = by + (_ghostHeight * 0.3) - (life * height);
            var alpha = (1 - life) * 0.8 * (1 - (leaving * 0.5));
            Blend((int)Math.Round(x), (int)Math.Round(y), 0xFF, 0xEA, 0xC8, alpha);
        }
    }

    /// <summary>At the left edge it comes apart: sparks that fly up and back from where it was.</summary>
    private void Sparks(double t, double cx, double cy, double leaving)
    {
        if (leaving <= 0) return;

        for (var i = 0; i < 36; i++)
        {
            var angle = -Math.PI / 2 + ((Hash(i, 3, 7) - 0.5) * 2.2);
            var speed = 6 + (Hash(i, 9, 1) * 14);
            var x = cx + (Math.Cos(angle) * speed * leaving) + ((Hash(i, 5, 2) - 0.5) * _ghostWidth);
            var y = cy + (Math.Sin(angle) * speed * leaving) + ((Hash(i, 8, 4) - 0.5) * _ghostHeight * 0.6);
            Blend((int)Math.Round(x), (int)Math.Round(y), 0xFF, 0xF4, 0xE0, 1 - leaving);
        }
    }

    // ===================================================================================================== HELPERS

    /// <summary>Lays one colour (straight BGR) over a cell with an alpha, premultiplied.</summary>
    private void Blend(int x, int y, byte b, byte g, byte r, double alpha)
    {
        if (x < 0 || y < 0 || x >= Columns || y >= Rows) return;
        alpha = Math.Clamp(alpha, 0, 1);
        if (alpha <= 0.004) return;

        var at = ((y * Columns) + x) * 4;
        var keep = 1 - alpha;
        Pixels[at] = (byte)((b * alpha) + (Pixels[at] * keep));
        Pixels[at + 1] = (byte)((g * alpha) + (Pixels[at + 1] * keep));
        Pixels[at + 2] = (byte)((r * alpha) + (Pixels[at + 2] * keep));
        Pixels[at + 3] = (byte)((255 * alpha) + (Pixels[at + 3] * keep));
    }

    private static double Ease(double t, double softness)
    {
        t = Math.Clamp(t, 0, 1);
        var smooth = t * t * (3 - (2 * t));
        return (t * softness) + (smooth * (1 - softness));
    }

    /// <summary>A stable number in [0, 1) for a cell and a moment, so the fraying flickers instead of crawling.</summary>
    private static double Hash(int x, int y, int z)
    {
        unchecked
        {
            var h = (uint)((x * 73856093) ^ (y * 19349663) ^ (z * 83492791));
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFF) / 65536.0;
        }
    }
}
