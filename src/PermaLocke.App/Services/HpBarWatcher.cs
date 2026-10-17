using System.Diagnostics;
using System.Runtime.InteropServices;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Battle;

namespace PermaLocke.App.Services;

/// <summary>
/// Waits, looking at the emulator's window, for the player's HP bar to reach zero.
/// </summary>
/// <remarks>
/// <para>
/// The battle tables say a Pokémon has fallen before the player sees it: the second one changes with the
/// «X used Y» message, ahead of the attack animation and the bar. The first real death showed the
/// ceremony freezing the frame with the bar at 5/12. This waits for the bar itself (§114 ter).
/// </para>
/// <para>
/// It only reads pixels, so it costs the emulator nothing. It assumes Azahar's default layout — top
/// screen above the bottom one, both centred — which is what the player uses; with any other layout, or
/// the window covered, the bar is never seen and the wait ends at <see cref="Limit"/>, so the ceremony is
/// late rather than missing. Covered is checked, not assumed: the screen copy would read whatever window is
/// in front as if it were the game (<see cref="GameWindow.Shows"/>). PermaLocke's own windows are copied too
/// — the first killcam proved it — but none is over the bar while this waits: the notices go in the bottom
/// corner and the ceremony only opens once the bar has been seen at zero.
/// </para>
/// </remarks>
public static class HpBarWatcher
{
    /// <summary>
    /// The longest wait. Long enough for an ordinary attack animation, which hides the box, and short
    /// enough that a bar that is never seen does not leave the death without its moment.
    /// </summary>
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);


    /// <summary>Where the bar is on screen: the picture's origin and scale, refreshed now and then.</summary>
    private readonly record struct Geometry(IntPtr Window, double OriginX, double OriginY, double Scale, int Left, int Top, int Width, int Height);

    /// <returns>What happened, in words for the log — with the readings, when the bar was not seen reaching zero.</returns>
    public static async Task<string> WaitUntilEmptyAsync(CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var watch = new HpBar.ZeroWatch();
        var trace = new List<string>();
        var lastShown = string.Empty;
        Geometry? geometry = null;
        var located = double.NegativeInfinity;
        var readings = 0;

        while (clock.Elapsed < Limit)
        {
            var ms = clock.Elapsed.TotalMilliseconds;

            // Localizar la ventana cuesta decenas de milisegundos -buscar el proceso y su superficie de
            // dibujo-, y hacerlo en cada lectura fue lo que dejó a la primera muerte con una sola lectura
            // de barra vacía. Se hace al empezar y cada medio segundo, por si se mueve la ventana.
            if (ms - located >= 500)
            {
                geometry = Locate();
                located = ms;
            }

            var reading = geometry is { } where ? Read(where) : new HpBarReading(HpBarState.Hidden, 0);
            readings++;

            var shown = reading.State == HpBarState.Filled ? $"color {reading.Fill:P0}" : reading.State == HpBarState.Empty ? "vacía" : "oculta";

            if (shown != lastShown && trace.Count < 40)
            {
                trace.Add($"{ms:F0} {shown}");
                lastShown = shown;
            }

            if (watch.Observe(reading, ms))
            {
                return $"barra a cero vista a los {ms:F0} ms";
            }

            // Sin caja en pantalla no hay nada que esperar: la caída ya se ha visto y el juego se la ha llevado.
            if (watch.GiveUpWithoutBox(ms))
            {
                return $"la caja de PS no ha aparecido en {ms:F0} ms: la caída ya estaba en pantalla "
                       + $"({readings} lecturas: {string.Join(" · ", trace)})";
            }

            // La barra estaba casi vacía y la caja se fue sin volver: el Pokémon ya ha caído, solo que el juego no enseñó el cero.
            if (watch.GiveUpHiddenAfterLow(ms))
            {
                return $"la caja de PS se escondió con la barra casi vacía y no ha vuelto en {HpBar.ZeroWatch.HiddenAfterLowLimit:F0} ms "
                       + $"({readings} lecturas: {string.Join(" · ", trace)})";
            }

            await Task.Delay(10, ct);
        }

        var what = watch.SawColour
            ? $"barra vista pero sin llegar a cero en {Limit.TotalSeconds:F0} s"
            : $"la caja de PS no se ha visto con color en {Limit.TotalSeconds:F0} s";

        return $"{what} ({readings} lecturas: {string.Join(" · ", trace)})";
    }

    /// <summary>The bar's state right now, or hidden when the window or the bar cannot be read.</summary>
    public static HpBarState ReadNow() => Locate() is { } where ? Read(where).State : HpBarState.Hidden;

    /// <summary>
    /// How the emulator lays the screens out (the panel sets it from the emulator's configuration): the bar is looked for where that layout puts it.
    /// Without it, the layout of the emulator's default is assumed, as it always was.
    /// </summary>
    public static Func<AzaharScreenLayout>? LayoutSource { get; set; }

    private static readonly object SampleGate = new();
    private static Geometry? _cached;
    private static long _cachedAt;

    /// <summary>
    /// One reading of the bar with how much colour it has, for the tracker that says when the game starts to show a change. The window is located
    /// again at most twice a second: locating it costs tens of milliseconds, a reading a few. Null when the bar cannot be looked for (a layout
    /// that is not computed, the emulator closed).
    /// </summary>
    public static HpBarReading? Sample()
    {
        Geometry? where;
        lock (SampleGate)
        {
            var now = Stopwatch.GetTimestamp();
            if (_cached is null || (now - _cachedAt) * 1000.0 / Stopwatch.Frequency >= 500)
            {
                _cached = Locate();
                _cachedAt = now;
            }

            where = _cached;
        }

        return where is { } found ? Read(found) : null;
    }

    private static Geometry? Locate()
    {
        var window = GameWindow.Handle();

        if (GameWindow.RenderBox(window) is not { } box)
        {
            return null;
        }

        // The picture is scaled to fit and centred; the top screen, the one with the player's box, is where the layout puts it (the default is 400×480,
        // the top screen at the top). A layout that is not computed has no bar to look for.
        if (HpBarGeometry.Of(LayoutSource?.Invoke() ?? AzaharScreenLayout.Default) is not { } place)
        {
            return null;
        }

        var scale = Math.Min(box.Width / (double)place.PictureWidth, box.Height / (double)place.PictureHeight);

        if (scale <= 0)
        {
            return null;
        }

        var originX = box.Left + ((box.Width - (place.PictureWidth * scale)) / 2) + (place.TopX * scale);
        var originY = box.Top + ((box.Height - (place.PictureHeight * scale)) / 2) + (place.TopY * scale);

        return new Geometry(window, originX, originY, scale,
            (int)Math.Floor(originX + (HpBar.Left * scale)),
            (int)Math.Floor(originY + (HpBar.FirstRow * scale)),
            (int)Math.Ceiling((HpBar.Right - HpBar.Left + 1) * scale) + 1,
            (int)Math.Ceiling((HpBar.LastRow - HpBar.FirstRow + 1) * scale) + 1);
    }

    private static HpBarReading Read(Geometry where)
    {
        // Con otra ventana delante se leería la barra de lo que sea que haya ahí: tapado cuenta como
        // oculta, que es lo que nunca dispara la escena.
        if (!GameWindow.Shows(where.Window, where.Left, where.Top, where.Width, where.Height)
            || Grab(where.Left, where.Top, where.Width, where.Height) is not { } pixels)
        {
            LastWhy = "tapada por otra ventana o sin poder capturarla";
            return new HpBarReading(HpBarState.Hidden, 0);
        }

        var rows = new List<HpBarReading>();
        var samples = new byte[HpBar.Samples * 4];

        for (var row = HpBar.FirstRow; row <= HpBar.LastRow; row++)
        {
            // El centro de cada píxel nativo, para no caer en la frontera entre dos.
            var y = Math.Clamp((int)((where.OriginY + ((row + 0.5) * where.Scale)) - where.Top), 0, where.Height - 1);

            for (var i = 0; i < HpBar.Samples; i++)
            {
                var x = Math.Clamp((int)((where.OriginX + ((HpBar.Left + i + 0.5) * where.Scale)) - where.Left), 0, where.Width - 1);
                Array.Copy(pixels, ((y * where.Width) + x) * 4, samples, i * 4, 4);
            }

            rows.Add(HpBar.Measure(samples));
        }

        var mid = ((Math.Clamp((int)((where.OriginY + ((HpBar.FirstRow + 1.5) * where.Scale)) - where.Top), 0, where.Height - 1) * where.Width) + (where.Width / 2)) * 4;
        LastWhy = $"en ({where.Left},{where.Top}) {where.Width}x{where.Height}, escala {where.Scale:0.00}, color en el centro de la barra {pixels[mid + 2]},{pixels[mid + 1]},{pixels[mid]}";
        return HpBar.Combine(rows);
    }

    /// <summary>Why the last look said what it said: where it looked and the colour it found, or that the box was covered. For the log.</summary>
    public static string LastWhy { get; private set; } = "";

    private static byte[]? Grab(int left, int top, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
        var bitmap = CreateDIBSection(screen, ref info, 0, out var bits, IntPtr.Zero, 0);

        try
        {
            if (bitmap == IntPtr.Zero)
            {
                return null;
            }

            var previous = SelectObject(memory, bitmap);
            var copied = BitBlt(memory, 0, 0, width, height, screen, left, top, SrcCopy);
            SelectObject(memory, previous);

            if (!copied)
            {
                return null;
            }

            var pixels = new byte[width * height * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                DeleteObject(bitmap);
            }

            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private const uint SrcCopy = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int Size, Width, Height;
        public short Planes, BitCount;
        public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr context);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr context);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr context, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr context, IntPtr item);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sx, int sy, uint operation);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr context);
}
