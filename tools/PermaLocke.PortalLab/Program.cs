using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;

namespace PermaLocke.PortalLab;

/// <summary>
/// Renders the randomizer's portal with steps and a finale made up for the test, to a strip of frames and a video. The steps the
/// app shows are the real ones; here they are only times.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "permalocke-portal");
        var failed = args.Contains("fallo");
        Directory.CreateDirectory(output);

        var root = FindRoot();
        var sprites = LoadSprites(Path.Combine(root, "Data", "sprites"));
        Console.WriteLine($"{sprites.Count} dibujos");

        string[] names = ["Encuentros salvajes", "Objetos del suelo", "Iniciales, fósiles y estáticos", "Entrenadores", "Datos de Pokémon",
            "Tiendas", "MT", "Tutores"];
        double[] pulses = [2.2, 3.0, 3.9, 5.1, 5.8, 6.7, 7.3, 7.9];
        var finaleAt = 8.6;
        var length = finaleAt + (failed ? UltraWormholeScene.FailLength : UltraWormholeScene.FinaleLength) + 0.2;

        var scene = new UltraWormholeScene(UltraWormholeScene.DesignWidth, UltraWormholeScene.DesignRows);
        const int fps = 30;
        const int zoom = 4;
        var frames = (int)(length * fps);
        var ffmpeg = FindFfmpeg();
        var video = Path.Combine(output, failed ? "portal-fallo.mp4" : "portal.mp4");

        using var encoder = Process.Start(new ProcessStartInfo(ffmpeg,
            $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt bgra -s {scene.Width}x{scene.Height} -r {fps} -i - " +
            $"-vf scale={scene.Width * zoom}:{scene.Height * zoom}:flags=neighbor,format=yuv420p -c:v libx264 -crf 16 \"{video}\"")
        { UseShellExecute = false, RedirectStandardInput = true })!;

        var picks = new[] { 0.12, 0.35, 1.0, 2.25, 3.95, 6.0, finaleAt + 0.3, finaleAt + 0.68, finaleAt + 0.95, finaleAt + 1.3, finaleAt + 1.8, finaleAt + 2.3 };
        var strip = new List<byte[]>();
        var watch = Stopwatch.StartNew();

        for (var f = 0; f < frames; f++)
        {
            var t = f / (double)fps;
            var done = pulses.Count(p => p <= t);
            var state = new WormholeState(t, pulses.Where(p => p <= t).ToList(), t >= finaleAt ? finaleAt : null, failed && t >= finaleAt,
                done > 0 ? names[Math.Min(done, names.Length) - 1] : null, sprites);
            scene.Render(state, toScreen: false);
            encoder.StandardInput.BaseStream.Write(scene.Pixels);
            if (picks.Any(p => Math.Abs(p - t) < 0.5 / fps)) strip.Add([.. scene.Pixels]);
        }

        encoder.StandardInput.Close();
        encoder.WaitForExit();
        Console.WriteLine($"{frames} fotogramas en {watch.Elapsed.TotalMilliseconds / frames:F1} ms de media: {video}");

        // La tira: cuatro columnas, a x2.
        var stripPath = Path.Combine(output, failed ? "portal-fallo.png" : "portal.png");
        using (var tile = Process.Start(new ProcessStartInfo(ffmpeg,
                   $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt bgra -s {scene.Width}x{scene.Height} -r 1 -i - " +
                   $"-vf scale={scene.Width * 2}:{scene.Height * 2}:flags=neighbor,tile=4x{(strip.Count + 3) / 4}:padding=4 -frames:v 1 \"{stripPath}\"")
               { UseShellExecute = false, RedirectStandardInput = true })!)
        {
            foreach (var frame in strip) tile.StandardInput.BaseStream.Write(frame);
            tile.StandardInput.Close();
            tile.WaitForExit();
        }

        Console.WriteLine(stripPath);
        return 0;
    }

    /// <summary>Pokémon, items and machines from the sprites the app has already taken from the cartridge, picked at random.</summary>
    private static List<RoomSprite> LoadSprites(string folder)
    {
        var random = new Random();
        var files = Directory.GetFiles(folder, "*.png").OrderBy(_ => random.Next()).Take(90)
            .Concat(Directory.GetFiles(Path.Combine(folder, "items"), "*.png").OrderBy(_ => random.Next()).Take(50));
        var sprites = new List<RoomSprite>();
        foreach (var file in files)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(file);
            image.EndInit();
            if (RoomSprite.From(image) is { } sprite) sprites.Add(sprite);
        }

        return sprites;
    }

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "PermaLocke.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("No encuentro la raíz del repositorio.");
    }

    private static string FindFfmpeg()
    {
        foreach (var candidate in new[] { @"C:\Program Files (x86)\Soundux\ffmpeg.exe", "ffmpeg" })
        {
            if (candidate == "ffmpeg" || File.Exists(candidate)) return candidate;
        }

        return "ffmpeg";
    }
}
