namespace PermaLocke.App.Views;

/// <summary>
/// Where everything inside one niche goes, in cells. Pure and shared by the still part of the panel (<see cref="CapNiche.Paint"/>)
/// and the moving part (<see cref="CapLive"/>): the bar that is filled over time is exactly the groove that was drawn.
/// </summary>
public readonly record struct CapNicheMetrics(
    CapRect Outer,
    CapRect Alcove,
    bool HalfSprite,
    int TextLeft,
    int TextRight,
    int NameY,
    bool BigName,
    int NameMax,
    CapRect LevelChip,
    CapRect HpBar,
    CapRect HpNumbers,
    CapRect Exp,
    int ChipsY,
    int ItemY,
    bool Stacked = false)
{
    public bool HasHp => HpBar.Width > 0;

    public bool HasExp => Exp.Width > 0;
}

/// <summary>One niche of the party: a recess with the Pokémon's window and what is known of it.</summary>
public static class CapNiche
{
    private const int Pad = 3;
    private const int Gap = 2;

    /// <summary>Lays a niche out for the room it has. What does not fit is left out, never squeezed.</summary>
    public static CapNicheMetrics Measure(CapRect outer, CapPlaqueMember? m, bool narrow)
    {
        var pad = outer.Height < 24 ? 2 : Pad;
        var c = new CapRect(outer.Left + pad, outer.Top + pad, outer.Width - (2 * pad), outer.Height - (2 * pad));
        var half = c.Height < 31;
        var alcoveW = half ? 24 : 44;
        var alcove = new CapRect(c.Left, c.Top, Math.Min(alcoveW, Math.Max(10, (int)(c.Width * 0.45))), c.Height);

        var left = alcove.Right + 4;
        var right = c.Right;
        var width = right - left;

        var empty = new CapRect(0, 0, 0, 0);
        if (m is null || m.Egg || width < 18)
        {
            // An egg tells nothing but that it is an egg; an empty niche tells nothing at all.
            var nameY = c.Top + ((c.Height - 5) / 2);
            return new CapNicheMetrics(outer, alcove, half, left, right, nameY, false, Math.Max(1, width / 4), empty, empty, empty, empty, 0, 0, false);
        }

        // From the top: name and level, the HP bar, the types and the status, the experience, the item and the moves. When the text
        // is too narrow for the name and the level side by side the level goes to the row of the types. What does not fit is dropped,
        // the least important first.
        var stacked = width < 52;
        var big = !narrow && !stacked && c.Height >= 22 && width >= 76;
        var nameH = big ? 7 : 5;
        var rows = new List<(string Name, int H, int Priority)> { ("name", nameH, 0), ("hp", 5, 0) };
        if (m.Types is { Count: > 0 } || m.Status != CapStatus.None || stacked) rows.Add(("chips", 7, 1));
        if (m.Exp >= 0 && !stacked) rows.Add(("exp", 2, 2));
        if ((m.Item is not null || m.Moves is { Count: > 0 }) && !stacked) rows.Add(("item", 5, 3));

        int Used() => rows.Sum(r => r.H) + (Gap * (rows.Count - 1));
        while (Used() > c.Height && rows.Count > 2)
        {
            var drop = rows.MaxBy(r => r.Priority);
            rows.Remove(drop);
        }

        // Display order: name, HP, experience, chips, item.
        string[] order = ["name", "hp", "exp", "chips", "item"];
        rows = [.. rows.OrderBy(r => Array.IndexOf(order, r.Name))];
        var y = c.Top + Math.Max(0, (c.Height - Used()) / 2);

        int nameYy = 0, chipsY = 0, itemY = 0;
        CapRect chip = empty, hp = empty, hpNumbers = empty, exp = empty;
        var levelText = $"NV{m.Level}";
        var chipW = CapArt.ChipWidth(levelText);

        foreach (var (name, h, _) in rows)
        {
            switch (name)
            {
                case "name":
                    nameYy = y;
                    if (!stacked) chip = new CapRect(right - chipW, y + (big ? 0 : -1), chipW, 7);
                    break;
                case "hp":
                    var numbers = $"{m.Hp}/{m.MaxHp}";
                    var numW = CapSheet.SmallWidth(numbers);
                    var showNumbers = !stacked && width >= numW + 32 && !(narrow && width < 70);
                    var barW = showNumbers ? width - numW - 3 : width;
                    hp = new CapRect(left, y, barW, 5);
                    if (showNumbers) hpNumbers = new CapRect(left + barW + 3, y, numW, 5);
                    break;
                case "exp":
                    exp = new CapRect(left, y, width, 2);
                    break;
                case "chips":
                    chipsY = y;
                    if (stacked) chip = new CapRect(left, y, chipW, 7);
                    break;
                case "item":
                    itemY = y;
                    break;
            }

            y += h + Gap;
        }

        var nameMax = Math.Max(1, (stacked ? width : width - chipW - 3) / (big ? 6 : 4));
        return new CapNicheMetrics(outer, alcove, half, left, right, nameYy, big, nameMax, chip, hp, hpNumbers, exp, chipsY, itemY, stacked);
    }

    /// <summary>The still part of a niche: recess, window, icon, name, level, the groove of the bar, its numbers, the chips and the item.</summary>
    public static void Paint(CapSheet p, CapRect outer, CapPlaqueMember? m, int cap, bool narrow, int slot)
    {
        var metrics = Measure(outer, m, narrow);
        var fallen = m?.Fallen ?? false;
        var egg = m?.Egg ?? false;
        var atCap = m is not null && !egg && !fallen && m.Level >= cap;

        var tint = m is null ? 0x3A2A1Au
            : fallen ? 0x2E2A28u
            : m.Types is { Count: > 0 } types && types[0] >= 0 ? CapArt.TypeColour(types[0])
            : CapArt.Hues[CapArt.StableHash(m.Name) % CapArt.Hues.Length];

        // The recess and its brass thread.
        p.Notched(outer.Left, outer.Top, outer.Width, outer.Height, CapArt.Ink, 2);
        p.Notched(outer.Left + 1, outer.Top + 1, outer.Width - 2, outer.Height - 2, CapArt.GoldDim, 2, fallen ? 110 : 255);
        p.Notched(outer.Left + 2, outer.Top + 2, outer.Width - 4, outer.Height - 4, CapSheet.Mix(CapArt.Back, tint, m is null ? 0.1 : 0.17), 1);
        p.VGrad(outer.Left + 3, outer.Top + 3, outer.Width - 6, outer.Height - 6, CapSheet.Mix(CapArt.Back, tint, m is null ? 0.1 : 0.2), CapSheet.Mix(CapArt.BackDeep, tint, 0.1));
        p.HLine(outer.Left + 3, outer.Top + 2, outer.Width - 6, CapArt.BackDeep);

        var floor = fallen ? 0x2E2A28u : tint;
        var window = CapArt.Alcove(p, metrics.Alcove.Left, metrics.Alcove.Top, metrics.Alcove.Width, metrics.Alcove.Height, floor, fallen);

        if (m is null)
        {
            const string empty = "VACIO";
            var w = CapSheet.SmallWidth(empty);
            p.Small(empty, metrics.TextLeft, metrics.NameY, CapSheet.Mix(CapArt.Back, CapArt.GoldDim, 0.5));
            _ = w;
            Ball(p, window.Left + (window.Width / 2) - 4, window.Top + (window.Height / 2) - 4, 0x4A3A28);
            return;
        }

        CapArt.Sprite(p, window, m.Sprite, metrics.HalfSprite, fallen);
        if (fallen)
        {
            p.Line(window.Left + 3, window.Top + 3, window.Right - 4, window.Bottom - 4, CapArt.HpRed, 200);
            p.Line(window.Right - 4, window.Top + 3, window.Left + 3, window.Bottom - 4, CapArt.HpRed, 200);
        }

        if (atCap)
        {
            foreach (var (dx, dy) in new[] { (0, 0), (-1, 0), (1, 0), (0, -1), (0, 1) }) p.Set(window.Right - 3 + dx, window.Top + 2 + dy, CapArt.Gold);
            p.Set(window.Right - 3, window.Top + 2, 0xFFF2B0);
        }

        var ink = fallen ? CapArt.Dim : CapArt.Paper;
        var name = egg ? "HUEVO" : m.Name.ToUpperInvariant();
        if (name.Length > metrics.NameMax) name = name[..metrics.NameMax];
        if (metrics.BigName) p.Text(name, metrics.TextLeft, metrics.NameY, ink, 1, CapArt.Ink);
        else p.Small(name, metrics.TextLeft, metrics.NameY, ink);

        if (egg || metrics.LevelChip.Width == 0) return;
        if (fallen) p.HLine(metrics.TextLeft, metrics.NameY + (metrics.BigName ? 3 : 2), CapSheet.SmallWidth(name), CapArt.HpRed, 220);

        var level = $"NV{m.Level}";
        var chip = metrics.LevelChip;
        CapArt.Chip(p, chip.Left, chip.Top, level, atCap ? CapArt.Gold : fallen ? 0x6A5A48u : CapArt.Engraved,
            atCap ? 0x4A2E00u : fallen ? 0xD8CCB0u : 0xF4D88Au, atCap ? 0x8A5A10u : CapArt.Brown);

        if (fallen)
        {
            if (metrics.HasHp)
            {
                p.Small("KO", metrics.HpBar.Left, metrics.HpBar.Top, CapArt.HpRed);
                p.Small("SIN PS", metrics.HpBar.Left + 11, metrics.HpBar.Top, CapArt.Dim);
            }

            return;
        }

        if (metrics.HasHp) CapArt.BarFrame(p, metrics.HpBar.Left, metrics.HpBar.Top, metrics.HpBar.Width, metrics.HpBar.Height);
        if (metrics.HpNumbers.Width > 0) p.Small($"{m.Hp}/{m.MaxHp}", metrics.HpNumbers.Left, metrics.HpNumbers.Top, CapArt.Dim);
        if (metrics.HasExp)
        {
            var e = metrics.Exp;
            p.Rect(e.Left, e.Top, e.Width, e.Height, 0x20150C);
            p.HLine(e.Left, e.Top + e.Height, e.Width, 0x5A3A10, 90);
        }

        if (metrics.ChipsY > 0)
        {
            var x = metrics.TextLeft + (metrics.Stacked ? metrics.LevelChip.Width + 2 : 0);
            foreach (var type in m.Types ?? [])
            {
                if (type < 0 || x + CapArt.TypeChipWidth(type) > metrics.TextRight) break;
                CapArt.TypeChip(p, x, metrics.ChipsY, type);
                x += CapArt.TypeChipWidth(type) + 2;
            }

            if (m.Status != CapStatus.None && x + CapArt.StatusChipWidth(m.Status) <= metrics.TextRight)
            {
                CapArt.StatusChip(p, x, metrics.ChipsY, m.Status);
            }
        }

        if (metrics.ItemY > 0)
        {
            var x = metrics.TextLeft;
            var moves = m.Moves ?? [];
            var pipsW = moves.Count * 5;
            if (m.Item is { Length: > 0 } item)
            {
                var room = (metrics.TextRight - x - pipsW - 6) / 4;
                var label = item.ToUpperInvariant();
                if (room >= 4)
                {
                    if (label.Length > room) label = label[..room];
                    p.Small(label, x, metrics.ItemY, CapSheet.Mix(CapArt.Paper, CapArt.Dim, 0.45));
                }
            }

            var px = metrics.TextRight - pipsW + 1;
            foreach (var move in moves)
            {
                var colour = move.Type >= 0 ? CapArt.TypeColour(move.Type) : 0x6A5A48u;
                p.Rect(px, metrics.ItemY, 4, 4, CapArt.Ink);
                p.Rect(px + 1, metrics.ItemY + 1, 2, 2, move.Empty ? 0x2A2018u : colour);
                px += 5;
            }
        }
    }

    /// <summary>A Poké Ball outline for an empty niche, 8×8.</summary>
    private static void Ball(CapSheet p, int x, int y, uint colour)
    {
        p.Disc(x + 4, y + 4, 4, colour, 160);
        p.Disc(x + 4, y + 4, 3, CapArt.BackDeep);
        p.HLine(x + 1, y + 4, 6, colour, 160);
        p.Set(x + 4, y + 4, colour);
    }
}
