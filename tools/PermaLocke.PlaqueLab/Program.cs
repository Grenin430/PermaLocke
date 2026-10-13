using System.Windows;

namespace PermaLocke.PlaqueLab;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (Array.IndexOf(args, "--servidor-falso") is var fake and >= 0 && fake + 1 < args.Length) return Latency.Serve(int.Parse(args[fake + 1]));
        if (args.Contains("--latencia")) return Latency.Run(args);

        var data = LabData.Load();

        // PROPOSALS (branch cap-propuestas): --equipo zigzag|columna|pares|colgado, --caidos dispersos|rotulo|hilera|ultimo, --eventos
        string? Option(string name) => Array.IndexOf(args, name) is var at and >= 0 && at + 1 < args.Length ? args[at + 1] : null;

        // --especie N: todo lo que pasa nadando es esa especie (para revisar un dibujo en el mar)
        if (Option("--especie") is { } forcedSpecies) PermaLocke.App.Views.CapVisitors.ForcedSpecies = int.Parse(forcedSpecies);

        // --ventana escenario t "preset" fichero: una ventana entera del emulador con el panel (el escenario de la captura del jugador)
        if (Option("--ventana") is { } windowScenario && Array.IndexOf(args, "--ventana") + 4 < args.Length + 0)
        {
            var at = Array.IndexOf(args, "--ventana");
            var scenario = data.Scenarios.First(s => s.Name == windowScenario);
            // "m" or "m+5": the moment that says most about the scenario.
            var tw = args[at + 2].StartsWith('m') ? (scenario.Moment ?? 0) + (args[at + 2].Length > 1 ? double.Parse(args[at + 2][1..], System.Globalization.CultureInfo.InvariantCulture) : 0)
                : double.Parse(args[at + 2], System.Globalization.CultureInfo.InvariantCulture);
            var preset = LabScreen.Presets.First(p => p.Name.StartsWith(args[at + 3], StringComparison.Ordinal));
            var (bgra, _) = LabScreen.Compose(scenario, tw, preset, new PermaLocke.App.Views.CapOptions(), game: true);
            PermaLocke.App.Views.CapSnap.Save(args[at + 4], bgra, preset.Width, preset.Height);
            return 0;
        }

        // --tira escenario fichero modo colsxfilas escala x,y,w,h t1,t2,...: fotogramas clave lado a lado, con los tiempos absolutos del escenario
        if (Array.IndexOf(args, "--tira") is var strip and >= 0 && strip + 7 < args.Length)
        {
            var scenario = data.Scenarios.First(s => s.Name == args[strip + 1]);
            var mode = Enum.Parse<PermaLocke.App.Views.CapMode>(args[strip + 3], true);
            var grid = args[strip + 4].Split('x').Select(int.Parse).ToArray();
            var scale = int.Parse(args[strip + 5]);
            var r = args[strip + 6].Split(',').Select(int.Parse).ToArray();
            var times = args[strip + 7].Split(',').Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Exports.TimedStrip(args[strip + 2], scenario, mode, grid[0], grid[1], scale, new PermaLocke.App.Views.CapRect(r[0], r[1], r[2], r[3]), times);
            return 0;
        }

        // --sonda escenario modo columnasxfilas t x,y x,y ...: el valor BGRA de unas celdas del panel del fondo del mar, para depurar
        if (Array.IndexOf(args, "--sonda") is var probe and >= 0 && probe + 5 < args.Length)
        {
            var scenario = data.Scenarios.First(s => s.Name == args[probe + 1]);
            var mode = Enum.Parse<PermaLocke.App.Views.CapMode>(args[probe + 2], true);
            var grid = args[probe + 3].Split('x').Select(int.Parse).ToArray();
            var t = double.Parse(args[probe + 4], System.Globalization.CultureInfo.InvariantCulture);
            var sheet = PermaLocke.App.Views.CapPlaque.Replay(scenario, mode, grid[0], grid[1], t, PermaLocke.App.Views.CapPanelKind.Deep);
            Console.WriteLine("celdas no opacas: " + Enumerable.Range(0, sheet.W * sheet.H).Count(k => sheet.B[(k * 4) + 3] != 255) + " de " + (sheet.W * sheet.H));
            foreach (var cell in args.Skip(probe + 5))
            {
                var xy = cell.Split(',').Select(int.Parse).ToArray();
                var i = ((xy[1] * sheet.W) + xy[0]) * 4;
                Console.WriteLine($"{cell}: B={sheet.B[i]} G={sheet.B[i + 1]} R={sheet.B[i + 2]} A={sheet.B[i + 3]}");
            }

            return 0;
        }

        var detail = Array.IndexOf(args, "--detalle");
        if (detail >= 0 && detail + 7 < args.Length)
        {
            // --detalle escenario modo columnasxfilas t x,y,w,h escala fichero: un trozo de un fotograma, ampliado, para mirar de cerca.
            var scenario = data.Scenarios.First(s => s.Name == args[detail + 1]);
            var mode = Enum.Parse<PermaLocke.App.Views.CapMode>(args[detail + 2], true);
            var grid = args[detail + 3].Split('x').Select(int.Parse).ToArray();
            var t = double.Parse(args[detail + 4], System.Globalization.CultureInfo.InvariantCulture);
            var r = args[detail + 5].Split(',').Select(int.Parse).ToArray();
            var panel = args.Contains("--fondo-del-mar") ? PermaLocke.App.Views.CapPanelKind.Deep : PermaLocke.App.Views.CapPanelKind.Party;
            var sheet = PermaLocke.App.Views.CapPlaque.Replay(scenario, mode, grid[0], grid[1], t, panel);
            PermaLocke.App.Views.CapSnap.Save(args[detail + 7], sheet, int.Parse(args[detail + 6]), 0x000000, 1, new PermaLocke.App.Views.CapRect(r[0], r[1], r[2], r[3]));
            return 0;
        }

        // --bichos fichero: cada Pokémon que pasa por el mar en seis momentos de su movimiento, ampliado
        if (Option("--bichos") is { } critters)
        {
            PermaLocke.App.Views.CapSnap.Save(critters, PermaLocke.App.Views.CapVisitors.Gallery(), 4);
            return 0;
        }

        var export = Array.IndexOf(args, "--exportar");
        if (export >= 0 && export + 1 < args.Length)
        {
            var count = Exports.All(args[export + 1], data);
            Console.WriteLine($"{count} PNG en {args[export + 1]}" + (data.RealIcons ? string.Empty : " (iconos de relleno: no hay ROM)"));
            return 0;
        }

        if (args.Contains("--medir")) return Bench.Frames(data);
        if (args.Contains("--medir-ventana")) return Bench.Windows(data);

        var app = new Application();
        app.Run(new LabWindow(args, data));
        return 0;
    }
}
