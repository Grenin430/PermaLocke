namespace PermaLocke.App.Views;

/// <summary>
/// One Pokémon of the party as a creature of the sea (2026-10-10): its icon with a halo of the colour of its type, a rim of the same light on
/// its edge and a dark outline so it keeps its shape against the water; and beside it, written in light, what is known of it. The picture and
/// the text are made once, when the Pokémon's data change; what moves (the bob, the breath of the halo, the beads of its life) is painted every
/// frame over those.
/// </summary>
public sealed class CapCreature
{
    private const int BeadCount = 18;

    /// <summary>PROPOSAL: where the HP numbers are painted live (the bar going down is read with them going down), or (0, 0).</summary>
    public (int X, int Y) NumbersAt { get; private set; }

    /// <summary>Where the held item's picture is (its middle, in the window's cells), or (0, 0) when it holds none: the glint of an item changing hands.</summary>
    public (int X, int Y) ItemAt { get; private set; }

    private (int X, int Y, uint Rgb)[] _body = [];
    private (int X, int Y, int Ring)[] _halo = [];
    private (int X, int Y)[] _outline = [];
    private int _spriteW;
    private int _spriteH;

    public CapPlaqueMember? Member { get; private set; }

    public required CapCreatureSlot Slot { get; init; }

    /// <summary>The text of the creature, painted into a sheet of the size of its info box: the box's top left is the sheet's.</summary>
    public CapSheet? Info { get; private set; }

    /// <summary>Where the text sheet goes in the window: the info box's corner, or under the little picture in the compact mode.</summary>
    public (int X, int Y) InfoAt { get; private set; }

    /// <summary>Where the level is written, in the window's cells: what flashes when the level goes up.</summary>
    public CapRect LevelBox { get; private set; }

    public uint Tint { get; private set; }

    /// <summary>Where the beads of the life go, in the window's cells, and how many cells wide they are.</summary>
    public (int X, int Y) BeadsAt { get; private set; }

    public int Beads { get; private set; }

    /// <summary>Where the ticker of the moves goes: the left of its text, and how many letters fit. Zero letters means no ticker.</summary>
    public (int X, int Y, int Letters) TickerAt { get; private set; }

    /// <summary>Where the statuses live: the middle of the picture.</summary>
    public (int X, int Y) Centre => (Slot.Sprite.Left + (Slot.Sprite.Width / 2), Slot.Sprite.Top + (Slot.Sprite.Height / 2));

    public static string KeyOf(CapPlaqueMember? m) => m is null ? string.Empty
        : $"{m.Name}|{m.Level}|{m.Hp}/{m.MaxHp}|{m.Egg}|{m.Status}|{m.Item}|{string.Join(",", m.Types ?? [])}|{m.Exp:F2}|{string.Join(",", (m.Moves ?? []).Select(x => x.Type + ":" + x.Empty + ":" + x.Name))}";

    /// <summary>Rebuilds the creature's picture and text for a Pokémon (or an empty slot: null).</summary>
    public void Build(CapPlaqueMember? m, CapSeaPalette pal, int cap, bool narrow)
    {
        Member = m;
        _body = [];
        _halo = [];
        _outline = [];
        Info = null;
        TickerAt = default;
        ItemAt = default;
        Beads = 0;
        if (m is null) return;

        var fallen = m.Fallen;
        Tint = fallen ? 0x5A6580u
            : m.Types is { Count: > 0 } types && types[0] >= 0 ? CapArt.TypeColour(types[0])
            : CapArt.Hues[CapArt.StableHash(m.Name) % CapArt.Hues.Length];

        BuildPicture(m, fallen);
        InfoAt = (Slot.Info.Left, Slot.Info.Top);
        if (Slot.Info.Width == 0 && Slot.Sprite.Width <= 14) BuildTiny(m, pal, cap, fallen);
        else BuildText(m, pal, cap, narrow, fallen);
    }

    // ---------------------------------------------------------------- the picture

    private void BuildPicture(CapPlaqueMember m, bool fallen)
    {
        var slot = Slot.Sprite;
        var scale = slot.Width <= 14 ? 4 : Slot.Half ? 2 : 1;
        if (ToastPixels.SpritePixels.From(m.Sprite) is not { } pixels) return;

        _spriteW = Math.Min(slot.Width, Math.Max(1, pixels.Width / scale));
        _spriteH = Math.Min(slot.Height, Math.Max(1, pixels.Height / scale));
        var offX = Math.Max(0, ((pixels.Width / scale) - _spriteW) / 2);
        var offY = Math.Max(0, ((pixels.Height / scale) - _spriteH) / 2);

        var solid = new bool[_spriteW, _spriteH];
        var colours = new uint[_spriteW, _spriteH];
        for (var y = 0; y < _spriteH; y++)
        {
            for (var x = 0; x < _spriteW; x++)
            {
                for (var k = 0; k < scale * scale; k++)
                {
                    var sx = ((offX + x) * scale) + (k % scale);
                    var sy = ((offY + y) * scale) + (k / scale);
                    if (!pixels.Solid(sx, sy)) continue;
                    var c = pixels.At(sx, sy);
                    var rgb = ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                    if (fallen)
                    {
                        var luma = ((0.30 * c.R) + (0.59 * c.G) + (0.11 * c.B)) / 255.0;
                        rgb = CapSheet.Mix(0x26324Cu, 0xB4C4E0u, luma * 0.95);
                    }

                    solid[x, y] = true;
                    colours[x, y] = rgb;
                    break;
                }
            }
        }

        bool Solid(int x, int y) => x >= 0 && y >= 0 && x < _spriteW && y < _spriteH && solid[x, y];

        var body = new List<(int, int, uint)>();
        var outline = new List<(int, int)>();
        var halo = new List<(int, int, int)>();
        for (var y = -3; y < _spriteH + 3; y++)
        {
            for (var x = -3; x < _spriteW + 3; x++)
            {
                if (Solid(x, y))
                {
                    var edge = !Solid(x - 1, y) || !Solid(x + 1, y) || !Solid(x, y - 1) || !Solid(x, y + 1);
                    var rgb = colours[x, y];

                    // The rim: the edge of the picture takes the light of its type, so it reads as lit from inside.
                    if (edge && !fallen) rgb = CapSheet.Lighter(CapSheet.Mix(rgb, Tint, 0.5), 0.18);
                    body.Add((x, y, rgb));
                    continue;
                }

                // How far this empty cell is from the picture, in cells (Chebyshev), up to three.
                var ring = 0;
                for (var d = 1; d <= 3 && ring == 0; d++)
                {
                    for (var dy = -d; dy <= d && ring == 0; dy++)
                    {
                        for (var dx = -d; dx <= d; dx++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != d || !Solid(x + dx, y + dy)) continue;
                            ring = d;
                            break;
                        }
                    }
                }

                if (ring == 1) outline.Add((x, y));
                else if (ring > 1) halo.Add((x, y, ring));
            }
        }

        _body = [.. body];
        _outline = [.. outline];
        _halo = [.. halo];
    }

    /// <summary>The picture of the creature at its place, over the water: halo first, then the outline, then the body. <paramref name="breath"/> is 0 to 1.</summary>
    /// <param name="wave">How far the picture sways in the water, in cells, row by row (each row a little behind the one above it, so it swims and does not slide).</param>
    /// <param name="wavePhase">The moment of the sway: the sea's clock times the creature's own speed.</param>
    /// <param name="haloScale">How much of the halo shows: the blink of its light.</param>
    public void PaintPicture(CapSheet p, int ox, int oy, uint ink, double breath, double strength = 1, uint? haloTint = null, double wave = 0, double wavePhase = 0,
        double haloScale = 1)
    {
        if (_body.Length == 0) return;

        var baseX = Slot.Sprite.Left + ox + ((Slot.Sprite.Width - _spriteW) / 2);
        var baseY = Slot.Sprite.Top + oy + ((Slot.Sprite.Height - _spriteH) / 2);

        // The sway of each row of the picture, worked out once: the rows run from -3 (the halo above) to the height plus three.
        Span<int> shift = stackalloc int[_spriteH + 7];
        for (var row = 0; row < shift.Length; row++) shift[row] = wave <= 0 ? 0 : (int)Math.Round(wave * Math.Sin(wavePhase + ((row - 3) * 0.45)));

        foreach (var (x, y, ring) in _halo)
        {
            // The inner ring is a steady glow; the outer one is a dither that breathes in and out.
            var halo = haloTint ?? Tint;
            var sx = baseX + x + shift[y + 3];
            if (ring == 2) p.Set(sx, baseY + y, halo, (int)((55 + (60 * breath)) * strength * haloScale));
            else if (((x + y) & 1) == 0) p.Set(sx, baseY + y, halo, (int)((20 + (50 * breath)) * strength * haloScale));
        }

        foreach (var (x, y) in _outline) p.Set(baseX + x + shift[y + 3], baseY + y, ink, 215);
        foreach (var (x, y, rgb) in _body) p.Set(baseX + x + shift[y + 3], baseY + y, rgb, (int)(255 * Math.Min(1, strength + 0.15)));
    }

    /// <summary>The body of the creature in one colour over itself, at a place: the white flash of a hit, the gold of a level, the glow of a heal.</summary>
    public void PaintTint(CapSheet p, int ox, int oy, uint colour, int alpha)
    {
        if (alpha <= 0) return;
        var baseX = Slot.Sprite.Left + ox + ((Slot.Sprite.Width - _spriteW) / 2);
        var baseY = Slot.Sprite.Top + oy + ((Slot.Sprite.Height - _spriteH) / 2);
        foreach (var (x, y, _) in _body) p.Set(baseX + x, baseY + y, colour, alpha);
    }

    /// <summary>PROPOSAL: the body painted in one colour only between two rows of the picture (0 is its top): a band of light that sweeps down it.</summary>
    public void PaintBand(CapSheet p, int ox, int oy, uint colour, int alpha, int fromRow, int toRow)
    {
        if (alpha <= 0) return;
        var baseX = Slot.Sprite.Left + ox + ((Slot.Sprite.Width - _spriteW) / 2);
        var baseY = Slot.Sprite.Top + oy + ((Slot.Sprite.Height - _spriteH) / 2);
        foreach (var (x, y, _) in _body)
        {
            if (y >= fromRow && y < toRow) p.Set(baseX + x, baseY + y, colour, alpha);
        }
    }

    /// <summary>The size of the picture in cells, for the effects that go round it.</summary>
    public (int W, int H) PictureSize => (_spriteW, _spriteH);

    // ---------------------------------------------------------------- the text

    /// <summary>The compact mode: no room for words, only the level in small letters under the picture and three beads for the life.</summary>
    private void BuildTiny(CapPlaqueMember m, CapSeaPalette pal, int cap, bool fallen)
    {
        var s = Slot.Sprite;
        InfoAt = (s.Left - 1, s.Bottom + 1);
        var sheet = new CapSheet(s.Width + 2, 6);
        Info = sheet;
        if (m.Egg) return;

        var level = m.Level.ToString();
        var gold = 0xFFD860u;
        var colour = fallen ? 0x7A86A4u : m.AtCap(cap) ? gold : pal.Glow;
        CapGlyphs.Outlined(sheet, level, Math.Max(1, (sheet.W - CapGlyphs.Width(level, false)) / 2), 1, colour, pal.Ink);
        LevelBox = new CapRect(InfoAt.X, InfoAt.Y, sheet.W, 6);
        if (fallen) return;

        Beads = Math.Min(3, Math.Max(1, s.Width / 4));
        BeadsAt = (s.Left, s.Bottom + 9);
    }

    private void BuildText(CapPlaqueMember m, CapSeaPalette pal, int cap, bool narrow, bool fallen)
    {
        var box = Slot.Info;
        if (box.Width < 20 || box.Height < 8) return;

        var sheet = new CapSheet(box.Width, box.Height);
        Info = sheet;
        var ink = pal.Ink;
        var glow = fallen ? 0x7A86A4u : pal.Glow;
        var gold = 0xFFD860u;
        var atCap = m.AtCap(cap);
        var wide = box.Width >= 70 && !narrow;

        var name = m.Egg ? "HUEVO" : m.Name.ToUpperInvariant();
        var level = m.Egg ? string.Empty : $"NV{m.Level}";
        var levelW = level.Length == 0 ? 0 : CapGlyphs.Width(level, big: true);
        var levelColour = fallen ? 0x6A7690u : atCap ? gold : glow;

        // The letters of a name: the big ones if they fit beside the level, else the small ones, else cut.
        var big = wide && box.Height >= 18;
        // The held item is a small picture beside the level (eight cells: the cartridge's icon at a third of its size), not a line of words.
        var hasItem = wide && m.Item is { Length: > 0 };
        var room = wide ? box.Width - levelW - 3 - (hasItem ? 11 : 0) : box.Width - 1;
        while (name.Length > 1 && CapGlyphs.Width(name, big) > room) name = name[..^1];

        var lineH = big ? 7 : 5;
        var y = 0;
        var padTop = Math.Max(0, (box.Height - Needed(box.Height, wide, m)) / 2);
        y += padTop;

        CapGlyphs.Outlined(sheet, name, 1, y + 1, fallen ? 0x7A86A4u : glow, ink, big);
        if (fallen) sheet.HLine(1, y + 1 + (lineH / 2), CapGlyphs.Width(name, big), 0xB8433Au);
        if (wide && level.Length > 0)
        {
            CapGlyphs.Outlined(sheet, level, box.Width - levelW - 1, y + 1, levelColour, ink, big: true);
            LevelBox = new CapRect(box.Left + box.Width - levelW - 2, box.Top + y, levelW + 2, 9);
            if (hasItem)
            {
                DrawItem(sheet, box.Width - levelW - 12, y, m.ItemIcon, glow, ink);
                ItemAt = (box.Left + box.Width - levelW - 8, box.Top + y + 4);
            }
        }
        y += lineH + 4;

        // The level goes under the name in a narrow text, so that both stay whole.
        if (!wide && level.Length > 0)
        {
            CapGlyphs.Outlined(sheet, level, 1, y, levelColour, ink, big: true);
            LevelBox = new CapRect(box.Left, box.Top + y - 1, levelW + 2, 9);
            y += 11;
        }

        if (m.Egg) return;
        if (fallen)
        {
            CapGlyphs.Outlined(sheet, "KO", 1, y, 0xFF6A5Au, ink);
            CapGlyphs.Outlined(sheet, "SIN PS", 12, y, 0x7A86A4u, ink);
            return;
        }

        // The beads of the life: where they go is the creature's to remember, because they are painted every frame.
        var beads = Math.Min(BeadCount, Math.Max(4, (wide ? box.Width - 34 : box.Width - 2) / 4));
        Beads = beads;
        BeadsAt = (box.Left + 1, box.Top + y);
        NumbersAt = default;
        if (wide)
        {
            var numbers = $"{m.Hp}/{m.MaxHp}";
            // The numbers are painted every frame, counting down and up with the bar, so they are not in the still text.
            NumbersAt = (box.Left + (beads * 4) + 3, box.Top + y - 1);
            _ = numbers;
        }

        y += 7;

        if (m.Exp >= 0 && box.Height >= 28 && wide)
        {
            var pearls = (beads * 4) / 2;
            CapGlyphs.Pearls(sheet, 1, y, pearls, (int)Math.Round(pearls * Math.Clamp(m.Exp, 0, 1)), m.NearCap(cap) ? gold : 0x9AD8FFu, ink);
            y += 3;
        }

        // One line for what a creature is and what it can do: its types, its status, and the moves rotating in what is left of the line. When
        // the letters of the types and of the status leave less than six letters for the moves, the types go as coloured spots only.
        var types = (m.Types ?? []).Where(t => t >= 0).Take(2).ToList();
        var hasMoves = wide && m.Moves is { Count: > 0 };
        var statusLook = m.Status != CapStatus.None ? CapArt.StatusLook(m.Status) : default;
        const int MovesMinimum = 7 + (6 * 4);

        int Used(bool letters) => 1 + types.Sum(t => 5 + (letters ? 2 + CapGlyphs.Width(CapArt.TypeName(t), false) : 0) + 3)
            + (m.Status != CapStatus.None ? 5 + (letters ? 2 + CapGlyphs.Width(statusLook.Text, false) : 0) + 3 : 0);

        var letters = wide && (!hasMoves || Used(true) + MovesMinimum <= box.Width);
        if (y + 5 <= box.Height)
        {
            var x = 1;
            foreach (var type in types)
            {
                var label = CapArt.TypeName(type);
                CapGlyphs.Spot(sheet, x, y, CapArt.TypeColour(type), ink);
                if (letters) CapGlyphs.Outlined(sheet, label, x + 7, y, glow, ink);
                x += 5 + (letters ? 2 + CapGlyphs.Width(label, false) : 0) + 3;
            }

            if (m.Status != CapStatus.None)
            {
                CapGlyphs.Spot(sheet, x, y, statusLook.Colour, ink);
                if (letters) CapGlyphs.Outlined(sheet, statusLook.Text, x + 7, y, glow, ink);
                x += 5 + (letters ? 2 + CapGlyphs.Width(statusLook.Text, false) : 0) + 3;
            }

            // The moves rotate in the rest of the line: the pip of the colour of the type, and the name coming one at a time.
            if (hasMoves)
            {
                var from = x + 3;
                TickerAt = (box.Left + from, box.Top + y, Math.Max(0, (box.Width - from - 7) / 4));
            }
        }
    }

    /// <summary>The picture of a held item in eight by eight cells, with a dark outline; a small gold gem when the cartridge has no icon for it.</summary>
    private static void DrawItem(CapSheet sheet, int x, int y, System.Windows.Media.Imaging.BitmapSource? icon, uint glow, uint ink)
    {
        if (ToastPixels.SpritePixels.From(icon) is not { } pixels)
        {
            CapGlyphs.Spot(sheet, x + 1, y + 1, 0xFFD860u, ink);
            return;
        }

        var cells = new uint?[8, 8];
        var step = Math.Max(1, pixels.Width / 8);
        for (var cy = 0; cy < 8; cy++)
        {
            for (var cx = 0; cx < 8; cx++)
            {
                int r = 0, g = 0, b = 0, n = 0;
                for (var dy = 0; dy < step; dy++)
                {
                    for (var dx = 0; dx < step; dx++)
                    {
                        var sx = (cx * step) + dx;
                        var sy = (cy * step) + dy;
                        if (!pixels.Solid(sx, sy)) continue;
                        var c = pixels.At(sx, sy);
                        (r, g, b, n) = (r + c.R, g + c.G, b + c.B, n + 1);
                    }
                }

                if (n * 3 >= step * step) cells[cx, cy] = CapSheet.Rgb(r / n, g / n, b / n);
            }
        }

        bool Has(int cx, int cy) => cx >= 0 && cy >= 0 && cx < 8 && cy < 8 && cells[cx, cy] is not null;
        for (var cy = -1; cy <= 8; cy++)
        {
            for (var cx = -1; cx <= 8; cx++)
            {
                if (!Has(cx, cy) && (Has(cx - 1, cy) || Has(cx + 1, cy) || Has(cx, cy - 1) || Has(cx, cy + 1))) sheet.Set(x + cx, y + cy, ink, 220);
            }
        }

        for (var cy = 0; cy < 8; cy++)
        {
            for (var cx = 0; cx < 8; cx++)
            {
                if (cells[cx, cy] is { } rgb) sheet.Set(x + cx, y + cy, rgb);
            }
        }

        _ = glow;
    }

    /// <summary>How many rows of the info box the lines will take, to centre them: the same walk as <see cref="BuildText"/> without painting.</summary>
    private static int Needed(int height, bool wide, CapPlaqueMember m)
    {
        if (m.Egg) return 8;
        var rows = (wide ? 10 : 8) + (wide ? 0 : 9) + 6;
        if (m.Exp >= 0 && height >= 28 && wide) rows += 3;
        if (m.Types is { Count: > 0 } || m.Status != CapStatus.None || m.Moves is { Count: > 0 }) rows += 8;
        return Math.Min(rows, height);
    }
}
