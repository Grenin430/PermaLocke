using System.Windows;

namespace PermaLocke.PlaqueLab;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var data = LabData.Load();

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
