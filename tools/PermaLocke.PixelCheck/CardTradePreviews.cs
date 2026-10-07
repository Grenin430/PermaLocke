using PermaLocke.App.Views;

namespace PermaLocke.PixelCheck;

/// <summary>
/// The card trade of the album (1.0.4.7): every moment draws, the screen is covered, and with <c>PERMALOCKE_PIXEL_DIR</c>
/// set, a sheet of frames as PNG.
/// </summary>
public sealed class CardTradePreviews
{
    private static TcgCard Card(string name, int species, int rarity, bool shiny, int type) => new(
        name, name, species, "Básico", 20, 60, [type],
        [new("Placaje", 0, 40, 100, 35, "Físico"), new("Gruñido", 0, 0, 100, 40, "Estado")],
        "Espesura", "Firme", 1, 3, "", "Ruta 1", 20, rarity, false, shiny, false, false, Previews.Dragon,
        [60, 50, 40, 30, 40, 50], [31, 20, 5, 31, 14, 28], [0, 0, 0, 0, 0, 0], (uint)species);

    [Fact]
    public void Every_moment_draws_and_covers_the_screen()
    {
        foreach (var (width, height) in new[] { (900, 560), (1400, 860), (500, 320) })
        {
            var scene = new CardTradeScene(width, height);
            var a = TcgCardArt.Render(Card("Pikipek", 731, 0, false, 0), TcgLayout.Full);
            var result = TcgCardArt.Render(Card("Lucario", 448, 3, true, 1), TcgLayout.Full);
            var back = TcgCardArt.RenderBack(TcgLayout.Full);

            for (var t = 0.0; t < CardTradeTimeline.Length + 0.5; t += 1 / 30.0)
            {
                scene.Render(a, back, result, 3, true, 5, t);
                Assert.All(scene.Pixels.Where((_, i) => i % 4 == 3), alpha => Assert.Equal(255, alpha));
            }
        }
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
        const int w = 900, h = 560;
        var scene = new CardTradeScene(w, h);
        var a = TcgCardArt.Render(Card("Pikipek", 731, 0, false, 0), TcgLayout.Full);
        var result = TcgCardArt.Render(Card("Lucario", 448, 3, true, 1), TcgLayout.Full);
        var back = TcgCardArt.RenderBack(TcgLayout.Full);
        double[] times = [0.3, 0.9, 1.5, 2.2, 2.8, 3.25, 3.45, 3.6, 3.9, 4.4, 5.0, 5.5, 5.9, 6.2, 6.6, 7.4];

        const int columns = 4;
        var rows = (times.Length + columns - 1) / columns;
        var sheet = new byte[w * columns * h * rows * 4];
        for (var i = 0; i < times.Length; i++)
        {
            scene.Render(a, back, result, 3, true, 5, times[i]);
            var left = (i % columns) * w;
            var top = (i / columns) * h;
            for (var y = 0; y < h; y++)
            {
                Buffer.BlockCopy(scene.Pixels, y * w * 4, sheet, (((top + y) * w * columns) + left) * 4, w * 4);
            }
        }

        PngFile.Write(sheet, w * columns, h * rows, 1, Path.Combine(folder, "intercambio.png"));
    }
}
