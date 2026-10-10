using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.PlaqueLab;

/// <summary>
/// PROPOSAL tool (branch cap-propuestas): measures the link between PermaLocke and an emulator that is NOT the real one. A second process
/// (<c>--servidor-falso</c>) speaks Azahar's RPC over UDP on loopback with the memory layout of a battle, and PermaLocke's own client and
/// battle reader run against it. What this measures is PermaLocke's side: socket, client, parsing, timers, threads, CPU. What it cannot
/// measure, and says so in its output: when the game writes the HP, how fast Azahar's RPC thread answers while it emulates, the bar of the game,
/// and the presentation of a layered WPF window.
///   dotnet run --project tools/PermaLocke.PlaqueLab -c Release -- --latencia [segundos-por-prueba]
/// </summary>
public static class Latency
{
    private const int Port = 46999;
    private const uint Linear = 0x30000000;
    private const uint TableB = 0x30009730, TableA = 0x30002748;
    private const int Stride = BattleLayout.Stride;

    // ---------------------------------------------------------------- the fake emulator (second process)

    public static int Serve(int port)
    {
        var memory = new byte[0x20000];
        foreach (var origin in new[] { TableA, TableB })
        {
            foreach (var id in new[] { 0, 1, 2, 3, 4, 5, 12 })
            {
                var header = (int)(origin - Linear) + (id * Stride) - BattleLayout.HeaderSize;
                BattleLayout.SearchPattern.CopyTo(memory.AsSpan(header));
                var data = header + BattleLayout.HeaderSize;
                BinaryPrimitives.WriteUInt32LittleEndian(memory.AsSpan(data + 0x20), 0x08000000u + (uint)id);
                BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan(data + 0x2C), (ushort)(100 + id));
                BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan(data + 0x2E), 200);
                BinaryPrimitives.WriteUInt16LittleEndian(memory.AsSpan(data + 0x30), 200);
                memory[data + 0x39] = (byte)id;
            }
        }

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
        var buffer = new byte[2048];
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            var n = socket.ReceiveFrom(buffer, ref from);
            if (n < 16) continue;
            var id = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4));
            var type = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(8));
            var address = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(16));
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(20));
            if (type == 255) return 0;

            byte[] payload = [];
            var offset = (long)address - Linear;
            if (type == 1 && offset >= 0 && offset + size <= memory.Length) payload = memory.AsSpan((int)offset, size).ToArray();
            else if (type == 2 && offset >= 0 && offset + size <= memory.Length) buffer.AsSpan(24, size).CopyTo(memory.AsSpan((int)offset));

            var reply = new byte[16 + payload.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(reply, 1);
            BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(4), id);
            BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(8), type);
            BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(12), (uint)payload.Length);
            payload.CopyTo(reply.AsSpan(16));
            socket.SendTo(reply, from);
        }
    }

    // ---------------------------------------------------------------- statistics

    private sealed record Stat(string Name, double Mean, double P50, double P95, double P99, double Max, int N)
    {
        public override string ToString() => $"| {Name,-58} | {Mean,9:F3} | {P50,9:F3} | {P95,9:F3} | {P99,9:F3} | {Max,9:F3} | {N,6} |";
    }

    private static Stat Of(string name, IList<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        double At(double q) => sorted[Math.Min(sorted.Length - 1, (int)(q * sorted.Length))];
        return new Stat(name, sorted.Average(), At(0.5), At(0.95), At(0.99), sorted[^1], sorted.Length);
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static void Header(string title, string unit)
    {
        Console.WriteLine();
        Console.WriteLine($"### {title}  ({unit})");
        Console.WriteLine("| prueba                                                     |      media |   mediana |       p95 |       p99 |    máximo |      n |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
    }

    // ---------------------------------------------------------------- the pipelined client (proposal)

    /// <summary>Sends every request first and then collects the replies: one trip of waiting instead of one per read.</summary>
    private sealed class PipeClient : IDisposable
    {
        private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        private readonly EndPoint _server = new IPEndPoint(IPAddress.Loopback, Port);
        private uint _next = 1000;

        public PipeClient() => _socket.ReceiveTimeout = 500;

        public byte[][] ReadMany((uint Address, int Size)[] ranges)
        {
            var first = _next;
            foreach (var (address, size) in ranges)
            {
                var request = new byte[24];
                BinaryPrimitives.WriteUInt32LittleEndian(request, 1);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), _next++);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(8), 1);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(12), 8);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(16), address);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(20), (uint)size);
                _socket.SendTo(request, _server);
            }

            var result = new byte[ranges.Length][];
            var got = 0;
            var buffer = new byte[2048];
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            while (got < ranges.Length)
            {
                var n = _socket.ReceiveFrom(buffer, ref from);
                var index = (int)(BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(4)) - first);
                if (index < 0 || index >= ranges.Length || result[index] is not null) continue;
                result[index] = buffer.AsSpan(16, n - 16).ToArray();
                got++;
            }

            return result;
        }

        public void Dispose() => _socket.Dispose();
    }

    // ---------------------------------------------------------------- timers

    [DllImport("kernel32.dll")] private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string? name, uint flags, uint access);
    [DllImport("kernel32.dll")] private static extern bool SetWaitableTimer(IntPtr timer, ref long due, int period, IntPtr routine, IntPtr arg, bool resume);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("ntdll.dll")] private static extern int NtQueryTimerResolution(out uint min, out uint max, out uint current);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr context);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr context);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr context, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr context, IntPtr item);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr target, int x, int y, int w, int h, IntPtr source, int sx, int sy, uint op);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr context);

    /// <summary>A high resolution waitable timer (Windows 10 1803 and later): sleeps close to what it is asked, without the 15.6 ms tick.</summary>
    private sealed class HighResolutionTimer
    {
        private readonly IntPtr _handle = CreateWaitableTimerExW(IntPtr.Zero, null, 0x2 /* HIGH_RESOLUTION */, 0x1F0003);

        public void Sleep(double milliseconds)
        {
            var due = -(long)(milliseconds * 10_000);
            SetWaitableTimer(_handle, ref due, 0, IntPtr.Zero, IntPtr.Zero, false);
            WaitForSingleObject(_handle, 1000);
        }
    }

    private static void TimerTests(List<Stat> sink)
    {
        NtQueryTimerResolution(out var min, out var max, out var current);
        Console.WriteLine($"Resolución del reloj del sistema en este proceso: actual {current / 10000.0:F2} ms (mejor posible {max / 10000.0:F2} ms, peor {min / 10000.0:F2} ms).");

        List<double> Collect(int count, Action wait)
        {
            var values = new List<double>(count);
            for (var i = 0; i < count; i++)
            {
                var start = Stopwatch.GetTimestamp();
                wait();
                values.Add(Ms(Stopwatch.GetTimestamp() - start));
            }

            return values;
        }

        Header("Cuánto duerme de verdad cada forma de esperar (se pide 1 ms, salvo donde se dice)", "ms");
        var hi = new HighResolutionTimer();
        var rows = new List<Stat>
        {
            Of("Task.Delay(1)", Collect(300, () => Task.Delay(1).Wait())),
            Of("Task.Delay(10)  (lo que usa HpBarWatcher)", Collect(300, () => Task.Delay(10).Wait())),
            Of("Thread.Sleep(1)", Collect(300, () => Thread.Sleep(1))),
            Of("temporizador de alta resolución, 1 ms", Collect(300, () => hi.Sleep(1))),
            Of("temporizador de alta resolución, 4 ms", Collect(300, () => hi.Sleep(4))),
            Of("espera activa (SpinWait) de 1 ms", Collect(300, () =>
            {
                var end = Stopwatch.GetTimestamp() + (Stopwatch.Frequency / 1000);
                while (Stopwatch.GetTimestamp() < end) Thread.SpinWait(20);
            }))
        };

        // The ticks of a PeriodicTimer and of a WPF DispatcherTimer, as intervals between ticks.
        var periodic = new List<double>();
        using (var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(4)))
        {
            var last = Stopwatch.GetTimestamp();
            for (var i = 0; i < 300; i++)
            {
                timer.WaitForNextTickAsync().AsTask().Wait();
                var now = Stopwatch.GetTimestamp();
                periodic.Add(Ms(now - last));
                last = now;
            }
        }

        rows.Add(Of("PeriodicTimer(4 ms): intervalo entre ticks", periodic));
        foreach (var period in new[] { 40, 83, 500 })
        {
            var ticks = new List<double>();
            var thread = new Thread(() =>
            {
                var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                var last = Stopwatch.GetTimestamp();
                var count = 0;
                var t = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Normal, dispatcher) { Interval = TimeSpan.FromMilliseconds(period) };
                t.Tick += (_, _) =>
                {
                    var now = Stopwatch.GetTimestamp();
                    ticks.Add(Ms(now - last));
                    last = now;
                    if (++count >= (period >= 500 ? 12 : 60)) dispatcher.InvokeShutdown();
                };
                t.Start();
                System.Windows.Threading.Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            rows.Add(Of($"DispatcherTimer({period} ms): intervalo entre ticks", ticks));
        }

        foreach (var row in rows)
        {
            Console.WriteLine(row);
            sink.Add(row);
        }
    }

    // ---------------------------------------------------------------- the round trips

    private static void RoundTrips(AzaharRpcClient client, List<Stat> sink)
    {
        Header("Una petición RPC de ida y vuelta, con el cliente real de PermaLocke (servidor falso, mejor caso)", "ms");
        foreach (var size in new[] { 4, 14, 28, 74, 232, 1024 })
        {
            var values = new List<double>(3000);
            for (var i = 0; i < 3000; i++)
            {
                var start = Stopwatch.GetTimestamp();
                client.ReadMemory(TableB + 0x30, size);
                values.Add(Ms(Stopwatch.GetTimestamp() - start));
            }

            var row = Of($"ReadMemory de {size} bytes", values);
            Console.WriteLine(row);
            sink.Add(row);
        }
    }

    /// <summary>What it costs to ask for what the app asks for today and for what is proposed: the number of requests is counted from the code.</summary>
    private static void Sequences(AzaharRpcClient client, List<Stat> sink)
    {
        Header("Una vuelta completa de lectura (todas las peticiones de una pasada, en serie)", "ms");
        var rows = new List<Stat>();

        // Today, the party every second: the chain of GameData (3 pointers, the party head, six member pointers) and one structure to name the trainer,
        // then the party itself: two reads (232 bytes and 28) for each of six members. 3 + 1 + 6 + 2 + 12 = 24 requests.
        List<double> Run(int reps, Action pass)
        {
            var values = new List<double>(reps);
            for (var i = 0; i < reps; i++)
            {
                var start = Stopwatch.GetTimestamp();
                pass();
                values.Add(Ms(Stopwatch.GetTimestamp() - start));
            }

            return values;
        }

        void Read(uint address, int size) => client.ReadMemory(address, size);

        rows.Add(Of("HOY equipo, cada 1 s: 24 peticiones (cadena + 6×(232+28))", Run(500, () =>
        {
            for (var i = 0; i < 3; i++) Read(TableB, 4);
            Read(TableB, 0x1C);
            for (var i = 0; i < 6; i++) Read(TableB, 4);
            Read(TableB, 232);
            Read(TableB, 28);
            for (var i = 0; i < 6; i++)
            {
                Read(TableB, 232);
                Read(TableB, 28);
            }
        })));

        var reader = new BattleTableReader(client, Microsoft.Extensions.Logging.Abstractions.NullLogger<BattleTableReader>.Instance);
        reader.Read(DateTimeOffset.UtcNow);
        var found = reader.Read(DateTimeOffset.UtcNow);
        rows.Add(Of($"HOY combate, cada 250 ms: BattleTableReader.Read real ({found.Sum(t => t.Blocks.Count)} bloques de 74 B)", Run(500, () => reader.Read(DateTimeOffset.UtcNow))));

        var blocks = Enumerable.Range(0, 6).Select(id => TableB + (uint)(id * Stride) + 0x2C).ToArray();
        rows.Add(Of("PROPUESTA combate, 6 lecturas de 14 B (especie, PS máx., PS, id), en serie", Run(2000, () =>
        {
            foreach (var address in blocks) Read(address, 14);
        })));

        using (var pipe = new PipeClient())
        {
            var ranges = blocks.Select(a => (a, 14)).ToArray();
            rows.Add(Of("PROPUESTA combate, las mismas 6 enviadas de golpe y recogidas después", Run(2000, () => pipe.ReadMany(ranges))));
            var all = Enumerable.Range(0, 12).Select(id => (TableB + (uint)((id % 6) * Stride) + 0x2C, 14)).ToArray();
            rows.Add(Of("PROPUESTA combate, 12 de golpe (las dos tablas)", Run(2000, () => pipe.ReadMany(all))));
        }

        rows.Add(Of("ALTERNATIVA: un solo bloque contiguo de 3,3 KB (4 trozos de ≤1024)", Run(1000, () => Read(TableB, 3300))));

        foreach (var row in rows)
        {
            Console.WriteLine(row);
            sink.Add(row);
        }
    }

    // ---------------------------------------------------------------- end to end

    private sealed record Strategy(string Name, Func<Action<long>, CancellationToken, Task> Loop);

    private static void WriteHp(AzaharRpcClient writer, int id, int hp)
    {
        var bytes = BitConverter.GetBytes((ushort)hp);
        foreach (var origin in new[] { TableB, TableA }) writer.WriteMemory(origin + (uint)(id * Stride) + 0x30, bytes);
    }

    /// <summary>
    /// How long after the memory changed the reader knows. The writer changes the HP at random moments (the server answers when the write is done,
    /// and that instant is taken as the moment of the change) and each strategy reports the instant it saw the new value.
    /// </summary>
    private static Stat DetectionLag(string name, int events, Func<AzaharRpcClient, ManualResetEventSlim, long[], CancellationToken, Thread> start)
    {
        using var writer = new AzaharRpcClient("127.0.0.1", Port, 500);
        using var reader = new AzaharRpcClient("127.0.0.1", Port, 500);
        var random = new Random(7);
        var lags = new List<double>(events);
        var seen = new long[1];
        using var done = new ManualResetEventSlim(false);
        using var stop = new CancellationTokenSource();
        var thread = start(reader, done, seen, stop.Token);
        thread.Start();
        Thread.Sleep(400);

        var hp = 200;
        for (var i = 0; i < events; i++)
        {
            Thread.Sleep(150 + random.Next(0, 300));
            hp = hp == 200 ? 120 : 200;
            done.Reset();
            seen[0] = 0;
            WriteHp(writer, 0, hp);
            var changed = Stopwatch.GetTimestamp();
            if (!done.Wait(2000)) continue;
            lags.Add(Ms(Interlocked.Read(ref seen[0]) - changed));
        }

        stop.Cancel();
        thread.Join();
        return Of(name, lags);
    }

    private static Thread Poller(Action<AzaharRpcClient, ManualResetEventSlim, long[], CancellationToken> body, AzaharRpcClient client, ManualResetEventSlim done, long[] seen, CancellationToken token, bool highPriority)
    {
        return new Thread(() => body(client, done, seen, token)) { IsBackground = true, Priority = highPriority ? ThreadPriority.AboveNormal : ThreadPriority.Normal };
    }

    private static int HpOf(AzaharRpcClient client, int id) => BitConverter.ToUInt16(client.ReadMemory(TableB + (uint)(id * Stride) + 0x30, 2));

    private static void EndToEnd(List<Stat> sink, int eventsPerTest)
    {
        Header("Retraso de LECTURA: desde que el dato cambia en memoria hasta que PermaLocke lo tiene (marca tomada al recibir la respuesta)", "ms");
        var rows = new List<Stat>();

        rows.Add(DetectionLag("HOY, combate: BattleTableReader.Read real cada 250 ms (Task.Delay)", eventsPerTest, (client, done, seen, token) =>
            Poller((c, d, s, t) =>
            {
                var reader = new BattleTableReader(c, Microsoft.Extensions.Logging.Abstractions.NullLogger<BattleTableReader>.Instance);
                var last = 200;
                while (!t.IsCancellationRequested)
                {
                    var tables = reader.Read(DateTimeOffset.UtcNow);
                    if (tables.Count > 0 && tables[0].Block(0) is { } block && block.CurrentHp != last)
                    {
                        last = block.CurrentHp;
                        Interlocked.Exchange(ref s[0], Stopwatch.GetTimestamp());
                        d.Set();
                    }

                    try { Task.Delay(250, t).Wait(t); } catch (OperationCanceledException) { }
                }
            }, client, done, seen, token, false)));

        foreach (var (label, period, pipelined) in new[] { ("PROPUESTA 60 Hz", 16.0, false), ("PROPUESTA 120 Hz", 8.0, false), ("PROPUESTA 240 Hz", 4.0, false), ("PROPUESTA 240 Hz, lecturas de golpe", 4.0, true) })
        {
            rows.Add(DetectionLag($"{label}: hilo propio, temporizador de alta resolución, 6×14 B, confirma con 2.ª lectura", eventsPerTest, (client, done, seen, token) =>
                Poller((c, d, s, t) =>
                {
                    var timer = new HighResolutionTimer();
                    using var pipe = pipelined ? new PipeClient() : null;
                    var ranges = Enumerable.Range(0, 6).Select(id => (TableB + (uint)(id * Stride) + 0x2C, 14)).ToArray();
                    var last = 200;
                    ushort Hp(byte[] block) => BitConverter.ToUInt16(block, 4);
                    byte[][] Read() => pipe is not null ? pipe.ReadMany(ranges) : [.. ranges.Select(r => c.ReadMemory(r.Item1, r.Item2))];
                    while (!t.IsCancellationRequested)
                    {
                        var first = Read();
                        if (Hp(first[0]) != last)
                        {
                            // A value counts when two readings in a row agree: a torn read costs one more round trip, not a false event.
                            var again = Read();
                            if (Hp(again[0]) == Hp(first[0]))
                            {
                                last = Hp(again[0]);
                                Interlocked.Exchange(ref s[0], Stopwatch.GetTimestamp());
                                d.Set();
                            }
                        }

                        timer.Sleep(period);
                    }
                }, client, done, seen, token, true)));
        }

        foreach (var row in rows)
        {
            Console.WriteLine(row);
            sink.Add(row);
        }
    }

    // ---------------------------------------------------------------- cost

    private static void Cost(double seconds)
    {
        Console.WriteLine();
        Console.WriteLine($"### Coste de CPU de PermaLocke leyendo en combate ({seconds:F0} s por estrategia; el servidor falso es otro proceso y no cuenta aquí)");
        Console.WriteLine("| estrategia | CPU de este proceso (% de un núcleo) | peticiones por segundo |");
        Console.WriteLine("|---|---:|---:|");

        void Measure(string name, Action<AzaharRpcClient, int[], CancellationToken> body, bool highRes)
        {
            using var client = new AzaharRpcClient("127.0.0.1", Port, 500);
            var counter = new int[1];
            using var cts = new CancellationTokenSource();
            var thread = new Thread(() => body(client, counter, cts.Token)) { IsBackground = true };
            thread.Start();
            Thread.Sleep(500);
            var process = Process.GetCurrentProcess();
            var cpu0 = process.TotalProcessorTime;
            var n0 = counter[0];
            var t0 = Stopwatch.GetTimestamp();
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            process.Refresh();
            var cpu = (process.TotalProcessorTime - cpu0).TotalMilliseconds;
            var wall = Ms(Stopwatch.GetTimestamp() - t0);
            var requests = (counter[0] - n0) / (wall / 1000.0);
            cts.Cancel();
            thread.Join();
            _ = highRes;
            Console.WriteLine($"| {name} | {100 * cpu / wall:F2} % | {requests:F0} |");
        }

        var blocks = Enumerable.Range(0, 6).Select(id => TableB + (uint)(id * Stride) + 0x2C).ToArray();
        var reader = new BattleTableReader(new AzaharRpcClient("127.0.0.1", Port, 500), Microsoft.Extensions.Logging.Abstractions.NullLogger<BattleTableReader>.Instance);
        reader.Read(DateTimeOffset.UtcNow);
        Measure("HOY: bucle de combate 250 ms (14 lecturas de 74 B)", (c, n, t) =>
        {
            var r = new BattleTableReader(c, Microsoft.Extensions.Logging.Abstractions.NullLogger<BattleTableReader>.Instance);
            r.Read(DateTimeOffset.UtcNow);
            while (!t.IsCancellationRequested)
            {
                r.Read(DateTimeOffset.UtcNow);
                n[0] += 14;
                try { Task.Delay(250, t).Wait(t); } catch (OperationCanceledException) { }
            }
        }, false);

        foreach (var hz in new[] { 60, 120, 240 })
        {
            Measure($"PROPUESTA {hz} Hz: 6×14 B en serie", (c, n, t) =>
            {
                var timer = new HighResolutionTimer();
                while (!t.IsCancellationRequested)
                {
                    foreach (var address in blocks) c.ReadMemory(address, 14);
                    n[0] += 6;
                    timer.Sleep(1000.0 / hz);
                }
            }, true);
        }

        Measure("PROPUESTA 240 Hz: 6×14 B de golpe", (c, n, t) =>
        {
            var timer = new HighResolutionTimer();
            using var pipe = new PipeClient();
            var ranges = blocks.Select(a => (a, 14)).ToArray();
            while (!t.IsCancellationRequested)
            {
                pipe.ReadMany(ranges);
                n[0] += 6;
                timer.Sleep(1000.0 / 240);
            }
        }, true);
    }

    // ---------------------------------------------------------------- the screen proxy

    private static void ScreenProxy(List<Stat> sink)
    {
        // The cost of looking at a 90×5 strip of the screen, as HpBarWatcher does: a proxy on the desktop, because the emulator's window is not here.
        var values = new List<double>(2000);
        for (var i = 0; i < 2000; i++)
        {
            var start = Stopwatch.GetTimestamp();
            var screen = GetDC(IntPtr.Zero);
            var memory = CreateCompatibleDC(screen);
            var bitmap = CreateCompatibleBitmap(screen, 90, 5);
            var previous = SelectObject(memory, bitmap);
            BitBlt(memory, 0, 0, 90, 5, screen, 10, 10, 0x00CC0020);
            SelectObject(memory, previous);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
            values.Add(Ms(Stopwatch.GetTimestamp() - start));
        }

        Header("Mirar la barra de PS en pantalla (proxy: una franja de 90×5 del escritorio, sin ventana de Azahar)", "ms");
        var row = Of("GetDC + BitBlt 90×5 + liberar (sin localizar la ventana ni comprobar que no está tapada)", values);
        Console.WriteLine(row);
        sink.Add(row);
    }

    // ---------------------------------------------------------------- entry

    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--latencia");
        var seconds = at + 1 < args.Length && double.TryParse(args[at + 1], out var s) ? s : 8;
        var self = Environment.ProcessPath!;
        using var server = Process.Start(new ProcessStartInfo(self, $"--servidor-falso {Port}") { UseShellExecute = false, CreateNoWindow = true })!;
        Thread.Sleep(600);

        try
        {
            var sink = new List<Stat>();
            Console.WriteLine($"# Latencia de lectura de PermaLocke contra un Azahar FALSO en bucle local ({DateTime.Now:yyyy-MM-dd HH:mm})");
            Console.WriteLine($"Procesador: {Environment.ProcessorCount} hilos · SO: {Environment.OSVersion.VersionString} · .NET {Environment.Version}");
            using var client = new AzaharRpcClient("127.0.0.1", Port, 500);
            TimerTests(sink);
            RoundTrips(client, sink);
            Sequences(client, sink);
            EndToEnd(sink, 60);
            Cost(seconds);
            ScreenProxy(sink);
            Console.WriteLine();
            Console.WriteLine("Medido: todo lo de arriba. NO medido: cuándo escribe el juego el dato, cuánto tarda el hilo RPC de Azahar mientras emula, la barra del juego, DWM.");
        }
        finally
        {
            using var stopper = new AzaharRpcClient("127.0.0.1", Port, 200);
            try
            {
                using var socket = new UdpClient();
                var quit = new byte[24];
                BinaryPrimitives.WriteUInt32LittleEndian(quit.AsSpan(8), 255);
                socket.Send(quit, quit.Length, new IPEndPoint(IPAddress.Loopback, Port));
            }
            catch (SocketException)
            {
            }

            if (!server.WaitForExit(1500)) server.Kill();
            _ = stopper;
        }

        return 0;
    }
}
