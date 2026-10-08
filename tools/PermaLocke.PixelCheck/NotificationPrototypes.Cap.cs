using PermaLocke.App.Views.Pixel;

namespace PermaLocke.PixelCheck;

/// <summary>Prototypes of the level cap panel (2026-10-08), in the family of the parchment notices: four ways to hang it.</summary>
public sealed partial class NotificationPrototypes
{
    private sealed record CapMember(string Name, int Level, int Hp, int Max, string Icon);

    private sealed record CapSample(string Trial, int Cap, int Next, int Highest, CapMember[] Party);

    private delegate Pix CapLook(CapSample sample, Func<string, Pix?> icons);

    private static readonly CapSample CapDemo = new("PRUEBA 3 DE 12", 24, 26, 22,
    [
        new("Pikachu", 22, 48, 52, "icon0025"),
        new("Charizard", 21, 20, 55, "icon0006"),
        new("Gengar", 14, 0, 40, "icon0094"),
        new("Lapras", 24, 9, 90, "icon0130"),
        new("Heracross", 18, 60, 60, "icon0214")
    ]);

    private static (string Name, string Title, CapLook Look)[] CapLooks =>
    [
        ("W", "MAPA DE LA RUTA", CapMap),
        ("X", "EDICTO DE LA LIGA", CapEdict),
        ("Y", "PERGAMINO HORIZONTAL", CapRibbon),
        ("Z", "PLACA DE TROFEO", CapPlaque)
    ];

    private const uint Ink = 0x4A2E14;
    private const uint Sepia = 0x7A5A38;
    private const uint HpGreen = 0x58A56E;
    private const uint HpAmber = 0xD8A83C;
    private const uint HpRed = 0xB8433A;
    private const uint Gold = 0xE8B83C;

    // ---------------------------------------------------------------- shared pieces

    private static readonly string[] CupArt =
    [
        "#########",
        "#.#####.#",
        "#.#####.#",
        ".#.###.#.",
        "..#####..",
        "...###...",
        "....#....",
        "...###...",
        "..#####.."
    ];

    private static void Cup(Pix p, int x, int y, uint colour)
    {
        for (var gy = 0; gy < CupArt.Length; gy++)
        {
            for (var gx = 0; gx < CupArt[gy].Length; gx++)
            {
                if (CupArt[gy][gx] == '#') p.Set(x + gx, y + gy, gy < 3 && gx < 4 ? Lighter(colour, 0.35) : colour);
            }
        }
    }

    /// <summary>Every other pixel of the icon: the cartridge's 40 by 30 drawn at half size on the cell grid.</summary>
    private static Pix Half(Pix icon)
    {
        var half = new Pix(icon.W / 2, icon.H / 2);
        for (var y = 0; y < half.H; y++)
        {
            for (var x = 0; x < half.W; x++)
            {
                foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                {
                    var i = ((((y * 2) + dy) * icon.W) + (x * 2) + dx) * 4;
                    if (icon.B[i + 3] < 128) continue;
                    var at = ((y * half.W) + x) * 4;
                    Buffer.BlockCopy(icon.B, i, half.B, at, 4);
                    break;
                }
            }
        }

        return half;
    }

    /// <summary>The paper of the scrolls: three bands of tone, stains from a hash and a one-cell ink edge that wobbles.</summary>
    private static void Sheet(Pix p, int x, int y, int w, int h, int seed, bool wobbleSides = true, bool dashes = true)
    {
        for (var yy = 0; yy < h; yy++)
        {
            var wave = wobbleSides ? (int)Math.Round(Math.Sin((yy + seed) * 0.35)) : 0;
            for (var xx = Math.Max(0, wave); xx < w - Math.Max(0, -wave) - (wave > 0 ? 0 : 0); xx++)
            {
                var t = xx / (double)w;
                var colour = t < 0.34 ? 0xF0DFB0u : t < 0.68 ? Mix(0xF0DFB0, 0xD8BE84, 0.35) : Mix(0xF0DFB0, 0xD8BE84, 0.7);
                var hash = StableHash($"{seed}:{xx}:{yy}") & 0x7FFFFFFF;
                if (hash % 31 == 0) colour = Mix(colour, 0xA88850, 0.35);
                else if (hash % 97 == 1) colour = Mix(colour, 0xA88850, 0.6);
                var edge = yy == 0 || yy == h - 1 || xx == Math.Max(0, wave) || xx == w - 1 - Math.Max(0, -wave);
                p.Set(x + xx, y + yy, edge ? 0x3A2410 : colour);
            }
        }

        if (!dashes) return;
        for (var xx = 5; xx < w - 5; xx += 2) p.Set(x + xx, y + 4, 0xA88850, 150);
        for (var xx = 5; xx < w - 5; xx += 2) p.Set(x + xx, y + h - 5, 0xA88850, 150);
    }

    /// <summary>A wooden roll lying across the top or the bottom, lit above its middle, with darker ends.</summary>
    private static void HRoll(Pix p, int x, int y, int w, int h)
    {
        for (var j = 0; j < h; j++)
        {
            var shade = Math.Round(Math.Clamp(Math.Abs(((j + 0.5) / h) - 0.38) * 2.2, 0, 1) * 3) / 3;
            for (var i = 0; i < w; i++)
            {
                var corner = (i == 0 || i == w - 1) && (j == 0 || j == h - 1);
                if (corner) continue;
                var colour = Mix(0xF2E2B4, 0x8E6A3A, shade);
                if (i < 3 || i >= w - 3) colour = Mix(colour, 0x3A2410, 0.4 + (shade * 0.2));
                else if (j == h / 2 && (i / 3) % 2 == 0) colour = Mix(colour, 0x3A2410, 0.22);
                if (i == 0 || i == w - 1 || j == 0 || j == h - 1) colour = 0x3A2410;
                p.Set(x + i, y + j, colour);
            }
        }
    }

    /// <summary>A wax seal hanging from two ribbon tails.</summary>
    private static void Wax(Pix p, double cx, double cy, double r, uint accent, bool tails = true)
    {
        var dark = Darker(accent, 0.4);
        var light = Lighter(accent, 0.4);
        if (tails)
        {
            for (var y = (int)(cy + (r * 0.5)); y < cy + r + 6; y++)
            {
                for (var k = 0; k < 3; k++)
                {
                    p.Set((int)cx - 4 + k, y, accent);
                    p.Set((int)cx + 1 + k, y, dark);
                }

                p.Set((int)cx - 5, y, 0x3A2410);
                p.Set((int)cx - 1, y, 0x3A2410);
                p.Set((int)cx, y, 0x3A2410);
                p.Set((int)cx + 4, y, 0x3A2410);
            }
        }

        for (var y = (int)(cy - r - 1); y <= cy + r + 1; y++)
        {
            for (var x = (int)(cx - r - 1); x <= cx + r + 1; x++)
            {
                var d = Math.Sqrt(Math.Pow(x + 0.5 - cx, 2) + Math.Pow(y + 0.5 - cy, 2));
                if (d > r) continue;
                p.Set(x, y, d > r - 1.1 ? 0x3A2410 : d > r - 2.4 ? (x < cx ? light : dark) : d > r - 3.6 ? accent : dark);
            }
        }
    }

    /// <summary>The team's level against the cap: a rope that burns up to the strongest one's level, with the flame at its tip.</summary>
    private static void Rope(Pix p, int x, int y, int w, double share, bool atCap, uint rope, uint ash)
    {
        var tip = (int)Math.Round(w * share);
        for (var i = 0; i < w; i++)
        {
            if (i < tip - 1) p.Set(x + i, y + 1, (i / 2) % 2 == 0 ? rope : Mix(rope, 0xFFFFFF, 0.2));
            else if (i > tip + 1 && i % 3 != 0) p.Set(x + i, y + 1, ash, 170);
        }

        var hot = atCap ? 0xFFC030u : 0xFFE070u;
        p.Set(x + tip, y + 1, hot);
        p.Set(x + tip - 1, y + 1, 0xF08A3A);
        p.Set(x + tip + 1, y + 1, 0xE85A20);
        p.Set(x + tip, y, 0xF08A3A);
        p.Set(x + tip, y + 2, 0xE85A20);
    }

    private static Pix Grey(Pix icon) => Gray(icon, 0.9);

    /// <summary>One member of the party: the icon, name and level, the HP bar the way the game paints it and the numbers.</summary>
    private static void Member(Pix p, int x, int y, int w, CapMember m, int cap, Func<string, Pix?> icons, uint ink, uint dim, uint track)
    {
        var fallen = m.Hp <= 0;
        if (icons(m.Icon) is { } icon)
        {
            var small = Half(icon);
            p.Blit(fallen ? Grey(small) : small, x, y, fallen ? 110 : 255);
        }

        var tx = x + 17;
        var name = m.Name.Length > 9 ? m.Name[..9] : m.Name;
        p.Small(name, tx, y + 1, fallen ? dim : ink);
        var level = $"NV. {m.Level}";
        p.Small(level, x + w - (level.Length * 4) + 1, y + 12, fallen ? dim : m.Level >= cap ? 0xB0661Cu : dim);

        var bar = w - 17;
        if (fallen)
        {
            p.Small("KO", tx, y + 8, HpRed);
            return;
        }

        var share = m.Hp / (double)m.Max;
        p.Rect(tx, y + 7, bar, 4, track);
        p.Rect(tx + 1, y + 8, Math.Max(1, (int)((bar - 2) * share)), 2, share > 0.5 ? HpGreen : share > 0.2 ? HpAmber : HpRed);
        p.Small($"{m.Hp}/{m.Max}", tx, y + 12, dim);
    }

    // ---------------------------------------------------------------- S: the vertical scroll

    private static Pix CapScroll(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 88, h = 186;
        var p = new Pix(w, h);
        var atCap = s.Highest >= s.Cap;
        Sheet(p, 4, 4, w - 8, h - 8, 3);
        HRoll(p, 0, 0, w, 9);
        HRoll(p, 0, h - 9, w, 9);

        // El título.
        Cup(p, 8, 14, Gold);
        p.Small("CAP DE NIVEL", 19, 14, Sepia);
        p.Small(s.Trial, 19, 21, Ink);

        // El sello con el cap de ahora y, al lado, el siguiente.
        Wax(p, 21, 43, 12, Gold);
        p.Small("NV.", 17, 36, 0x4A2E14);
        p.Text(s.Cap.ToString(), 15, 42, 0xFFF4D8, shadow: 0x6A4A10);
        p.Small("SIGUIENTE", 38, 36, Sepia);
        p.Text($"NV. {s.Next}", 38, 43, Ink);

        // La mecha: hasta dónde ha llegado el más fuerte del equipo.
        p.Small($"EQUIPO {s.Highest} / {s.Cap}", 9, 65, atCap ? 0xB0661Cu : Sepia);
        Rope(p, 9, 72, w - 18, Math.Clamp(s.Highest / (double)s.Cap, 0, 1), atCap, Ink, 0xB8A27A);


        for (var i = 0; i < s.Party.Length; i++) Member(p, 9, 84 + (i * 18), w - 18, s.Party[i], s.Cap, icons, Ink, Sepia, 0x3A2410);
        return p;
    }

    // ---------------------------------------------------------------- T: the board with notes nailed to it

    private static Pix CapBoard(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 90, h = 190;
        var p = new Pix(w, h);
        var atCap = s.Highest >= s.Cap;

        // Las dos cuerdas de las que cuelga y el tablón.
        foreach (var rx in new[] { 14, w - 15 })
        {
            for (var y = 0; y < 9; y++)
            {
                p.Set(rx, y, 0xD8C080);
                p.Set(rx + 1, y, 0x806030);
            }
        }

        p.Notched(0, 8, w - 1, h - 10, 0x2E1A0A, 2);
        Wood(p, 1, 9, w - 3, h - 12, 0xA8703A, 5);
        p.Outline(3, 11, w - 7, h - 16, Darker(0xA8703A, 0.45));
        p.Notched(2, h - 2, w - 4, 2, 0x1A0E06, 1, 150);
        foreach (var (nx, ny) in new[] { (5, 13), (w - 7, 13), (5, h - 8), (w - 7, h - 8) })
        {
            p.Rect(nx, ny, 2, 2, 0x4A3018);
            p.Set(nx, ny, 0xD8C8A0);
        }

        // La nota del cap, clavada un poco torcida.
        var note = new Pix(66, 42);
        Sheet(note, 0, 0, 66, 42, 8, false, false);
        note.Small(s.Trial, 33 - (((s.Trial.Length * 4) - 1) / 2), 7, Sepia);
        var cap = $"NV. {s.Cap}";
        note.Text(cap, 33 - (((cap.Length * 6) - 1) / 2), 16, Ink, shadow: 0xD8BE84);
        var next = $"SIGUIENTE: {s.Next}";
        note.Small(next, 33 - (((next.Length * 4) - 1) / 2), 32, Sepia);
        p.Blit(Turned(note, -2.5), 12, 18);
        Disc(p, 45, 19, 2.4, 0x7A2A14);
        Disc(p, 44.5, 18.5, 1.2, 0xE86A50);

        // La mecha, sobre la madera.
        p.Small($"EQUIPO {s.Highest} / {s.Cap}", 9, 62, atCap ? 0xFFD060u : 0xFFF0CC);
        p.Rect(8, 69, w - 17, 5, 0x3A2410);
        Rope(p, 9, 70, w - 19, Math.Clamp(s.Highest / (double)s.Cap, 0, 1), atCap, 0xF2DDA8, 0x7A5A38);

        // Una tira de papel clavada por Pokémon.
        for (var i = 0; i < s.Party.Length; i++)
        {
            var y = 80 + (i * 20);
            var strip = new Pix(w - 14, 19);
            Sheet(strip, 0, 0, w - 14, 19, 20 + i, false, false);
            Member(strip, 3, 0, w - 21, s.Party[i], s.Cap, icons, Ink, Sepia, 0x3A2410);
            p.Blit(strip, 7, y);
            p.Rect(w - 12, y + 2, 2, 2, 0x4A3018);
        }

        return p;
    }

    // ---------------------------------------------------------------- U: the field notebook

    private static Pix CapNotebook(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 94, h = 186;
        var p = new Pix(w, h);
        var atCap = s.Highest >= s.Cap;

        // La tapa de cuero asoma a los lados y la página, con renglones y margen.
        p.Notched(0, 0, w - 1, h - 1, 0x2A1810, 3);
        p.Notched(1, 1, w - 3, h - 3, 0x5A3820, 3);
        p.Notched(7, 3, w - 12, h - 8, 0x3A2410, 2);
        for (var y = 4; y < h - 6; y++)
        {
            for (var x = 8; x < w - 6; x++) p.Set(x, y, y < 10 ? 0xEBE2C8 : Mix(0xF4EEDC, 0xE6DCBE, (x - 8) / (double)w));
        }

        for (var y = 22; y < h - 8; y += 15) p.HLine(9, y, w - 16, 0x8AA4C8, 90);
        p.VLine(17, 4, h - 10, 0xD87870, 130);

        // La espiral.
        for (var y = 8; y < h - 10; y += 12)
        {
            p.Rect(1, y, 11, 3, 0x2A1810);
            p.Rect(2, y, 9, 2, 0xB8B8C4);
            p.Rect(2, y, 9, 1, 0xE8E8F0);
            Disc(p, 8.5, y + 1.5, 1.6, 0x1A0E08);
        }

        // El título a bolígrafo y el cap como un sello de tinta estampado en la página.
        Cup(p, 20, 9, 0xB8433A);
        p.Small("CAP DE NIVEL", 32, 9, 0x6A5A48);
        p.Small(s.Trial, 20, 19, 0x2A3A8A);

        var stamp = new Pix(62, 24);
        stamp.Outline(0, 0, 62, 24, 0xB8433A);
        stamp.Outline(2, 2, 58, 20, 0xB8433A, 200);
        var cap = $"NV. {s.Cap}";
        stamp.Text(cap, 31 - (((cap.Length * 6) - 1) / 2), 9, 0xB8433A);
        stamp.Small("TOPE ACTUAL", 31 - 21, 4, 0xB8433A);
        var random = new Random(11);
        for (var i = 0; i < 40; i++) Erase(stamp, random.Next(62), random.Next(24));
        p.Blit(Turned(stamp, -3), 19, 28);
        p.Small($"SIGUIENTE: {s.Next}", 20, 56, 0x6A5A48);
        p.HLine(20, 60, 62, 0x2A3A8A, 120);

        // La mecha y el equipo, a renglón.
        p.Small($"EQUIPO {s.Highest} / {s.Cap}", 20, 66, atCap ? 0xB0661Cu : 0x6A5A48u);
        Rope(p, 20, 73, 60, Math.Clamp(s.Highest / (double)s.Cap, 0, 1), atCap, 0x2A3A8A, 0xB8A27A);

        for (var i = 0; i < s.Party.Length; i++)
        {
            var y = 82 + (i * 18);
            Member(p, 19, y, 68, s.Party[i], s.Cap, icons, 0x2A3A8A, 0x6A5A48, 0x3A2410);
            if (s.Party[i].Hp <= 0) Line(p, 36, y + 3, 36 + (Math.Min(9, s.Party[i].Name.Length) * 4), y + 3, 0x2A3A8A);
        }

        // El punto de libro.
        p.Rect(w - 20, 0, 6, 15, 0xB8433A);
        p.Rect(w - 20, 0, 2, 15, Lighter(0xB8433A, 0.3));
        p.Rect(w - 20, 12, 2, 3, 0xB8433A);
        p.Rect(w - 16, 12, 2, 3, 0xB8433A);
        return p;
    }

    // ---------------------------------------------------------------- V: the tags on the cord

    private static Pix CapTags(CapSample s, Func<string, Pix?> icons)
    {
        const int w = 90, h = 190;
        var p = new Pix(w, h);
        var atCap = s.Highest >= s.Cap;

        // La cuerda que baja por la izquierda, con un nudo arriba y otro abajo.
        for (var y = 2; y < h - 3; y++)
        {
            p.Set(5, y, (y / 2) % 2 == 0 ? 0xD8C080u : 0xA88850u);
            p.Set(6, y, 0x6A4A28);
        }

        Disc(p, 5.5, 3, 3, 0x806030);
        Disc(p, 5.5, h - 4, 3, 0x806030);

        // La etiqueta grande del cap.
        void Tag(int x, int y, int tw, int th, int hash)
        {
            Sheet(p, x, y, tw, th, hash, false);
            for (var k = 0; k < 4; k++)
            {
                for (var j = 0; j < 4 - k; j++)
                {
                    Erase(p, x + k, y + j);
                    Erase(p, x + k, y + th - 1 - j);
                }
            }

            Disc(p, x + 4, y + (th / 2.0), 2.6, 0x2A1A0C);
            Disc(p, x + 4, y + (th / 2.0), 1.4, 0x806030);
        }

        Tag(8, 8, 78, 46, 4);
        p.Small("CAP DE NIVEL", 16, 12, Sepia);
        p.Small(s.Trial, 16, 19, Ink);
        p.Text($"NV. {s.Cap}", 16, 28, Ink, shadow: 0xD8BE84);
        p.Small($"SIGUIENTE: {s.Next}", 16, 43, Sepia);
        Wax(p, 72, 36, 7, Gold, tails: false);
        Cup(p, 68, 32, 0x6A4A10);

        // La mecha.
        p.Small($"EQUIPO {s.Highest} / {s.Cap}", 11, 62, atCap ? 0xB0661Cu : 0xE8D8B0u);
        Rope(p, 11, 70, w - 20, Math.Clamp(s.Highest / (double)s.Cap, 0, 1), atCap, 0xE8D8B0, 0x7A5A38);

        // Una etiqueta por Pokémon, ensartada en la cuerda.
        for (var i = 0; i < s.Party.Length; i++)
        {
            var y = 80 + (i * 20);
            Tag(8, y, 78, 19, 30 + i);
            Member(p, 17, y, 64, s.Party[i], s.Cap, icons, Ink, Sepia, 0x3A2410);
        }

        return p;
    }

    // ---------------------------------------------------------------- composing

    private static Pix CapScreen(CapLook look, string assets)
    {
        var screen = new Pix(1600, 1000);
        screen.Rect(0, 0, 1600, 1000, 0x0A0A12);
        if (ReadPng(Path.Combine(assets, "bg1.png")) is { } game)
        {
            var big = game.Scaled(2);
            screen.Blit(big, (1600 - big.W) / 2, 20);
        }

        var panel = look(CapDemo, name => ReadPng(Path.Combine(assets, name + ".png"))).Scaled(3);
        screen.Blit(panel, 1600 - panel.W - 48, 40);
        return screen;
    }

    [Fact]
    public void Every_cap_look_draws_with_pixels_in_it()
    {
        foreach (var (_, _, look) in CapLooks)
        {
            var panel = look(CapDemo, _ => null);
            Assert.Contains(panel.B.Where((_, i) => i % 4 == 3), alpha => alpha == 255);
        }
    }

    [Fact]
    public void Writes_the_cap_pictures_when_asked()
    {
        var folder = Dir("PERMALOCKE_PIXEL_DIR");
        var assets = Dir("PERMALOCKE_PROTO_ASSETS");
        if (folder is null || assets is null) return;

        Directory.CreateDirectory(folder);
        foreach (var (name, _, look) in CapLooks)
        {
            var screen = CapScreen(look, assets);
            PngFile.Write(screen.B, screen.W, screen.H, 1, Path.Combine(folder, $"cap-{name}.png"));
        }

        // Las cuatro juntas, con un aviso de pergamino debajo para ver que son de la misma familia.
        var compare = new Pix(1200, 720);
        compare.Rect(0, 0, 1200, 720, 0x0A0A12);
        if (ReadPng(Path.Combine(assets, "bg3.png")) is { } game) compare.Blit(game, 0, 0, 255);
        var x = 14;
        foreach (var (name, title, look) in CapLooks)
        {
            var panel = look(CapDemo, n => ReadPng(Path.Combine(assets, n + ".png"))).Scaled(2);
            compare.Rect(x - 4, 6, panel.W + 8, panel.H + 34, 0x0A0A12, 150);
            compare.Blit(panel, x, 30);
            compare.Text(name, x, 8, 0xFFFFFF, scale: 2);
            x += panel.W + 20;
        }

        var notice = Scroll(Samples[0], ReadPng(Path.Combine(assets, Samples[0].Icon + ".png"))).Scaled(2);
        compare.Blit(notice, 1200 - notice.W - 12, 720 - notice.H - 12);
        PngFile.Write(compare.B, compare.W, compare.H, 1, Path.Combine(folder, "cap-comparar.png"));
    }
}
