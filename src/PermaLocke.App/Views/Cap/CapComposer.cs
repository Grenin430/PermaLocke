namespace PermaLocke.App.Views;

/// <summary>Where each part of the panel goes, in cells, for a mode and a grid: computed once and used by the still part and the moving part.</summary>
public sealed class CapLayout
{
    public CapMode Mode { get; init; }

    public CapPanelKind Panel { get; init; }

    public int Cols { get; init; }

    public int Rows { get; init; }

    /// <summary>The niches are narrow: the slim column, the strip's cards and the compact rows.</summary>
    public bool Narrow { get; init; }

    /// <summary>Where the case starts: the cup stands above it.</summary>
    public int Top { get; init; }

    public int Frame { get; init; }

    public CapRect Inner { get; init; }

    public CapRect Header { get; init; }

    public CapRect Shield { get; init; }

    public CapRect Banner { get; init; }

    public CapRect Gems { get; init; }

    public int GemSize { get; init; }

    public CapRect Fuse { get; init; }

    public CapRect Graves { get; init; }

    public CapRect Plinth { get; init; }

    public CapRect Body { get; init; }

    public CapRect Foot { get; init; }

    public CapRect[] Niches { get; init; } = [];

    /// <summary>Where everything inside each niche goes, measured when the still part is painted and used by the moving part.</summary>
    public CapNicheMetrics[] Metrics { get; set; } = [];

    public bool HasCup { get; init; }
}

/// <summary>
/// The composition of the cap panel (2026-10-10): for a mode and a grid of cells it decides where the shield, the fuse, the
/// niches and the little graves go (<see cref="Arrange"/>) and paints the still part of it (<see cref="Paint"/>).
/// </summary>
/// <remarks>
/// The grid is fluid: the geometry gives each mode a design size and the panel is drawn on whatever the band gives, so what is
/// left over becomes taller niches, thicker plinth and wider margins instead of an odd hole. What moves is
/// <see cref="CapLive"/>'s.
/// </remarks>
public static class CapComposer
{
    public const int PartySlots = 6;

    private static CapRect None => new(0, 0, 0, 0);

    public static CapLayout Arrange(CapMode mode, CapPanelKind panel, int cols, int rows, CapPlaqueData data, bool gravesInside)
    {
        return panel == CapPanelKind.Memorial ? ArrangeMemorial(mode, cols, rows, data)
            : mode switch
            {
                CapMode.Strip => ArrangeStrip(cols, rows, data),
                CapMode.Compact => ArrangeCompact(cols, rows, data),
                CapMode.Slim => ArrangeColumn(CapMode.Slim, cols, rows, data, gravesInside),
                _ => ArrangeColumn(CapMode.Column, cols, rows, data, gravesInside)
            };
    }

    // ---------------------------------------------------------------- the tall column and the slim one

    private static CapLayout ArrangeColumn(CapMode mode, int cols, int rows, CapPlaqueData data, bool gravesInside)
    {
        var slim = mode == CapMode.Slim;
        var dense = rows < 280;
        var frame = slim ? 4 : 5;
        const int top = 12;
        var inner = new CapRect(frame, top + frame, cols - (2 * frame), rows - top - (2 * frame));
        var margin = slim ? 2 : 3;
        var width = inner.Width - (2 * margin);

        var y = inner.Top + 3;
        var headerH = slim ? (dense ? 34 : 38) : dense ? 40 : 44;
        var header = new CapRect(inner.Left + margin, y, width, headerH);
        var shieldW = slim ? 28 : 40;
        var shield = new CapRect(header.Left, y, shieldW, headerH);
        var banner = new CapRect(shield.Right - 5, y + (slim ? 5 : 6), header.Right - shield.Right + 5, headerH - (slim ? 12 : 12));
        y += headerH + 2;

        var gems = None;
        var gemSize = width >= 120 ? 6 : 4;
        if (data.Trials > 0)
        {
            gems = new CapRect(inner.Left + margin, y, width, gemSize);
            y += gemSize + 3;
        }

        var fuseH = slim ? 20 : dense ? 22 : 28;
        var fuse = new CapRect(inner.Left + margin + 1, y, width - 2, fuseH);
        y += fuseH + 3;

        var gravesH = gravesInside ? (slim ? 16 : dense ? 20 : 24) : 0;
        var plinthH = slim ? 6 : 8;
        var bottom = inner.Bottom - 2;
        var avail = bottom - y - gravesH - (gravesH > 0 ? 3 : 0) - plinthH - 3;

        var gap = 2;
        var nicheMax = slim ? 28 : 40;
        var h = Math.Clamp((avail - (gap * (PartySlots - 1))) / PartySlots, 17, nicheMax);
        var leftover = avail - ((PartySlots * h) + (gap * (PartySlots - 1)));
        var extraPlinth = Math.Min(Math.Max(0, leftover), 30);
        plinthH += extraPlinth;
        leftover -= extraPlinth;
        gap += Math.Clamp(leftover / (PartySlots - 1), 0, 3);

        var niches = new CapRect[PartySlots];
        for (var i = 0; i < PartySlots; i++)
        {
            niches[i] = new CapRect(inner.Left + margin, y, width, h);
            y += h + gap;
        }

        y -= gap;
        var graves = None;
        if (gravesH > 0)
        {
            y += 3;
            graves = new CapRect(inner.Left + margin, y, width, gravesH);
        }

        var plinth = new CapRect(inner.Left, bottom - plinthH + 1, inner.Width, plinthH + 1);
        return new CapLayout
        {
            Mode = mode, Panel = CapPanelKind.Party, Cols = cols, Rows = rows, Narrow = slim, Top = top, Frame = frame, Inner = inner,
            Header = header, Shield = shield, Banner = banner, Gems = gems, GemSize = gemSize, Fuse = fuse, Graves = graves,
            Plinth = plinth, Niches = niches, HasCup = true
        };
    }

    // ---------------------------------------------------------------- the strip

    private static CapLayout ArrangeStrip(int cols, int rows, CapPlaqueData data)
    {
        const int frame = 4;
        var inner = new CapRect(frame, frame, cols - (2 * frame), rows - (2 * frame));
        var pad = 3;
        var h = inner.Height - (2 * pad);
        var x = inner.Left + pad;

        var shieldW = Math.Min(40, (int)(h * 0.9));
        var shield = new CapRect(x, inner.Top + pad, shieldW, Math.Min(h, 46));
        x = shield.Right + 3;

        // A narrow strip drops the plate with the name of the trial: the shield says the cap and that is the part that matters.
        var bannerW = cols >= 520 ? 74 : 0;
        var banner = bannerW > 0 ? new CapRect(x, inner.Top + pad + 2, bannerW, Math.Min(h - 4, 34)) : None;
        if (bannerW > 0) x = banner.Right + 4;

        var fuseW = Math.Clamp(inner.Width / 6, 70, 130);
        var fuse = new CapRect(x, inner.Top + pad, fuseW, Math.Min(h, 30));
        x = fuse.Right + 4;

        var rest = inner.Right - pad - x;
        var gap = 3;
        var cardW = (rest - ((PartySlots - 1) * gap)) / PartySlots;
        var niches = new CapRect[PartySlots];
        for (var i = 0; i < PartySlots; i++)
        {
            niches[i] = new CapRect(x + (i * (cardW + gap)), inner.Top + pad, cardW, h);
        }

        return new CapLayout
        {
            Mode = CapMode.Strip, Panel = CapPanelKind.Party, Cols = cols, Rows = rows, Narrow = true, Top = 0, Frame = frame,
            Inner = inner, Header = banner, Shield = shield, Banner = banner, Fuse = fuse, Niches = niches
        };
    }

    // ---------------------------------------------------------------- the compact plaque

    private static CapLayout ArrangeCompact(int cols, int rows, CapPlaqueData data)
    {
        const int frame = 3;
        var inner = new CapRect(frame, frame, cols - (2 * frame), rows - (2 * frame));
        var y = inner.Top + 2;
        var shield = new CapRect(inner.Left + 2, y, 24, 28);
        var banner = new CapRect(shield.Right + 2, y + 3, inner.Right - shield.Right - 4, 22);
        y += 30;
        var fuse = new CapRect(inner.Left + 3, y, inner.Width - 6, 12);
        y += 14;
        var rowH = Math.Clamp((inner.Bottom - y - 2) / PartySlots, 6, 9);
        var niches = new CapRect[PartySlots];
        for (var i = 0; i < PartySlots; i++) niches[i] = new CapRect(inner.Left + 2, y + (i * rowH), inner.Width - 4, rowH - 1);

        return new CapLayout
        {
            Mode = CapMode.Compact, Panel = CapPanelKind.Party, Cols = cols, Rows = rows, Narrow = true, Top = 0, Frame = frame,
            Inner = inner, Header = banner, Shield = shield, Banner = banner, Fuse = fuse, Niches = niches
        };
    }

    // ---------------------------------------------------------------- the memorial

    private static CapLayout ArrangeMemorial(CapMode mode, int cols, int rows, CapPlaqueData data)
    {
        if (mode == CapMode.Strip)
        {
            const int sf = 4;
            var si = new CapRect(sf, sf, cols - (2 * sf), rows - (2 * sf));
            var sh = si.Height - 6;
            var banner = new CapRect(si.Left + 3, si.Top + 3, 74, Math.Min(sh, 34));
            var body = new CapRect(banner.Right + 4, si.Top + 3, si.Right - banner.Right - 7, sh);
            return new CapLayout
            {
                Mode = mode, Panel = CapPanelKind.Memorial, Cols = cols, Rows = rows, Narrow = true, Top = 0, Frame = sf, Inner = si,
                Header = banner, Banner = banner, Body = body
            };
        }

        var slim = mode == CapMode.Slim;
        var frame = slim ? 4 : 5;
        const int top = 12;
        var inner = new CapRect(frame, top + frame, cols - (2 * frame), rows - top - (2 * frame));
        var margin = slim ? 2 : 3;
        var width = inner.Width - (2 * margin);
        var y = inner.Top + 3;
        var header = new CapRect(inner.Left + margin, y, width, slim ? 26 : 30);
        y += header.Height + 3;
        var footH = slim ? 26 : 34;
        var plinthH = slim ? 6 : 8;
        var foot = new CapRect(inner.Left + margin, inner.Bottom - 2 - plinthH - 3 - footH, width, footH);
        var bodyRect = new CapRect(inner.Left + margin, y, width, foot.Top - 3 - y);
        var plinth = new CapRect(inner.Left, inner.Bottom - 2 - plinthH + 1, inner.Width, plinthH + 1);
        return new CapLayout
        {
            Mode = mode, Panel = CapPanelKind.Memorial, Cols = cols, Rows = rows, Narrow = slim, Top = top, Frame = frame, Inner = inner,
            Header = header, Banner = header, Body = bodyRect, Foot = foot, Plinth = plinth, HasCup = true
        };
    }

    // ---------------------------------------------------------------- the still part

    /// <summary>Paints everything that does not move: the case, the cup, the shield, the texts, the grooves of the bars and the niches.</summary>
    public static void Paint(CapSheet p, CapLayout layout, CapPlaqueData data)
    {
        p.Clear();
        var seed = CapArt.StableHash(data.Trial) + data.Cap;

        if (layout.Panel == CapPanelKind.Memorial)
        {
            PaintMemorial(p, layout, data, seed);
            return;
        }

        CapArt.Case(p, layout.Cols, layout.Rows, layout.Top, layout.Frame, seed);
        if (layout.HasCup) Cup(p, layout);

        PaintShield(p, layout, data);
        PaintBanner(p, layout, data);
        PaintGems(p, layout, data);
        PaintFuse(p, layout, data);

        layout.Metrics = new CapNicheMetrics[layout.Niches.Length];
        for (var i = 0; i < layout.Niches.Length; i++)
        {
            var member = i < data.Party.Count ? data.Party[i] : null;
            if (layout.Mode == CapMode.Compact)
            {
                layout.Metrics[i] = default(CapNicheMetrics) with { Outer = layout.Niches[i], HpBar = PaintCompactRow(p, layout.Niches[i], member, data.Cap) };
            }
            else
            {
                layout.Metrics[i] = CapNiche.Measure(layout.Niches[i], member, layout.Narrow);
                CapNiche.Paint(p, layout.Niches[i], member, data.Cap, layout.Narrow, i);
            }
        }

        if (layout.Graves.Width > 0) PaintGraves(p, layout, data);
        if (layout.Plinth.Width > 0) CapArt.Plinth(p, layout.Plinth, layout.Plinth.Height >= 14 ? "PERMALOCKE" : null);
    }

    private static void Cup(CapSheet p, CapLayout layout)
    {
        var x = (layout.Cols / 2) - (CapArt.CupWidth / 2);
        CapArt.Cup(p, x, 0);
        CapArt.Brass(p, (layout.Cols / 2) - 9, layout.Top - 2, 19, 4, false);
    }

    private static void PaintShield(CapSheet p, CapLayout layout, CapPlaqueData data)
    {
        var s = layout.Shield;
        var face = CapArt.Shield(p, s.Left, s.Top, s.Width, s.Height);
        var cap = data.Cap.ToString();

        var scale = 1;
        foreach (var candidate in new[] { 3, 2 })
        {
            if (CapSheet.TextWidth(cap, candidate) <= face.Width - 2 && 7 * candidate <= face.Height - 6) { scale = candidate; break; }
        }

        var nvY = face.Top + 1;
        var small = layout.Mode == CapMode.Compact;
        if (!small) p.Small("NV", s.Left + ((s.Width - 7) / 2), nvY, CapArt.BrassHigh);
        var textY = small ? face.Top + 1 : nvY + 7;
        var textX = s.Left + ((s.Width - CapSheet.TextWidth(cap, scale)) / 2);
        p.Text(cap, textX, textY, CapArt.Gold, scale, CapArt.Ink);
    }

    private static void PaintBanner(CapSheet p, CapLayout layout, CapPlaqueData data)
    {
        var b = layout.Banner;
        if (b.Width < 20 || b.Height < 12) return;

        CapArt.Brass(p, b.Left, b.Top, b.Width, b.Height);
        var textX = b.Left + 9;
        var room = (b.Width - 16) / 4;
        var y = b.Top + 4;

        void Engrave(string text, int yy)
        {
            p.Small(text, textX + 1, yy + 1, CapArt.Shine);
            p.Small(text, textX, yy, CapArt.Engraved);
        }

        if (layout.Mode == CapMode.Compact)
        {
            Engrave("CAP", b.Top + 3);
            Engrave(data.Next is { } n ? $"SIG {n}" : "ULTIMO", b.Top + 11);
            return;
        }

        Engrave("CAP DE NIVEL", y);
        var trial = data.Trial.Length > room ? ShortTrial(data.Trial) : data.Trial;
        var lines = Wrap(trial, Math.Max(6, room), 2);
        for (var i = 0; i < lines.Count; i++) Engrave(lines[i], y + 8 + (i * 6));
        if (b.Height >= 26 + ((lines.Count - 1) * 6)) Engrave(data.Next is { } next ? $"SIG: {next}" : "ULTIMO", y + 8 + (lines.Count * 6) + 1);
    }

    /// <summary>«PRUEBA 8 DE 12» as «PRUEBA 8/12», for a plate that is too narrow for it; any other name as it is.</summary>
    public static string ShortTrial(string trial) =>
        trial.StartsWith("PRUEBA ", StringComparison.Ordinal) ? trial.Replace(" DE ", "/", StringComparison.Ordinal) : trial;

    /// <summary>A trial name in at most <paramref name="max"/> lines of <paramref name="chars"/> letters: «PRUEBA 3 DE 12» stays whole if it fits.</summary>
    public static List<string> Wrap(string text, int chars, int max)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (candidate.Length <= chars || line.Length == 0)
            {
                line = candidate.Length > chars ? candidate[..chars] : candidate;
                continue;
            }

            lines.Add(line);
            line = word.Length > chars ? word[..chars] : word;
        }

        if (line.Length > 0) lines.Add(line);
        return lines.Take(max).ToList();
    }

    private static void PaintGems(CapSheet p, CapLayout layout, CapPlaqueData data)
    {
        if (layout.Gems.Width == 0 || data.Trials <= 0) return;
        var size = layout.GemSize;
        var step = size + 1;
        var width = (data.Trials * step) - 1;
        var x = layout.Gems.Left + ((layout.Gems.Width - width) / 2);
        for (var i = 0; i < data.Trials; i++) CapArt.Gem(p, x + (i * step), layout.Gems.Top, size, i < data.Cleared);
    }

    // ---------------------------------------------------------------- the fuse

    /// <summary>Where the rope runs: the track of the fuse, in cells.</summary>
    public static (int X, int Y, int Span) FuseTrack(CapLayout layout)
    {
        var f = layout.Fuse;
        var compact = layout.Mode == CapMode.Compact;
        var span = f.Width - (compact ? 6 : 14);
        return (f.Left, f.Top + (compact ? 8 : layout.Mode == CapMode.Strip ? 14 : f.Height - 10), Math.Max(10, span));
    }

    /// <summary>What the fuse is called, for the room it has.</summary>
    public static string FuseLabel(CapLayout layout) => layout.Fuse.Width >= 110 ? "NIVEL DEL EQUIPO" : "EQUIPO";

    private static void PaintFuse(CapSheet p, CapLayout layout, CapPlaqueData data)
    {
        var f = layout.Fuse;
        var (x, y, span) = FuseTrack(layout);
        if (layout.Mode == CapMode.Compact)
        {
            for (var t = 0; t <= span; t += 6) p.VLine(x + t, y + 3, t % 12 == 0 ? 3 : 2, CapArt.Paper, 140);
            CapArt.Pennant(p, x + span + 1, y - 7, CapArt.Gold);
            return;
        }

        p.Small(FuseLabel(layout), f.Left, f.Top, CapArt.Paper);
        for (var t = 0; t <= span; t += 6) p.VLine(x + t, y + 4, t % 12 == 0 ? 3 : 2, CapArt.Paper, 140);
        CapArt.Pennant(p, x + span + 3, y - 9, CapArt.Gold);
        p.Small($"{data.Cap}", x + span - 1, y + 8, CapArt.Paper);
    }

    // ---------------------------------------------------------------- compact rows and graves

    private static CapRect PaintCompactRow(CapSheet p, CapRect r, CapPlaqueMember? m, int cap)
    {
        p.Rect(r.Left, r.Top, r.Width, r.Height, CapSheet.Darker(CapArt.Back, 0.2));
        if (m is null) return None;
        var fallen = m.Fallen;
        var name = m.Egg ? "HUEVO" : m.Name.ToUpperInvariant();
        var chars = Math.Max(3, (r.Width - 34) / 4);
        if (name.Length > chars) name = name[..chars];
        p.Small(name, r.Left + 2, r.Top + 1, fallen ? CapArt.Dim : CapArt.Paper);
        if (m.Egg) return None;
        if (fallen)
        {
            p.HLine(r.Left + 2, r.Top + 3, CapSheet.SmallWidth(name), CapArt.HpRed, 220);
            return None;
        }

        var level = m.Level.ToString();
        p.Small(level, r.Right - 3 - CapSheet.SmallWidth(level), r.Top + 1, m.Level >= cap ? CapArt.Gold : CapArt.BrassHigh);
        var bar = new CapRect(r.Left + 2 + (chars * 4) + 2, r.Top + 1, r.Width - 8 - (chars * 4) - CapSheet.SmallWidth(level), 4);
        CapArt.BarFrame(p, bar.Left, bar.Top, bar.Width, bar.Height);
        return bar;
    }

    private static void PaintGraves(CapSheet p, CapLayout layout, CapPlaqueData data)
    {
        var g = layout.Graves;
        var graves = data.Graves ?? [];
        var slim = layout.Narrow;
        p.Rect(g.Left, g.Top, g.Width, g.Height, CapSheet.Darker(CapArt.Back, 0.25));
        p.HLine(g.Left, g.Top, g.Width, CapArt.GoldDim, 120);

        var label = graves.Count == 0 ? "SIN CAIDOS" : $"CAIDOS {graves.Count}";
        p.Small(label, g.Left + 3, g.Top + 3, graves.Count == 0 ? CapArt.Dim : CapArt.HpRed);
        if (graves.Count == 0) return;

        var stoneW = slim ? 9 : 11;
        var stoneH = g.Height - 6;
        var x0 = g.Left + 3 + CapSheet.SmallWidth(label) + 5;
        var room = (g.Right - 3 - x0) / (stoneW + 2);
        var shown = Math.Min(graves.Count, Math.Max(0, room));
        for (var i = 0; i < shown; i++)
        {
            var more = graves.Count > shown && i == shown - 1;
            if (more)
            {
                p.Small($"+{graves.Count - shown + 1}", x0 + (i * (stoneW + 2)), g.Top + 3 + (stoneH / 2) - 2, CapArt.Paper);
                break;
            }

            CapArt.Tombstone(p, x0 + (i * (stoneW + 2)), g.Top + 3, stoneW, stoneH, graves[i].Sprite, i);
        }
    }

    // ---------------------------------------------------------------- the memorial

    private static void PaintMemorial(CapSheet p, CapLayout layout, CapPlaqueData data, int seed)
    {
        CapArt.Case(p, layout.Cols, layout.Rows, layout.Top, layout.Frame, seed + 7);
        var graves = data.Graves ?? [];

        if (layout.HasCup)
        {
            var x = (layout.Cols / 2) - 3;
            Candle(p, x, 0);
            CapArt.Brass(p, (layout.Cols / 2) - 9, layout.Top - 2, 19, 4, false);
        }

        var b = layout.Banner;
        CapArt.Brass(p, b.Left, b.Top, b.Width, b.Height);
        if (layout.Mode == CapMode.Strip)
        {
            p.Small("CEMENTERIO", b.Left + 9, b.Top + 5, CapArt.Shine);
            p.Small("CEMENTERIO", b.Left + 8, b.Top + 4, CapArt.Engraved);
            p.Small(graves.Count == 0 ? "SIN CAIDOS" : $"{graves.Count} CAIDOS", b.Left + 8, b.Top + 13, CapArt.Engraved);
            PaintTombstones(p, layout.Body, graves, layout.Narrow, strip: true);
            return;
        }

        var title = "CEMENTERIO";
        var tx = b.Left + ((b.Width - CapSheet.TextWidth(title)) / 2);
        p.Text(title, tx + 1, b.Top + 5, CapArt.Shine);
        p.Text(title, tx, b.Top + 4, CapArt.Engraved);
        var count = graves.Count == 0 ? "NINGUN CAIDO" : graves.Count == 1 ? "1 CAIDO" : $"{graves.Count} CAIDOS";
        var cx = b.Left + ((b.Width - CapSheet.SmallWidth(count)) / 2);
        p.Small(count, cx + 1, b.Top + b.Height - 9, CapArt.Shine);
        p.Small(count, cx, b.Top + b.Height - 10, CapArt.Engraved);

        var shelf = Shelf(layout);
        PaintShelf(p, shelf, layout);
        PaintTombstones(p, new CapRect(layout.Body.Left, layout.Body.Top, layout.Body.Width, Math.Max(10, shelf.Top - 4 - layout.Body.Top)), graves, layout.Narrow, strip: false);
        PaintRun(p, layout, data);
        if (layout.Plinth.Width > 0) CapArt.Plinth(p, layout.Plinth, layout.Plinth.Height >= 14 ? "EN MEMORIA" : null);
    }

    /// <summary>The shelf at the foot of the memorial, where the candles stand.</summary>
    public static CapRect Shelf(CapLayout layout) =>
        new(layout.Body.Left, layout.Body.Bottom - 16, layout.Body.Width, 16);

    /// <summary>Where each candle of the shelf stands: the x of its middle, spread along the shelf.</summary>
    public static IEnumerable<int> CandleXs(CapLayout layout)
    {
        var shelf = Shelf(layout);
        var count = Math.Max(2, shelf.Width / 46);
        for (var i = 0; i < count; i++) yield return shelf.Left + (int)((i + 0.5) * shelf.Width / count);
    }

    private static void PaintShelf(CapSheet p, CapRect shelf, CapLayout layout)
    {
        p.Rect(shelf.Left, shelf.Bottom - 3, shelf.Width, 3, CapArt.Walnut);
        p.HLine(shelf.Left, shelf.Bottom - 3, shelf.Width, CapArt.WalnutLight);
        p.HLine(shelf.Left, shelf.Bottom - 1, shelf.Width, CapArt.WalnutDark);
        foreach (var cx in CandleXs(layout)) Candle(p, cx - 3, shelf.Top + 1);
    }

    private static void Candle(CapSheet p, int x, int y)
    {
        // A brass holder, a wax stub and room for the flame above it (drawn by the moving part).
        p.Rect(x, y + 9, 7, 2, CapArt.GoldDim);
        p.HLine(x, y + 9, 7, CapArt.BrassHigh);
        p.Rect(x + 2, y + 4, 3, 5, CapArt.Paper);
        p.VLine(x + 2, y + 4, 5, CapSheet.Lighter(CapArt.Paper, 0.3));
        p.VLine(x + 4, y + 4, 5, CapSheet.Darker(CapArt.Paper, 0.2));
    }

    private static void PaintTombstones(CapSheet p, CapRect body, IReadOnlyList<CapGrave> graves, bool narrow, bool strip)
    {
        if (graves.Count == 0)
        {
            var text = "TODOS SIGUEN VIVOS";
            var x = body.Left + ((body.Width - CapSheet.SmallWidth(text)) / 2);
            var y = body.Top + (body.Height / 2) - 2;
            p.Small(text, x, y, CapArt.Dim);
            Flower(p, body.Left + (body.Width / 2) - 3, y - 14);
            return;
        }

        var stoneW = strip ? 18 : narrow ? 22 : 26;
        var stoneH = strip ? Math.Min(body.Height, 30) : narrow ? 26 : 32;
        var labelH = strip ? 0 : narrow ? 7 : 14;
        var cellW = stoneW + (strip ? 6 : narrow ? 8 : 12);
        var cellH = stoneH + labelH + (strip ? 0 : 5);
        var columns = Math.Max(1, body.Width / cellW);
        var rows = Math.Max(1, body.Height / cellH);
        var capacity = columns * rows;
        var shown = graves.Count > capacity ? capacity - 1 : graves.Count;
        var gridW = columns * cellW;
        var ox = body.Left + ((body.Width - gridW) / 2);

        // A few graves sit in the middle of the room, not hung from the top of it.
        var usedRows = ((shown + (graves.Count > shown ? 1 : 0)) + columns - 1) / columns;
        var oy = body.Top + Math.Min(body.Height / 4, Math.Max(0, (body.Height - (usedRows * cellH)) / 2));

        for (var i = 0; i < shown; i++)
        {
            var cx = ox + ((i % columns) * cellW);
            var cy = oy + ((i / columns) * cellH);
            var stoneX = cx + ((cellW - stoneW) / 2);
            CapArt.Tombstone(p, stoneX, cy, stoneW, stoneH, graves[i].Sprite, i + 3);
            if (strip) continue;

            var name = graves[i].Name.ToUpperInvariant();
            var chars = Math.Max(3, cellW / 4);
            if (name.Length > chars) name = name[..chars];
            p.Small(name, cx + ((cellW - CapSheet.SmallWidth(name)) / 2), cy + stoneH + 2, CapArt.Paper);
            if (!narrow && graves[i].Level > 0)
            {
                var lv = $"NV{graves[i].Level}";
                p.Small(lv, cx + ((cellW - CapSheet.SmallWidth(lv)) / 2), cy + stoneH + 9, CapArt.Dim);
            }
        }

        if (graves.Count > shown)
        {
            var i = shown;
            var cx = ox + ((i % columns) * cellW);
            var cy = oy + ((i / columns) * cellH);
            var more = $"+{graves.Count - shown}";
            p.Small(more, cx + ((cellW - CapSheet.SmallWidth(more)) / 2), cy + (stoneH / 2), CapArt.Paper);
        }
    }

    private static void Flower(CapSheet p, int x, int y)
    {
        p.VLine(x + 3, y + 5, 7, 0x4A6A3A);
        p.Set(x + 2, y + 8, 0x4A6A3A);
        p.Set(x + 4, y + 9, 0x4A6A3A);
        foreach (var (dx, dy) in new[] { (3, 1), (1, 3), (5, 3), (3, 5) }) p.Rect(x + dx - 1, y + dy - 1, 2, 2, 0xE86A8A);
        p.Rect(x + 2, y + 2, 2, 2, 0xF8D030);
    }

    /// <summary>The run's summary at the foot of the memorial: the trials cleared and the party still alive.</summary>
    private static void PaintRun(CapSheet p, CapLayout layout, CapPlaqueData data)
    {
        var f = layout.Foot;
        p.Rect(f.Left, f.Top, f.Width, f.Height, CapSheet.Darker(CapArt.Back, 0.25));
        p.HLine(f.Left, f.Top, f.Width, CapArt.GoldDim, 120);

        var alive = data.Party.Count(m => m is { Egg: false, Fallen: false });
        var line1 = data.Trials > 0 ? $"PRUEBAS {Math.Min(data.Cleared, data.Trials)} DE {data.Trials}" : data.Trial;
        p.Small(line1, f.Left + 4, f.Top + 4, CapArt.Paper);
        var line2 = $"EQUIPO VIVO {alive}";
        p.Small(line2, f.Right - 4 - CapSheet.SmallWidth(line2), f.Top + 4, alive == 0 ? CapArt.HpRed : CapArt.HpGreen);

        if (data.Trials <= 0) return;
        var size = layout.Narrow ? 4 : 6;
        var step = size + 1;
        var total = (data.Trials * step) - 1;
        var x = f.Left + ((f.Width - total) / 2);
        for (var i = 0; i < data.Trials; i++) CapArt.Gem(p, x + (i * step), f.Top + 14, size, i < data.Cleared);
    }
}
