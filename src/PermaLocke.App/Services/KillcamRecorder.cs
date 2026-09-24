using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace PermaLocke.App.Services;

/// <summary>
/// Keeps the last seconds of the battle on screen, so a death can be saved with what led to it.
/// </summary>
/// <remarks>
/// <para>
/// It only reads pixels — the 3DS top screen out of Azahar's window, scaled down to its native 400×240 —
/// and asks the emulator for nothing, which after two freezes caused by searching its memory (§114 ter)
/// is the point. It records only while a battle is in progress, at <see cref="FramesPerSecond"/>, and
/// holds <see cref="Kept"/> of it: outside a battle it costs nothing.
/// </para>
/// <para>
/// When a death is seen, <see cref="Mark"/> notes the moment the bar reached zero and
/// <see cref="SaveAsync"/> waits a little longer — the faint itself — before writing the clip.
/// </para>
/// <para>
/// The death ceremony is on screen by then, and it <b>does</b> get copied: this said once that the capture
/// left out layered windows, and the first real killcam (Exploud, +646 ms) showed the black bar coming
/// down, the grey and the pixel sprite. With the desktop composited, a copy of the screen is what is on
/// the screen. So the ceremony says when it covers the game (<see cref="CoverBegins"/>) and no frame is
/// kept while it does: the clip ends on the last frame of the game and not on PermaLocke's own drawing.
/// Hiding the ceremony from every capture would have been the other way, and it would also hide it from
/// anyone streaming the game.
/// </para>
/// </remarks>
public sealed class KillcamRecorder(ILogger<KillcamRecorder> logger) : IKillcamRecorder, IDisposable
{
    public const int Width = 400;
    public const int Height = 240;
    public const int FramesPerSecond = 20;

    /// <summary>How much is held: enough for the attack animation and the bar before a death.</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromSeconds(7);

    /// <summary>What a clip keeps before the bar reaches zero, and after.</summary>
    public static readonly TimeSpan Before = TimeSpan.FromSeconds(4.5);
    public static readonly TimeSpan After = TimeSpan.FromSeconds(1.3);

    private readonly object _gate = new();
    private readonly KillcamFrameBuffer _frames = new((int)(Kept.TotalSeconds * FramesPerSecond) + 1, Width * Height * 4);

    /// <summary>When a frame was not taken because something was in front of the emulator.</summary>
    private readonly LinkedList<double> _covered = new();

    /// <summary>How far back a cover reaches: longer than one copy of the screen takes.</summary>
    private const double CoverMargin = 80;

    /// <summary>PermaLocke's own ceremony is over the game. Only changed under the gate.</summary>
    private bool _coveredByUs;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly CancellationTokenSource _stopping = new();
    private Thread? _loop;
    private volatile bool _recording;
    private bool _disposed;

    /// <summary>Off in CONFIGURACIÓN: nothing is recorded, so a death has no replay.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether a battle is in progress. A new battle starts a new recording; the end of one keeps what
    /// was recorded, because a death is saved a moment after it happens and the battle may end first.
    /// </summary>
    public bool Recording
    {
        get => _recording;
        set
        {
            value &= Enabled;
            lock (_gate)
            {
                if (_disposed) return;
                if (value && !_recording)
                {
                    _frames.Clear();
                    _covered.Clear();
                }
                _recording = value;
                if (value && _loop is null)
                {
                    // GDI contexts stay on their owning thread.
                    _loop = new Thread(Run) { IsBackground = true, Name = "Killcam", Priority = ThreadPriority.BelowNormal };
                    _loop.Start();
                }
            }
        }
    }

    /// <summary>The moment to cut the clip around: now.</summary>
    public double Mark() => _clock.Elapsed.TotalMilliseconds;

    /// <summary>
    /// Something of PermaLocke's is about to cover the game: nothing from now on is the game.
    /// </summary>
    /// <remarks>
    /// Also drops the last <see cref="CoverMargin"/> of frames, because a copy that started just before the
    /// window appeared can finish after it — a frame is stamped when its copy starts.
    /// </remarks>
    public void CoverBegins()
    {
        var now = _clock.Elapsed.TotalMilliseconds;

        lock (_gate)
        {
            _coveredByUs = true;

            _frames.RemoveAfter(now - CoverMargin);
        }
    }

    /// <summary>The game is on screen again.</summary>
    public void CoverEnds()
    {
        lock (_gate)
        {
            _coveredByUs = false;
        }
    }

    /// <summary>
    /// Waits for the faint to be on the recording too, then writes the clip around <paramref name="mark"/>.
    /// </summary>
    /// <returns>The file written, or null when there was nothing to write.</returns>
    public async Task<string?> SaveAsync(string path, double mark, CancellationToken ct = default)
    {
        var wait = mark + After.TotalMilliseconds - _clock.Elapsed.TotalMilliseconds;

        if (wait > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(wait), ct);
        }

        List<(int Milliseconds, byte[] Bgra)> clip;
        int covered;
        bool Within(double at) => at >= mark - Before.TotalMilliseconds && at <= mark + After.TotalMilliseconds;

        lock (_gate)
        {
            clip = _frames.Snapshot(mark, Before.TotalMilliseconds, After.TotalMilliseconds);
            covered = _covered.Count(Within);
        }

        if (clip.Count < FramesPerSecond)
        {
            logger.LogWarning("Killcam sin grabar: solo había {Frames} fotogramas de ese momento, y en {Covered} Azahar estaba tapado o fuera de la pantalla (ahora: {Hiding})",
                clip.Count, covered, HidingNow());
            return null;
        }

        if (covered > 0)
        {
            logger.LogInformation("Killcam con huecos: {Covered} fotogramas descartados porque Azahar estaba tapado", covered);
        }

        await Task.Run(() => KillcamClip.Write(path, Width, Height, clip), ct);
        logger.LogInformation("Killcam guardada: {Frames} fotogramas en {Path}", clip.Count, path);
        return path;
    }

    private void Run()
    {
        try
        {
            CaptureLoop();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Grabación de killcam detenida");
        }
        finally
        {
            lock (_gate)
            {
                _frames.Clear(release: true);
                _covered.Clear();
                _recording = false;
                _loop = null;
                if (_disposed) _stopping.Dispose();
            }
        }
    }

    private void CaptureLoop()
    {
        using var grabber = new Grabber(Width, Height);
        var window = IntPtr.Zero;
        (double Left, double Top, double Scale)? screen = null;
        var located = double.NegativeInfinity;
        var period = 1000.0 / FramesPerSecond;

        while (!_stopping.IsCancellationRequested)
        {
            var started = _clock.Elapsed.TotalMilliseconds;

            try
            {
                if (_recording)
                {
                    // La ventana se localiza una vez por segundo, no en cada fotograma: cuesta de 8 a
                    // 11 ms medidos, que a 20 por segundo sería media captura.
                    if (started - located >= 1000)
                    {
                        window = GameWindow.Handle();
                        screen = GameWindow.TopScreen(window);
                        located = started;
                    }

                    // Lo que haya delante de Azahar se copiaría como si fuera el juego: si está tapado, ese
                    // fotograma no existe. Se pregunta en cada uno, porque tapar una ventana es un clic.
                    if (screen is { } where && !GameWindow.Shows(window, where.Left, where.Top, Width * where.Scale, Height * where.Scale))
                    {
                        lock (_gate)
                        {
                            _covered.AddLast(started);
                            Forget(_covered, started, at => at);
                        }
                    }
                    else if (screen is { } shown && grabber.Grab(shown.Left, shown.Top, shown.Scale) is { } pixels)
                    {
                        lock (_gate)
                        {
                            // Se mira después de copiar y bajo el cerrojo: si la escena ha salido mientras se
                            // copiaba, este fotograma ya puede llevarla.
                            if (!_coveredByUs)
                            {
                                _frames.Add(started, pixels);
                                _frames.ForgetBefore(started - Kept.TotalMilliseconds);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Un fotograma perdido cuesta un fotograma: la muerte se registra igual.
                logger.LogDebug(ex, "Fotograma de killcam perdido");
            }

            lock (_gate)
            {
                _frames.ForgetBefore(_clock.Elapsed.TotalMilliseconds - Kept.TotalMilliseconds);
                Forget(_covered, _clock.Elapsed.TotalMilliseconds, at => at);
                // Allow pending death clips to finish, then release idle memory.
                if (!_recording && _frames.Count == 0) _frames.Clear(release: true);
            }

            var rest = _recording ? period - (_clock.Elapsed.TotalMilliseconds - started) : 200;

            if (_stopping.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(Math.Max(1, rest))))
            {
                return;
            }
        }
    }

    /// <summary>What is in front of the emulator's top screen at this moment, for the log.</summary>
    private static string HidingNow()
    {
        var window = GameWindow.Handle();

        return GameWindow.TopScreen(window) is { } where
            ? GameWindow.Hiding(window, where.Left, where.Top, Width * where.Scale, Height * where.Scale) ?? "nada, se ve"
            : "no se encuentra la pantalla del emulador";
    }

    /// <summary>Drops what is older than <see cref="Kept"/>.</summary>
    private static void Forget<T>(LinkedList<T> list, double now, Func<T, double> at)
    {
        while (list.First is { } oldest && now - at(oldest.Value) > Kept.TotalMilliseconds)
        {
            list.RemoveFirst();
        }
    }

    public void Dispose()
    {
        Thread? loop;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _recording = false;
            _stopping.Cancel();
            loop = _loop;
            if (loop is null) _stopping.Dispose();
        }
        // The capturing thread owns its GDI handles and disposes the token after its last wait.
        // Do not freeze shutdown if a graphics driver is stuck in a capture.
        loop?.Join(TimeSpan.FromSeconds(2));
    }

    /// <summary>The top screen scaled down to its native size with GDI, into one reusable buffer.</summary>
    private sealed class Grabber : IDisposable
    {
        private readonly int _width, _height;
        private readonly byte[] _pixels;
        private readonly IntPtr _screen, _memory, _bitmap, _bits, _previous;

        public Grabber(int width, int height)
        {
            _pixels = new byte[width * height * 4];
            _width = width;
            _height = height;
            _screen = GetDC(IntPtr.Zero);
            _memory = CreateCompatibleDC(_screen);

            var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            _bitmap = CreateDIBSection(_screen, ref info, 0, out _bits, IntPtr.Zero, 0);
            _previous = SelectObject(_memory, _bitmap);

            // HALFTONE: reducir promediando y no saltándose píxeles, o el texto de la caja se deshace.
            SetStretchBltMode(_memory, 4);
        }

        public byte[]? Grab(double left, double top, double scale)
        {
            if (_bitmap == IntPtr.Zero
                || !StretchBlt(_memory, 0, 0, _width, _height, _screen, (int)Math.Round(left), (int)Math.Round(top),
                    (int)Math.Round(_width * scale), (int)Math.Round(_height * scale), SrcCopy))
            {
                return null;
            }

            var pixels = _pixels;
            Marshal.Copy(_bits, pixels, 0, pixels.Length);

            // GDI deja el alfa a cero; una imagen con alfa cero es transparente al dibujarla.
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }

            return pixels;
        }

        public void Dispose()
        {
            SelectObject(_memory, _previous);

            if (_bitmap != IntPtr.Zero)
            {
                DeleteObject(_bitmap);
            }

            DeleteDC(_memory);
            ReleaseDC(IntPtr.Zero, _screen);
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
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr context, int mode);
    [DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sx, int sy, int sw, int sh, uint operation);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr context);
}
