using PermaLocke.App.Views.Pixel;

namespace PermaLocke.PixelCheck;

/// <summary>Second round of notice prototypes (2026-10-08): five looks that are objects rather than boxes.</summary>
public sealed partial class NotificationPrototypes
{
    private static (string Name, string Title, Look Look)[] RoundTwo =>
    [
        ("F", "CARTA HOLOGRÁFICA", HoloCard),
        ("G", "RECIBO DE LA POKÉ TIENDA", Receipt),
        ("H", "HOLOGRAMA DE BALL", Hologram),
        ("I", "PÁGINA DE PASAPORTE", Passport),
        ("J", "MINI 3DS", Console)
    ];

    // ---------------------------------------------------------------- helpers

    private static List<string> SmallLines(string text, int cells, int max = 2)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' '))
        {
            var next = line.Length == 0 ? word : line + " " + word;
            if (next.Length * 4 <= cells) { line = next; continue; }
            if (line.Length > 0) lines.Add(line);
            line = word;
        }

        if (line.Length > 0) lines.Add(line);
        return lines.Take(max).ToList();
    }

    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var ch in text) hash = unchecked((hash * 31) + ch);
        return hash;
    }

    private static void Erase(Pix p, int x, int y)
    {
        if (x < 0 || y < 0 || x >= p.W || y >= p.H) return;
        var i = ((y * p.W) + x) * 4;
        p.B[i] = p.B[i + 1] = p.B[i + 2] = p.B[i + 3] = 0;
    }

    private static void Disc(Pix p, double cx, double cy, double radius, uint colour, int alpha = 255)
    {
        for (var y = (int)Math.Floor(cy - radius); y <= (int)Math.Ceiling(cy + radius); y++)
        {
            for (var x = (int)Math.Floor(cx - radius); x <= (int)Math.Ceiling(cx + radius); x++)
            {
                var dx = x + 0.5 - cx;
                var dy = y + 0.5 - cy;
                if ((dx * dx) + (dy * dy) <= radius * radius) p.Set(x, y, colour, alpha);
            }
        }
    }

    private static void Ring(Pix p, double cx, double cy, double outer, double inner, uint colour, int alpha = 255)
    {
        for (var y = (int)Math.Floor(cy - outer); y <= (int)Math.Ceiling(cy + outer); y++)
        {
            for (var x = (int)Math.Floor(cx - outer); x <= (int)Math.Ceiling(cx + outer); x++)
            {
                var d = Math.Sqrt(Math.Pow(x + 0.5 - cx, 2) + Math.Pow(y + 0.5 - cy, 2));
                if (d <= outer && d >= inner) p.Set(x, y, colour, alpha);
            }
        }
    }

    private static void Line(Pix p, int x0, int y0, int x1, int y1, uint colour, int alpha = 255)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        while (true)
        {
            p.Set(x0, y0, colour, alpha);
            if (x0 == x1 && y0 == y1) return;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>The picture turned by an angle, nearest pixel: a rubber stamp that did not land straight.</summary>
    private static Pix Turned(Pix source, double degrees)
    {
        var angle = degrees * Math.PI / 180;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var turned = new Pix(source.W, source.H);
        double cx = source.W / 2.0, cy = source.H / 2.0;
        for (var y = 0; y < source.H; y++)
        {
            for (var x = 0; x < source.W; x++)
            {
                var ux = x + 0.5 - cx;
                var uy = y + 0.5 - cy;
                var sx = (int)Math.Floor((ux * cos) + (uy * sin) + cx);
                var sy = (int)Math.Floor((-ux * sin) + (uy * cos) + cy);
                if (sx < 0 || sy < 0 || sx >= source.W || sy >= source.H) continue;
                Buffer.BlockCopy(source.B, ((sy * source.W) + sx) * 4, turned.B, ((y * source.W) + x) * 4, 4);
            }
        }

        return turned;
    }

    private static string Subject(Sample s) => s.Label switch
    {
        "BAJA" => "pedicure",
        "VARIOCOLOR" => "Charizard",
        "DUPLICADO" => "Frogadier",
        "FANTASMA" => "Volvo",
        _ => "Bavi"
    };

    private static uint Rainbow(double t)
    {
        t -= Math.Floor(t);
        var h = t * 6;
        var f = h - Math.Floor(h);
        return ((int)h % 6) switch
        {
            0 => Rgb(255, (int)(255 * f), 80),
            1 => Rgb((int)(255 * (1 - f)), 255, 80),
            2 => Rgb(80, 255, (int)(255 * f)),
            3 => Rgb(80, (int)(255 * (1 - f)), 255),
            4 => Rgb((int)(255 * f), 80, 255),
            _ => Rgb(255, 80, (int)(255 * (1 - f)))
        };
    }

    private static Pix Slim(Sample s, uint body, uint edge, uint text, int style)
    {
        var p = new Pix(104, 17);
        p.Notched(0, 0, 102, 16, edge, 2);
        p.Notched(1, 1, 100, 14, body, 2);
        p.Notched(4, 4, 9, 9, s.Accent, style == 0 ? 3 : 2);
        p.Small("BAVI ESTÁ JUGANDO", 18, 6, text);
        return p;
    }

    // ---------------------------------------------------------------- F: the holographic card

    private static Pix HoloCard(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0x1A1430, 0x07050C, 0xE8E0FF, 0);

        const int w = 132, h = 58;
        var p = new Pix(w, h);
        var death = s.Mark == Mark.Skull;
        var shiny = s.Mark == Mark.Star;
        var metal = death ? Mix(s.Accent, 0x6A6470, 0.6) : s.Accent;

        // El marco: papel de aluminio. Arco iris en una variocolor, metal del color del tipo en las demás, gris en una baja.
        p.Notched(0, 0, w, h, 0x07050C, 3);
        for (var y = 1; y < h - 1; y++)
        {
            for (var x = 1; x < w - 1; x++)
            {
                var diagonal = (x + (y * 2)) / (double)(w + (h * 2));
                var foil = shiny ? Mix(Rainbow(diagonal * 1.6), 0xFFFFFF, 0.18)
                    : Mix(Lighter(metal, 0.6), Darker(metal, 0.4), Math.Abs(((diagonal * 3) % 2) - 1));
                p.Set(x, y, foil);
            }
        }

        p.Notched(4, 4, w - 8, h - 8, 0x07050C, 2);
        p.Notched(5, 5, w - 10, h - 10, 0x17122A, 2);
        p.VGrad(6, 6, w - 12, h - 12, 0x211A3A, 0x100C1C);

        // La ventana del dibujo: fondo del color del tipo, rayos y el Pokémon.
        const int ax = 9, ay = 9, aw = 44, ah = 40;
        p.Rect(ax - 1, ay - 1, aw + 2, ah + 2, Lighter(metal, 0.5));
        p.VGrad(ax, ay, aw, ah, Lighter(metal, 0.35), Darker(metal, 0.45));
        for (var i = 0; i < 14; i++)
        {
            var angle = i * Math.PI / 7;
            Line(p, ax + (aw / 2), ay + (ah / 2) + 2, ax + (aw / 2) + (int)(Math.Cos(angle) * 50), ay + (ah / 2) + 2 + (int)(Math.Sin(angle) * 50), 0xFFFFFF, 26);
        }

        for (var y = ay; y < ay + ah; y++)
        {
            for (var x = ax; x < ax + aw; x++)
            {
                if (((x + y) % 4 == 0)) p.Set(x, y, 0xFFFFFF, 18);
            }
        }

        if (icon is not null)
        {
            var big = icon.Scaled(icon.W * 2 <= aw && icon.H * 2 <= ah ? 2 : 1);
            p.Blit(big, ax + ((aw - big.W) / 2), ay + ((ah - big.H) / 2) + 1);
        }

        if (death)
        {
            // La baja: ceniza sobre el dibujo y una grieta que lo cruza.
            p.Rect(ax, ay, aw, ah, 0x15121A, 90);
            var x0 = ax + 28;
            var y0 = ay;
            foreach (var (dx, dy) in new[] { (-3, 6), (4, 5), (-4, 7), (3, 6), (-2, 7), (3, 9) })
            {
                Line(p, x0, y0, x0 + dx, y0 + dy, 0x07050C);
                x0 += dx;
                y0 += dy;
            }
        }

        // El nombre en su barra, en dos líneas.
        var lines = PixelFont.Wrap(s.Title, 66);
        p.Rect(57, 8, 68, 25, 0x0C0916);
        p.HLine(57, 8, 68, Lighter(metal, 0.5));
        p.HLine(57, 32, 68, Darker(metal, 0.2));
        for (var i = 0; i < Math.Min(2, lines.Count); i++) p.Text(lines[i], 60, 5 + (i * 12), 0xFFFFFF, shadow: 0x000000);

        // El tipo, el detalle y los «PS» que son la cuenta atrás.
        p.Notched(57, 36, (s.Label.Length * 4) + 7, 7, metal, 1);
        p.Small(s.Label, 60, 37, death ? 0x120A0Au : 0x07050Cu);
        p.Small(FitSmall(s.Message, 46), 57, 45, 0xB8B0D8);
        p.Small("PS", 106, 38, 0xFFFFFF);
        p.Rect(114, 38, 11, 3, 0x07050C);
        p.Rect(115, 39, 9 * Lit / 12, 1, death ? 0xD84030u : 0x58D870u);

        // La rareza.
        var stars = shiny ? 3 : death ? 1 : 2;
        for (var i = 0; i < stars; i++) DrawMark(p, Mark.Star, 105 + (i * 8), 46, shiny ? 0xFFE070u : Lighter(metal, 0.5));

        // El brillo del holograma: una banda diagonal y destellos.
        for (var y = 1; y < h - 1; y++)
        {
            var x = 50 + ((h - y) * 2);
            p.Rect(x, y, 6, 1, 0xFFFFFF, 20);
            p.Rect(x + 8, y, 2, 1, 0xFFFFFF, 14);
        }

        foreach (var (sx, sy) in new[] { (20, 14), (118, 12), (92, 38), (40, 44) })
        {
            p.Set(sx, sy, 0xFFFFFF);
            p.Set(sx - 1, sy, 0xFFFFFF, 120);
            p.Set(sx + 1, sy, 0xFFFFFF, 120);
            p.Set(sx, sy - 1, 0xFFFFFF, 120);
            p.Set(sx, sy + 1, 0xFFFFFF, 120);
        }

        if (death)
        {
            // La esquina quemada: un mordisco en diagonal con brasas en el borde.
            for (var y = 0; y < 14; y++)
            {
                for (var x = 0; x < 14; x++)
                {
                    if (x + y > 17 && w - 1 - x >= 0) Erase(p, w - 1 - x, h - 1 - y);
                }
            }

            for (var i = 0; i < 14; i++)
            {
                var ember = i % 3 == 0 ? 0xFFB040u : i % 3 == 1 ? 0xE85A20u : 0x7A2A14u;
                p.Set(w - 1 - i + 4, h - 1 - (17 - i) - 1, ember);
                p.Set(w - 1 - i + 5, h - 1 - (17 - i), 0x2A1008);
            }
        }

        return p;
    }

    // ---------------------------------------------------------------- G: the receipt

    private static Pix Receipt(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0xEFE6D0, 0x5A4C38, 0x3A2F28, 1);

        const int w = 116, h = 86;
        var p = new Pix(w, h);
        var paper = 0xF2E9D4u;
        var ink = 0x3A2F28u;
        var faint = 0x8D7F6Au;
        var random = new Random(StableHash(s.Label));

        // El papel térmico, con bordes en sierra arriba y abajo y una sombra a un lado.
        for (var x = 0; x < w; x++)
        {
            var zig = Math.Abs((x % 6) - 3);
            for (var y = zig; y < h - zig; y++) p.Set(x, y, Mix(paper, 0xD8CCB0, (y / (double)h * 0.35) + (random.NextDouble() * 0.04)));
        }

        for (var y = 3; y < h - 3; y++)
        {
            p.Set(0, y, 0xB8AA90);
            p.Set(w - 1, y, 0xC8BBA0);
        }

        // La cabecera.
        var head = "POKÉ TIENDA";
        p.Small(head, (w - ((head.Length * 4) - 1)) / 2, 5, ink);
        for (var x = 6; x < w - 6; x += 2) p.Set(x, 12, faint);
        p.Small("AVISO Nº 0042", 6, 15, faint);

        // El Pokémon, impreso como la impresora térmica lo haría: dos tintas y trama.
        p.Rect(5, 22, 28, 28, 0xE6DBC2);
        if (icon is not null)
        {
            for (var y = 0; y < icon.H; y++)
            {
                for (var x = 0; x < icon.W; x++)
                {
                    var i = ((y * icon.W) + x) * 4;
                    if (icon.B[i + 3] < 128) continue;
                    var luma = ((icon.B[i + 2] * 0.3) + (icon.B[i + 1] * 0.59) + (icon.B[i] * 0.11)) / 255;
                    var threshold = (((x % 2) * 2) + (y % 2)) / 4.0;
                    p.Set(5 + ((28 - icon.W) / 2) + x, 22 + ((28 - icon.H) / 2) + y, luma + (threshold * 0.35) - 0.17 > 0.5 ? 0xC8B994u : ink);
                }
            }
        }

        p.Outline(5, 22, 28, 28, faint);

        // Las líneas del tique.
        var lines = PixelFont.Wrap(s.Title, 72);
        for (var i = 0; i < Math.Min(2, lines.Count); i++) p.Text(lines[i], 37, 18 + (i * 11), ink);
        var detail = SmallLines(s.Message, 72);
        for (var i = 0; i < detail.Count; i++) p.Small(detail[i], 37, 44 + (i * 7), i == 0 ? ink : faint);

        for (var x = 6; x < w - 6; x += 2) p.Set(x, 58, faint);

        // El total y el código de barras: la cuenta atrás, barra a barra.
        p.Small("TOTAL", 6, 62, ink);
        for (var i = 0; i < 24; i++)
        {
            var x = 6 + (i * 2);
            var lit = i < Lit * 2;
            p.Rect(x, 69, i % 3 == 0 ? 2 : 1, 9, lit ? ink : 0xC8B994u);
        }

        // El sello del tipo, cruzado en la esquina y ladeado como si lo hubieran dado a mano.
        var stamp = new Pix(30, 30);
        Ring(stamp, 15, 15, 14, 12.5, s.Accent, 235);
        Ring(stamp, 15, 15, 10.5, 9.5, s.Accent, 205);
        DrawMark(stamp, s.Mark, 11, 6, s.Accent);
        var word = s.Label.Length > 8 ? s.Label[..8] : s.Label;
        stamp.Small(word, 15 - (((word.Length * 4) - 1) / 2), 17, s.Accent);
        for (var i = 0; i < 40; i++) Erase(stamp, random.Next(30), random.Next(30));
        p.Blit(Turned(stamp, -13), w - 36, h - 34);
        return p;
    }

    // ---------------------------------------------------------------- H: the ball's hologram

    private static Pix Hologram(Sample s, Pix? icon)
    {
        if (s.Compact)
        {
            var c = new Pix(104, 17);
            for (var y = 0; y < 17; y++) c.Rect(0, y, 102, 1, 0x0A1420, y % 2 == 0 ? 175 : 150);
            c.Outline(0, 0, 102, 16, 0x7FE8FF, 150);
            c.Rect(0, 0, 4, 1, 0xFFFFFF);
            c.Rect(0, 0, 1, 4, 0xFFFFFF);
            Disc(c, 9, 8, 4, 0x7FE8FF);
            c.Small("BAVI ESTÁ JUGANDO", 18, 6, 0xBFF4FF);
            return c;
        }

        const int w = 160, h = 60;
        var p = new Pix(w, h);
        var glow = s.Mark == Mark.Skull ? 0xFF5A48u : Lighter(s.Accent, 0.35);
        var death = s.Mark == Mark.Skull;

        // La ball, con la tapa entreabierta: de ahí sale la luz.
        var ball = death ? 0x2A2630u : s.Accent;
        Disc(p, 15, 29, 11, 0x07050C);
        for (var y = 18; y < 41; y++)
        {
            for (var x = 4; x < 27; x++)
            {
                var dx = x + 0.5 - 15.5;
                var dy = y + 0.5 - 29.5;
                if ((dx * dx) + (dy * dy) > 100) continue;
                var top = dy < -1;
                var shade = Math.Clamp((-dx - dy) / 22.0 + 0.5, 0, 1);
                p.Set(x, y, top ? Mix(Lighter(ball, 0.35), Darker(ball, 0.35), shade) : Mix(0xF6F2FF, 0xA8A0C0, shade));
            }
        }

        p.Rect(4, 28, 23, 2, 0x07050C);
        Disc(p, 15.5, 29.5, 3.5, 0x07050C);
        Disc(p, 15.5, 29.5, 2.2, glow);
        p.Rect(5, 25, 21, 1, 0xFFFFFF, 220);
        for (var x = 6; x < 26; x++) p.Set(x, 26, glow, 190);

        // El haz: un cono de luz tramado desde la rendija hasta el panel.
        for (var x = 27; x < 52; x++)
        {
            var spread = (x - 27) * 0.7;
            for (var y = 26 - (int)spread; y <= 28 + (int)spread; y++)
            {
                if ((x + y) % 2 == 0) p.Set(x, y, glow, 120 - ((x - 27) * 3));
                else if ((x + y) % 4 == 1) p.Set(x, y, 0xFFFFFF, 40);
            }
        }

        // El panel holográfico: cristal casi transparente, rejilla y esquinas.
        const int px = 52;
        var pw = w - px - 1;
        for (var y = 2; y < h - 2; y++) p.Rect(px, y, pw, 1, 0x081824, y % 2 == 0 ? 170 : 140);
        for (var y = 4; y < h - 4; y += 4)
        {
            for (var x = px + 2; x < px + pw - 2; x += 4) p.Set(x, y, glow, 60);
        }

        foreach (var (cx, cy, dx, dy) in new[] { (px, 2, 1, 1), (px + pw - 1, 2, -1, 1), (px, h - 3, 1, -1), (px + pw - 1, h - 3, -1, -1) })
        {
            for (var i = 0; i < 7; i++)
            {
                p.Set(cx + (dx * i), cy, 0xFFFFFF);
                p.Set(cx, cy + (dy * i), 0xFFFFFF);
            }

            p.Set(cx + dx, cy + dy, glow);
        }

        p.HLine(px + 8, 2, pw - 16, glow, 150);
        p.HLine(px + 8, h - 3, pw - 16, glow, 150);

        // El Pokémon: proyectado, con el tinte del color y huecos de barrido.
        var ix = px + 6;
        if (icon is not null)
        {
            for (var y = 0; y < icon.H; y++)
            {
                for (var x = 0; x < icon.W; x++)
                {
                    var i = ((y * icon.W) + x) * 4;
                    if (icon.B[i + 3] < 100 || (y % 3 == 2)) continue;
                    var c = Mix(Rgb(icon.B[i + 2], icon.B[i + 1], icon.B[i]), glow, 0.45);
                    p.Set(ix + x, 12 + y, c, 235);
                }
            }
        }

        // El texto: titulares con halo, en dos líneas como cabe en el panel.
        var tx = ix + 29;
        var room = w - tx - 6;
        var rows = PixelFont.Wrap(s.Title, room);
        p.Small(s.Label, tx, 6, glow);
        for (var i = 0; i < Math.Min(2, rows.Count); i++)
        {
            p.Text(rows[i], tx + 1, 12 + (i * 11) + 1, glow);
            p.Text(rows[i], tx, 12 + (i * 11), 0xF4FFFF);
        }

        var detail = SmallLines(s.Message, room);
        for (var i = 0; i < detail.Count; i++) p.Small(detail[i], tx, 40 + (i * 7), Lighter(glow, 0.2));

        // La cuenta atrás: puntos de luz que se apagan.
        for (var i = 0; i < 16; i++) p.Rect(tx + (i * 4), 54, 3, 1, glow, i < Lit * 16 / 12 ? 230 : 50);

        // Chispas de la proyección.
        var random = new Random(StableHash(s.Label));
        for (var i = 0; i < 16; i++) p.Set(random.Next(px, w - 2), random.Next(3, h - 3), 0xFFFFFF, 90);
        return p;
    }

    // ---------------------------------------------------------------- I: the passport page

    private static Pix Passport(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0xE6D8B4, 0x4A3A22, 0x3A2C1A, 1);

        const int w = 136, h = 66;
        var p = new Pix(w, h);
        var paper = 0xEADDBAu;
        var ink = 0x3A2C1Au;
        var faint = 0x9A8A68u;

        // La página, con el lomo oscuro a la izquierda y el filo sombreado.
        p.Notched(0, 0, w - 1, h - 1, 0x2A1E10, 2);
        for (var y = 1; y < h - 2; y++)
        {
            for (var x = 1; x < w - 2; x++)
            {
                var gutter = Math.Max(0, 1 - (x / 14.0)) * 0.28;
                p.Set(x, y, Mix(Mix(paper, 0xD8C898, y / (double)h * 0.3), 0x6A5230, gutter));
            }
        }

        // El fondo de seguridad: ondas finas que se cruzan, del color del tipo.
        for (var k = 0; k < 4; k++)
        {
            for (var x = 6; x < w - 4; x++)
            {
                var y = (int)(h / 2.0 + (Math.Sin((x * 0.16) + (k * 1.4)) * 14));
                p.Set(x, y, s.Accent, 38);
            }
        }

        p.Outline(4, 3, w - 9, h - 8, faint, 160);

        // Encabezado.
        p.Small("VISADO · REGIÓN DE ALOLA", 9, 6, faint);
        p.Small("Nº 0042", w - 40, 6, faint);

        // La foto, con las esquinas pegadas.
        p.Rect(9, 14, 32, 36, 0xF4EEDC);
        p.Rect(11, 16, 28, 32, Mix(s.Accent, paper, 0.55));
        for (var y = 17; y < 47; y += 3) p.HLine(11, y, 28, 0xFFFFFF, 30);
        if (icon is not null) p.Blit(icon, 25 - (icon.W / 2), 32 - (icon.H / 2));
        foreach (var (cx, cy, dx, dy) in new[] { (11, 16, 1, 1), (38, 16, -1, 1), (11, 47, 1, -1), (38, 47, -1, -1) })
        {
            for (var i = 0; i < 4; i++) p.Set(cx + (dx * i), cy, 0x3A2C1A);
            for (var i = 0; i < 4; i++) p.Set(cx, cy + (dy * i), 0x3A2C1A);
        }

        // Los campos.
        var fields = new (string, string)[]
        {
            ("NOMBRE", Subject(s)),
            ("MOTIVO", s.Label),
            ("DETALLE", s.Message)
        };
        for (var i = 0; i < fields.Length; i++)
        {
            var y = 14 + (i * 14);
            p.Small(fields[i].Item1, 47, y, faint);
            p.Small(FitSmall(fields[i].Item2, 56), 47, y + 6, ink);
            p.HLine(47, y + 12, 56, faint, 90);
        }

        // El sello: tinta del color del tipo, algo corrido y ladeado.
        var stamp = new Pix(46, 46);
        var random = new Random(StableHash(s.Label) + 3);
        Ring(stamp, 23, 23, 21.5, 19, s.Accent, 225);
        Ring(stamp, 23, 23, 16.5, 15, s.Accent, 205);
        var big = new Pix(7, 7);
        DrawMark(big, s.Mark, 0, 0, s.Accent);
        stamp.Blit(big.Scaled(2), 16, 11);
        var word = s.Label.Length > 9 ? s.Label[..9] : s.Label;
        stamp.Rect(5, 28, 36, 8, s.Accent, 225);
        stamp.Small(word, 23 - (((word.Length * 4) - 1) / 2), 30, paper);
        for (var i = 0; i < 70; i++) Erase(stamp, random.Next(46), random.Next(46));
        p.Blit(Turned(stamp, -16), w - 54, 6, 235);

        // La cuenta atrás: sellitos en el borde de abajo.
        for (var i = 0; i < 12; i++) p.Rect(47 + (i * 6), h - 7, 4, 2, i < Lit ? s.Accent : faint, i < Lit ? 230 : 110);
        return p;
    }

    // ---------------------------------------------------------------- J: the mini 3DS

    private static Pix Console(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0x2A2A44, 0x07050C, 0xFFFFFF, 0);

        const int w = 152, h = 74;
        var p = new Pix(w, h);
        var shell = s.Mark == Mark.Skull ? 0x3A3440u : Mix(s.Accent, 0xE8E4F0, 0.35);
        var shellHi = Lighter(shell, 0.5);
        var shellLo = Darker(shell, 0.4);

        // La tapa de arriba y la base de abajo, con la bisagra en medio.
        p.Notched(0, 0, w - 2, 36, 0x07050C, 5);
        p.Notched(1, 1, w - 4, 34, shell, 5);
        p.VGrad(2, 2, w - 6, 32, shellHi, shell);
        p.Rect(0, 35, w - 2, 5, 0x07050C);
        p.VGrad(1, 36, w - 4, 3, shellLo, Darker(shell, 0.6));
        p.Notched(0, 39, w - 2, 33, 0x07050C, 5);
        p.Notched(1, 40, w - 4, 31, shell, 5);
        p.VGrad(2, 40, w - 6, 30, shell, shellLo);
        p.Notched(2, 72, w - 4, 2, 0x050308, 1, 150);

        // La pantalla de arriba: el Pokémon y el título.
        p.Rect(9, 5, w - 20, 28, 0x07050C);
        p.VGrad(10, 6, w - 22, 26, 0x1A2250, 0x0A0F2C);
        p.HLine(10, 6, w - 22, 0x5A78E0, 90);
        for (var y = 7; y < 32; y += 2) p.HLine(10, y, w - 22, 0x000000, 40);
        p.Rect(13, 8, 24, 22, Darker(s.Accent, 0.5));
        p.Outline(13, 8, 24, 22, s.Accent, 200);
        if (icon is not null) p.Blit(icon, 25 - (icon.W / 2), 19 - (icon.H / 2));
        p.Text(Fit(s.Title, 94), 42, 7, 0xFFFFFF, shadow: 0x050A30);
        p.Small(s.Label, 42, 24, s.Accent);

        // El altavoz y la luz de aviso en la bisagra, a juego con el tipo.
        for (var i = 0; i < 5; i++)
        {
            p.Set(4 + (i * 2), 36, 0x07050C);
            p.Set(w - 14 + (i * 2), 36, 0x07050C);
        }

        Disc(p, w / 2.0, 37.5, 1.6, s.Accent);

        // La pantalla de abajo: el detalle y la cuenta atrás como la batería.
        p.Rect(24, 44, w - 50, 25, 0x07050C);
        p.VGrad(25, 45, w - 52, 23, 0x161A38, 0x0B0E24);
        var detail = SmallLines(s.Message, w - 62);
        for (var i = 0; i < detail.Count; i++) p.Small(detail[i], 29, 47 + (i * 6), 0xBDD4FF);
        p.Outline(29, 60, 52, 6, 0xBDD4FF);
        p.Rect(81, 62, 2, 2, 0xBDD4FF);
        for (var i = 0; i < 12; i++) p.Rect(30 + (i * 4), 61, 3, 4, i < Lit ? s.Accent : 0x2A2F58);
        p.Small("PERMALOCKE", w - 66, 62, 0x5A64A8);

        // La cruceta a la izquierda y los cuatro botones a la derecha.
        p.Rect(9, 53, 9, 3, 0x07050C);
        p.Rect(12, 50, 3, 9, 0x07050C);
        p.Rect(10, 54, 7, 1, 0x5A5470);
        foreach (var (bx, by, c) in new[] { (w - 22, 50, 0xE8C040u), (w - 26, 54, 0x58B868u), (w - 18, 54, 0xD85048u), (w - 22, 58, 0x4A78D0u) })
        {
            Disc(p, bx + 1.5, by + 1.5, 2.2, 0x07050C);
            Disc(p, bx + 1.5, by + 1.5, 1.4, c);
        }

        return p;
    }
}
