using PermaLocke.App.Views.Pixel;

namespace PermaLocke.PixelCheck;

/// <summary>The cap panel prototypes, third pass (2026-10-08): four new ideas in the parchment family.</summary>
public sealed partial class NotificationPrototypes
{
    private static readonly string[] TrophyArt =
    [
        "#############",
        "#.#########.#",
        "#.#########.#",
        "#..#######..#",
        ".#.#######.#.",
        "..#.#####.#..",
        "....#####....",
        ".....###.....",
        "......#......",
        "......#......",
        ".....###.....",
        "...#######...",
        "...#######...",
        "..#########.."
    ];

    private static void Trophy(Pix p, int x, int y, uint colour)
    {
        for (var gy = 0; gy < TrophyArt.Length; gy++)
        {
            for (var gx = 0; gx < TrophyArt[gy].Length; gx++)
            {
                if (TrophyArt[gy][gx] != '#') continue;
                var shade = gx < 5 ? Lighter(colour, 0.35) : gx > 8 ? Darker(colour, 0.3) : colour;
                if (gy >= 11) shade = gy == 11 ? Darker(0x6A3A1C, 0.0) : Darker(0x6A3A1C, 0.25);
                p.Set(x + gx, y + gy, shade);
            }
        }

        p.Set(x + 3, y + 1, 0xFFF6C8);
        p.Set(x + 4, y + 2, 0xFFF6C8);
    }

    /// <summary>A wooden roll standing up, as on the notices: lit a little left of its middle, darker ends.</summary>
    private static void VRoll(Pix p, int x, int y, int w, int h)
    {
        for (var i = 0; i < w; i++)
        {
            var shade = Math.Round(Math.Clamp(Math.Abs(((i + 0.5) / w) - 0.38) * 2.2, 0, 1) * 3) / 3;
            for (var j = 0; j < h; j++)
            {
                if ((i == 0 || i == w - 1) && (j == 0 || j == h - 1)) continue;
                var colour = Mix(0xF2E2B4, 0x8E6A3A, shade);
                if (j < 3 || j >= h - 3) colour = Mix(colour, Brown, 0.4 + (shade * 0.2));
                else if (i == w / 2 && (j / 3) % 2 == 0) colour = Mix(colour, Brown, 0.22);
                if (i == 0 || i == w - 1 || j == 0 || j == h - 1) colour = Brown;
                p.Set(x + i, y + j, colour);
            }
        }
    }

    /// <summary>A brass plate screwed on: bevel, sheen and a screw at each end.</summary>
    private static void Brass(Pix p, int x, int y, int w, int h)
    {
        p.Notched(x + 1, y + 1, w, h, 0x1A0A04, 2, 120);
        p.Notched(x, y, w, h, 0x5A3A10, 2);
        GradNotched(p, x + 1, y + 1, w - 2, h - 2, 0xF4D88A, 0xA87828, 1);
        p.HLine(x + 2, y + 1, w - 4, 0xFFF2C0);
        p.HLine(x + 2, y + h - 2, w - 4, 0x7A5418);
        for (var i = 0; i < h - 4; i++) p.Set(x + 8 + i, y + 2 + i, 0xFFFFFF, 40);
        foreach (var sx in new[] { x + 3, x + w - 4 })
        {
            var sy = y + (h / 2.0);
            Disc(p, sx + 0.5, sy, 1.6, 0x6A5030);
            p.Set(sx, (int)sy, 0x2A1A08);
            p.Set(sx - 1, (int)sy, 0x2A1A08);
        }
    }

    private static int TrialNumber(CapSample s) =>
        int.TryParse(s.Trial.Split(' ').ElementAtOrDefault(1), out var n) ? n : 1;

    // ---------------------------------------------------------------- W: the route map

    private static Pix CapMap(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 112, h = 262;
        var p = new Pix(w, h);
        var current = TrialNumber(s) - 1;

        Sheet(p, 4, 3, w - 8, h - 6, 12);
        Scorch(p, 5, 4, w - 10, h - 8, 0, 21);
        Scorch(p, 5, 4, w - 10, h - 8, 1, 22);
        Scorch(p, 5, 4, w - 10, h - 8, 2, 23);
        Scorch(p, 5, 4, w - 10, h - 8, 3, 24);

        // Cabecera y rosa de los vientos.
        p.Small("RUTA DE LA LIGA", 11, 10, Sepia);
        p.Small("CAP DE NIVEL", 11, 17, Ink);
        var (rx, ry) = (w - 18, 17);
        for (var i = -7; i <= 7; i++)
        {
            p.Set(rx + i, ry, i == 0 ? 0xB8433Au : Sepia, 220);
            p.Set(rx, ry + i, i < 0 ? 0xB8433Au : Sepia, 220);
        }

        for (var i = -4; i <= 4; i++)
        {
            p.Set(rx + i, ry + i, Sepia, 120);
            p.Set(rx + i, ry - i, Sepia, 120);
        }

        Ring(p, rx + 0.5, ry + 0.5, 5.2, 4.5, Sepia, 130);
        p.Small("N", rx - 1, ry - 14, 0xB8433A);

        // El mapa: mar con olas, islas con montes y palmeras, y el camino de las doce pruebas.
        const int top = 28, bottom = 98;
        for (var y = top; y < bottom; y += 5)
        {
            for (var x = 10; x < w - 10; x++)
            {
                if (((x + (y * 3)) / 3) % 4 == 0 && Math.Sin((x * 0.5) + y) > 0.2) p.Set(x, y + (int)Math.Round(Math.Sin(x * 0.6)), 0x6A8AB0, 80);
            }
        }

        foreach (var (ix, iy, iw, ih) in new[] { (12, 30, 40, 14), (60, 50, 42, 15), (14, 72, 36, 16), (66, 80, 34, 12) })
        {
            for (var y = 0; y < ih; y++)
            {
                for (var x = 0; x < iw; x++)
                {
                    var dx = (x - (iw / 2.0)) / (iw / 2.0);
                    var dy = (y - (ih / 2.0)) / (ih / 2.0);
                    var d = (dx * dx) + (dy * dy) + (Math.Sin((x * 0.9) + y) * 0.08);
                    if (d < 1) p.Set(ix + x, iy + y, d > 0.82 ? 0xA88850u : 0xE2CC94u, d > 0.82 ? 200 : 140);
                }
            }
        }

        void Mountain(int mx, int my)
        {
            for (var i = 0; i < 5; i++)
            {
                p.Set(mx - i, my + i, Sepia);
                p.Set(mx + i, my + i, Darker(Sepia, 0.2));
                if (i > 1) p.HLine(mx - i + 1, my + i, i, 0xB8A070, 120);
            }
        }

        void Palm(int px, int py)
        {
            p.Set(px, py, 0x6A4A28);
            p.Set(px, py + 1, 0x6A4A28);
            p.Set(px + 1, py + 2, 0x6A4A28);
            p.Set(px + 1, py + 3, 0x6A4A28);
            foreach (var (dx, dy) in new[] { (-2, -1), (-1, -1), (1, -1), (2, -1), (-3, 0), (3, 0), (0, -2) }) p.Set(px + dx, py + dy, Leaf);
        }

        Mountain(22, 31);
        Mountain(28, 33);
        Palm(46, 34);
        Mountain(88, 51);
        Palm(66, 53);
        Palm(40, 76);
        Mountain(86, 80);

        var nodes = new List<(int X, int Y)>();
        for (var row = 0; row < 3; row++)
        {
            for (var k = 0; k < 4; k++)
            {
                var column = row % 2 == 0 ? k : 3 - k;
                nodes.Add((18 + (column * ((w - 36) / 3)), 38 + (row * 22)));
            }
        }

        for (var i = 0; i < nodes.Count - 1; i++)
        {
            var (ax, ay) = nodes[i];
            var (bx, by) = nodes[i + 1];
            var steps = Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay));
            for (var t = 0; t <= steps; t += 3)
            {
                var x = ax + ((bx - ax) * t / Math.Max(1, steps));
                var y = ay + ((by - ay) * t / Math.Max(1, steps));
                if (ay == by) y += (int)Math.Round(Math.Sin(t * 0.25) * 1.5);
                p.Set(x, y, i < current ? 0xB8433Au : Ink, i < current ? 230 : 170);
            }
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var (nx, ny) = nodes[i];
            if (i == nodes.Count - 1)
            {
                Cup(p, nx - 4, ny - 4, Gold);
            }
            else if (i < current)
            {
                Line(p, nx - 2, ny - 2, nx + 2, ny + 2, 0xB8433A);
                Line(p, nx + 2, ny - 2, nx - 2, ny + 2, 0xB8433A);
            }
            else if (i == current)
            {
                Ring(p, nx + 0.5, ny + 0.5, 4.6, 3.4, 0xB8433A);
                Disc(p, nx + 0.5, ny + 0.5, 1.4, 0xB8433A);
                Pennant(p, nx + 4, ny - 13, 0xB8433A);
                Chip(p, nx + 6, ny - 6, $"NV{s.Cap}", Gold, 0x4A2E00, Brown);
            }
            else
            {
                Ring(p, nx + 0.5, ny + 0.5, 2.4, 1.4, Ink, 200);
            }
        }

        p.Outline(9, top - 1, w - 18, bottom - top + 2, Sepia, 150);

        // La leyenda: el tope en su sello y el siguiente.
        Wax(p, 22, 112, 10, Gold, tails: false);
        p.Small("NV", 19, 104, 0x4A2E00);
        p.Text($"{s.Cap}", 17, 109, 0xFFF4D8, shadow: 0x6A4A10);
        p.Small("TOPE AHORA", 37, 103, Sepia);
        p.Small(s.Trial, 37, 110, Ink);
        p.Small($"SIGUIENTE: NV {s.Next}", 37, 117, Sepia);

        Divider(p, 10, 127, w - 20, Sepia);
        Gauge(p, 12, 131, w - 24, s, Ink, 0xB8A27A, 0xE8DCC0, Ink, Sepia);
        Divider(p, 10, 160, w - 20, Sepia);

        for (var i = 0; i < s.Party.Length; i++)
        {
            var y = 166 + (i * 18);
            Entry(p, 12, y, w - 24, s.Party[i], s.Cap, icons, Ink, Sepia, Brown, 0x2A1C10, i);
        }

        return p;
    }

    // ---------------------------------------------------------------- X: the League's edict

    private static Pix CapEdict(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 112, h = 292;
        var p = new Pix(w, h);
        var red = 0x8A2A1Au;
        var paperBottom = h - 22;

        Sheet(p, 4, 2, w - 8, paperBottom, 31);
        Scorch(p, 5, 3, w - 10, paperBottom - 2, 0, 41);
        Scorch(p, 5, 3, w - 10, paperBottom - 2, 3, 42);
        p.Outline(9, 8, w - 18, paperBottom - 12, red, 200);
        p.Outline(11, 10, w - 22, paperBottom - 16, red, 110);
        Flourish(p, 13, 12, 1, 1, red, 200);
        Flourish(p, w - 14, 12, -1, 1, red, 200);
        Flourish(p, 13, paperBottom - 7, 1, -1, red, 200);
        Flourish(p, w - 14, paperBottom - 7, -1, -1, red, 200);

        // EDICTO, en grande, y quien lo firma.
        var title = "EDICTO";
        p.Text(title, (w / 2) - PixelFont.Measure(title), 15, red, scale: 2, shadow: 0xD8BE84);
        var sub = "DE LA LIGA POKEMON";
        p.Small(sub, (w / 2) - (((sub.Length * 4) - 1) / 2), 36, Sepia);
        Divider(p, 16, 45, w - 32, red);

        // El texto del bando, con el número enmarcado.
        void Centred(string text, int y, uint colour) => p.Small(text, (w / 2) - (((text.Length * 4) - 1) / 2), y, colour);
        Centred("NINGUN POKEMON", 50, Ink);
        Centred("PASARA DEL", 57, Ink);
        p.Outline(30, 65, w - 60, 32, red);
        p.Outline(32, 67, w - 64, 28, red, 140);
        Centred("NIVEL", 70, red);
        var number = $"{s.Cap}";
        p.Text(number, (w / 2) - PixelFont.Measure(number), 77, red, scale: 2, shadow: 0xD8BE84);
        Centred("HASTA SUPERAR LA", 101, Ink);
        Centred(s.Trial, 108, Ink);
        Centred($"DESPUES: NIVEL {s.Next}", 117, Sepia);


        Gauge(p, 16, 126, w - 32, s, Ink, 0xB8A27A, 0xE8DCC0, Ink, Sepia);
        Centred("INSCRITOS", 157, red);

        for (var i = 0; i < s.Party.Length; i++)
        {
            Entry(p, 15, 166 + (i * 18), w - 30, s.Party[i], s.Cap, icons, Ink, Sepia, Brown, 0x2A1C10, i);
        }

        // El gran sello de lacre, colgando del borde con sus dos cintas.
        for (var y = paperBottom - 4; y < h - 1; y++)
        {
            var sway = (y - paperBottom) / 5;
            p.Rect((w / 2) - 9 - sway, y, 4, 1, red);
            p.Rect((w / 2) + 5 + sway, y, 4, 1, Darker(red, 0.3));
        }

        Wax(p, w / 2.0, paperBottom - 2, 12, 0xB8433A, tails: false);
        Ring(p, w / 2.0, paperBottom - 2, 9, 8.2, Lighter(0xB8433A, 0.35), 170);
        Cup(p, (w / 2) - 4, paperBottom - 7, Darker(0xB8433A, 0.45));
        return p;
    }

    // ---------------------------------------------------------------- Y: the horizontal scroll, like the notices

    private static Pix CapRibbon(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 164, h = 80;
        var p = new Pix(w, h);
        var atCap = s.Highest >= s.Cap;

        p.Rect(4, 6, w - 6, h - 8, 0x000000, 120);
        Sheet(p, 6, 3, w - 12, h - 6, 51, false);
        VRoll(p, 0, 0, 9, h - 2);
        VRoll(p, w - 10, 0, 9, h - 2);
        for (var y = 3; y < h - 4; y++)
        {
            p.Set(9, y, Darker(0xD8BE84, 0.3));
            p.Set(w - 11, y, Darker(0xD8BE84, 0.3));
        }

        // El tope en su sello con laurel.
        Laurel(p, 27, 24, 15);
        Wax(p, 27, 24, 12, Gold, tails: false);
        Ring(p, 27, 24, 9.2, 8.5, Lighter(Gold, 0.5), 150);
        p.Small("NV", 24, 16, 0x4A2E00);
        p.Text($"{s.Cap}", 21, 22, 0xFFF4D8, shadow: 0x6A4A10);

        p.Small("CAP DE NIVEL", 47, 8, Sepia);
        p.Small(s.Trial, 47, 15, Ink);
        p.Small($"SIGUIENTE: NV {s.Next}", 47, 22, Sepia);

        p.Small("EQUIPO", w - 50, 8, Sepia);
        p.Small($"{s.Highest}/{s.Cap}", w - 50, 15, atCap ? 0xB0661Cu : Ink);
        Rope(p, 47, 31, w - 74, Math.Clamp(s.Highest / (double)s.Cap, 0, 1), atCap, Ink, 0xB8A27A);
        Pennant(p, w - 24, 23, Gold);

        Divider(p, 14, 40, w - 28, Sepia);

        // El equipo, en una fila de ventanitas con su barra y su nivel.
        for (var i = 0; i < s.Party.Length; i++)
        {
            var m = s.Party[i];
            var x = 14 + (i * 25);
            var fallen = m.Hp <= 0;
            var atTop = !fallen && m.Level >= s.Cap;
            Medallion(p, x, 45, icons(m.Icon), fallen, StableHash(m.Name) & 0x7F, Brown, atTop);
            if (fallen)
            {
                p.Small("KO", x + 7, 64, HpRed);
            }
            else
            {
                var share = m.Hp / (double)m.Max;
                Bar2(p, x, 63, 22, share, share > 0.5 ? HpGreen : share > 0.2 ? HpAmber : HpRed, Brown, 0x2A1C10);
            }

            var level = $"NV{m.Level}";
            p.Small(level, x + 11 - (((level.Length * 4) - 1) / 2), 70, fallen ? Sepia : atTop ? 0xB0661Cu : Ink);
        }

        // Los huecos vacíos del equipo, punteados.
        for (var i = s.Party.Length; i < 6; i++)
        {
            var x = 14 + (i * 25);
            for (var k = 0; k < 22; k += 2)
            {
                p.Set(x + k, 45, Sepia, 150);
                p.Set(x + k, 61, Sepia, 150);
            }

            for (var k = 0; k < 17; k += 2)
            {
                p.Set(x, 45 + k, Sepia, 150);
                p.Set(x + 21, 45 + k, Sepia, 150);
            }
        }

        return p;
    }

    // ---------------------------------------------------------------- Z: the trophy plaque

    private static Pix CapPlaque(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 112, h = 240;
        var p = new Pix(w, h);
        const int top = 14, point = 30;
        var engraved = 0x3A2808u;
        var shine = 0xFFF2C0u;

        // El escudo de nogal: vetas, filo claro arriba e izquierda, punta abajo.
        Wood(p, 0, top, w, h - top, 0x5A3018, 9);
        for (var y = top; y < h; y++)
        {
            var inset = y > h - point ? (int)Math.Round((y - (h - point)) * (w / 2.0) / point) : 0;
            for (var x = 0; x < w; x++)
            {
                var outside = x < inset || x >= w - inset || ((y == top) && (x < 3 || x >= w - 3));
                if (outside) Erase(p, x, y);
                else if (x == inset || x == w - 1 - inset || y == top) p.Set(x, y, 0x1A0A04);
                else if (x == inset + 1 || y == top + 1) p.Set(x, y, 0x8A5A30);
                else if (x == w - 2 - inset) p.Set(x, y, 0x2A1408);
            }
        }

        // La copa de latón, atornillada encima.
        Trophy(p, (w / 2) - 6, 1, Gold);
        p.Rect((w / 2) - 9, 15, 19, 3, 0x5A3A10);
        p.HLine((w / 2) - 8, 15, 17, Gold);

        // La placa grande grabada.
        Brass(p, 10, 22, w - 20, 40);
        var head = "CAP DE NIVEL";
        p.Small(head, (w / 2) - (((head.Length * 4) - 1) / 2), 26, engraved);
        p.Small(head, (w / 2) - (((head.Length * 4) - 1) / 2) + 1, 27, shine);
        p.Small(head, (w / 2) - (((head.Length * 4) - 1) / 2), 26, engraved);
        p.Small("NV", 22, 42, engraved);
        p.Text($"{s.Cap}", 32, 35, engraved, scale: 2, shadow: shine);
        p.Small("PRUEBA " + TrialNumber(s), 64, 36, engraved);
        p.Small(s.Trial[(s.Trial.IndexOf(" DE ", StringComparison.Ordinal) + 1)..], 64, 43, engraved);
        p.Small($"SIG: {s.Next}", 64, 52, engraved);

        // La mecha, sobre la madera.
        Gauge(p, 14, 68, w - 28, s, 0xF2DDA8, 0x7A5A38, 0xF4D88A, engraved, 0xE8D0A0);

        // Una placa por Pokémon.
        for (var i = 0; i < s.Party.Length; i++)
        {
            var y = 98 + (i * 21);
            Brass(p, 8, y, w - 16, 20);
            Entry(p, 13, y + 2, w - 28, s.Party[i], s.Cap, icons, engraved, 0x6A4A18, 0x5A3A10, 0x3A2808, i);
        }

        return p;
    }
}
