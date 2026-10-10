using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PermaLocke.App.Services;
using PermaLocke.App.Views;

namespace PermaLocke.PlaqueLab;

/// <summary>
/// What a frame of the cap panel costs (2026-10-10). <see cref="Frames"/> runs the real panel (<see cref="CapPlaque"/>: still part, dissolve,
/// moving part, one reused bitmap) for the scenarios, with no window, and gives the time of each <c>Tick</c> and of each change of data.
/// <see cref="Windows"/> does the same in two real overlay windows like the app's and measures the processor time of the process: it puts
/// two windows over the top left of the desktop for ten seconds, so it is not run without being asked for.
/// </summary>
public static class Bench
{
    public static int Frames(LabData data)
    {
        Console.WriteLine("Coste de un fotograma del panel del cap, sin ventana (CapPlaque.Set y Tick, mapa de bits reutilizado)");
        Console.WriteLine($"{"escenario",-14} {"panel",-6} {"modo",-8} {"rejilla",-9} {"Tick medio",11} {"p95",8} {"maximo",8} {"Set medio",10} {"KB/fotograma",13} {"fotogramas",10}");

        foreach (var (panel, mode, cols, rows) in new[]
                 {
                     (CapPanelKind.Party, CapMode.Column, 178, 321), (CapPanelKind.Deep, CapMode.Column, 178, 321), (CapPanelKind.Party, CapMode.Column, 132, 250),
                     (CapPanelKind.Party, CapMode.Slim, 100, 321), (CapPanelKind.Party, CapMode.Strip, 636, 52), (CapPanelKind.Deep, CapMode.Strip, 636, 52),
                     (CapPanelKind.Party, CapMode.Compact, 70, 90)
                 })
        {
            foreach (var name in new[] { "captura", "combate", "ev-golpe", "ev-revivir", "ev-curas", "caida" })
            {
                var scenario = data.Scenarios.First(s => s.Name == name);
                var plaque = new CapPlaque(panel);
                var ticks = new List<double>();
                var bytes = new List<long>();
                var sets = new List<double>();
                var watch = new Stopwatch();

                // Sixty frames a second while something moves (the render loop of WPF, as the app does), twelve at rest.
                var t = 0.0;
                CapPlaqueData? shown = null;
                while (t < 12)
                {
                    var data1 = scenario.At(t);
                    if (!ReferenceEquals(data1, shown))
                    {
                        watch.Restart();
                        plaque.Set(data1, new CapSlot(panel, mode, new CapRect(0, 0, cols * 3, rows * 3), 3, cols, rows), t);
                        sets.Add(watch.Elapsed.TotalMilliseconds);
                        shown = data1;
                    }

                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    watch.Restart();
                    plaque.Tick(t);
                    ticks.Add(watch.Elapsed.TotalMilliseconds);
                    bytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocated);
                    t += plaque.Busy(t) ? 1.0 / 60 : 0.083;
                }

                ticks.Sort();
                Console.WriteLine($"{name,-14} {panel,-6} {mode,-8} {cols + "x" + rows,-9} {ticks.Average(),9:F2} ms {ticks[(int)(ticks.Count * 0.95)],6:F2} ms {ticks[^1],6:F2} ms {sets.Average(),8:F2} ms {bytes.Average() / 1024.0,11:F1}   {ticks.Count,10}");
            }
        }

        return 0;
    }

    [STAThread]
    public static int Windows(LabData data)
    {
        var app = new Application();
        var scenario = data.Scenarios.First(s => s.Name == "completo");
        var plan = new[] { (CapPanelKind.Party, 178, 321, 3), (CapPanelKind.Deep, 178, 321, 3) };
        var windows = new List<(Window Window, CapPlaque Plaque)>();
        var left = 0;

        foreach (var (panel, cols, rows, cell) in plan)
        {
            var plaque = new CapPlaque(panel);
            plaque.Set(scenario.At(0), new CapSlot(panel, CapMode.Column, new CapRect(0, 0, cols * cell, rows * cell), cell, cols, rows), 0);
            var window = new Window
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false,
                ShowActivated = false, Focusable = false, Topmost = true, ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight, UseLayoutRounding = true, Opacity = 0.97, Content = plaque
            };
            OverlayWindows.MakeUntouchable(window);
            window.Show();
            OverlayWindows.PlaceOver(window, (left, 40, cols * cell, rows * cell));
            left += cols * cell;
            windows.Add((window, plaque));
        }

        var process = Process.GetCurrentProcess();
        var clock = Stopwatch.StartNew();
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(83) };
        timer.Tick += (_, _) =>
        {
            foreach (var (_, plaque) in windows) plaque.Tick(clock.Elapsed.TotalSeconds);
        };

        var phases = new (string Name, double Seconds, int Interval)[] { ("ambiente, 12 fotogramas por segundo", 8, 83), ("a tope, 25 fotogramas por segundo", 8, 40) };
        var index = -1;
        var phaseStart = TimeSpan.Zero;
        var cpuStart = TimeSpan.Zero;
        var step = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };

        void Next()
        {
            if (index >= 0)
            {
                process.Refresh();
                var cpu = (process.TotalProcessorTime - cpuStart).TotalSeconds / (clock.Elapsed - phaseStart).TotalSeconds / Environment.ProcessorCount * 100;
                Console.WriteLine($"{phases[index].Name,-40} {cpu,5:F2} % de la CPU total ({(process.TotalProcessorTime - cpuStart).TotalMilliseconds / ((clock.Elapsed - phaseStart).TotalSeconds * 1000 / phases[index].Interval):F2} ms de CPU por fotograma)");
            }

            index++;
            if (index >= phases.Length)
            {
                step.Stop();
                timer.Stop();
                app.Shutdown();
                return;
            }

            timer.Interval = TimeSpan.FromMilliseconds(phases[index].Interval);
            process.Refresh();
            cpuStart = process.TotalProcessorTime;
            phaseStart = clock.Elapsed;
        }

        step.Tick += (_, _) =>
        {
            if (index < 0 || (clock.Elapsed - phaseStart).TotalSeconds >= phases[index].Seconds) Next();
        };

        Console.WriteLine("Dos ventanas del panel (completo, 178x321 celdas a 3 px) sobre la esquina del escritorio.");
        timer.Start();
        step.Start();
        app.Run();
        return 0;
    }
}
