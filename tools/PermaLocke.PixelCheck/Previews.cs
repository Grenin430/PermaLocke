using System.IO.Compression;
using System.Windows.Media;
using PermaLocke.App.Views;

namespace PermaLocke.PixelCheck;

/// <summary>
/// Pictures of the album to look at (§187): with <c>PERMALOCKE_PIXEL_DIR</c> set, the spread in both sizes, a page
/// turning, a card of every finish with its back, and the card in the hand in several poses, as PNG.
/// </summary>
/// <remarks>
/// The creatures are drawn here by hand: the icons of the cartridge are not in the repository, and a preview must not
/// need the player's ROM. Without the variable nothing is written and the test passes.
/// </remarks>
public sealed class Previews
{
    private static RoomSprite Creature(Color body, Color belly, Color dark, bool wings)
    {
        const int w = 40, h = 30;
        var px = new byte[w * h * 4];

        void Put(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            var a = ((y * w) + x) * 4;
            px[a] = c.B;
            px[a + 1] = c.G;
            px[a + 2] = c.R;
            px[a + 3] = 255;
        }

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                double dx = (x - 20) / 9.0, dy = (y - 18) / 9.0;
                if ((dx * dx) + (dy * dy) <= 1) Put(x, y, ((x - 20) * (x - 20) / 16.0) + ((y - 21) * (y - 21) / 25.0) <= 1 ? belly : body);
                double hx = (x - 20) / 6.0, hy = (y - 7) / 5.0;
                if ((hx * hx) + (hy * hy) <= 1) Put(x, y, body);
                if (wings && y > 8 && y < 20 && ((x < 12 && x > 4 + ((y - 8) / 2)) || (x > 28 && x < 36 - ((y - 8) / 2)))) Put(x, y, dark);
            }
        }

        Put(17, 6, Color.FromRgb(20, 20, 30));
        Put(23, 6, Color.FromRgb(20, 20, 30));
        return new RoomSprite(px, w, h);
    }

    private static readonly RoomSprite Dragon = Creature(Color.FromRgb(0xF0, 0x98, 0x40), Color.FromRgb(0xF8, 0xE0, 0xA0), Color.FromRgb(0x30, 0x80, 0x78), true);
    private static readonly RoomSprite Fish = Creature(Color.FromRgb(0x50, 0x90, 0xE0), Color.FromRgb(0xE0, 0xF0, 0xFF), Color.FromRgb(0x20, 0x40, 0x90), false);
    private static readonly RoomSprite Leaf = Creature(Color.FromRgb(0x60, 0xB0, 0x50), Color.FromRgb(0xD0, 0xF0, 0xA0), Color.FromRgb(0x20, 0x60, 0x30), true);
    private static readonly RoomSprite Ghost = Creature(Color.FromRgb(0x80, 0x60, 0xB0), Color.FromRgb(0xC0, 0xA8, 0xE8), Color.FromRgb(0x40, 0x28, 0x60), false);

    private static TcgCard Card(string name, int[] types, RoomSprite sprite, int rarity, bool gacha, bool shiny, bool fallen, uint seed) => new(
        name, name, 149, "Fase 2", 55, 182, types,
        [new("Lanzallamas", 9, 90, 100, 15, "Especial"), new("Puño Trueno", 12, 75, 100, 15, "Físico"),
         new("Danza Dragón", 15, 0, 0, 20, "Estado"), new("Golpe Cuerpo", 0, 85, 100, 15, "Físico")],
        "Intimidación", "Firme", 1, 3, "Restos", "Colina del Recuerdo", 12, rarity, gacha, shiny, false, fallen, sprite,
        [182, 204, 128, 95, 110, 132], [31, 20, 5, 31, 14, 28], [0, 252, 0, 0, 4, 252], seed);

    [Fact]
    public void Writes_the_pictures_when_asked()
    {
        var folder = Environment.GetEnvironmentVariable("PERMALOCKE_PIXEL_DIR");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        RoomSprite[] zoo = [Dragon, Fish, Leaf, Ghost];

        foreach (var layout in new[] { TcgLayout.Full, TcgLayout.Mini })
        {
            var scene = new AlbumScene(layout);
            TcgCard?[] Page(int offset) =>
            [
                .. Enumerable.Range(0, scene.PocketsPerPage).Select(i => (i + offset) % 5 == 3 ? null
                    : Card($"Poke{i}", [((i * 5) + offset) % 18], zoo[(i + offset) % 4], i % 5, i % 4 == 0, i == 2, i == 6, (uint)((i * 13) + offset)))
            ];

            AlbumTab[] tabs = [new("EQ", true), new("1", false), new("2", false), new("12", false)];
            var spread = new AlbumSpread(new AlbumPage(Page(0), "CAJA 1 · CAMPO DE FLORES", 1), new AlbumPage(Page(9), "", 2), layout, 1, tabs, 1);
            scene.Render(spread, 0.4, hover: 4, hoverTab: 2);
            Png(scene.Canvas, 2, Path.Combine(folder, $"album-{layout}.png"));

            var from = new AlbumSpread(new AlbumPage(Page(20), "EQUIPO", 1), new AlbumPage(Page(30), "", 2), layout, 0, tabs, 0);
            scene.Render(spread, 0.4, turn: new AlbumTurn(from, true, 0.32));
            Png(scene.Canvas, 2, Path.Combine(folder, $"album-{layout}-turn.png"));
        }

        // Una carta de cada acabado, por delante y por detrás.
        var cards = new[]
        {
            Card("Común", [0], Fish, 0, false, false, false, 3),
            Card("Holo", [9], Dragon, 2, false, false, false, 5),
            Card("Inversa", [7, 3], Ghost, 3, false, false, false, 6),
            Card("Dorada", [15, 2], Dragon, 4, true, false, false, 7),
            Card("Variocolor", [10], Fish, 1, false, true, false, 11),
            Card("Caída", [11, 7], Leaf, 2, false, false, true, 23)
        };

        var sheet = new CellCanvas((cards.Length * 76) + 6, 208);
        sheet.Rect(0, 0, sheet.Width, sheet.Height, Color.FromRgb(0x1A, 0x14, 0x28));
        for (var i = 0; i < cards.Length; i++)
        {
            var front = TcgCardArt.Render(cards[i], TcgLayout.Full);
            sheet.Stamp(front.Canvas, 4 + (i * 76), 4);
            TcgCardArt.Animate(front, sheet, 4 + (i * 76), 4, 1.1, cards[i].Seed, 0.45);
            sheet.Stamp(TcgCardArt.Render(cards[i], TcgLayout.Full, back: true).Canvas, 4 + (i * 76), 106);
        }

        Png(sheet, 3, Path.Combine(folder, "cards.png"));

        // La carta en la mano: en reposo, inclinada, de canto, vuelta y volando.
        HandPose[] poses =
        [
            new(0, 0, 0, 4, 200, 230, 0.3),
            new(0.38, -0.22, 0, 4, 200, 230, 0.5),
            new(1.2, 0.05, 0, 4, 200, 230, 0.8),
            new(Math.PI - 0.25, 0.1, 0, 4, 200, 230, 0.4),
            new(0.5, 0.2, 0.3, 2.2, 150, 280, 1)
        ];

        foreach (var card in new[] { cards[3], cards[4], cards[5] })
        {
            var front = TcgCardArt.Render(card, TcgLayout.Full);
            var back = TcgCardArt.Render(card, TcgLayout.Full, back: true);
            var montage = new CellCanvas(poses.Length * 400, 460);
            montage.Rect(0, 0, montage.Width, montage.Height, Color.FromRgb(0x12, 0x0C, 0x20));

            for (var i = 0; i < poses.Length; i++)
            {
                var hand = new HandScene(400, 460);
                hand.Render(front, back, poses[i], 1.3 + (i * 0.4), card.Seed, i == 4 ? 0.2 : 99);
                for (var y = 0; y < 460; y++)
                {
                    for (var x = 0; x < 400; x++)
                    {
                        var a = ((y * 400) + x) * 4;
                        var alpha = hand.Pixels[a + 3] / 255.0;
                        var under = montage.At((i * 400) + x, y);
                        montage.Put((i * 400) + x, y, Color.FromRgb(
                            (byte)(hand.Pixels[a + 2] + (under.R * (1 - alpha))),
                            (byte)(hand.Pixels[a + 1] + (under.G * (1 - alpha))),
                            (byte)(hand.Pixels[a] + (under.B * (1 - alpha)))));
                    }
                }
            }

            Png(montage, 1, Path.Combine(folder, $"hand-{card.Name}.png"));
        }
    }

    /// <summary>A canvas as PNG, each cell as <paramref name="scale"/> × <paramref name="scale"/> pixels.</summary>
    private static void Png(CellCanvas canvas, int scale, string path)
    {
        int w = canvas.Width * scale, h = canvas.Height * scale;
        var raw = new byte[h * ((w * 4) + 1)];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var colour = canvas.At(x / scale, y / scale);
                var o = (y * ((w * 4) + 1)) + 1 + (x * 4);
                raw[o] = colour.R;
                raw[o + 1] = colour.G;
                raw[o + 2] = colour.B;
                raw[o + 3] = colour.A;
            }
        }

        using var file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BigEndian(header, 0, w);
        BigEndian(header, 4, h);
        header[8] = 8;
        header[9] = 6;
        Chunk(file, "IHDR", header);
        using var packed = new MemoryStream();
        using (var zlib = new ZLibStream(packed, CompressionLevel.Optimal, true))
        {
            zlib.Write(raw);
        }

        Chunk(file, "IDAT", packed.ToArray());
        Chunk(file, "IEND", []);
    }

    private static void BigEndian(byte[] bytes, int at, int value)
    {
        bytes[at] = (byte)(value >> 24);
        bytes[at + 1] = (byte)(value >> 16);
        bytes[at + 2] = (byte)(value >> 8);
        bytes[at + 3] = (byte)value;
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        BigEndian(length, 0, data.Length);
        stream.Write(length);
        var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(body);
        var crc = new byte[4];
        BigEndian(crc, 0, (int)Crc(body));
        stream.Write(crc);
    }

    private static uint Crc(byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            c ^= b;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
        }

        return c ^ 0xFFFFFFFF;
    }
}
