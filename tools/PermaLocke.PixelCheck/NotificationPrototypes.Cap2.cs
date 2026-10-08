using PermaLocke.App.Views.Pixel;

namespace PermaLocke.PixelCheck;

/// <summary>The cap panel prototypes, second pass (2026-10-08): the same four ideas, with the detail of a finished piece.</summary>
public sealed partial class NotificationPrototypes
{
    private static readonly uint[] MedalHues = [0x4A6A8A, 0x8A4A3A, 0x4A7A5A, 0x7A5A8A, 0x8A7A3A, 0x3A7A7A];
    private const uint Brown = 0x3A2410;
    private const uint Silver = 0xB4B6C4;
    private const uint Leaf = 0x4C9A4C;

    // ---------------------------------------------------------------- pieces

    private static void Flourish(Pix p, int x, int y, int dx, int dy, uint colour, int alpha = 255)
    {
        string[] art = ["######", "#.....", "#.###.", "#.#...", "#.#.#.", "#.#..."];
        for (var gy = 0; gy < art.Length; gy++)
        {
            for (var gx = 0; gx < art[gy].Length; gx++)
            {
                if (art[gy][gx] == '#') p.Set(x + (dx * gx), y + (dy * gy), colour, alpha);
            }
        }
    }

    /// <summary>Darkened, uneven corner: paper that sat near a flame.</summary>
    private static void Scorch(Pix p, int x, int y, int w, int h, int corner, int seed)
    {
        var random = new Random(seed);
        var radius = 9;
        for (var j = 0; j < radius; j++)
        {
            for (var i = 0; i < radius; i++)
            {
                var d = Math.Sqrt((i * i) + (j * j)) + (random.NextDouble() * 2.2);
                if (d > radius) continue;
                var px = corner is 0 or 2 ? x + i : x + w - 1 - i;
                var py = corner is 0 or 1 ? y + j : y + h - 1 - j;
                p.Set(px, py, 0x5A3A18, (int)(150 * (1 - (d / radius))));
            }
        }
    }

    private static void Chip(Pix p, int x, int y, string text, uint fill, uint ink, uint rim)
    {
        var w = (text.Length * 4) + 3;
        p.Notched(x, y, w, 7, rim, 1);
        p.Notched(x + 1, y + 1, w - 2, 5, fill, 1);
        p.Small(text, x + 2, y + 1, ink);
    }

    /// <summary>A health bar with bevel and ticks, in the colours the game paints it.</summary>
    private static void Bar2(Pix p, int x, int y, int w, double share, uint fill, uint outline, uint groove)
    {
        p.Rect(x, y, w, 5, outline);
        p.Rect(x + 1, y + 1, w - 2, 3, groove);
        var f = share > 0 ? Math.Max(1, (int)((w - 2) * share)) : 0;
        p.Rect(x + 1, y + 1, f, 3, fill);
        p.HLine(x + 1, y + 1, f, Lighter(fill, 0.45));
        p.HLine(x + 1, y + 3, f, Darker(fill, 0.3));
        for (var t = x + 6; t < x + 1 + f; t += 5) p.Set(t, y + 2, Darker(fill, 0.22));
        p.Set(x + 2, y + 1, 0xFFFFFF, 190);
    }

    /// <summary>The Pokémon's window: a rimmed box with a dithered floor in its own tint, the icon half size and, at the cap, a gold star.</summary>
    private static void Medallion(Pix p, int x, int y, Pix? icon, bool fallen, int hue, uint rim, bool atCap)
    {
        p.Notched(x, y, 22, 17, rim, 2);
        var floor = fallen ? 0x2E2A28u : Mix(0x2A1C10, MedalHues[hue % 6], 0.42);
        p.Notched(x + 1, y + 1, 20, 15, floor, 2);
        for (var yy = 11; yy < 16; yy++)
        {
            for (var xx = 1; xx < 21; xx++)
            {
                if ((xx + yy) % 2 == 0 && (yy > 12 || xx % 2 == 0)) p.Set(x + xx, y + yy, Lighter(floor, 0.16));
            }
        }

        p.HLine(x + 2, y + 1, 18, Lighter(floor, 0.28));
        if (icon is not null)
        {
            var small = Half(icon);
            p.Blit(fallen ? Grey(small) : small, x + 1, y + 1, fallen ? 105 : 255);
        }

        if (fallen)
        {
            Line(p, x + 4, y + 3, x + 17, y + 14, 0xB8433A, 200);
            Line(p, x + 17, y + 3, x + 4, y + 14, 0xB8433A, 200);
        }

        if (atCap)
        {
            foreach (var (dx, dy) in new[] { (0, 0), (-1, 0), (1, 0), (0, -1), (0, 1) }) p.Set(x + 19 + dx, y + 1 + dy, Gold);
            p.Set(x + 19, y + 1, 0xFFF2B0);
        }
    }

    /// <summary>One Pokémon of the party in two lines: name and level, then HP bar and numbers.</summary>
    private static void Entry(Pix p, int x, int y, int w, CapMember m, int cap, Func<string, Pix?> icons, uint ink, uint dim, uint rim, uint groove, int index)
    {
        var fallen = m.Hp <= 0;
        var atCap = !fallen && m.Level >= cap;
        Medallion(p, x, y, icons(m.Icon), fallen, StableHash(m.Name) & 0x7F, rim, atCap);

        var tx = x + 25;
        var name = (m.Name.Length > 9 ? m.Name[..9] : m.Name).ToUpperInvariant();
        p.Small(name, tx, y + 1, fallen ? dim : ink);
        if (fallen) p.HLine(tx, y + 3, (name.Length * 4) - 1, HpRed, 220);

        var level = $"NV{m.Level}";
        var chipW = (level.Length * 4) + 3;
        Chip(p, x + w - chipW, y, level, atCap ? Gold : fallen ? Mix(dim, 0xE8DCC0, 0.55) : Mix(0xE8DCC0, dim, 0.25), atCap ? 0x4A2E00u : ink, atCap ? 0x8A5A10u : rim);

        var bar = w - 25 - 24;
        if (fallen)
        {
            p.Small("KO", tx, y + 10, HpRed);
            p.Small("SIN PS", tx + 11, y + 10, dim);
            return;
        }

        var share = m.Hp / (double)m.Max;
        Bar2(p, tx, y + 9, bar, share, share > 0.5 ? HpGreen : share > 0.2 ? HpAmber : HpRed, rim, groove);
        p.Small($"{m.Hp}/{m.Max}", tx + bar + 2, y + 10, dim);
    }

    /// <summary>Laurel branches on both sides of a point: the cap's crown.</summary>
    private static void Laurel(Pix p, double cx, double cy, double radius)
    {
        foreach (var side in new[] { -1, 1 })
        {
            for (var i = 0; i < 7; i++)
            {
                var a = Math.PI * (0.30 + (i * 0.11));
                var px = (int)Math.Round(cx + (side * radius * Math.Sin(a)));
                var py = (int)Math.Round(cy + (radius * Math.Cos(a)));
                p.Rect(px - 1, py, 3, 2, i % 2 == 0 ? Leaf : Darker(Leaf, 0.25));
                p.Set(px + (side * 2), py - 1, Lighter(Leaf, 0.3));
            }
        }
    }

    private static void Pennant(Pix p, int x, int y, uint colour)
    {
        p.VLine(x, y, 13, Brown);
        for (var i = 0; i < 5; i++) p.Rect(x + 1, y + 1 + i, 8 - (i * 2), 1, colour);
        p.Set(x + 1, y + 1, Lighter(colour, 0.4));
    }

    /// <summary>The team against the cap: a fuse with the level of the strongest in a chip over the flame and the cap on a pennant.</summary>
    private static void Gauge(Pix p, int x, int y, int w, CapSample s, uint rope, uint ash, uint chipFill, uint chipInk, uint label)
    {
        var atCap = s.Highest >= s.Cap;
        p.Small("NIVEL DEL EQUIPO", x, y, label);
        var share = Math.Clamp(s.Highest / (double)s.Cap, 0, 1);
        var span = w - 12;
        var tip = (int)Math.Round(span * share);
        var chip = $"NV {s.Highest}";
        Chip(p, Math.Clamp(x + tip - 9, x, x + span - 16), y + 8, chip, atCap ? Gold : chipFill, atCap ? 0x4A2E00u : chipInk, Brown);
        Rope(p, x, y + 18, span, share, atCap, rope, ash);
        for (var t = 0; t <= span; t += 6) p.VLine(x + t, y + 22, t % 12 == 0 ? 3 : 2, label, 140);
        Pennant(p, x + span + 3, y + 8, Gold);
        p.Small($"{s.Cap}", x + span - 1, y + 25, label);
    }

    private static void Divider(Pix p, int x, int y, int w, uint colour)
    {
        for (var i = x; i < x + w; i += 2) if (Math.Abs(i - (x + (w / 2))) > 5) p.Set(i, y, colour);
        var cx = x + (w / 2);
        p.Set(cx, y - 2, colour);
        p.Rect(cx - 1, y - 1, 3, 3, colour);
        p.Set(cx, y + 2, colour);
    }

    // ---------------------------------------------------------------- S: vertical scroll, detailed

    private static Pix CapScroll2(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 112, h = 248;
        var p = new Pix(w, h);
        var atCap = s.Highest >= s.Cap;

        // El papel: tonos, manchas, dos dobleces, esquinas chamuscadas y una guarda con florituras.
        Sheet(p, 7, 5, w - 14, h - 10, 3);
        foreach (var fy in new[] { 71, 138 })
        {
            p.HLine(8, fy, w - 16, 0x8A6A3A, 70);
            p.HLine(8, fy + 1, w - 16, 0xFFF4D8, 80);
        }

        Scorch(p, 8, 6, w - 16, h - 12, 0, 1);
        Scorch(p, 8, 6, w - 16, h - 12, 3, 2);
        Scorch(p, 8, 6, w - 16, h - 12, 1, 5);
        Flourish(p, 12, 28, 1, 1, Sepia, 200);
        Flourish(p, w - 13, 28, -1, 1, Sepia, 200);
        Flourish(p, 12, h - 31, 1, -1, Sepia, 200);
        Flourish(p, w - 13, h - 31, -1, -1, Sepia, 200);

        // Los rollos, con remates de latón y borlas.
        HRoll(p, 4, 0, w - 8, 10);
        HRoll(p, 4, h - 10, w - 8, 10);
        foreach (var ky in new[] { 5.0, h - 5.0 })
        {
            foreach (var kx in new[] { 2.0, w - 3.0 })
            {
                Disc(p, kx, ky, 3.2, 0x5A3A10);
                Disc(p, kx, ky, 2.4, Gold);
                p.Set((int)kx - 1, (int)ky - 1, 0xFFF2B0);
            }
        }

        // La cinta del título, con las orejas dobladas por detrás.
        p.Rect(4, 17, 6, 9, Darker(0xB8433A, 0.45));
        p.Rect(w - 10, 17, 6, 9, Darker(0xB8433A, 0.45));
        p.Rect(9, 12, w - 18, 11, 0xB8433A);
        p.HLine(9, 12, w - 18, Lighter(0xB8433A, 0.4));
        p.HLine(9, 22, w - 18, Darker(0xB8433A, 0.4));
        for (var i = 0; i < 3; i++)
        {
        }

        p.Text("Cap de nivel", (w / 2) - (PixelFont.Measure("Cap de nivel") / 2), 14, 0xFFF4D8, shadow: 0x5A1810);

        // La prueba, entre dos copas.
        var trial = s.Trial;
        var tw = (trial.Length * 4) - 1;
        Cup(p, (w / 2) - (tw / 2) - 13, 28, Gold);
        Cup(p, (w / 2) + (tw / 2) + 5, 28, Gold);
        p.Small(trial, (w / 2) - (tw / 2), 31, Ink);

        // El tope de ahora, en un sello grande con laurel, y el siguiente, en uno plateado más pequeño.
        Laurel(p, 32, 59, 17);
        Wax(p, 32, 59, 13.5, Gold, tails: false);
        Ring(p, 32, 59, 10.5, 9.8, Lighter(Gold, 0.5), 150);
        p.Small("NV", 29, 51, 0x4A2E00);
        p.Text(s.Cap.ToString(), 26, 58, 0xFFF4D8, shadow: 0x6A4A10);
        p.Small("AHORA", 24, 79, Sepia);


        p.Rect(52, 58, 15, 3, Sepia);
        for (var k = 0; k < 4; k++) p.Rect(66 + k, 56 + k, 1, 7 - (k * 2), Sepia);

        Wax(p, w - 32, 59, 10, Silver, tails: false);
        Ring(p, w - 32, 59, 7.5, 6.9, 0xFFFFFF, 130);
        p.Small("NV", w - 35, 53, Darker(Silver, 0.65));
        p.Text(s.Next.ToString(), w - 38, 58, 0x3A3A4A, shadow: 0xE8E8F0);
        p.Small("SIGUIENTE", w - 50, 73, Sepia);

        // La mecha del equipo.
        Divider(p, 12, 88, w - 24, Sepia);
        Gauge(p, 13, 93, w - 26, s, Ink, 0xB8A27A, 0xE8DCC0, Ink, Sepia);
        Divider(p, 12, 121, w - 24, Sepia);

        // El equipo, en filas con una franja alterna.
        p.Small("TU EQUIPO", (w / 2) - 17, 126, Sepia);
        for (var i = 0; i < s.Party.Length; i++)
        {
            var y = 135 + (i * 19);
            if (i % 2 == 0) p.Rect(10, y - 1, w - 20, 19, 0xA88850, 28);
            Entry(p, 13, y, w - 26, s.Party[i], s.Cap, icons, Ink, Sepia, Brown, 0x2A1C10, i);
        }

        _ = atCap;
        return p;
    }

    // ---------------------------------------------------------------- T: board, detailed

    private static Pix CapBoard2(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 112, h = 238;
        var p = new Pix(w, h);

        // Cuerdas con nudo.
        foreach (var rx in new[] { 16, w - 17 })
        {
            for (var y = 0; y < 10; y++)
            {
                p.Set(rx, y, (y / 2) % 2 == 0 ? 0xD8C080u : 0xA88850u);
                p.Set(rx + 1, y, 0x6A4A28);
            }

            Disc(p, rx + 0.5, 9, 2.4, 0x806030);
        }

        // El tablón: tres tablas con juntas, vetas, nudos y clavos con sombra.
        p.Notched(0, 8, w - 1, h - 10, 0x2E1A0A, 3);
        for (var plank = 0; plank < 3; plank++)
        {
            var py = 9 + (plank * ((h - 12) / 3));
            Wood(p, 1, py, w - 3, ((h - 12) / 3) - 1, plank % 2 == 0 ? 0xA8703Au : 0x9A6432u, 5 + plank);
            p.HLine(1, py + ((h - 12) / 3) - 1, w - 3, 0x2E1A0A);
        }

        foreach (var (kx, ky) in new[] { (76, 40), (20, 95), (70, 150), (34, 192) })
        {
            Ring(p, kx, ky, 4.5, 3.6, 0x5A3418, 200);
            Ring(p, kx, ky, 2.6, 1.8, 0x6A4220, 200);
            Disc(p, kx, ky, 1, 0x4A2A12);
        }

        p.Outline(3, 11, w - 7, h - 16, Darker(0xA8703A, 0.5));
        p.Outline(4, 12, w - 9, h - 18, Lighter(0xA8703A, 0.2), 110);
        foreach (var (nx, ny) in new[] { (6, 14), (w - 9, 14), (6, h - 10), (w - 9, h - 10) })
        {
            p.Rect(nx + 1, ny + 1, 3, 3, 0x2A1808, 140);
            p.Rect(nx, ny, 3, 3, 0x6A6270);
            p.Set(nx, ny, 0xE8E4F0);
        }

        // Una flor de hibisco y hojas en la esquina, como el cartel de Alola.
        foreach (var (dx, dy) in new[] { (0, -3), (3, -1), (2, 3), (-2, 3), (-3, -1) })
        {
            p.Rect(w - 15 + dx - 1, 19 + dy - 1, 3, 3, 0xD8483A);
            p.Set(w - 15 + dx, 19 + dy - 1, 0xF08A70);
        }

        p.Rect(w - 16, 18, 3, 3, 0xFFE070);
        for (var i = 0; i < 6; i++) p.Rect(w - 22 - i, 26 + (i / 2), 2, 2, i % 2 == 0 ? Leaf : Darker(Leaf, 0.3));

        // La nota del tope: papel con borde rasgado, cinta y chincheta.
        var note = new Pix(76, 56);
        Sheet(note, 0, 0, 76, 56, 8, false, false);
        for (var x = 0; x < 76; x++)
        {
            var tear = StableHash("t" + x) & 3;
            for (var j = 0; j < tear; j++) Erase(note, x, j);
        }

        note.Small(s.Trial, 38 - (((s.Trial.Length * 4) - 1) / 2), 8, Sepia);
        Cup(note, 6, 5, Gold);
        Cup(note, 61, 5, Gold);
        note.Small("TOPE DE NIVEL", 38 - 25, 16, Sepia);
        var cap = $"{s.Cap}";
        note.Text("NV", 14, 27, Sepia);
        note.Text(cap, 38 - 5, 26, Ink, scale: 1, shadow: 0xD8BE84, bold: true);
        for (var i = 0; i < 28; i++) note.Set(20 + i, 37 + (int)Math.Round(Math.Sin(i * 0.9)), 0xB8433A, 200);
        note.Small($"SIGUIENTE: NV {s.Next}", 38 - 31, 43, Sepia);
        for (var x = 6; x < 70; x += 2) note.Set(x, 51, 0xA88850, 160);
        p.Blit(Turned(note, -2), (w - 76) / 2, 24);
        p.Rect((w / 2) - 11, 22, 22, 7, 0xF0E4A0, 150);
        p.HLine((w / 2) - 11, 22, 22, 0xFFFFFF, 130);
        Disc(p, w - 26, 31, 3, 0x6A1A10);
        Disc(p, w - 26.5, 30.5, 2.2, 0xD84A3A);
        p.Set(w - 27, 29, 0xFFFFFF, 220);

        // La mecha, sobre una ranura tallada.
        p.Small("NIVEL DEL EQUIPO", 12, 88, 0xFFF0CC);
        p.Rect(10, 106, w - 20, 8, 0x2E1A0A);
        p.Rect(11, 107, w - 22, 6, 0x4A2E14);
        var share = Math.Clamp(s.Highest / (double)s.Cap, 0, 1);
        Rope(p, 14, 108, w - 38, share, s.Highest >= s.Cap, 0xF2DDA8, 0x7A5A38);
        Chip(p, Math.Clamp(14 + (int)((w - 38) * share) - 9, 12, w - 44), 96, $"NV {s.Highest}", s.Highest >= s.Cap ? Gold : 0xE8DCC0, 0x4A2E14, Brown);
        Pennant(p, w - 20, 96, Gold);
        p.Small($"{s.Cap}", w - 21, 116, 0xFFF0CC);

        // Un billete por Pokémon: talón con agujeritos, clavo y ligera inclinación.
        for (var i = 0; i < s.Party.Length; i++)
        {
            var y = 124 + (i * 21);
            var ticket = new Pix(w - 12, 20);
            Sheet(ticket, 0, 0, w - 12, 20, 20 + i, false, false);
            for (var k = 3; k < 18; k += 3) ticket.Rect(26, k, 1, 1, 0xA88850);
            Entry(ticket, 3, 1, w - 18, s.Party[i], s.Cap, icons, Ink, Sepia, Brown, 0x2A1C10, i);
            p.Blit(Turned(ticket, i % 2 == 0 ? 0.8 : -0.8), 6, y);
            p.Rect(w - 9, y + 3, 2, 2, 0x4A3018);
            p.Set(w - 9, y + 3, 0xD8C8A0);
        }

        return p;
    }

    // ---------------------------------------------------------------- U: notebook, detailed

    private static Pix CapNotebook2(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 124, h = 216;
        var p = new Pix(w, h);
        var blue = 0x2A3A8Au;
        var atCap = s.Highest >= s.Cap;

        // El cuero, con su pespunte y las esquineras de latón.
        p.Notched(0, 0, w - 1, h - 1, 0x1E100A, 4);
        GradNotched(p, 1, 1, w - 3, h - 3, 0x6A4228, 0x4A2C1A, 4);
        for (var x = 6; x < w - 7; x += 3)
        {
            p.Set(x, 3, 0xC8A070);
            p.Set(x, h - 5, 0xC8A070);
        }

        for (var y = 6; y < h - 7; y += 3)
        {
            p.Set(w - 6, y, 0xC8A070);
        }

        foreach (var (cx, cy, dx, dy) in new[] { (w - 3, 2, -1, 1), (w - 3, h - 4, -1, -1) })
        {
            for (var i = 0; i < 9; i++) p.Rect(cx + (dx * i), cy + (dy * 0), 1, 1, Gold);
            for (var i = 0; i < 9; i++)
            {
                for (var j = 0; j < 9 - i; j++) p.Set(cx + (dx * i), cy + (dy * j), j + i < 3 ? 0xFFF2B0u : Gold);
            }
        }

        // Las hojas: dos capas asoman por debajo, la de arriba con renglones, margen y agujeros.
        p.Rect(10, 7, w - 17, h - 12, 0xD8CCAA);
        p.Rect(9, 6, w - 17, h - 12, 0xE8DEC0);
        p.Rect(8, 5, w - 17, h - 11, 0xF6F0DE);
        for (var y = 5; y < h - 6; y++)
        {
            for (var x = 8; x < w - 9; x++)
            {
                var hash = StableHash($"u{x}:{y}") & 0x7FFFFFFF;
                if (hash % 53 == 0) p.Set(x, y, 0xE4D8B8);
            }
        }

        for (var y = 30; y < h - 10; y += 8) p.HLine(9, y, w - 19, 0x8AA4C8, 80);
        p.VLine(20, 5, h - 11, 0xD87870, 120);
        p.VLine(22, 5, h - 11, 0xD87870, 70);

        // La espiral: anillos con brillo y sombra, y el agujero de cada uno.
        for (var y = 9; y < h - 12; y += 12)
        {
            p.Rect(2, y + 1, 14, 3, 0x120A06, 130);
            p.Rect(1, y, 14, 3, 0x2A1810);
            p.Rect(2, y, 12, 2, 0xB4B4C0);
            p.Rect(2, y, 12, 1, 0xF0F0FA);
            Disc(p, 9.5, y + 1.5, 1.8, 0x14100C);
        }

        // El título a bolígrafo, subrayado con un garabato, y una estrella dibujada.
        Cup(p, 26, 9, 0xB8433A);
        p.Text("Cap de nivel", 38, 9, blue);
        for (var i = 0; i < 58; i++) p.Set(26 + i, 19 + (int)Math.Round(Math.Sin(i * 0.7) * 0.8), blue, 190);
        p.Small(s.Trial, 27, 23, 0x6A5A48);
        DrawMark(p, Mark.Star, 90, 22, 0xD8A83C);

        // El sello rojo del tope, torcido, con las manchas de la tinta.
        var stamp = new Pix(62, 30);
        stamp.Outline(0, 0, 62, 30, 0xB8433A);
        stamp.Outline(2, 2, 58, 26, 0xB8433A, 200);
        stamp.Small("TOPE ACTUAL", 31 - 21, 5, 0xB8433A);
        stamp.Text($"NV {s.Cap}", 31 - 16, 14, 0xB8433A, bold: true);
        var random = new Random(11);
        for (var i = 0; i < 90; i++) Erase(stamp, random.Next(62), random.Next(30));
        p.Blit(Turned(stamp, -3), 24, 34);

        // La nota amarilla pegada con el siguiente tope.
        var sticky = new Pix(26, 26);
        sticky.Rect(0, 0, 26, 26, 0xF4E070);
        sticky.Rect(0, 22, 26, 4, 0xE0C84C);
        sticky.HLine(0, 0, 26, 0xFFF2A0);
        sticky.Small("SIG.", 5, 3, 0x6A5A10);
        sticky.Text($"{s.Next}", 8, 11, 0x4A3A08);
        p.Blit(Turned(sticky, 4), w - 40, 36);
        p.Rect(w - 34, 34, 14, 4, 0x9AD0A0, 170);

        // La mecha y el equipo.
        Gauge(p, 25, 74, w - 40, s, blue, 0xB8A27A, 0xE8DCC0, blue, 0x6A5A48);

        for (var i = 0; i < s.Party.Length; i++)
        {
            var y = 106 + (i * 20);
            var member = s.Party[i];
            Entry(p, 24, y, w - 36, member, s.Cap, icons, blue, 0x6A5A48, 0x3A2410, 0x2A1C10, i);

            // La casilla a lápiz: tilde si está bien, aspa si ha caído, estrella si está en el tope.
            p.Outline(11, y + 5, 7, 7, 0x6A5A48);
            if (member.Hp <= 0)
            {
                Line(p, 12, y + 6, 16, y + 10, 0xB8433A);
                Line(p, 16, y + 6, 12, y + 10, 0xB8433A);
            }
            else if (member.Level >= s.Cap)
            {
                DrawMark(p, Mark.Star, 11, y + 5, 0xD8A83C);
            }
            else
            {
                Line(p, 12, y + 8, 14, y + 10, 0x2E8A4A);
                Line(p, 14, y + 10, 18, y + 4, 0x2E8A4A);
            }
        }

        // El punto de libro con borla, y la cinta washi.
        p.Rect(w - 17, 0, 6, 17, 0xB8433A);
        p.Rect(w - 17, 0, 2, 17, Lighter(0xB8433A, 0.3));
        p.Rect(w - 17, 14, 2, 3, 0xB8433A);
        p.Rect(w - 13, 14, 2, 3, 0xB8433A);
        p.Rect(w - 44, 31, 16, 5, 0xE8C0D0, 150);
        _ = atCap;
        return p;
    }

    // ---------------------------------------------------------------- V: tags, detailed

    private static Pix CapTags2(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 112, h = 226;
        var p = new Pix(w, h);

        // La cuerda trenzada, con los nudos de los extremos y una borla abajo.
        for (var y = 2; y < h - 8; y++)
        {
            var phase = (y / 2) % 2;
            p.Set(4, y, phase == 0 ? 0xD8C080u : 0xA88850u);
            p.Set(5, y, phase == 0 ? 0xA88850u : 0xD8C080u);
            p.Set(6, y, 0x6A4A28);
        }

        Disc(p, 5, 3, 3.4, 0x806030);
        Disc(p, 4.5, 2.5, 1.6, 0xC8A060);
        for (var i = 0; i < 7; i++) p.Rect(2 + i, h - 8 + (i % 2), 1, 7, i % 2 == 0 ? 0xC8A060u : 0x806030u);
        Disc(p, 5, h - 9, 2.6, 0x806030);

        void Tag(Pix t, int x, int y, int tw, int th, int hash, uint band)
        {
            Sheet(t, x, y, tw, th, hash, false, false);
            for (var k = 0; k < 5; k++)
            {
                for (var j = 0; j < 5 - k; j++)
                {
                    Erase(t, x + k, y + j);
                    Erase(t, x + k, y + th - 1 - j);
                }
            }

            t.Rect(x + 8, y + 1, 2, th - 2, band);
            t.Rect(x + 8, y + 1, 1, th - 2, Lighter(band, 0.35));
            Disc(t, x + 4.5, y + (th / 2.0), 2.8, 0x7A5A28);
            Disc(t, x + 4.5, y + (th / 2.0), 1.6, 0x2A1A0C);
            Ring(t, x + 4.5, y + (th / 2.0), 2.8, 2.2, Lighter(Gold, 0.2), 230);
        }

        // La etiqueta grande del tope: chapa de latón con el número y un sello.
        Tag(p, 9, 7, 91, 60, 4, 0xB8433A);
        p.Small("CAP DE NIVEL", 22, 11, Sepia);
        p.Small(s.Trial, 22, 18, Ink);
        Cup(p, 79, 10, Gold);
        p.Notched(22, 27, 42, 20, 0x3A2410, 3);
        p.Notched(23, 28, 40, 18, Gold, 3);
        p.VGrad(24, 29, 38, 16, Lighter(Gold, 0.35), Darker(Gold, 0.15));
        p.Small("NV", 27, 31, 0x4A2E00);
        p.Text($"{s.Cap}", 41, 31, 0x3A2410, shadow: Lighter(Gold, 0.5), bold: true);
        for (var x = 27; x < 60; x += 4) p.Set(x, 43, 0x8A5A10);
        p.Small($"SIGUIENTE: NV {s.Next}", 22, 52, Sepia);
        Wax(p, 82, 40, 9, 0xB8433A, tails: false);
        Ring(p, 82, 40, 6.5, 5.8, Lighter(0xB8433A, 0.4), 160);
        for (var x = 79; x <= 85; x++) p.Set(x, 40, 0x3A2410);
        p.Set(82, 39, Lighter(0xB8433A, 0.5));
        p.Set(82, 41, Lighter(0xB8433A, 0.5));

        // La mecha, colgada de la cuerda.
        Gauge(p, 14, 74, w - 22, s, 0xE8D8B0, 0x7A5A38, 0xE8DCC0, Ink, 0xE8D8B0);

        // Una etiqueta por Pokémon, cada una con su cinta de color y un hilo a la cuerda.
        for (var i = 0; i < s.Party.Length; i++)
        {
            var y = 108 + (i * 21);
            var band = MedalHues[(StableHash(s.Party[i].Name) & 0x7F) % 6];
            var tag = new Pix(w - 6, 20);
            Tag(tag, 0, 0, w - 8, 20, 30 + i, Mix(band, 0xFFFFFF, 0.2));
            Entry(tag, 13, 1, w - 26, s.Party[i], s.Cap, icons, Ink, Sepia, Brown, 0x2A1C10, i);
            p.Blit(Turned(tag, i % 2 == 0 ? 1.0 : -1.0), 8, y);
            p.Rect(5, y + 8, 5, 1, 0x806030);
        }

        return p;
    }
}
