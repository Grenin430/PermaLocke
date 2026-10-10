using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.App.Services;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Battle;
using PermaLocke.GameLink.Rpc;
using PermaLocke.GameLink.Vitals;

namespace PermaLocke.Probe;

/// <summary>
/// The two measurements that need a real Azahar and a real battle (2026-10-11), written so that the player runs them and brings the numbers:
///   Probe --combate sincronia [segundos] [--layout 0|1|3]   how long after memory changes the game's bar starts and how long it takes
///   Probe --combate latencia  [segundos]                    how long after the change each way of reading learns of it, with Azahar for real
/// Both only read (memory and the screen). Neither writes anything to the game.
/// </summary>
public static class LiveProbes
{
    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "permalocke-sincronia");

    /// <summary>The player's blocks of the battle, in both tables, from a normal reading; null when there is no battle.</summary>
    private static IReadOnlyList<BattleTable>? Battle(AzaharRpcClient client)
    {
        var reader = new BattleTableReader(client, NullLogger<BattleTableReader>.Instance);
        reader.Read(DateTimeOffset.Now);
        var tables = BattleFaintTracker.InProgress(reader.Read(DateTimeOffset.Now));
        return tables.Count >= 2 ? tables : null;
    }

    private sealed record Change(long At, int Table, int Id, int From, int To, int Max);

    private sealed record Look(long At, HpBarState State, double Fill);

    // ---------------------------------------------------------------- the bar of the game

    public static int Sync(AzaharRpcClient client, int seconds, int layoutOption)
    {
        if (Battle(client) is not { } tables)
        {
            Console.WriteLine("No hay combate: entra en uno y quédate en el menú de ataques.");
            return 1;
        }

        var layout = new AzaharScreenLayout(layoutOption, false, false);
        if (HpBarGeometry.Of(layout) is not { } place)
        {
            Console.WriteLine("Ese layout no se calcula: usa 0 (apilado), 1 (una pantalla) o 3 (lado a lado).");
            return 1;
        }

        var handle = Process.GetProcessesByName("azahar").Select(p => p.MainWindowHandle).FirstOrDefault(h => h != IntPtr.Zero);
        if (handle == IntPtr.Zero || BarProbe.RenderBox(handle) is not { } box)
        {
            Console.WriteLine("No se encuentra la ventana de Azahar o su superficie de dibujo.");
            return 1;
        }

        var scale = Math.Min(box.Width / (double)place.PictureWidth, box.Height / (double)place.PictureHeight);
        var originX = box.Left + ((box.Width - (place.PictureWidth * scale)) / 2) + (place.TopX * scale);
        var originY = box.Top + ((box.Height - (place.PictureHeight * scale)) / 2) + (place.TopY * scale);
        var screen = ((int)originX, (int)originY, (int)(400 * scale), (int)(240 * scale));
        Console.WriteLine($"Ventana {box.Width}x{box.Height}, escala {scale:F3}, pantalla de arriba en ({screen.Item1},{screen.Item2}). "
                          + $"Combate en 0x{tables[0].Origin:X8} y 0x{tables[1].Origin:X8}. Juega: deja que te den golpes de tamaños distintos ({seconds} s).");

        var changes = new List<Change>();
        var looks = new List<Look>();
        var stop = false;
        var batch = new AzaharBatchReader(100);
        var ranges = new List<(uint, int)>();
        var keys = new List<(int Table, int Id)>();
        for (var t = 0; t < tables.Count; t++)
        {
            foreach (var block in tables[t].Blocks.Where(b => b.IsPlayers))
            {
                ranges.Add((tables[t].Origin + (uint)(block.BattleId * BattleLayout.Stride) + BattleHotReader.Offset, BattleHotReader.Length));
                keys.Add((t, block.BattleId));
            }
        }

        var memory = new Thread(() =>
        {
            var last = new int[keys.Count];
            Array.Fill(last, -1);
            while (!Volatile.Read(ref stop))
            {
                var answers = batch.ReadMany(ranges);
                var at = Stopwatch.GetTimestamp();
                for (var i = 0; i < keys.Count; i++)
                {
                    if (answers[i] is not { Length: >= BattleHotReader.Length } bytes) continue;
                    var hp = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(BattleHotReader.HpAt));
                    var max = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(BattleHotReader.MaxAt));
                    if (last[i] >= 0 && hp != last[i] && hp <= max) lock (changes) changes.Add(new Change(at, keys[i].Table, keys[i].Id, last[i], hp, max));
                    last[i] = hp;
                }

                Thread.SpinWait(2000);
            }
        }) { IsBackground = true };

        var bar = new Thread(() =>
        {
            using var capture = new BarProbe.Capture(screen, 400, 240);
            while (!Volatile.Read(ref stop))
            {
                var frame = capture.Grab();
                var at = Stopwatch.GetTimestamp();
                var (state, _, reading) = BarProbe.Bar(frame, HpBar.Left, HpBar.Right, HpBar.FirstRow, HpBar.LastRow);
                lock (looks) looks.Add(new Look(at, state, reading.Fill));
                Thread.Sleep(10);
            }
        }) { IsBackground = true };

        var origin = Stopwatch.GetTimestamp();
        memory.Start();
        bar.Start();
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        Volatile.Write(ref stop, true);
        memory.Join();
        bar.Join();

        return AnalyseBar(changes, looks, origin);
    }

    private static int AnalyseBar(List<Change> changes, List<Look> looks, long origin)
    {
        Directory.CreateDirectory(Folder);
        var rows = new List<(double Share, double LagB, double LagA, double Duration, double Fill, int Damage)>();
        var csv = new StringBuilder("golpe;daño;cuota;t_memoria_ms;tabla_B_a_barra_ms;tabla_A_a_barra_ms;barra_dura_ms;relleno_final\n");

        // A hit: the first change of a player's block that lowers it, taken in the table that changed first; the other table's change follows it.
        var seen = new HashSet<(int, int)>();
        foreach (var first in changes.OrderBy(c => c.At))
        {
            if (first.To >= first.From || !seen.Add((first.Id, (int)(first.At / (Stopwatch.Frequency * 3))))) continue;

            var other = changes.FirstOrDefault(c => c.Table != first.Table && c.Id == first.Id && c.At >= first.At && c.To <= first.From);
            var before = looks.LastOrDefault(l => l.At <= first.At && l.State == HpBarState.Filled);
            if (before is null) continue;

            // The bar's start: the first look after the change whose fill is below where it was by a pixel or two, with no hidden stretch of grey; its end: still for four looks.
            Look? start = null;
            Look? end = null;
            var still = 0;
            var lastFill = before.Fill;
            foreach (var look in looks.Where(l => l.At > first.At && l.At < first.At + (8 * Stopwatch.Frequency)))
            {
                if (look.State == HpBarState.Hidden) continue;
                if (start is null)
                {
                    if (before.Fill - look.Fill >= BarTracker.Step) (start, lastFill) = (look, look.Fill);
                    continue;
                }

                still = Math.Abs(look.Fill - lastFill) < 0.006 ? still + 1 : 0;
                lastFill = look.Fill;
                if (still >= BarTracker.SettleReadings)
                {
                    end = look;
                    break;
                }
            }

            if (start is null || end is null) continue;
            var share = (first.From - first.To) / (double)first.Max;
            var lagB = Ms(start.At - first.At);
            var lagA = other is null ? double.NaN : Ms(start.At - other.At);
            var duration = Ms(end.At - start.At);
            rows.Add((share, lagB, lagA, duration, end.Fill, first.From - first.To));
            csv.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{rows.Count};{first.From - first.To};{share:F3};{Ms(first.At - origin):F0};{lagB:F0};{lagA:F0};{duration:F0};{end.Fill:F3}"));
        }

        File.WriteAllText(Path.Combine(Folder, "golpes.csv"), csv.ToString());
        Console.WriteLine($"{changes.Count} cambios de PS en memoria, {looks.Count} miradas a la barra ({looks.Count / Math.Max(1, Ms(looks[^1].At - looks[0].At) / 1000):F0} por segundo).");
        if (rows.Count == 0)
        {
            Console.WriteLine("Ningún golpe se vio entero (memoria y barra). Prueba con la ventana de Azahar delante, sin tapar, y golpes que no maten.");
            return 1;
        }

        Console.WriteLine("golpe  daño  cuota   tabla B→barra  tabla A→barra  la barra dura  relleno final");
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            Console.WriteLine($"{i + 1,5}  {r.Damage,4}  {r.Share,5:P0}  {r.LagB,10:F0} ms  {r.LagA,10:F0} ms  {r.Duration,10:F0} ms  {r.Fill,10:P0}");
        }

        // The two lines the panel needs, as straight lines of the share lost (least squares), in seconds.
        var (baseLag, slopeLag) = Fit(rows.Select(r => (r.Share, r.LagB / 1000)).ToList());
        var (baseBar, slopeBar) = Fit(rows.Select(r => (r.Share, r.Duration / 1000)).ToList());
        Console.WriteLine();
        Console.WriteLine("Para src/PermaLocke.App/Services/HpPresenter.cs (HpSyncTable), si los golpes eran de tamaños distintos:");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"    CalculatedBaseSeconds = {baseLag:F2}, CalculatedPerShareSeconds = {slopeLag:F2};"));
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"    BarSeconds(share, drop: true) = {baseBar:F2} + ({slopeBar:F2} * share);"));
        Console.WriteLine($"Ficheros: {Folder}");
        return 0;
    }

    private static (double Base, double Slope) Fit(IReadOnlyList<(double X, double Y)> points)
    {
        var n = points.Count;
        var mx = points.Average(p => p.X);
        var my = points.Average(p => p.Y);
        var vx = points.Sum(p => (p.X - mx) * (p.X - mx));
        if (n < 3 || vx < 1e-6) return (my, 0);
        var slope = points.Sum(p => (p.X - mx) * (p.Y - my)) / vx;
        return (my - (slope * mx), slope);
    }

    // ---------------------------------------------------------------- the latency of each way of reading

    public static int Latency(AzaharRpcClient client, int seconds)
    {
        if (Battle(client) is not { } tables)
        {
            Console.WriteLine("No hay combate: entra en uno y quédate en el menú de ataques.");
            return 1;
        }

        var player = tables[0].Blocks.First(b => b.IsPlayers);
        Console.WriteLine($"Midiendo con el Pokémon de la posición {player.BattleId} ({seconds} s). Juega: que te den golpes y que te curen.");

        // Three readers of the same block: the reference (as fast as the link goes), the pump as the app has it (120 a second), and the cold loop of today.
        var reference = new List<(long At, int Hp)>();
        var hot = new List<(long At, int Hp)>();
        var cold = new List<(long At, int Hp)>();
        var rtt = new List<double>();
        var stop = false;

        Thread Loop(Action body) => new(() => body()) { IsBackground = true, Priority = ThreadPriority.AboveNormal };

        var referenceThread = Loop(() =>
        {
            using var batch = new AzaharBatchReader(100);
            var ranges = new List<(uint, int)> { (tables[1].Origin + (uint)(player.BattleId * BattleLayout.Stride) + BattleHotReader.Offset, BattleHotReader.Length) };
            var last = -1;
            while (!Volatile.Read(ref stop))
            {
                var started = Stopwatch.GetTimestamp();
                var answers = batch.ReadMany(ranges);
                var at = Stopwatch.GetTimestamp();
                if (answers[0] is { } bytes)
                {
                    rtt.Add(Ms(at - started));
                    var hp = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(BattleHotReader.HpAt));
                    if (hp != last) reference.Add((at, hp));
                    last = hp;
                }
            }
        });

        var hotThread = Loop(() =>
        {
            using var batch = new AzaharBatchReader(100);
            var vitals = new VitalsReader(batch);
            var live = BattleFaintTracker.InProgress(tables);
            var timer = new HighResolutionTimer();
            VitalsFrame? accepted = null;
            var last = -1;
            while (!Volatile.Read(ref stop))
            {
                var started = Stopwatch.GetTimestamp();
                var frame = vitals.ReadConfirmed(accepted, live, null, 0);
                if (frame is { Battle.Count: > 0 })
                {
                    var hp = frame.Battle.First(s => s.BattleId == player.BattleId && s.Table == 1).Hp;
                    if (hp != last) hot.Add((frame.ReadAt, hp));
                    last = hp;
                    accepted = frame;
                }

                timer.Sleep(Math.Max(0.2, (1000.0 / 120) - Ms(Stopwatch.GetTimestamp() - started)));
            }
        });

        var coldThread = Loop(() =>
        {
            var reader = new BattleTableReader(client, NullLogger<BattleTableReader>.Instance);
            var last = -1;
            while (!Volatile.Read(ref stop))
            {
                var read = reader.Read(DateTimeOffset.Now);
                var at = Stopwatch.GetTimestamp();
                if (read.Count > 1 && read[1].Block(player.BattleId) is { } block)
                {
                    if (block.CurrentHp != last) cold.Add((at, block.CurrentHp));
                    last = block.CurrentHp;
                }

                Thread.Sleep(250);
            }
        });

        referenceThread.Start();
        hotThread.Start();
        coldThread.Start();
        Thread.Sleep(TimeSpan.FromSeconds(seconds));
        Volatile.Write(ref stop, true);
        referenceThread.Join();
        hotThread.Join();
        coldThread.Join();

        Console.WriteLine($"{reference.Count - 1} cambios vistos por la referencia. Tiempo de una petición con el emulador de verdad: "
                          + $"media {rtt.Average():F3} ms, p95 {rtt.OrderBy(v => v).ElementAt((int)(rtt.Count * 0.95)):F3} ms, máximo {rtt.Max():F3} ms ({rtt.Count} lecturas).");

        List<double> Lags(List<(long At, int Hp)> reader) => [.. reference.Skip(1).Select(r => reader.Where(x => x.Hp == r.Hp && x.At >= r.At - Stopwatch.Frequency / 200).Select(x => (double?)Ms(x.At - r.At)).FirstOrDefault()).Where(v => v is not null).Select(v => v!.Value)];

        Console.WriteLine("| lector | cambios | media ms | p95 ms | máximo ms |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        foreach (var (name, reader) in new[] { ("rápido a 120 Hz (el de la aplicación)", hot), ("lento, cada 250 ms (el de antes)", cold) })
        {
            var lags = Lags(reader);
            if (lags.Count == 0)
            {
                Console.WriteLine($"| {name} | 0 | - | - | - |");
                continue;
            }

            var sorted = lags.OrderBy(v => v).ToArray();
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"| {name} | {lags.Count} | {lags.Average():F1} | {sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.95))]:F1} | {sorted[^1]:F1} |"));
        }

        return 0;
    }
}
