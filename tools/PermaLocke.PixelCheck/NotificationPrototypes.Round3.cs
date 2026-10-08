using PermaLocke.App.Views.Pixel;

namespace PermaLocke.PixelCheck;

/// <summary>Third round of notice prototypes (2026-10-08): the seven ideas the player picked from the list.</summary>
public sealed partial class NotificationPrototypes
{
    private static (string Name, string Title, Look Look)[] RoundThree =>
    [
        ("K", "MENÚ DE ALOLA", AlolaMenu),
        ("L", "ROTOM MÓVIL", RotomChat),
        ("M", "ALARMA DE LABORATORIO", LabAlarm),
        ("N", "CINTA DE VÍDEO", VideoTape),
        ("O", "PERGAMINO", Scroll),
        ("P", "CARTAS EN ABANICO", CardFan),
        ("Q", "SPRITE QUE SALTA", PopOut)
    ];

    // ---------------------------------------------------------------- helpers

    private static void GradNotched(Pix p, int x, int y, int w, int h, uint top, uint bottom, int n)
    {
        for (var yy = 0; yy < h; yy++)
        {
            var inset = yy < n ? n - yy : yy >= h - n ? yy - (h - n) + 1 : 0;
            p.Rect(x + inset, y + yy, w - (2 * inset), 1, Mix(top, bottom, h <= 1 ? 0 : yy / (double)(h - 1)));
        }
    }

    private static Pix Gray(Pix source, double amount)
    {
        var grey = new Pix(source.W, source.H);
        for (var i = 0; i < source.B.Length; i += 4)
        {
            var luma = (byte)((source.B[i + 2] * 0.3) + (source.B[i + 1] * 0.59) + (source.B[i] * 0.11));
            for (var c = 0; c < 3; c++) grey.B[i + c] = (byte)((source.B[i + c] * (1 - amount)) + (luma * amount));
            grey.B[i + 3] = source.B[i + 3];
        }

        return grey;
    }

    /// <summary>The picture with a red and a cyan ghost at its sides: a tape that lost its tracking.</summary>
    private static void ChromaBlit(Pix p, Pix icon, int x, int y)
    {
        for (var sy = 0; sy < icon.H; sy++)
        {
            for (var sx = 0; sx < icon.W; sx++)
            {
                var i = ((sy * icon.W) + sx) * 4;
                if (icon.B[i + 3] < 128) continue;
                var shift = (sy / 3) % 7 == 0 ? 2 : 0;
                p.Set(x + sx - 1 + shift, y + sy, 0xFF3040, 110);
                p.Set(x + sx + 1 + shift, y + sy, 0x30E0FF, 110);
            }
        }

        for (var sy = 0; sy < icon.H; sy++)
        {
            for (var sx = 0; sx < icon.W; sx++)
            {
                var i = ((sy * icon.W) + sx) * 4;
                if (icon.B[i + 3] < 128) continue;
                var shift = (sy / 3) % 7 == 0 ? 2 : 0;
                p.Set(x + sx + shift, y + sy, Rgb(icon.B[i + 2], icon.B[i + 1], icon.B[i]));
            }
        }
    }

    // ---------------------------------------------------------------- K: the Alola menu

    private static Pix AlolaMenu(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0xF4F0FF, 0x20284A, 0x20284A, 0);

        const int w = 150, h = 46;
        var p = new Pix(w, h);
        var death = s.Mark == Mark.Skull;
        var tint = death ? Mix(s.Accent, 0x777080, 0.45) : s.Accent;
        var navy = 0x20284Au;

        // La sombra, el borde azul marino y el cuerpo blanco con un degradado hacia el color del tipo.
        p.Notched(2, 3, w - 3, h - 4, 0x000000, 5, 90);
        p.Notched(0, 0, w - 3, h - 4, navy, 5);
        GradNotched(p, 1, 1, w - 5, h - 6, 0xFFFFFF, Mix(0xFFFFFF, tint, death ? 0.55 : 0.4), 4);
        p.HLine(6, 2, w - 15, 0xFFFFFF, 200);

        // La luna de Ultra Luna, mordida, en la esquina.
        for (var y = 3; y < 15; y++)
        {
            for (var x = w - 20; x < w - 6; x++)
            {
                double ax = x + 0.5 - (w - 13), ay = y + 0.5 - 8;
                double bx = x + 0.5 - (w - 10.5), by = y + 0.5 - 6.5;
                if ((ax * ax) + (ay * ay) <= 20 && (bx * bx) + (by * by) > 14) p.Set(x, y, 0xF0C850);
            }
        }

        // La insignia del Pokémon: aro blanco sobre azul, centro del color del tipo.
        Disc(p, 22.5, 21.5, 16, navy);
        Disc(p, 22.5, 21.5, 14.5, 0xFFFFFF);
        Disc(p, 22.5, 21.5, 12.5, tint);
        Disc(p, 22.5, 21.5, 10.5, Darker(tint, 0.45));
        Ring(p, 22.5, 21.5, 13, 12.2, 0xFFFFFF, 160);
        if (icon is not null) p.Blit(icon, 23 - (icon.W / 2), 22 - (icon.H / 2));

        // La pestaña del rótulo, el título y el detalle.
        p.Notched(43, 4, (s.Label.Length * 4) + 7, 8, tint, 2);
        p.Small(s.Label, 46, 6, 0xFFFFFF);
        p.Text(Fit(s.Title, 98), 43, 14, navy);
        p.Small(FitSmall(s.Message, 98), 43, 26, 0x5A607E);

        // La cuenta atrás: gominolas redondas.
        for (var i = 0; i < 12; i++)
        {
            var on = i < Lit;
            p.Notched(43 + (i * 7), 34, 6, 4, on ? tint : 0xC8CCE0, 1);
            if (on) p.Set(44 + (i * 7), 34, 0xFFFFFF, 150);
        }

        return p;
    }

    // ---------------------------------------------------------------- L: the Rotom phone chat

    private static Pix RotomChat(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0xFFFFFF, 0x2A2A44, 0x2A2A44, 0);

        const int w = 154, h = 54;
        var p = new Pix(w, h);
        var death = s.Mark == Mark.Skull;
        var navy = 0x2A2A44u;

        // El aparato de Rotom: casco rojo, un ojo enorme y un rayito amarillo en la antena.
        p.Notched(1, 11, 28, 34, navy, 5);
        p.Notched(2, 12, 26, 32, 0xD2402F, 4);
        p.VGrad(3, 13, 24, 8, 0xF26A4F, 0xD2402F);
        Disc(p, 15, 28, 9, navy);
        Disc(p, 15, 28, 8, 0xFFFFFF);
        if (death)
        {
            Line(p, 11, 24, 19, 32, 0xD84030);
            Line(p, 12, 24, 20, 32, 0xD84030);
            Line(p, 19, 24, 11, 32, 0xD84030);
            Line(p, 20, 24, 12, 32, 0xD84030);
        }
        else
        {
            Disc(p, 16, 28, 4, navy);
            Disc(p, 17, 27, 1.2, 0xFFFFFF);
        }

        foreach (var (x0, y0, x1, y1) in new[] { (15, 10, 12, 6), (12, 6, 16, 5), (16, 5, 13, 1) }) Line(p, x0, y0, x1, y1, 0xFFE070);
        p.Set(25, 40, 0x58D870);
        p.Set(22, 40, 0xFFE070);

        // El globo de chat con su rabito hacia Rotom.
        p.Notched(33, 5, w - 34, 44, navy, 4);
        p.Notched(34, 6, w - 36, 42, 0xFFFFFF, 3);
        for (var dy = -4; dy <= 4; dy++)
        {
            var span = 5 - Math.Abs(dy);
            p.Rect(33 - span, 28 + dy, span + 1, 1, navy);
        }

        for (var dy = -3; dy <= 3; dy++)
        {
            var span = 4 - Math.Abs(dy);
            p.Rect(34 - span, 28 + dy, span + 1, 1, 0xFFFFFF);
        }

        // Rotom habla: remitente, título, detalle y la etiqueta del tipo.
        p.Small("ROTOM · ¡ZZZT!", 40, 9, 0x8A8AB0);
        p.Notched(w - 12 - ((s.Label.Length * 4) + 6), 8, (s.Label.Length * 4) + 6, 8, s.Accent, 2);
        p.Small(s.Label, w - 9 - ((s.Label.Length * 4) + 6), 10, 0xFFFFFF);
        p.Text(Fit(s.Title, 90), 40, 17, navy);
        p.Small(FitSmall(s.Message, 78), 40, 30, 0x5A5A80);

        // El Pokémon en una burbuja pequeña y los puntitos de «escribiendo» como cuenta atrás.
        Disc(p, w - 20, 39, 8, Mix(s.Accent, 0xFFFFFF, 0.5));
        if (icon is not null) p.Blit(icon, w - 20 - (icon.W / 2), 39 - (icon.H / 2));
        for (var i = 0; i < 12; i++) Disc(p, 42 + (i * 5), 42, 1.4, i < Lit ? s.Accent : 0xD0D0E4);
        return p;
    }

    // ---------------------------------------------------------------- M: the lab alarm

    private static Pix LabAlarm(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0x2A1E10, 0xF0C020, 0xFFE9A0, 1);

        const int w = Width, h = 48;
        var p = new Pix(w, h);
        var death = s.Mark == Mark.Skull;
        var hazard = death ? 0xE8E050u : s.Accent;

        // El cuerpo oscuro, con franjas de peligro arriba y abajo.
        p.Notched(0, 0, w - 2, h - 2, 0x050308, 3);
        p.VGrad(1, 6, w - 4, h - 12, Mix(0x1A1018, hazard, 0.1), 0x0C0810);
        for (var y = 0; y < 5; y++)
        {
            for (var x = 1; x < w - 3; x++)
            {
                var yellow = (((x + y) / 4) & 1) == 0;
                var inCorner = (y < 2 && (x < 3 || x > w - 6));
                if (inCorner) continue;
                p.Set(x, 1 + y, yellow ? hazard : 0x0C0810);
                p.Set(x, h - 7 + y, yellow ? hazard : 0x0C0810);
            }
        }

        // La sirena: base, cúpula y los haces de luz a los lados.
        p.Rect(5, 35, 20, 4, 0x4A4458);
        p.Rect(5, 35, 20, 1, 0x7A7490);
        for (var y = 21; y < 35; y++)
        {
            var half = Math.Sqrt(Math.Max(0, 1 - Math.Pow((y - 35) / 14.0, 2))) * 9;
            for (var x = 15 - (int)half; x <= 15 + (int)half; x++)
            {
                var shade = Math.Clamp(((x - 15) / 9.0) + 0.5, 0, 1);
                p.Set(x, y, Mix(Lighter(hazard, 0.55), Darker(hazard, 0.35), shade));
            }
        }

        p.Rect(11, 24, 2, 5, 0xFFFFFF, 190);
        for (var i = 0; i < 8; i++)
        {
            var angle = Math.PI * (0.1 + (i * 0.114));
            for (var r = 12; r < 24; r++)
            {
                if ((r + i) % 2 == 0) p.Set(15 + (int)(Math.Cos(angle) * r * 0.85), 29 - (int)(Math.Sin(angle) * r), hazard, 130 - (r * 3));
            }
        }

        // El monitor con el Pokémon: marco con esquinas marcadas.
        p.Rect(32, 10, 28, 28, 0x120C16);
        p.Outline(32, 10, 28, 28, hazard, 120);
        foreach (var (cx, cy, dx, dy) in new[] { (32, 10, 1, 1), (59, 10, -1, 1), (32, 37, 1, -1), (59, 37, -1, -1) })
        {
            for (var i = 0; i < 5; i++)
            {
                p.Set(cx + (dx * i), cy, hazard);
                p.Set(cx, cy + (dy * i), hazard);
            }
        }

        if (icon is not null) p.Blit(death ? Gray(icon, 0.7) : icon, 46 - (icon.W / 2), 24 - (icon.H / 2));

        // Texto: etiqueta de alerta, título, detalle y barra de tiempo en segmentos.
        p.Notched(64, 8, (s.Label.Length * 4) + 17, 8, hazard, 1);
        DrawMark(p, Mark.Warn, 66, 8, 0x0C0810);
        p.Small(s.Label, 76, 10, 0x0C0810);
        p.Text(Fit(s.Title, 90), 64, 17, 0xFFFFFF, shadow: 0x000000);
        p.Small(FitSmall(s.Message, 90), 64, 29, Lighter(hazard, 0.3));
        for (var i = 0; i < 14; i++) p.Rect(64 + (i * 6), 36, 5, 2, i < Lit * 14 / 12 ? hazard : 0x2A2230);
        return p;
    }

    // ---------------------------------------------------------------- N: the video tape

    private static Pix VideoTape(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0x0E1218, 0x000000, 0xE8E8D8, 0);

        const int w = 152, h = 54;
        var p = new Pix(w, h);
        var random = new Random(StableHash(s.Label) + 9);
        var amber = 0xF0C060u;

        // La pantalla del visor, con viñeta y líneas de barrido.
        p.Notched(0, 0, w - 2, h - 2, 0x000000, 3);
        p.VGrad(1, 1, w - 4, h - 4, 0x16202A, 0x0A0E14);
        for (var y = 2; y < h - 3; y += 2) p.HLine(1, y, w - 4, 0x000000, 60);

        // Las esquinas del encuadre.
        foreach (var (cx, cy, dx, dy) in new[] { (5, 5, 1, 1), (w - 8, 5, -1, 1), (5, h - 8, 1, -1), (w - 8, h - 8, -1, -1) })
        {
            for (var i = 0; i < 6; i++)
            {
                p.Set(cx + (dx * i), cy, 0xE8E8D8);
                p.Set(cx, cy + (dy * i), 0xE8E8D8);
            }
        }

        // REC, el tiempo y la batería.
        Disc(p, 12, 10, 2.2, 0xFF3030);
        p.Small("REC", 17, 8, 0xE8E8D8);
        p.Small(s.Label, 38, 8, s.Accent);
        p.Small("00:12:07", w - 42, 8, 0xE8E8D8);

        // El Pokémon con la cinta desajustada.
        if (icon is not null) ChromaBlit(p, icon, 24 - (icon.W / 2), 30 - (icon.H / 2));

        // Texto del cuadro.
        p.Text(Fit(s.Title, 90), 46, 15, 0xFFFFFF, shadow: 0x000000);
        p.Small(FitSmall(s.Message, 90), 46, 27, amber);

        // La línea de tiempo: lo que queda de cinta, con el cabezal.
        p.HLine(46, 38, 90, 0x40505A);
        p.HLine(46, 38, 90 * Lit / 12, s.Accent);
        p.Rect(46 + (90 * Lit / 12) - 1, 35, 3, 7, 0xFFFFFF);
        p.Small("08 10 2026", w - 50, 45, amber);

        // Fallos de seguimiento: una banda desplazada y ruido.
        p.Rect(3, 44, w - 8, 1, 0xFFFFFF, 34);
        for (var i = 0; i < 20; i++) p.Rect(random.Next(3, w - 20), 31 + random.Next(2), 4 + random.Next(14), 1, 0xFFFFFF, 90);
        for (var i = 0; i < 60; i++) p.Set(random.Next(2, w - 4), random.Next(2, h - 4), 0xFFFFFF, 40);
        return p;
    }

    // ---------------------------------------------------------------- O: the scroll

    private static Pix Scroll(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0xE8D4A0, 0x5A3A1A, 0x4A2E14, 1);

        const int w = 160, h = 56;
        var p = new Pix(w, h);
        var random = new Random(StableHash(s.Label) + 4);
        var ink = 0x4A2E14u;
        var sepia = 0x7A5A38u;

        // La hoja, con el borde de arriba y de abajo algo ondulado.
        for (var x = 6; x < w - 6; x++)
        {
            var wave = (int)Math.Round(Math.Sin(x * 0.35) * 1.2);
            for (var y = 6 + wave; y < h - 6 + wave; y++) p.Set(x, y, Mix(Mix(0xF0DFB0, 0xD8BE84, (y - 6) / 44.0), 0xB89860, random.NextDouble() * 0.08));
        }

        for (var i = 0; i < 24; i++) Disc(p, random.Next(12, w - 12), random.Next(10, h - 10), 1 + random.NextDouble() * 2, 0xA88850, 40);
        p.HLine(14, 9, w - 28, sepia, 100);
        p.HLine(14, h - 10, w - 28, sepia, 100);

        // Los dos rollos, de arriba abajo, con sombreado de cilindro.
        foreach (var rx in new[] { 0, w - 12 })
        {
            for (var x = 0; x < 12; x++)
            {
                var shade = Math.Abs(((x + 0.5) / 12) - 0.35);
                for (var y = 2; y < h - 2; y++)
                {
                    var end = y < 5 || y > h - 6;
                    p.Set(rx + x, y, end ? Darker(0xC8A468, 0.35 + shade) : Mix(Lighter(0xE8D4A0, 0.2), Darker(0xA88850, 0.4), shade * 1.4));
                }
            }

            p.Rect(rx + 1, 1, 10, 1, 0x3A2410);
            p.Rect(rx + 1, h - 2, 10, 1, 0x3A2410);
            p.VLine(rx + 5, 3, h - 6, 0x6A4A28, 90);
        }

        // El grabado del Pokémon, en sepia, con marco de tinta.
        p.Outline(16, 12, 26, 32, ink, 200);
        p.Rect(17, 13, 24, 30, Mix(s.Accent, 0xF0DFB0, 0.7), 150);
        if (icon is not null) p.Blit(Tinted(icon, 0x8A6A40, 0.3), 29 - (icon.W / 2), 28 - (icon.H / 2));

        // Texto con tinta, y el rótulo como cinta roja.
        p.Notched(44, 10, (s.Label.Length * 4) + 8, 8, s.Accent, 1);
        p.Small(s.Label, 48, 12, 0xFFF4D8);
        p.Text(Fit(s.Title, 90), 44, 20, ink);
        p.Small(FitSmall(s.Message, 76), 44, 31, sepia);

        // La cuenta atrás: una mecha que se consume; lo que ya ardió queda negro.
        var burnt = 44 + (72 * Lit / 12);
        p.HLine(44, 41, burnt - 44, ink);
        p.HLine(burnt, 41, 44 + 72 - burnt, 0x2A1A0C, 90);
        Disc(p, burnt, 41, 2.6, 0xE85A20, 200);
        Disc(p, burnt, 41, 1.4, 0xFFE070);

        // El sello de lacre con su cinta.
        p.Rect(w - 31, h - 15, 3, 9, s.Accent);
        p.Rect(w - 25, h - 15, 3, 9, Darker(s.Accent, 0.3));
        Disc(p, w - 25, h - 17, 8, Darker(s.Accent, 0.35));
        Disc(p, w - 25.5, h - 17.5, 7, s.Accent);
        Ring(p, w - 25.5, h - 17.5, 5.5, 4.8, Lighter(s.Accent, 0.4), 160);
        DrawMark(p, s.Mark, w - 29, h - 21, Darker(s.Accent, 0.45));
        return p;
    }

    private static Pix Tinted(Pix source, uint colour, double amount)
    {
        var tinted = new Pix(source.W, source.H);
        for (var i = 0; i < source.B.Length; i += 4)
        {
            tinted.B[i] = (byte)((source.B[i] * (1 - amount)) + ((colour & 255) * amount));
            tinted.B[i + 1] = (byte)((source.B[i + 1] * (1 - amount)) + (((colour >> 8) & 255) * amount));
            tinted.B[i + 2] = (byte)((source.B[i + 2] * (1 - amount)) + (((colour >> 16) & 255) * amount));
            tinted.B[i + 3] = source.B[i + 3];
        }

        return tinted;
    }

    // ---------------------------------------------------------------- P: the fan of cards

    private static Pix CardBack(uint accent, int w, int h)
    {
        var back = new Pix(w, h);
        back.Notched(0, 0, w, h, 0x0A0814, 3);
        back.Notched(1, 1, w - 2, h - 2, 0xF4F0E4, 3);
        back.Notched(3, 3, w - 6, h - 6, Darker(accent, 0.45), 2);
        for (var y = 3; y < h - 3; y++)
        {
            for (var x = 3; x < w - 3; x++)
            {
                if (((x + y) / 3) % 2 == 0) back.Set(x, y, accent, 70);
            }
        }

        Disc(back, w / 2.0, h / 2.0, 9, 0x0A0814);
        Disc(back, w / 2.0, h / 2.0, 8, 0xF4F0E4);
        for (var y = 0; y < 8; y++) back.Rect((w / 2) - 8, (h / 2) - 8 + y, 16, 1, accent);
        Disc(back, w / 2.0, h / 2.0, 3, 0x0A0814);
        Disc(back, w / 2.0, h / 2.0, 1.8, 0xF4F0E4);
        return back;
    }

    private static Pix CardFan(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0xF4F0E4, 0x0A0814, 0x0A0814, 0);

        const int w = 152, h = 70, cw = 132, ch = 50;
        var p = new Pix(w, h);
        var death = s.Mark == Mark.Skull;
        var tint = death ? Mix(s.Accent, 0x707078, 0.5) : s.Accent;

        // Las cartas de detrás asoman en abanico: son los avisos que esperan.
        foreach (var (angle, tone) in new[] { (-9.0, 0.35), (6.0, 0.15) })
        {
            var layer = new Pix(w, h);
            layer.Blit(CardBack(Mix(tint, 0x303040, tone), cw, ch), 10, 12);
            p.Blit(Turned(layer, angle), 0, 0);
        }

        // La carta de delante.
        const int x = 8, y = 11;
        p.Notched(x + 2, y + 3, cw, ch, 0x000000, 3, 90);
        p.Notched(x, y, cw, ch, 0x0A0814, 3);
        GradNotched(p, x + 1, y + 1, cw - 2, ch - 2, 0xFFFDF4, 0xE8E0CC, 3);
        p.Notched(x + 2, y + 2, cw - 4, 11, tint, 2);
        DrawMark(p, s.Mark, x + 4, y + 4, 0xFFFFFF);
        p.Small(s.Label, x + 14, y + 5, 0xFFFFFF);
        p.Small("PS", x + cw - 22, y + 5, 0xFFFFFF);
        p.Small("120", x + cw - 15, y + 5, 0xFFFFFF);

        // La ventana del dibujo.
        p.Rect(x + 4, y + 15, 32, 31, 0x0A0814);
        p.VGrad(x + 5, y + 16, 30, 29, Lighter(tint, 0.45), Darker(tint, 0.3));
        for (var i = 0; i < 10; i++) p.Set(x + 6 + ((i * 7) % 28), y + 17 + ((i * 11) % 26), 0xFFFFFF, 90);
        if (icon is not null) p.Blit(death ? Gray(icon, 0.6) : icon, x + 20 - (icon.W / 2), y + 31 - (icon.H / 2));

        p.Text(Fit(s.Title, 90), x + 40, y + 16, 0x20202C);
        p.HLine(x + 40, y + 26, 88, 0x20202C, 70);
        p.Small(FitSmall(s.Message, 88), x + 40, y + 29, 0x5A5A68);

        // La cuenta atrás: energías que se vacían, en círculos.
        for (var i = 0; i < 12; i++)
        {
            var cx = x + 43 + (i * 6.2);
            Disc(p, cx, y + 42, 2.2, 0x20202C);
            Disc(p, cx, y + 42, 1.5, i < Lit ? tint : 0xD0CCBC);
        }

        return p;
    }

    // ---------------------------------------------------------------- Q: the sprite that pops out

    private static Pix PopOut(Sample s, Pix? icon)
    {
        if (s.Compact) return Slim(s, 0x1C1832, 0x050308, 0xFFFFFF, 0);

        const int w = 150, h = 64, top = 22;
        var p = new Pix(w, h);
        var death = s.Mark == Mark.Skull;

        // La losa del aviso.
        p.Notched(0, top, w - 2, h - top - 2, 0x050308, 3);
        GradNotched(p, 1, top + 1, w - 4, h - top - 4, 0x2A2448, 0x14102A, 3);
        p.HLine(4, top + 1, w - 10, s.Accent);
        p.HLine(4, top + 2, w - 10, Darker(s.Accent, 0.35));

        // La sombra del que salta, sobre la losa, y las marcas de impulso.
        for (var y = -3; y <= 3; y++)
        {
            var half = (int)(15 * Math.Sqrt(1 - (y * y / 9.0)));
            p.Rect(23 - half, top + 11 + y, half * 2, 1, 0x000000, 110);
        }

        foreach (var (x0, y0, x1, y1) in new[] { (6, 14, 2, 11), (7, 20, 1, 20), (40, 14, 44, 11), (39, 20, 46, 20), (23, 1, 23, -3) })
        {
            Line(p, x0, y0, x1, y1, Lighter(s.Accent, 0.35));
        }

        // El Pokémon sale disparado de la losa; una baja queda tumbado y gris.
        if (icon is not null)
        {
            var sprite = death ? Turned(Gray(icon, 0.8), 80) : icon;
            p.Blit(sprite, 23 - (sprite.W / 2), top + 11 - sprite.H);
        }

        if (death)
        {
            foreach (var (sx, sy) in new[] { (12, 4), (23, 1), (34, 4) }) DrawMark(p, Mark.Star, sx - 3, sy, 0xFFE070);
        }
        else
        {
            foreach (var (sx, sy) in new[] { (5, 6), (41, 5) })
            {
                p.Set(sx, sy, 0xFFFFFF);
                p.Set(sx - 1, sy, 0xFFFFFF, 130);
                p.Set(sx + 1, sy, 0xFFFFFF, 130);
                p.Set(sx, sy - 1, 0xFFFFFF, 130);
                p.Set(sx, sy + 1, 0xFFFFFF, 130);
            }
        }

        // Texto de la losa.
        p.Notched(52, top + 4, (s.Label.Length * 4) + 15, 8, s.Accent, 1);
        DrawMark(p, s.Mark, 54, top + 4, Darker(s.Accent, 0.7));
        p.Small(s.Label, 64, top + 6, Darker(s.Accent, 0.78));
        p.Text(Fit(s.Title, 90), 52, top + 13, 0xFFFFFF, shadow: 0x000000);
        p.Small(FitSmall(s.Message, 90), 52, top + 26, 0xB8B0D8);
        p.Rect(52, top + 34, 90, 2, 0x050308);
        p.Rect(52, top + 34, 90 * Lit / 12, 2, s.Accent);
        p.Rect(52, top + 34, 90 * Lit / 12, 1, Lighter(s.Accent, 0.45));
        return p;
    }
}
