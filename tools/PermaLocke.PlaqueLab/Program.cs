using System.Windows;

namespace PermaLocke.PlaqueLab;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var data = LabData.Load();

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
