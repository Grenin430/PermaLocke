using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.App.Services;
using PermaLocke.App.Views;
using PermaLocke.Infrastructure;

namespace PermaLocke.ItemLab;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var root = LabData.Root() ?? throw new InvalidOperationException("No se encuentra PermaLocke.slnx subiendo desde " + AppContext.BaseDirectory);

        // Los iconos salen como en la app: de la ROM y de Expansion/ de esta carpeta, a Data/sprites (que no se versiona).
        var sprites = new PokemonSpriteService(new AppPaths(root), NullLogger<PokemonSpriteService>.Instance);
        Task.Run(() => sprites.PrepareAsync()).GetAwaiter().GetResult();

        var data = new LabData(sprites, root);

        var report = Array.IndexOf(args, "--iconos");
        if (report >= 0 && report + 1 < args.Length) return Icons(data, args[report + 1]);

        var app = new Application();
        app.Run(new LabWindow(args, data));
        return 0;
    }

    /// <summary>
    /// <c>--iconos informe.txt</c>: takes out of the cartridge the icon of every item the game draws one for, by category,
    /// and lists the ones that should have it and did not come out. Exit code 1 if there is any.
    /// </summary>
    private static int Icons(LabData data, string file)
    {
        var lines = new List<string> { "categoria  con icono real / total" };
        var failed = new List<Entry>();

        foreach (var group in data.Entries.GroupBy(e => e.Class.Category).OrderBy(g => g.Key))
        {
            var real = 0;
            foreach (var entry in group)
            {
                if (!data.HasIcon(entry.Id)) continue;

                var (pixels, width, height) = data.Icon(entry.Id);
                var parcel = ItemScene.Parcel();
                if (width == parcel.Width && height == parcel.Height && pixels.AsSpan().SequenceEqual(parcel.Pixels))
                {
                    failed.Add(entry);
                }
                else
                {
                    real++;
                }
            }

            lines.Add($"{group.Key,-10} {real,4} / {group.Count(),4}");
        }

        lines.Add(string.Empty);
        lines.Add("Con entrada en la tabla del cartucho y sin icono extraido: " + failed.Count);
        lines.AddRange(failed.Select(e => $"  {e.Id}\t{e.Name}"));

        lines.Add(string.Empty);
        lines.Add("El juego los dibuja como un signo de interrogacion (tabla del cartucho = 768):");
        lines.AddRange(data.Entries.Where(e => !data.HasIcon(e.Id)).Select(e => $"  {e.Id}\t{e.Name}\t{e.Class.Category}"));

        File.WriteAllLines(file, lines, new System.Text.UTF8Encoding(true));
        return failed.Count == 0 ? 0 : 1;
    }
}
