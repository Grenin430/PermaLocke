using PermaLocke.App.Views;

namespace PermaLocke.PixelCheck;

/// <summary>
/// The card of a caught Pokémon going into the album over the game (§190): every moment of it draws inside the top
/// screen, the card ends in its pocket, and with <c>PERMALOCKE_PIXEL_DIR</c> set, a sheet of frames as PNG.
/// </summary>
public sealed class CatchPreviews
{
    private static TcgCard Card(int rarity, bool shiny) => new(
        "Pikipek", "Pikipek", 731, "Básico", 4, 18, [0, 2],
        [new("Picotazo", 2, 35, 100, 35, "Físico"), new("Gruñido", 0, 0, 100, 40, "Estado")],
        "Vista Lince", "Firme", 1, 3, "", "Ruta 1", 4, rarity, false, shiny, false, false, Previews.Dragon,
        [18, 12, 8, 7, 8, 11], [31, 20, 5, 31, 14, 28], [0, 0, 0, 0, 0, 0], 99);

    [Fact]
    public void Every_moment_draws_inside_the_top_screen()
    {
        foreach (var (width, height, pixel) in new[] { (812, 487, 2.03), (400, 240, 1.0), (1200, 720, 3.0) })
        {
            var scene = new CatchScene(width, height, pixel);
            var card = Card(3, true);
            var front = TcgCardArt.Render(card, TcgLayout.Full);
            var back = TcgCardArt.Render(card, TcgLayout.Full, back: true);

            for (var t = -0.1; t < CatchTimeline.Length + 0.2; t += 1 / 60.0)
            {
                scene.Render(front, back, card, t);
            }

            // Al final no queda nada encima del juego.
            Assert.All(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => Assert.Equal(0, alpha));

            // Mostrándose, la carta está entera en pantalla y ocupa su buen trozo.
            scene.Render(front, back, card, (CatchTimeline.Shown + CatchTimeline.Fly) / 2);
            var opaque = scene.Pixels.Where((_, i) => i % 4 == 3).Count(alpha => alpha == 255);
            Assert.InRange(opaque, width * height / 12, width * height / 2);
        }
    }

    /// <summary>The card lands where the pocket is: the last frame of its flight is over the target pocket.</summary>
    [Fact]
    public void The_card_ends_in_its_pocket()
    {
        var scene = new CatchScene(812, 487, 2.03);
        var card = Card(1, false);
        var front = TcgCardArt.Render(card, TcgLayout.Full);
        var back = TcgCardArt.Render(card, TcgLayout.Full, back: true);
        var (px, py) = scene.Pocket;

        scene.Render(front, back, card, CatchTimeline.Inserted - 0.01);
        Assert.InRange(px, scene.Width * 0.7, scene.Width);
        Assert.Equal(255, scene.Pixels[((((int)py * scene.Width) + (int)px) * 4) + 3]);
    }

    [Fact]
    public void Writes_the_pictures_when_asked()
    {
        var folder = Environment.GetEnvironmentVariable("PERMALOCKE_PIXEL_DIR");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        const int w = 812, h = 487;
        var scene = new CatchScene(w, h, 2.03);
        var card = Card(3, true);
        var front = TcgCardArt.Render(card, TcgLayout.Full);
        var back = TcgCardArt.Render(card, TcgLayout.Full, back: true);
        double[] times = [0.2, 0.42, 0.6, 0.92, 1.1, 1.35, 1.7, 1.85, 2.2, 2.6, 2.85, 3.05, 3.25, 3.55, 3.7, 3.85];

        const int columns = 4;
        var rows = (times.Length + columns - 1) / columns;
        var sheet = new byte[w * columns * h * rows * 4];
        for (var i = 0; i < times.Length; i++)
        {
            scene.Render(front, back, card, times[i]);
            var left = (i % columns) * w;
            var top = (i / columns) * h;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    // Sobre un campo verde de mentira, para ver lo que tapa.
                    var at = ((y * w) + x) * 4;
                    var to = ((((top + y) * w * columns) + left + x) * 4);
                    var a = scene.Pixels[at + 3];
                    byte gb = (byte)(((x / 16) + (y / 16)) % 2 == 0 ? 0x58 : 0x68), gg = (byte)(gb + 0x40), gr = (byte)(gb - 0x10);
                    sheet[to] = (byte)(scene.Pixels[at] + (gb * (255 - a) / 255));
                    sheet[to + 1] = (byte)(scene.Pixels[at + 1] + (gg * (255 - a) / 255));
                    sheet[to + 2] = (byte)(scene.Pixels[at + 2] + (gr * (255 - a) / 255));
                    sheet[to + 3] = 255;
                }
            }
        }

        PngFile.Write(sheet, w * columns, h * rows, 1, Path.Combine(folder, "captura.png"));
    }
}
