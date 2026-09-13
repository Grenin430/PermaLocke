using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Reads the two battle tables and films the emulator's top screen <b>on the same clock</b>, so the HP
/// bar on screen can be lined up with the numbers in memory.
/// </summary>
/// <remarks>
/// The first real death fired the ceremony with the bar still going down: a recording showed Leavanny
/// draining 12 → 11 → 9 → 6 → 4 → 2 and the scene freezing the frame at 5/12. The second table had been
/// taken for «the bar's», and it is not — it drops when the bar <b>starts</b> draining. What is missing
/// is how long the bar takes from there, and whether that depends on how much was lost; a screen
/// recording on another clock cannot say it to better than a second. This can.
/// </remarks>
public static class BarProbe
{
    private const int FrameWidth = 400;

    public static int Run(AzaharRpcClient client, int seconds)
    {
        var tables = new BattleTableReader(client, NullLogger<BattleTableReader>.Instance).Read(DateTimeOffset.Now);

        if (tables.Count < 2)
        {
            Console.WriteLine("No hay combate: entra en uno y quédate en el menú de ataques.");
            return 1;
        }

        if (TopScreen() is not { } area)
        {
            Console.WriteLine("No se encuentra la ventana de Azahar.");
            return 1;
        }

        var ids = tables[0].Blocks.Where(block => block.IsPlayers && block.CurrentHp > 0).Select(block => block.BattleId).ToArray();
        var height = FrameWidth * area.Height / area.Width;
        var samples = new List<Sample>();
        var clock = Stopwatch.StartNew();

        Console.WriteLine($"Esperando un golpe (hasta {seconds} s): posiciones {string.Join(",", ids)} "
                          + $"en 0x{tables[0].Origin:X8} y 0x{tables[1].Origin:X8}.");

        using var capture = new Capture(area, FrameWidth, height);

        // Hasta el primer cambio solo se guarda el último segundo de imágenes; desde él, seis más.
        double? hitAt = null;

        while (hitAt is null ? clock.Elapsed.TotalSeconds < seconds : clock.Elapsed.TotalMilliseconds < hitAt + 6000)
        {
            if (hitAt is null && samples.Count > 40)
            {
                samples.RemoveAt(0);
            }

            if (hitAt is null && samples.Count >= 2 && !SameHp(samples[^1].Hp, samples[^2].Hp))
            {
                hitAt = samples[^1].Ms;
                Console.WriteLine("Golpe visto: grabando 6 s más.");
            }

            var at = clock.Elapsed.TotalMilliseconds;
            var hp = new int[2, ids.Length];

            for (var t = 0; t < 2; t++)
            {
                for (var i = 0; i < ids.Length; i++)
                {
                    hp[t, i] = client.TryReadMemory(tables[t].Origin + (uint)(ids[i] * BattleLayout.Stride) + 0x30, 2, out var bytes)
                        ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
                        : -1;
                }
            }

            samples.Add(new Sample(at, hp, capture.Grab()));
            Thread.Sleep(5);
        }

        var folder = Path.Combine(Path.GetTempPath(), "permalocke-barra");
        Directory.CreateDirectory(folder);

        foreach (var old in Directory.GetFiles(folder))
        {
            File.Delete(old);
        }

        var changes = new List<int>();

        for (var s = 1; s < samples.Count; s++)
        {
            if (!SameHp(samples[s].Hp, samples[s - 1].Hp))
            {
                changes.Add(s);
            }
        }

        var csv = new StringBuilder("frame;ms;" + string.Join(";", ids.Select(id => $"T0_{id};T1_{id}")) + "\n");

        if (changes.Count == 0)
        {
            Console.WriteLine("No ha cambiado ningún PS en la grabación.");
            return 0;
        }

        var first = Math.Max(0, changes[0] - 12);
        var last = Math.Min(samples.Count - 1, changes[^1] + 80);
        var written = 0;

        for (var s = first; s <= last; s++)
        {
            var sample = samples[s];
            WriteBmp(Path.Combine(folder, $"f{written:D4}.bmp"), sample.Pixels, FrameWidth, height);
            csv.Append($"{written};{sample.Ms:F0};{string.Join(";", ids.Select((_, i) => $"{sample.Hp[0, i]};{sample.Hp[1, i]}"))}\n");
            written++;
        }

        File.WriteAllText(Path.Combine(folder, "tiempos.csv"), csv.ToString());

        Console.WriteLine($"{samples.Count} muestras en {clock.Elapsed.TotalSeconds:F1} s ({samples.Count / clock.Elapsed.TotalSeconds:F0} por segundo).");

        foreach (var s in changes)
        {
            var sample = samples[s];
            Console.WriteLine($"  {sample.Ms,7:F0} ms (fotograma {s - first,4})  "
                              + string.Join("  ", ids.Select((id, i) => $"pos{id}: {sample.Hp[0, i]}/{sample.Hp[1, i]}")));
        }

        Console.WriteLine($"{written} fotogramas en {folder}");
        return 0;
    }

    /// <summary>
    /// Only looks at the screen: the player's HP bar as the application will read it, every 100 ms.
    /// </summary>
    /// <remarks>
    /// Not one request to the emulator. It exists to check the default-layout geometry against the
    /// player's real window before trusting it with the ceremony: hits should show the colour shrinking,
    /// attack animations and message boxes «oculta», and nothing else.
    /// </remarks>
    public static int See(int seconds)
    {
        var handle = Process.GetProcessesByName("azahar").Select(p => p.MainWindowHandle).FirstOrDefault(h => h != IntPtr.Zero);

        if (handle == IntPtr.Zero || RenderBox(handle) is not { } render)
        {
            Console.WriteLine("No se encuentra la ventana de Azahar o su superficie de dibujo.");
            return 1;
        }

        var corner = new Spot { X = render.Left, Y = render.Top };
        var width = render.Width;
        var height = render.Height;
        var scale = Math.Min(width / 400.0, height / 480.0);
        var originX = corner.X + ((width - (400 * scale)) / 2);
        var originY = corner.Y + ((height - (480 * scale)) / 2);

        var left = (int)Math.Floor(originX + (HpBar.Left * scale));
        var top = (int)Math.Floor(originY + (HpBar.FirstRow * scale));
        var w = (int)Math.Ceiling((HpBar.Right - HpBar.Left + 1) * scale) + 1;
        var h = (int)Math.Ceiling((HpBar.LastRow - HpBar.FirstRow + 1) * scale) + 1;

        Console.WriteLine($"Ventana {width}x{height} en ({corner.X},{corner.Y}), escala {scale:F3}: barra en ({left},{top}) {w}x{h}.");

        using var capture = new Capture((left, top, w, h), w, h);

        // La pantalla de arriba entera, para ver qué había cada vez que cambia la lectura.
        var topScreen = ((int)originX, (int)originY, (int)(400 * scale), (int)(240 * scale));
        using var whole = new Capture(topScreen, 400, 240);
        var folder = Path.Combine(Path.GetTempPath(), "permalocke-verbarra");
        Directory.CreateDirectory(folder);

        foreach (var old in Directory.GetFiles(folder))
        {
            File.Delete(old);
        }

        var clock = Stopwatch.StartNew();
        var last = string.Empty;
        var shots = 0;

        // Las dos barras con la misma regla que la aplicación: la tuya y la del rival, que baja igual y
        // se puede ver llegar a cero sin que se muera nadie del jugador.
        var playerWatch = new HpBar.ZeroWatch();
        var rivalWatch = new HpBar.ZeroWatch();
        var lastRival = string.Empty;

        while (clock.Elapsed.TotalSeconds < seconds)
        {
            var frame = whole.Grab();
            var (player, playerCells, playerReading) = Bar(frame, HpBar.Left, HpBar.Right, HpBar.FirstRow, HpBar.LastRow);
            var (rival, rivalCells, rivalReading) = Bar(frame, 287, 370, 21, 23);

            var state = Describe(player, playerCells, HpBar.Right - HpBar.Left + 1);
            var rivalState = Describe(rival, rivalCells, 370 - 287 + 1);
            var now = clock.ElapsedMilliseconds;

            if (playerWatch.Observe(playerReading, now))
            {
                Console.WriteLine($"{now,7} ms  *** TU BARRA HA LLEGADO A CERO ***");
            }

            if (rivalWatch.Observe(rivalReading, now))
            {
                Console.WriteLine($"{now,7} ms  *** LA BARRA DEL RIVAL HA LLEGADO A CERO ***");
                WriteBmp(Path.Combine(folder, $"c{shots++:D3}-{now}ms-rival-cero.bmp"), frame, 400, 240);
            }

            if (state != last || rivalState != lastRival)
            {
                Console.WriteLine($"{now,7} ms  tú: {state,-20} rival: {rivalState,-20} (captura {shots})");
                WriteBmp(Path.Combine(folder, $"c{shots++:D3}-{now}ms.bmp"), frame, 400, 240);
                last = state;
                lastRival = rivalState;
            }

            Thread.Sleep(15);
        }

        return 0;
    }

    /// <summary>One bar read from a 400×240 frame of the top screen, and how many cells had colour.</summary>
    private static (HpBarState State, int Cells, HpBarReading Reading) Bar(byte[] frame, int from, int to, int firstRow, int lastRow)
    {
        var rows = new List<HpBarReading>();
        var samples = new byte[(to - from + 1) * 4];
        var cells = 0;

        for (var row = firstRow; row <= lastRow; row++)
        {
            Array.Copy(frame, ((row * 400) + from) * 4, samples, 0, samples.Length);
            rows.Add(HpBar.Measure(samples));

            if (row == (firstRow + lastRow) / 2)
            {
                for (var i = 0; i < samples.Length / 4; i++)
                {
                    var high = Math.Max(samples[(i * 4) + 2], samples[(i * 4) + 1]);
                    cells += high > 150 && samples[i * 4] < 110 && high - samples[i * 4] > 80 ? 1 : 0;
                }
            }
        }

        var combined = HpBar.Combine(rows);
        return (combined.State, cells, combined);
    }

    private static string Describe(HpBarState state, int cells, int of) => state switch
    {
        HpBarState.Hidden => "oculta",
        HpBarState.Empty => "VACÍA",
        _ => $"color {cells}/{of}"
    };

    /// <summary>The child window Qt draws the game in: its box is the picture, menu bar excluded.</summary>
    private static (int Left, int Top, int Width, int Height)? RenderBox(IntPtr window)
    {
        (int, int, int, int)? found = null;
        var name = new StringBuilder(128);

        EnumChildWindows(window, (child, _) =>
        {
            name.Clear();

            if (IsWindowVisible(child) && GetClassName(child, name, name.Capacity) > 0
                && name.ToString().Contains("OwnDC", StringComparison.Ordinal)
                && GetWindowRect(child, out var box))
            {
                found = (box.Left, box.Top, box.Right - box.Left, box.Bottom - box.Top);
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private delegate bool EnumChildProc(IntPtr child, IntPtr parameter);

    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Box box);

    private static bool SameHp(int[,] a, int[,] b)
    {
        for (var t = 0; t < a.GetLength(0); t++)
        {
            for (var i = 0; i < a.GetLength(1); i++)
            {
                if (a[t, i] != b[t, i])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private sealed record Sample(double Ms, int[,] Hp, byte[] Pixels);

    /// <summary>The top half of Azahar's client area in screen pixels: where the battle's HP boxes are.</summary>
    private static (int Left, int Top, int Width, int Height)? TopScreen()
    {
        var handle = Process.GetProcessesByName("azahar").Select(p => p.MainWindowHandle).FirstOrDefault(h => h != IntPtr.Zero);

        if (handle == IntPtr.Zero || !GetClientRect(handle, out var inside))
        {
            return null;
        }

        var corner = new Spot();

        return ClientToScreen(handle, ref corner) ? (corner.X, corner.Y, inside.Right, inside.Bottom / 2) : null;
    }

    private static void WriteBmp(string path, byte[] bgra, int width, int height)
    {
        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);

        var size = 54 + bgra.Length;
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(size);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(-height);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0);
        writer.Write(bgra.Length);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        writer.Write(bgra);
    }

    /// <summary>Copies a screen area scaled down into a 32-bit buffer with GDI.</summary>
    private sealed class Capture : IDisposable
    {
        private readonly (int Left, int Top, int Width, int Height) _area;
        private readonly int _width, _height;
        private readonly IntPtr _screen, _memory, _bitmap, _bits, _previous;

        public Capture((int Left, int Top, int Width, int Height) area, int width, int height)
        {
            _area = area;
            _width = width;
            _height = height;
            _screen = GetDC(IntPtr.Zero);
            _memory = CreateCompatibleDC(_screen);

            var info = new BitmapInfo
            {
                Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32, Compression = 0
            };

            _bitmap = CreateDIBSection(_screen, ref info, 0, out _bits, IntPtr.Zero, 0);
            _previous = SelectObject(_memory, _bitmap);
            SetStretchBltMode(_memory, 4);
        }

        public byte[] Grab()
        {
            StretchBlt(_memory, 0, 0, _width, _height, _screen, _area.Left, _area.Top, _area.Width, _area.Height, 0x00CC0020);

            var pixels = new byte[_width * _height * 4];
            Marshal.Copy(_bits, pixels, 0, pixels.Length);
            return pixels;
        }

        public void Dispose()
        {
            SelectObject(_memory, _previous);
            DeleteObject(_bitmap);
            DeleteDC(_memory);
            ReleaseDC(IntPtr.Zero, _screen);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Spot
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Box
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int Size, Width, Height;
        public short Planes, BitCount;
        public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant;
    }

    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Box box);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Spot spot);
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
