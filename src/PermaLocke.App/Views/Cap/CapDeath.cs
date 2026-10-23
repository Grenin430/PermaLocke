namespace PermaLocke.App.Views;

/// <summary>The deaths a Pokémon can have in the overlay (2026-10-15, the approved prototypes in imagenes/revision/prototipo_muerte).</summary>
public enum CapDeathKind
{
    /// <summary>The Ultra Wormhole takes it: it cracks like the wormhole's glass and its shards are sucked into the core.</summary>
    Wormhole,

    /// <summary>Terastallized: it turns to glass from where it was hit, splits along gold seams and its shards tumble down.</summary>
    Tera,

    /// <summary>Turned to stone from the feet up; the rocks drop, bounce on the line of its row and crumble; its head splits on an ember.</summary>
    Stone,

    /// <summary>Up to the sky: its soul gathers, looks back at the team, and flies to the top of the Sky Pillar.</summary>
    Sky,

    /// <summary>Corruption (MissingNo.): glitch blocks over the whole window, rows torn sideways, the writing turned to garbage.</summary>
    Corruption,

    /// <summary>The storm: clouds over both skies, rain, a huge bolt, and it burns from its edges inwards.</summary>
    Storm,

    /// <summary>Ultra Space bolts: violet bolts bounce out of the wormhole through the party onto it; it swells and bursts.</summary>
    Bolts,

    /// <summary>Necrozma comes out of a rift, drinks the colour of the whole layout and takes it, as a cinematic.</summary>
    Necrozma,

    /// <summary>Self-Destruct: it glows, trembles and swells, bursts in a fireball whose shockwave shoves the party and lifts the abyss's sea.</summary>
    SelfDestruct
}

/// <summary>One death being shown: whose (its place in the party), which, and when it starts.</summary>
public sealed record CapDeathTurn(int Index, uint Pid, CapDeathKind Kind, double Start)
{
    public double End => Start + CapDeath.Seconds(Kind);
}

/// <summary>
/// When the deaths of the party are shown and which each one is (2026-10-15). A death is one of <see cref="CapDeathKind"/>, picked from the
/// Pokémon's PID, so both windows and every repaint agree on it; when several fall together (a wipe) each waits for the one before it to end.
/// After its death a fallen one is not drawn in the overlay any more: the cemetery keeps it.
/// </summary>
public static class CapDeath
{
    private static readonly CapDeathKind[] Kinds = Enum.GetValues<CapDeathKind>();

    /// <summary>The lab only: every fall gets this death (the scenarios can no longer pick one by PID since the sequence starts from the clock).</summary>
    public static CapDeathKind? Forced { get; set; }

    /// <summary>How long each death lasts, from its first frame to its last.</summary>
    public static double Seconds(CapDeathKind kind) => kind switch
    {
        CapDeathKind.Wormhole => 3.6,
        CapDeathKind.Tera => 3.3,
        CapDeathKind.Stone => 3.3,
        CapDeathKind.Sky => 3.6,
        CapDeathKind.Corruption => 3.6,
        CapDeathKind.Storm => 7.4,
        CapDeathKind.Bolts => 3.8,
        CapDeathKind.SelfDestruct => SelfDestructDeath.Blast + 4.4,
        _ => 6.6
    };

    /// <summary>
    /// The death of a Pokémon: one of all of them, by its PID (by its place when it has none) and by which of its falls this is (2026-10-15): the same
    /// Pokémon falling again, before the first Poké Ball, gets another one each time, never the one of the fall before.
    /// </summary>
    public static CapDeathKind KindOf(uint pid, int index, int fall = 0)
    {
        var h = pid != 0 ? CapArt.Hash((int)(pid & 0x7FFFFFFF), 7919 + (int)(pid >> 31)) : CapArt.Hash(index, 911);
        var step = Kinds.Length % 2 == 0 ? 1 + (2 * ((h / Kinds.Length) % (Kinds.Length / 2))) : 1;
        return Kinds[(int)((h + ((long)fall * step)) % Kinds.Length)];
    }

    /// <summary>The deaths of the party in the order they happened, each starting when the one before it has ended.</summary>
    public static IReadOnlyList<CapDeathTurn> Schedule(CapPlaqueData data, CapAnim? anim, double t)
    {
        if (anim is null) return [];
        var falls = new List<(int Index, uint Pid, double At)>();
        for (var i = 0; i < data.Party.Count && i < CapWorld.Creatures; i++)
        {
            if (anim.SinceFall(i, t) is { } since) falls.Add((i, data.Party[i].Pid, t - since));
        }

        var turns = new List<CapDeathTurn>(falls.Count);
        var end = double.NegativeInfinity;
        foreach (var (index, pid, at) in falls.OrderBy(f => f.At).ThenBy(f => f.Index))
        {
            var kind = Forced ?? anim.Death(index) ?? KindOf(pid, index, anim.Falls(index));
            var start = Math.Max(at, end);
            turns.Add(new CapDeathTurn(index, pid, kind, start));
            end = start + Seconds(kind);
        }

        return turns;
    }

    // ---------------------------------------------------------------- what the deaths share: colours, randomness, easing

    internal static double Rnd(int a, int b) => (CapArt.Hash(a, b) % 10000) / 10000.0;

    internal static double Clamp01(double k) => Math.Clamp(k, 0, 1);

    internal static double Ease(double k)
    {
        k = Clamp01(k);
        return k * k * (3 - (2 * k));
    }

    internal static double Span(double a, double b, double t) => Clamp01((t - a) / (b - a));

    internal static int R(double v) => (int)Math.Floor(v + 0.5);

    internal static int Red(uint c) => (int)((c >> 16) & 255);

    internal static int Green(uint c) => (int)((c >> 8) & 255);

    internal static int Blue(uint c) => (int)(c & 255);

    internal static double Luma(uint c) => (0.3 * Red(c)) + (0.59 * Green(c)) + (0.11 * Blue(c));

    internal static uint Grey(uint c)
    {
        var l = (int)Math.Round(Luma(c));
        return CapSheet.Rgb(l, l, l);
    }

    internal static uint Negative(uint c) => CapSheet.Rgb(255 - Red(c), 255 - Green(c), 255 - Blue(c));

    internal static uint Swap(uint c) => CapSheet.Rgb(Green(c), Blue(c), Red(c));

    internal static int Sat(uint c) => Math.Max(Red(c), Math.Max(Green(c), Blue(c))) - Math.Min(Red(c), Math.Min(Green(c), Blue(c)));

    internal static readonly uint[] Rainbow = [0xFF4040, 0xFF9030, 0xFFE040, 0x50E060, 0x40B0FF, 0x5060E0, 0xB050E0];

    internal static readonly (int X, int Y)[] Round4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    internal static void Put(CapSheet p, double x, double y, uint c, double alpha = 255) => p.Set(R(x), R(y), c, (int)Math.Clamp(Math.Round(alpha), 0, 255));

    internal static void Line(double x0, double y0, double x1, double y1, Action<int, int> plot)
    {
        int ax = R(x0), ay = R(y0), bx = R(x1), by = R(y1);
        int dx = Math.Abs(bx - ax), dy = -Math.Abs(by - ay), sx = ax < bx ? 1 : -1, sy = ay < by ? 1 : -1, err = dx + dy;
        while (true)
        {
            plot(ax, ay);
            if (ax == bx && ay == by) return;
            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; ax += sx; }
            if (e2 <= dx) { err += dx; ay += sy; }
        }
    }

    internal static void Bubble(CapSheet p, double x, double y, uint c, double a)
    {
        foreach (var (ox, oy) in Round4) Put(p, x + ox, y + oy, c, a);
        Put(p, x - 1, y - 1, 0xFFFFFFu, a);
    }

    internal static void Puff(CapSheet p, double x, double y, double r, uint c, double a)
    {
        int cx = R(x), cy = R(y);
        for (var dy = -(int)Math.Ceiling(r); dy <= r; dy++)
        {
            for (var dx = -(int)Math.Ceiling(r); dx <= r; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy)) / r;
                if (d < 1 && ((cx + dx + cy + dy) & 1) == 0) Put(p, cx + dx, cy + dy, c, a * (1 - (d * 0.6)));
            }
        }
    }

    internal static void Glow(CapSheet p, double x, double y, double rx, double ry, uint c, double a, int clipX = int.MaxValue)
    {
        if (a <= 0 || rx <= 0 || ry <= 0) return;
        int cx = R(x), cy = R(y);
        for (var dy = -(int)Math.Ceiling(ry); dy <= ry; dy++)
        {
            for (var dx = -(int)Math.Ceiling(rx); dx <= rx; dx++)
            {
                var d = Math.Sqrt(((dx / rx) * (dx / rx)) + ((dy / ry) * (dy / ry)));
                if (d < 1 && cx + dx < clipX) p.Set(cx + dx, cy + dy, c, (int)(a * (1 - d)));
            }
        }
    }

    /// <summary>Every cell of the window that is painted (not the notch), for the effects that touch all of it.</summary>
    internal static void Each(CapSheet p, Action<int, int> fn)
    {
        for (var y = 0; y < p.H; y++)
        {
            for (var x = 0; x < p.W; x++)
            {
                if (y < p.NotchY1 && x >= p.NotchX0 && x < p.NotchX1) continue;
                fn(x, y);
            }
        }
    }

    /// <summary>The window moved a few cells this frame (a shake): its content shifted, its edges stretched.</summary>
    internal static void Shake(CapSheet p, double since, double strength, double span)
    {
        if (since < 0 || since > span) return;
        var dx = R(Math.Sin(since * 90) * strength * (1 - (since / span)));
        var dy = R(Math.Cos(since * 70) * strength * 0.7 * (1 - (since / span)));
        if (dx == 0 && dy == 0) return;
        var copy = (byte[])p.B.Clone();
        bool Notch(int x, int y) => y < p.NotchY1 && x >= p.NotchX0 && x < p.NotchX1;
        for (var y = 0; y < p.H; y++)
        {
            for (var x = 0; x < p.W; x++)
            {
                // nothing is ever painted over the game, nor taken from over it
                if (Notch(x, y)) continue;
                var sx = Math.Clamp(x - dx, 0, p.W - 1);
                var sy = Math.Clamp(y - dy, 0, p.H - 1);
                if (Notch(sx, sy)) continue;
                Buffer.BlockCopy(copy, ((sy * p.W) + sx) * 4, p.B, ((y * p.W) + x) * 4, 4);
            }
        }
    }
}
