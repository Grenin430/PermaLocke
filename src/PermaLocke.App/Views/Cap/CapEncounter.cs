using PermaLocke.App.Views.Pixel;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// A route's first encounter in the overlay (2026-10-19): the wild Pokémon's sprite, the route, and on the panel's clock when it was seen
/// and when its battle ended (none while it goes on: the route stays as long as the battle, 2026-10-20); whether it is an allowed static (the
/// same animation and banner as a route's first encounter); whether it is a duplicate the player may take or leave (the same animation, an
/// amber banner saying «DUPLICADO, TÚ ELIGES»).
/// </summary>
/// <remarks>
/// The battle's outcome (2026-10-20) and when it came: caught, fainted or fled, each with its animation in the grass; the sea waits for it
/// before coming back up. <paramref name="Tracked"/> is false for an allowed static fought without the wild battle counter, whose outcome
/// the guard does not follow: its battle ends with no animation.
/// </remarks>
public sealed record CapEncounterTurn(BitmapSource? Sprite, string Route, double Start, double? End = null, bool Catchable = false, bool Duplicate = false,
    CapEncounterOutcome Outcome = CapEncounterOutcome.None, double OutcomeAt = 0, bool Tracked = true)
{
    /// <summary>When the sea starts coming back up: when the battle ended, or once the outcome has played, whichever is later.</summary>
    public double? RefillAt => End is { } end ? Math.Max(end, Outcome == CapEncounterOutcome.None ? end : OutcomeAt + CapEncounter.Hold(Outcome)) : null;

    /// <summary>Whether the route is on the overlay at a moment: from when it was seen until the sea has come back up after the battle.</summary>
    public bool Showing(double t) => t >= Start && (RefillAt is not { } refill || t <= refill + CapEncounter.Refill);

    /// <summary>Whether it is done with: the battle over, its outcome played and the sea back up.</summary>
    public bool Over(double t) => RefillAt is { } refill && t > refill + CapEncounter.Refill + 0.5;
}

/// <summary>How the battle of a route's encounter ended, for its animation in the grass.</summary>
public enum CapEncounterOutcome { None, Caught, Fainted, Fled }

/// <summary>
/// A route's first encounter: the overlay becomes the route (2026-10-20, the player's choice, «the layout turns into the route»). Once the
/// wild Pokémon is to be seen in the game, the sea of both windows drains away downwards behind a line of foam and leaves a route seen from
/// above as the handheld games draw them, one map across both windows: the path the player drew, a house, a berry field, tall grass, a pond,
/// trees, rocks and wood, all shaded in the sky's dots; the wild Pokémon in the grass of the abyss's window; a Pidgey crossing both skies
/// with a banner. It stays while the battle lasts and the sea comes back up when it ends. A function of the moment; drawn in world columns.
/// </summary>
public static class CapEncounter
{
    /// <summary>How long the sea takes to drain away and to come back up.</summary>
    public const double Drain = 1.6, Refill = 1.4;

    // ---------------------------------------------------------------- palettes, darkest first

    private static readonly uint[] GrassPal = [0x2E6E2Au, 0x3F8F35u, 0x58A845u, 0x73C058u, 0x9AD870u];
    private static readonly uint[] TallPal = [0x173F1Cu, 0x24662Au, 0x3A8E36u, 0x5CB848u, 0x9CE07Au];
    private static readonly uint[] LeafPal = [0x163A1Au, 0x245E28u, 0x357F34u, 0x4E9F42u, 0x74C058u, 0xA8E080u];
    private static readonly uint[] BarkPal = [0x2E1C0Eu, 0x553519u, 0x7A5028u, 0xA0703Au, 0xC09058u];
    private static readonly uint[] DirtPal = [0x7A5A34u, 0xA6865Au, 0xC8AA78u, 0xDEC696u, 0xF0E0B8u];
    private static readonly uint[] RockPal = [0x2E3238u, 0x5C626Au, 0x868C94u, 0xB0B6BEu, 0xD8DCE2u];
    private static readonly uint[] WoodPal = [0x3E220Cu, 0x6E4422u, 0x986234u, 0xC08A50u, 0xE0B07Au];
    private static readonly uint[] FarPal = [0x6FA894u, 0x86BCA4u, 0x9ED0B6u, 0xB6E0C8u];
    private static readonly uint[] NearPal = [0x2F7A3Au, 0x3F9244u, 0x55AA52u, 0x72C262u, 0x98D87Eu];
    private static readonly uint[] SkyPal = [0x8CCCEEu, 0xA6D8F2u, 0xC2E6F6u];
    private static readonly uint[] WaterPal = [0x2E66B8u, 0x3C7ED0u, 0x5096E0u, 0x8CC4F0u, 0xD8F0FFu];

    private const uint Foam = 0xF0FBFFu, FoamLo = 0xB8E4F0u, Wet = 0x3E8A40u, Shadow = 0x2F7430u, Letters = 0x3A2010u;

    // ---------------------------------------------------------------- the hand-drawn pieces (letters index their palette: 0 darkest)

    /// <summary>The grass, one 16×16 tile: its colour with tufts in it (a dark root, a light blade, a lit tip).</summary>
    private static readonly string[] GrassTile =
    [
        "2222222222222222",
        "2222322222222222",
        "2223132222222222",
        "2221222222224222",
        "2222222222231322",
        "2222222222212222",
        "2222222222222222",
        "2422222222222222",
        "3132222223222222",
        "1222222231322222",
        "2222222212222222",
        "2222222222222242",
        "2222422222222313",
        "2223132222222212",
        "2221222222222222",
        "2222222222222222",
    ];

    /// <summary>The grass, a quieter tile: a few short blades.</summary>
    private static readonly string[] GrassPlain =
    [
        "2222222222222222",
        "2222222222222222",
        "2222222232222222",
        "2222222212222222",
        "2232222222222222",
        "2212222222222222",
        "2222222222222232",
        "2222222222222212",
        "2222222222222222",
        "2222223222222222",
        "2222221222222222",
        "2222222222222222",
        "2222222222322222",
        "2232222222122222",
        "2212222222222222",
        "2222222222222222",
    ];

    /// <summary>The grass with a patch of clover in it: little round leaves, lit on top.</summary>
    private static readonly string[] GrassClover =
    [
        "2222222222222222",
        "2222222222222222",
        "2222223422222222",
        "2222234343222222",
        "2222233434222222",
        "2222221331222222",
        "2222222112222222",
        "2222222222222222",
        "2222222222222222",
        "2222222222224222",
        "2222222222231322",
        "2222222222221222",
        "2422222222222222",
        "3132222222222222",
        "2122222222222222",
        "2222222222222222",
    ];

    /// <summary>The tall grass, one 16×8 strip: blades in pointed clumps, tips lit, roots in shadow (the next strip down is set half over).</summary>
    private static readonly string[] TallStrip =
    [
        ".4.....4.....4..",
        "432...432...432.",
        "3322.43322.43322",
        "3221133211332211",
        "221102211022110.",
        "2110.2110.2110..",
        "110..110..110...",
        "10...10...10....",
    ];

    /// <summary>A flower, two frames: petals of two tones round a yellow heart, a stem with a leaf.</summary>
    private static readonly string[][] FlowerArt =
    [
        [
            ".a.",
            "aya",
            ".b.",
            "gs.",
            ".s.",
        ],
        [
            "a..",
            "yab",
            "b..",
            ".sg",
            ".s.",
        ],
    ];

    /// <summary>A post of the fence: a lit cap, the post lit on its left, a dark side.</summary>
    private static readonly string[] PostArt =
    [
        ".44.",
        "4432",
        "0321",
        "0321",
        "0321",
        "0321",
        "0321",
        "0321",
        "0321",
        "0221",
        ".00.",
    ];

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaContext ctx, CapEncounterTurn turn, double s)
    {
        if (!turn.Showing(s + turn.Start) || l.SurfaceY <= 0) return;

        // where the water stands now: draining down from the surface, coming back up at the end
        var top = l.SurfaceY;
        var drain = Smooth(s / Drain);
        if (turn.RefillAt is { } refill) drain *= 1 - Smooth((s + turn.Start - refill) / Refill);
        var water = top + (int)Math.Round((l.Rows - top + 4) * drain);
        if (water <= top) return;

        Map(p, l, ctx, top, water, s);
        Features(p, l, ctx, top, water, s);
        Fence(p, l, ctx, top, water);
        if (l.Panel == CapPanelKind.Deep) Wild(p, l, ctx, top, turn, water, s);
        Walls(p, l, ctx, top, water, s);
        SetPieces(p, l, ctx, top, water, s);
        Shore(p, l, water, s);
        Flyover(p, l, ctx, s, turn.Duplicate);
    }

    private static double Smooth(double k)
    {
        k = Math.Clamp(k, 0, 1);
        return k * k * (3 - (2 * k));
    }

    private static uint Hash(int a, int b) => (uint)CapArt.Hash(a, b);

    private static int Mod(int a, int m) => ((a % m) + m) % m;

    /// <summary>The wind at a column of the world: gusts running across the land from the left, stronger and weaker by turns (over 0.5, a gust).</summary>
    private static double Wind(double wx, double s) => Math.Sin((wx * 0.035) - (s * 2.4)) * (0.65 + (0.35 * Math.Sin(s * 0.7)));

    /// <summary>
    /// A tone out of a ramp at a fractional step (0 its first colour), in the sky's ordered dither (asked 2026-10-20: the meadow drawn in the
    /// sky's dots): a cell between two steps takes one or the other by the 4×4 pattern, so the tones blend as the sky's do.
    /// </summary>
    private static uint Dith(uint[] ramp, double step, int x, int y) => CapSeaPalette.Dithered(ramp, Math.Clamp(step, 0, ramp.Length - 1) / (ramp.Length - 1), x, y);

    /// <summary>A cell of shadow on the grass, in the dither's pattern: the darker the stronger (0 to 1), as the sky fades its bands.</summary>
    private static void Shade(CapSheet p, int x, int y, double strength, int water)
    {
        if (y < water && (CapSeaPalette.Bayer[((y & 3) * 4) + (x & 3)] + 0.5) / 16.0 < strength) p.Set(x, y, GrassPal[0]);
    }

    /// <summary>A cell, if it is above the water.</summary>
    private static void Set(CapSheet p, int x, int y, uint c, int water)
    {
        if (y < water) p.Set(x, y, c);
    }

    // ---------------------------------------------------------------- the ground

    /// <summary>The meadow's colour at a cell: the grass tile, shifted by the light's patches.</summary>
    private static uint Grass(int wx, int ry, int x, int y)
    {
        // the light over the meadow: broad soft patches, lighter and darker, blended in the sky's dots
        var v = 2.1 + ((Math.Sin((wx * 0.045) + (ry * 0.02)) + Math.Sin((wx * 0.017) - (ry * 0.05) + 1.3)) * 0.38);
        // a blade's tuft here and there; every other tile mirrored, so they do not line up in rows
        var tx = Mod(wx, 16);
        var ty = Mod(ry, 16);
        if (Hash(wx >> 4, ry >> 4) % 2 == 0) tx = 15 - tx;
        var tile = Hash(wx >> 4, (ry >> 4) * 7) % 4 == 0 ? GrassClover : GrassPlain;
        var t = tile[ty][tx] - '0';
        if (t != 2) return GrassPal[Math.Clamp((int)Math.Round(v) + t - 2, 0, GrassPal.Length - 1)];
        return Dith(GrassPal, v, x, y);
    }

    /// <summary>The path's dirt at a cell: lit along its upper edge, dark along its lower, speckles, cracks, pebbles.</summary>
    private static uint Dirt(int wx, int y, double dp)
    {
        if (dp < -5.5) return DirtPal[3];
        if (dp > 5) return DirtPal[0];
        if (dp > 3.5) return DirtPal[1];
        var h = Hash(wx, y);
        // a pebble: a lit grey cell over a dark one
        if (h % 61 == 0) return RockPal[3];
        if (Hash(wx, y - 1) % 61 == 0) return RockPal[1];
        // speckles and little cracks
        if (h % 13 == 0) return DirtPal[1];
        if (h % 17 == 0) return DirtPal[4];
        if (Mod(wx + (y * 3), 37) == 0 && Mod(y, 3) == 0) return DirtPal[0];
        return DirtPal[2];
    }

    // ---------------------------------------------------------------- the ridge: trees along the near crest

    /// <summary>The clusters a tree's crown is made of, from its middle: back ones first, each a ball of leaves (offset and radius in units of the crown's radius).</summary>
    private static readonly (double X, double Y, double R)[] Clusters =
    [
        (-0.55, -0.15, 0.55), (0.55, -0.1, 0.55), (0.0, -0.55, 0.6), (-0.35, 0.3, 0.55), (0.4, 0.3, 0.55), (0.0, 0.05, 0.62),
        (-0.62, 0.45, 0.4), (0.65, 0.45, 0.4), (0.05, -0.85, 0.38),
    ];

    /// <summary>
    /// A round tree: its crown a heap of leaf clusters, each lit at its top left (the lightest leaves in a crescent there), darker towards its
    /// lower right, the clusters in front drawn over the ones behind with a dark seam where they meet, a dark ink edge round the whole, a few
    /// stray lit leaves; a trunk of bark lit on its left with a knot, roots spreading at its foot, and an oval of shadow on the grass.
    /// </summary>
    private static void Tree(CapSheet p, int x, int foot, int water, int r, int seed, double wind)
    {
        // the shadow on the grass
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -r - 2; dx <= r + 4; dx++)
            {
                var e = ((dx - 1) * (dx - 1) / (double)((r + 3) * (r + 3))) + (dy * dy / 6.0);
                if (e > 1) continue;
                Shade(p, x + dx, foot + dy, 1.05 - (e * 0.6), water);
            }
        }

        // the trunk and its roots
        var trunk = r + 2;
        for (var h = 0; h < trunk; h++)
        {
            var y = foot - h;
            var flare = h < 2 ? 2 - h : 0;
            for (var dx = -2 - flare; dx <= 2 + flare; dx++)
            {
                uint c = dx == -2 - flare || dx == 2 + flare ? BarkPal[0] : dx < 0 ? BarkPal[3] : dx == 0 ? BarkPal[2] : BarkPal[1];
                if (h == trunk / 2 && dx == 0) c = BarkPal[0];          // a knot
                if (Mod(h + dx, 4) == 0 && dx > 0) c = BarkPal[0];
                Set(p, x + dx, y, c, water);
            }
        }

        // the crown, from its cells worked out once for its size and lean; then the ink edge round it
        var cy = foot - trunk - (int)(r * 0.6);
        foreach (var (dx, dy, rim, lit) in Crown(r, wind > 0.5))
        {
            int ix = x + dx, iy = cy + dy;
            Set(p, ix, iy, rim ? LeafPal[1] : Dith(LeafPal, 3 + (lit * 2.3), ix, iy), water);
        }

        foreach (var (dx, dy) in CrownEdge(r)) Set(p, x + dx, cy + dy, LeafPal[0], water);
    }

    /// <summary>A crown's cells by its size and whether it leans in a gust, and its outer edge by its size: worked out once (2026-10-20, the frame rate).</summary>
    private static readonly Dictionary<(int R, bool Lean), (int X, int Y, bool Rim, double Lit)[]> Crowns = [];
    private static readonly Dictionary<int, (int X, int Y)[]> CrownEdges = [];

    /// <summary>
    /// The cells of a crown, from its middle: each cluster in turn over the ones behind (on whole cells, so no seam shows through between them),
    /// the upper ones a cell over in a gust; each cell lit from the top left by where it sits in its cluster, or a dark seam on the cluster's rim.
    /// </summary>
    private static (int X, int Y, bool Rim, double Lit)[] Crown(int r, bool lean)
    {
        lock (Crowns)
        {
            if (Crowns.TryGetValue((r, lean), out var done)) return done;
            var cells = new Dictionary<(int, int), (bool Rim, double Lit)>();
            foreach (var (ox, oy, or) in Clusters)
            {
                var shift = lean && oy < 0 ? 1 : 0;
                int bx = (int)Math.Round(ox * r) + shift, by = (int)Math.Round(oy * r);
                var br = or * r;
                for (var dy = -(int)br - 1; dy <= br + 1; dy++)
                {
                    for (var dx = -(int)br - 1; dx <= br + 1; dx++)
                    {
                        var d = Math.Sqrt((dx * dx) + (dy * dy));
                        if (d > br + 0.3) continue;
                        cells[(bx + dx, by + dy)] = (d > br - 1, ((-dx * 0.6) - (dy * 0.8)) / br);
                    }
                }
            }

            return Crowns[(r, lean)] = [.. cells.Select(c => (c.Key.Item1, c.Key.Item2, c.Value.Rim, c.Value.Lit))];
        }
    }

    /// <summary>The crown's outer edge: every crown cell with a neighbour outside the crown's shape.</summary>
    private static (int X, int Y)[] CrownEdge(int r)
    {
        lock (CrownEdges)
        {
            if (CrownEdges.TryGetValue(r, out var done)) return done;
            var edge = new List<(int, int)>();
            for (var dy = -(int)(r * 1.3) - 2; dy <= (int)(r * 1.0) + 2; dy++)
            {
                for (var dx = -(int)(r * 1.3) - 2; dx <= (int)(r * 1.3) + 2; dx++)
                {
                    if (!InCrown(dx, dy, r)) continue;
                    if (InCrown(dx - 1, dy, r) && InCrown(dx + 1, dy, r) && InCrown(dx, dy - 1, r) && InCrown(dx, dy + 1, r)) continue;
                    edge.Add((dx, dy));
                }
            }

            return CrownEdges[r] = [.. edge];
        }
    }

    private static bool InCrown(int dx, int dy, int r)
    {
        foreach (var (ox, oy, or) in Clusters)
        {
            double ddx = dx - (ox * r), ddy = dy - (oy * r), br = or * r;
            if ((ddx * ddx) + (ddy * ddy) <= (br + 0.3) * (br + 0.3)) return true;
        }

        return false;
    }

    /// <summary>
    /// A palm: a trunk of rings (each lit on its left, a dark line under it) curving up, a crown of fronds drooping out of it, each a lit midrib
    /// with leaflets hanging off it in a jagged edge, darker underneath; coconuts in the crown; a shadow on the grass.
    /// </summary>
    private static void Palm(CapSheet p, int x, int foot, int water, int lean, double wind)
    {
        for (var dx = -6; dx <= 8; dx++) { Shade(p, x + dx, foot + 1, 0.8, water); Shade(p, x + dx, foot + 2, 0.4, water); }
        const int Tall = 30;
        double tx = x, ty = foot;
        for (var h = 0; h < Tall; h++)
        {
            tx = x + (lean * Math.Pow(h / (double)Tall, 2) * 6);
            ty = foot - h;
            var ring = h % 4 == 0;
            int ix = (int)Math.Round(tx), iy = (int)Math.Round(ty);
            Set(p, ix - 2, iy, BarkPal[0], water);
            Set(p, ix - 1, iy, ring ? BarkPal[1] : BarkPal[4], water);
            Set(p, ix, iy, ring ? BarkPal[1] : BarkPal[3], water);
            Set(p, ix + 1, iy, ring ? BarkPal[0] : BarkPal[2], water);
            Set(p, ix + 2, iy, BarkPal[0], water);
        }

        int cx = (int)Math.Round(tx), cy = (int)Math.Round(ty);
        // the fronds, the ones behind first: each a thick blade arching out of the crown and drooping to a point, lit along its upper side,
        // darker under, an ink edge over it, its lower edge jagged where the leaflets hang
        foreach (var (angle, len, back) in Fronds)
        {
            var a = angle + (lean * 0.08) + (wind * 0.07);
            for (var d = 1; d <= len; d++)
            {
                var u = d / (double)len;
                double fx = cx + (Math.Cos(a) * d), fy = cy + (Math.Sin(a) * d * 0.55) + (u * u * len * 0.7);
                int ix = (int)Math.Round(fx), iy = (int)Math.Round(fy);
                var thick = u < 0.75 ? 2 : u < 0.92 ? 1 : 0;
                Set(p, ix, iy - 1, LeafPal[0], water);
                Set(p, ix, iy, back ? LeafPal[3] : LeafPal[5], water);
                if (thick >= 1) Set(p, ix, iy + 1, back ? LeafPal[2] : LeafPal[4], water);
                if (thick >= 2) Set(p, ix, iy + 2, back ? LeafPal[1] : LeafPal[3], water);
                // the jagged lower edge: a leaflet tip every other cell
                var tip = iy + thick + 1 + (d % 2 == 0 && u > 0.2 && u < 0.85 ? 1 : 0);
                Set(p, ix, tip, LeafPal[back ? 0 : 1], water);
            }
        }

        // the coconuts
        foreach (var (ox, oy) in Nuts) { Set(p, cx + ox, cy + oy, BarkPal[1], water); Set(p, cx + ox, cy + oy - 1, BarkPal[3], water); }
    }

    private static readonly (int X, int Y)[] Nuts = [(-1, 2), (1, 2), (0, 3)];

    /// <summary>A palm's fronds: their angle out of the crown, their length, and whether they are behind (darker).</summary>
    private static readonly (double Angle, int Len, bool Back)[] Fronds =
    [
        (-2.1, 11, true), (-1.05, 11, true), (-1.6, 10, true),
        (-2.95, 14, false), (-2.45, 13, false), (-0.7, 13, false), (-0.2, 14, false), (-1.35, 12, false),
    ];

    // ---------------------------------------------------------------- the meadow's things

    /// <summary>
    /// A patch of tall grass: strips of pointed blade clumps, each strip set half a clump over the one above, the lit tips of the front ones over
    /// the shadowed roots of those behind; a few clumps swaying a cell now and then; its ground dark under it.
    /// </summary>
    private static void Tall(CapSheet p, int x, int y, int w, int h, int seed, int water, double s)
    {
        var width = w * 16;
        for (var dy = 2; dy < (h * 8) + 2; dy++) for (var dx = 1; dx < width - 1; dx++) Set(p, x + dx, y + dy, TallPal[0], water);
        for (var row = 0; row < h * 2; row++)
        {
            var shift = (row % 2) * 6;
            var ry = y + (row * 4);
            for (var r = 0; r < TallStrip.Length; r++)
            {
                var line = TallStrip[r];
                for (var dx = 0; dx < width; dx++)
                {
                    var stir = Mod((int)Math.Floor(s * 1.3) + seed, 6) == 0 && Mod((int)Math.Floor(s * 12) + dx / 6, 2) == 0;
                    var sway = r < 4 && (stir || Wind(x + dx, s) > 0.45) ? 1 : 0;
                    var ch = line[Mod(dx + shift + sway, 16)];
                    if (ch == '.') continue;
                    Set(p, x + dx, ry + r, TallPal[ch - '0'], water);
                }
            }
        }
    }


    /// <summary>A flower: its petals in one of three colours.</summary>
    private static void Flower(CapSheet p, int x, int y, uint kind, int frame, int water)
    {
        var art = FlowerArt[frame];
        var (a, b) = kind switch { 0 => (0xF84848u, 0xB82828u), 1 => (0xFFFFFFu, 0xC8D0E0u), _ => (0xF890D0u, 0xC05898u) };
        for (var r = 0; r < art.Length; r++)
        {
            for (var c = 0; c < art[r].Length; c++)
            {
                uint? colour = art[r][c] switch { 'a' => a, 'b' => b, 'y' => 0xFFD840u, 's' => GrassPal[1], 'g' => GrassPal[3], _ => null };
                if (colour is { } cc) Set(p, x + c, y + r, cc, water);
            }
        }
    }

    /// <summary>A piece drawn from its letters (0 the darkest of its palette), its top left at a point.</summary>
    private static void Sprite(CapSheet p, string[] art, uint[] pal, int x, int y, int water)
    {
        for (var r = 0; r < art.Length; r++)
        {
            for (var c = 0; c < art[r].Length; c++)
            {
                var ch = art[r][c];
                if (ch is < '0' or > '9') continue;
                Set(p, x + c, y + r, pal[Math.Min(pal.Length - 1, ch - '0')], water);
            }
        }
    }

    // ---------------------------------------------------------------- the map, seen from above as the games draw their routes

    /// <summary>The size of a tile of the map, in cells.</summary>
    private const int Tile = 16;

    /// <summary>How many tiles high the walls of trees are, top and bottom.</summary>
    private const int WallRows = 2;

    /// <summary>How many columns of tiles the route's plan spans before it repeats.</summary>
    private const int Period = 48;

    /// <summary>The row (in tiles from the land's top) of the upper stretch of the path.</summary>
    private static int Basis(CapWorldLayout l, int top) => Math.Max(WallRows + 3, (int)((l.Rows - top) / Tile * 0.38));

    /// <summary>
    /// The row (in tiles from the land's top) the path runs along at a column of tiles of the world, as the player drew it (2026-10-20): the
    /// upper stretch on the left, the lower one, three rows down, from the first crossing on.
    /// </summary>
    private static int PathRow(CapWorldLayout l, int top, int tx) => Basis(l, top) + (Mod(tx, Period) is >= 10 and < 46 ? 3 : 0);

    /// <summary>Whether a column of tiles is one of the two crossings that climb from the path to a gap in the top wall of trees.</summary>
    private static bool Climb(int tx) => Mod(tx, Period) is 10 or 11 or 34 or 35;

    private static int Floor(int a, int b) => (int)Math.Floor(a / (double)b);

    /// <summary>Whether a tile is path: two rows along, and at each step the bend down between the two stretches.</summary>
    private static bool InPath(CapWorldLayout l, int top, int tx, int ty)
    {
        var row = PathRow(l, top, tx);
        if (ty == row || ty == row + 1) return true;
        // the crossings: two columns going up from it to a gap in the wall of trees
        if (Climb(tx) && ty >= WallRows - 1 && ty < row) return true;
        // the bend back up to the upper stretch, where the plan starts over
        var b = Basis(l, top);
        return Mod(tx, Period) >= 46 && ty >= b && ty <= b + 4;
    }

    /// <summary>What a block of the meadow holds: tall grass, a pond, a grove of trees.</summary>
    private enum Kind { Grass, Pond, Grove }

    /// <summary>How many columns of tiles a block of the meadow spans: each block holds one feature at most.</summary>
    private const int Block = 5;

    /// <summary>
    /// The feature (in tiles) of a block of the world's columns of tiles, if it has one: what, where, how wide and high; by the path, above it
    /// when there is room or below it, clear of the path, the ledge, the walls and (in the abyss's window) the wild Pokémon's patch.
    /// </summary>
    private static (Kind Kind, int Tx, int Ty, int W, int H)? Feature(CapWorldLayout l, int top, CapSeaContext ctx, int block)
    {
        if (Mod(block, 3) != 1 && Hash(block, 61) % 6 == 0) return null;
        var kind = Mod(block, 3) == 1 ? Kind.Pond : (Hash(block, 67) % 3) switch { 0 => Kind.Grove, _ => Kind.Grass };
        var rows = (l.Rows - top) / Tile;
        var w = (kind == Kind.Grove ? 2 : 3) + (int)(Hash(block, 62) % 2);
        var h = 2 + (int)(Hash(block, 63) % 2);
        var tx = (block * Block) + (int)(Hash(block, 64) % (uint)Math.Max(1, Block + 1 - w));
        var row = PathRow(l, top, tx);
        var above = Hash(block, 65) % 2 == 0 && row - WallRows - 2 >= h + 1;
        var ty = above ? row - 1 - h - (int)(Hash(block, 66) % 2) : row + 2 + (int)(Hash(block, 66) % 3);
        // in the abyss's window, what would fall on the wild Pokémon's patch goes below it
        if (HidesWild(l, top, ctx, tx, ty, w, h)) { ty = WildTile(l, top, ctx).Ty + 3; h = Math.Max(2, Math.Min(h, rows - WallRows - ty)); }
        if (ty < WallRows + 1 || ty + h > rows - WallRows) return null;
        for (var x = tx; x < tx + w; x++) for (var y = ty; y < ty + h; y++) if (InPath(l, top, x, y) || y == PathRow(l, top, x) - 3 || Reserved(l, top, x, y)) return null;
        return (kind, tx, ty, w, h);
    }

    /// <summary>Whether a tile of a feature's rectangle is in its shape: its corners bitten off now and then, so no two look alike.</summary>
    private static bool InShape(int block, int i, int j, int w, int h)
    {
        var corner = (i == 0 || i == w - 1) && (j == 0 || j == h - 1);
        return !corner || Hash(block * 4 + i, j + 71) % 3 != 0;
    }

    /// <summary>Whether a rectangle of tiles of the world falls on the wild Pokémon's patch (only in the abyss's window, where it stands).</summary>
    private static bool HidesWild(CapWorldLayout l, int top, CapSeaContext ctx, int tx, int ty, int w, int h)
    {
        if (l.Panel != CapPanelKind.Deep) return false;
        var (wx0, wy0) = WildTile(l, top, ctx);
        return tx < wx0 + 6 && tx + w > wx0 - 1 && ty < wy0 + 3 && ty + h > wy0 - 1;
    }

    /// <summary>The top-left tile (in the world's grid) of the wild Pokémon's patch: five tiles wide under the path, in the middle of the abyss's window.</summary>
    private static (int Tx, int Ty) WildTile(CapWorldLayout l, int top, CapSeaContext ctx)
    {
        var tx = Floor(l.Main.Left + (l.Main.Width / 2) + ctx.WorldX, Tile) - 2;
        return (tx, PathRow(l, top, tx) + 3);
    }

    /// <summary>Whether a tile is taken by a feature.</summary>
    private static bool Occupied(CapWorldLayout l, int top, CapSeaContext ctx, int tx, int ty)
    {
        if (Feature(l, top, ctx, Floor(tx, Block)) is not { } f) return false;
        return tx >= f.Tx && tx < f.Tx + f.W && ty >= f.Ty && ty < f.Ty + f.H;
    }

    /// <summary>The meadow's features block by block: tall grass in its shape, ponds, groves; then the rocks and the shrubs in free tiles.</summary>
    private static void Features(CapSheet p, CapWorldLayout l, CapSeaContext ctx, int top, int water, double s)
    {
        for (var b = Floor(ctx.WorldX, Tile * Block) - 1; b * Tile * Block < ctx.WorldX + l.Cols + Tile; b++)
        {
            if (Feature(l, top, ctx, b) is not { } f || HidesWild(l, top, ctx, f.Tx, f.Ty, f.W, f.H)) continue;
            var x0 = (f.Tx * Tile) - ctx.WorldX;
            var y0 = top + (f.Ty * Tile);
            if (f.Kind == Kind.Grass)
            {
                for (var j = 0; j < f.H; j++) for (var i = 0; i < f.W; i++) if (InShape(b, i, j, f.W, f.H)) Tall(p, x0 + (i * Tile), y0 + (j * Tile), 1, 2, b + i + j, water, s);
            }
            else if (f.Kind == Kind.Pond)
            {
                Pond(p, x0, y0, f.W * Tile, f.H * Tile, b, water, s);
            }
            else
            {
                for (var i = 0; i < f.W; i += 2)
                {
                    var tx = (f.Tx + i) * Tile;
                    Tree(p, tx - ctx.WorldX + 8, y0 + (f.H * Tile) - 4 - ((i / 2) % 2 * 10), water, 8, b * 13 + i, Wind(tx, s));
                }
            }
        }

        // rocks to smash and shrubs to cut, in free tiles here and there
        var rows = (l.Rows - top) / Tile;
        for (var tx = Floor(ctx.WorldX, Tile) - 1; tx * Tile < ctx.WorldX + l.Cols + Tile; tx++)
        {
            for (var ty = WallRows + 1; ty < rows - WallRows - 1; ty++)
            {
                var h = Hash(tx * 3, ty * 5);
                if (h % 15 > 4 || !Free(l, top, ctx, tx, ty)) continue;
                var x = (tx * Tile) - ctx.WorldX;
                var y = top + (ty * Tile);
                switch (h % 15)
                {
                    case 0: Boulder(p, x + 1, y + 4, water, tx + ty); break;
                    case 1:
                        Tree(p, x + 8, y + 14, water, 7, tx * 7 + ty, Wind(tx * Tile, s));
                        if (h % 2 == 0) BerryPile(p, x + 10, y + 14, tx + ty, water);
                        break;
                    case 2: Hibiscus(p, x + 8, y + 13, tx + ty, water, Wind(tx * Tile, s)); break;
                    case 3: Stump(p, x + 3, y + 5, water); break;
                    default:
                        if (Free(l, top, ctx, tx + 1, ty)) Log(p, x + 3, y + 6, water);
                        break;
                }
            }
        }
    }

    /// <summary>Whether a tile of the meadow is free for something to lie on: not path, ledge or the fence's row, nor taken by anything else.</summary>
    private static bool Free(CapWorldLayout l, int top, CapSeaContext ctx, int tx, int ty)
    {
        var row = PathRow(l, top, tx);
        return !InPath(l, top, tx, ty) && !Occupied(l, top, ctx, tx, ty) && ty != row - 3 && ty != row - 1 && !Reserved(l, top, tx, ty)
            && !HidesWild(l, top, ctx, tx, ty, 1, 1);
    }

    /// <summary>
    /// A pond: rounded at its corners, a rim of stones (lit on top, dark below) round water in bands, deeper in its middle, lines of light
    /// running over it, lily pads floating (round, a notch, a lit edge, one with a pink flower), rings now and then.
    /// </summary>
    private static void Pond(CapSheet p, int x0, int y0, int w, int h, int seed, int water, double s)
    {
        const double R = 7;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                // the rounded corners
                double cx = Math.Clamp(x, R, w - 1 - R), cy = Math.Clamp(y, R, h - 1 - R);
                var d = Math.Sqrt(((x - cx) * (x - cx)) + ((y - cy) * (y - cy)));
                if (d > R) continue;
                uint c;
                if (d > R - 2) c = y < h / 2 ? RockPal[3] : RockPal[2];                 // the stones of its rim
                else if (d > R - 3) c = RockPal[0];
                else
                {
                    // how far in from the rim, rounded at the corners as the rim is
                    var deep = x >= R && x <= w - 1 - R && y >= R && y <= h - 1 - R ? R + Math.Min(Math.Min(x - R, w - 1 - R - x), Math.Min(y - R, h - 1 - R - y)) : R - d;
                    c = Dith(WaterPal, 2.2 - Math.Min(1.4, deep / 5.0) - (y < 6 ? (6 - y) / 6.0 : 0), x0 + x, y0 + y);
                    // lines of light running over it
                    if (Mod(x + y + (int)(s * 6), 23) == 0 && Mod(y, 4) == 1) c = WaterPal[3];
                    if (Mod(x - (y * 2) - (int)(s * 4), 31) == 0 && Mod(y, 5) == 2) c = WaterPal[4];
                }

                Set(p, x0 + x, y0 + y, c, water);
            }
        }

        // lily pads
        for (var n = 0; n < 3; n++)
        {
            int px = x0 + 10 + (int)(Hash(seed, n) % (uint)Math.Max(1, w - 20)), py = y0 + 8 + (int)(Hash(seed, n + 5) % (uint)Math.Max(1, h - 16));
            px += (int)Math.Round(Math.Sin((s * 0.8) + n));
            for (var dy = 0; dy < LilyPad.Length; dy++)
            {
                for (var dx = 0; dx < LilyPad[dy].Length; dx++)
                {
                    var v = LilyPad[dy][dx];
                    if (v != '.') Set(p, px - 3 + dx, py - 2 + dy, GrassPal[v - '0'], water);
                }
            }

            if (n == 0) { Set(p, px - 1, py - 1, 0xF890C0u, water); Set(p, px - 1, py - 2, 0xFFD0E8u, water); }
        }

        // a ring spreading now and then
        var ring = (s * 0.5) + seed;
        var u = ring - Math.Floor(ring);
        if (u < 0.6)
        {
            int rx = x0 + (w / 2) + (int)(Hash(seed, (int)ring) % 9) - 4, ry = y0 + (h / 2);
            var r = 2 + (u * 10);
            for (var a = 0; a < 24; a++)
            {
                var t = a / 24.0 * Math.PI * 2;
                Set(p, rx + (int)Math.Round(Math.Cos(t) * r), ry + (int)Math.Round(Math.Sin(t) * r * 0.5), WaterPal[4], water);
            }
        }
    }

    /// <summary>A lily pad: round, a notch cut into it, lit on top, its rim darker below.</summary>
    private static readonly string[] LilyPad =
    [
        ".4444..",
        "443344.",
        "4333...",
        "13333.1",
        ".11111.",
    ];

    /// <summary>
    /// The ground of the map, cell by cell down to the water: grass everywhere (its tufts leaning in the gusts); the path's sand (speckled, a few
    /// pebbles) with a dark rim where it meets the grass and the grass's tufts hanging over its upper edge; a ledge across the map above the
    /// path, broken here and there (a lit lip, a dark face, its shadow); flower tiles, two flowers to a tile, nodding in turn.
    /// </summary>
    private static void Map(CapSheet p, CapWorldLayout l, CapSeaContext ctx, int top, int water, double s)
    {
        var bottom = Math.Min(water, l.Rows);
        var ground = Ground(l, ctx, top);
        for (var y = top - 4; y < bottom; y++)
        {
            var row = (y - top + 4) * l.Cols;
            for (var x = 0; x < l.Cols; x++) p.Set(x, y, ground[row + x]);
        }

        // the flower tiles: two flowers each, nodding in turn
        for (var tx = Floor(ctx.WorldX, Tile) - 1; tx * Tile < ctx.WorldX + l.Cols + Tile; tx++)
        {
            for (var ty = WallRows; ty < ((l.Rows - top) / Tile) - WallRows; ty++)
            {
                if (Hash(Floor(tx, 2), Floor(ty, 2) * 31) % 7 != 0 || InPath(l, top, tx, ty) || ty == PathRow(l, top, tx) - 3) continue;
                if (Occupied(l, top, ctx, tx, ty) || Reserved(l, top, tx, ty)) continue;
                var x = (tx * Tile) - ctx.WorldX;
                var y = top + (ty * Tile);
                var kind = Hash(Floor(tx, 2), Floor(ty, 2)) % 3;
                var frame = Mod((int)Math.Floor((s * 2) + tx), 2);
                Flower(p, x + 3, y + 3, kind, frame, bottom);
                Flower(p, x + 10, y + 9, kind, 1 - frame, bottom);
            }
        }
    }

    /// <summary>The ground of each window, kept: it is the same every frame (2026-10-20, the frame rate). A few at most: the two windows, a resize.</summary>
    private static readonly Dictionary<(int Cols, int Rows, int Top, int WorldX), uint[]> Grounds = [];

    private static uint[] Ground(CapWorldLayout l, CapSeaContext ctx, int top)
    {
        lock (Grounds)
        {
            var key = (l.Cols, l.Rows, top, ctx.WorldX);
            if (Grounds.TryGetValue(key, out var done)) return done;
            if (Grounds.Count >= 6) Grounds.Clear();
            var ground = new uint[(l.Rows - top + 4) * l.Cols];
            PaintGround(ground, l, ctx, top);
            return Grounds[key] = ground;
        }
    }

    /// <summary>
    /// The ground of the map, cell by cell down to the window's foot: grass everywhere; the path's sand with a dark rim where it meets the grass
    /// and the grass's tufts hanging over its upper edge; a ledge across the map above the path, broken here and there, with steps where a
    /// crossing goes over it.
    /// </summary>
    private static void PaintGround(uint[] ground, CapWorldLayout l, CapSeaContext ctx, int top)
    {
        var bottom = l.Rows;
        for (var x = 0; x < l.Cols; x++)
        {
            var wx = x + ctx.WorldX;
            var tx = Floor(wx, Tile);
            var lx = Mod(wx, Tile);
            for (var y = top - 4; y < bottom; y++)
            {
                var wy = Math.Max(0, y - top);
                var ty = wy / Tile;
                var ly = wy % Tile;
                uint c = Grass(wx, wy, x, y);
                if (InPath(l, top, tx, ty))
                {
                    // the rim: where a neighbouring tile is grass
                    var up = !InPath(l, top, tx, ty - 1) && ly < 2;
                    var down = !InPath(l, top, tx, ty + 1) && ly > 13;
                    var left = !InPath(l, top, tx - 1, ty) && lx < 2;
                    var right = !InPath(l, top, tx + 1, ty) && lx > 13;
                    c = Sand(wx, wy, x, y);
                    if (!InPath(l, top, tx, ty - 1) && ly < 6 && c != RockPal[3] && c != RockPal[1]) c = Dith(DirtPal, 2 + (ly / 6.0), x, y);
                    if (down) c = ly == 15 ? DirtPal[0] : DirtPal[1];
                    if (left || right) c = (lx is 0 or 15) ? DirtPal[0] : DirtPal[1];
                    // the grass hanging over its upper edge in tufts
                    if (up && (ly == 0 || (ly == 1 && Mod(wx, 4) < 2))) c = ly == 0 ? GrassPal[1] : GrassPal[2];
                }
                else if (InPath(l, top, tx, ty + 1) && ly == 15)
                {
                    c = Dith(GrassPal, 1.4, x, y);                              // the grass's shadow over the path's upper edge
                }

                // the ledge: a row of tiles above the path, broken here and there; steps where the branch goes over it
                var ledge = PathRow(l, top, tx) - 3;
                if (ty == ledge && InPath(l, top, tx, ty) && !InPath(l, top, tx - 1, ty) && !InPath(l, top, tx + 2, ty) && ly is >= 9 and <= 15)
                {
                    c = ly % 2 == 1 ? DirtPal[1] : ly == 9 ? DirtPal[4] : DirtPal[3];
                }
                else if (ty == ledge && Mod(Floor(tx, 6), 3) != 2 && !InPath(l, top, tx, ty) && !Reserved(l, top, tx, ty) && !InPath(l, top, tx, ty + 1))
                {
                    c = ly switch
                    {
                        9 => GrassPal[4],
                        10 => GrassPal[3],
                        11 => GrassPal[0],
                        12 or 13 => Dith(GrassPal, ly == 12 ? 0.75 : 0.4, x, y),
                        14 => GrassPal[0],
                        15 => GrassPal[1],
                        _ => c,
                    };
                }

                ground[((y - top + 4) * l.Cols) + x] = c;
            }
        }
    }

    /// <summary>The path's sand at a cell: soft patches lighter and darker blended in the sky's dots, a pebble now and then.</summary>
    private static uint Sand(int wx, int wy, int x, int y)
    {
        var h = Hash(wx, wy);
        if (h % 397 == 0) return RockPal[3];
        if (Hash(wx, wy - 1) % 397 == 0) return RockPal[1];
        return Dith(DirtPal, 3 + ((Math.Sin((wx * 0.06) + (wy * 0.11)) + Math.Sin((wx * 0.023) - (wy * 0.07) + 2.1)) * 0.3), x, y);
    }

    /// <summary>A wooden fence along the path's upper edge here and there: posts every tile with their caps, two rails lit on top, a shadow under.</summary>
    private static void Fence(CapSheet p, CapWorldLayout l, CapSeaContext ctx, int top, int water)
    {
        for (var x = 0; x < l.Cols; x++)
        {
            var wx = x + ctx.WorldX;
            var tx = Floor(wx, Tile);
            var c = Mod(tx, Period);
            var fenced = c is >= 6 and <= 8 or >= 37 and <= 43 || (c is >= 13 and <= 32 && Mod(Floor(tx, 5), 3) == 0);
            if (!fenced) continue;
            var y = top + (PathRow(l, top, tx) * Tile) - 4;
            Set(p, x, y + 1, GrassPal[1], water);
            foreach (var rail in (ReadOnlySpan<int>)[-7, -3])
            {
                Set(p, x, y + rail, WoodPal[4], water);
                Set(p, x, y + rail + 1, WoodPal[2], water);
                Set(p, x, y + rail + 2, WoodPal[0], water);
            }

            if (Mod(wx, Tile) == 0) Sprite(p, PostArt, WoodPal, x - 1, y - 10, water);
        }
    }

    /// <summary>
    /// The walls of trees along the map's top and bottom, as the routes are closed in: two rows each, the back row set half a tree over, every
    /// few trees a palm (this is Alola).
    /// </summary>
    private static void Walls(CapSheet p, CapWorldLayout l, CapSeaContext ctx, int top, int water, double s)
    {
        var first = Floor(ctx.WorldX - 24, 14);
        for (var row = 0; row < 2; row++)
        {
            for (var k = first; k * 14 < ctx.WorldX + l.Cols + 24; k++)
            {
                var wx = (k * 14) + (row * 7);
                var x = wx - ctx.WorldX;
                var foot = top + 18 + (row * 13);
                if (Climb(Floor(wx + 4, Tile)) || Climb(Floor(wx - 4, Tile))) continue;
                if (Mod(k + (row * 3), 6) == 0) Palm(p, x, foot, water, Mod(k, 2) == 0 ? -1 : 1, Wind(wx, s));
                else Tree(p, x, foot, water, 8, k + (row * 1000), Wind(wx, s));
            }
        }

        for (var row = 0; row < 2; row++)
        {
            for (var k = first; k * 14 < ctx.WorldX + l.Cols + 24; k++)
            {
                var wx = (k * 14) + (row * 7);
                Tree(p, wx - ctx.WorldX, l.Rows - 12 + (row * 14), water, 8, k + 2000 + (row * 1000), Wind(wx, s));
            }
        }
    }

    // ---------------------------------------------------------------- the house and the berry field, and what grows and lies about

    private static readonly uint[] RoofPal = [0x4A1A16u, 0x7E2A22u, 0xB0402Eu, 0xD4624Au, 0xF08E6Eu];
    private static readonly uint[] WallPal = [0x8A7A60u, 0xB8A888u, 0xE0D4B8u, 0xF6F0E0u];
    private static readonly uint[] SoilPal = [0x3A2414u, 0x5A3820u, 0x7A5030u, 0x966A42u];
    private const uint Ink = 0x24160Cu, Glass = 0x6CA8E0u, GlassLit = 0xC0E4FAu;

    /// <summary>The berries the field grows and the piles hold, each a colour and its lit tone: Oran, Cheri, Pecha, Sitrus.</summary>
    private static readonly (uint Body, uint Lit, uint Dark)[] Berries =
    [
        (0x3C6CE0u, 0x9CC0FFu, 0x1E3C90u), (0xD82828u, 0xFF8A78u, 0x801010u), (0xF078B0u, 0xFFC8E0u, 0xA03870u), (0xE8B820u, 0xFFF090u, 0x906810u),
    ];

    /// <summary>Whether the route's plan has room for the house above its path: three rows between the wall of trees and the path.</summary>
    private static bool HasHouse(CapWorldLayout l, int top) => Basis(l, top) - 3 >= WallRows;

    /// <summary>Whether a tile is taken by the house or the berry field, which are laid out by hand, not drawn by lot.</summary>
    private static bool Reserved(CapWorldLayout l, int top, int tx, int ty)
    {
        var c = Mod(tx, Period);
        var b = Basis(l, top);
        if (HasHouse(l, top) && c is >= 2 and <= 4 && ty >= b - 3 && ty <= b - 1) return true;
        return c is >= 0 and <= 2 && ty >= b + 3 && ty <= b + 5 && ty < ((l.Rows - top) / Tile) - WallRows;
    }

    /// <summary>The house and the berry field of each stretch of the plan the window shows.</summary>
    private static void SetPieces(CapSheet p, CapWorldLayout l, CapSeaContext ctx, int top, int water, double s)
    {
        var b = Basis(l, top);
        for (var k = Floor(ctx.WorldX, Period * Tile) - 1; k * Period * Tile < ctx.WorldX + l.Cols + Tile; k++)
        {
            var x0 = (k * Period * Tile) - ctx.WorldX;
            if (HasHouse(l, top)) House(p, x0 + (2 * Tile) + 2, top + ((b - 3) * Tile) + 4, water, s);
            if (b + 6 <= ((l.Rows - top) / Tile) - WallRows) Field(p, x0 + 2, top + ((b + 3) * Tile) + 2, (3 * Tile) - 4, (3 * Tile) - 4, water, s);
        }
    }

    /// <summary>
    /// A hibiscus bush, Alola's flower: a low round bush of leaves lit at its top left, an ink rim, its shadow on the grass, and big red and
    /// pink flowers on it (five petals round a yellow heart), nodding in the gusts.
    /// </summary>
    private static void Hibiscus(CapSheet p, int x, int foot, int seed, int water, double wind)
    {
        const double Rx = 7.5, Ry = 5.5;
        var cy = foot - 6;
        for (var dx = -8; dx <= 9; dx++) { Shade(p, x + dx, foot, 0.85, water); Shade(p, x + dx + 1, foot + 1, 0.4, water); }
        for (var dy = -6; dy <= 6; dy++)
        {
            for (var dx = -8; dx <= 8; dx++)
            {
                var e = (dx * dx / (Rx * Rx)) + (dy * dy / (Ry * Ry));
                if (e > 1) continue;
                Set(p, x + dx, cy + dy, e > 0.8 ? LeafPal[0] : Dith(LeafPal, 2.8 - ((dx + dy) / 5.0), x + dx, cy + dy), water);
            }
        }

        var nod = wind > 0.5 ? 1 : 0;
        for (var n = 0; n < 3; n++)
        {
            int fx = x - 4 + (n * 4) + nod, fy = cy - 2 + (int)(Hash(seed, n) % 4);
            uint petal = (seed + n) % 2 == 0 ? 0xE8304Au : 0xF878A8u, lit = (seed + n) % 2 == 0 ? 0xFF8090u : 0xFFC0D8u;
            Set(p, fx, fy - 1, lit, water);
            Set(p, fx - 1, fy, petal, water);
            Set(p, fx + 1, fy, petal, water);
            Set(p, fx - 1, fy + 1, petal, water);
            Set(p, fx + 1, fy + 1, petal, water);
            Set(p, fx, fy, 0xFFE060u, water);
            Set(p, fx, fy + 1, 0xC03040u, water);
        }
    }

    /// <summary>A pile of berries fallen at a tree's foot, as in Alola: berries of the four kinds heaped in a little pyramid, each lit and shaded.</summary>
    private static void BerryPile(CapSheet p, int x, int y, int seed, int water)
    {
        foreach (var (bx, by) in PileAt)
        {
            var berry = Berries[(int)(Hash(seed, bx * 7 + by) % (uint)Berries.Length)];
            Set(p, x + bx, y + by, berry.Lit, water);
            Set(p, x + bx + 1, y + by, berry.Body, water);
            Set(p, x + bx, y + by + 1, berry.Body, water);
            Set(p, x + bx + 1, y + by + 1, berry.Dark, water);
        }
    }

    private static readonly (int X, int Y)[] PileAt = [(0, 0), (3, 0), (6, 0), (1, -2), (4, -2), (2, -4)];

    // ---------------------------------------------------------------- the pieces in detail: rocks, wood, the field, the house

    private static readonly uint[] ShutterPal = [0x1E3E28u, 0x2E5C3Au, 0x427E50u, 0x62A270u];
    private static readonly uint[] GlassRamp = [0x3A6EB0u, Glass, GlassLit];
    private static readonly uint[] MailRamp = [0x9A1818u, 0xD83030u, 0xFF7060u];
    private static readonly uint[] CutPal = [0x6A4220u, 0x9A6A3Au, 0xC69A62u, 0xE2C08Au, 0xF4DCAEu];

    /// <summary>
    /// A rock to smash: an irregular boulder cut in planes (each facet lit by how it faces the sun at the top left, blended in the dots), an ink
    /// edge round it, two cracks across it with a lit lip under each, specks of light on its top, moss in the shade of its top, grass growing
    /// against its foot, pebbles beside it and its shadow on the grass.
    /// </summary>
    private static void Boulder(CapSheet p, int x, int y, int water, int seed)
    {
        int cx = x + 7, cy = y + 5;
        // its shadow, to the right and below
        for (var dy = 2; dy <= 8; dy++)
        {
            for (var dx = -6; dx <= 11; dx++)
            {
                var e = ((dx - 2.5) * (dx - 2.5) / 72.0) + ((dy - 5.5) * (dy - 5.5) / 6.0);
                if (e <= 1) Shade(p, cx + dx, cy + dy, 1.05 - (e * 0.7), water);
            }
        }

        for (var dy = -7; dy <= 5; dy++)
        {
            for (var dx = -9; dx <= 9; dx++)
            {
                if (!InRock(dx, dy, seed)) continue;
                int px = cx + dx, py = cy + dy;
                if (!InRock(dx - 1, dy, seed) || !InRock(dx + 1, dy, seed) || !InRock(dx, dy - 1, seed) || !InRock(dx, dy + 1, seed))
                {
                    Set(p, px, py, RockPal[0], water);
                    continue;
                }

                // the facet this cell is on: its normal snapped to one of six planes round a flat top
                double nx = dx / 7.5, ny = dy / 5.5;
                var e = (nx * nx) + (ny * ny);
                var angle = Math.Atan2(ny, nx);
                var sector = Math.Round((angle / (Math.PI / 3)) + (seed % 2 * 0.5));
                double fx = e < 0.3 ? 0 : Math.Cos(sector * Math.PI / 3) * 0.8, fy = e < 0.3 ? -0.3 : Math.Sin(sector * Math.PI / 3) * 0.8;
                var fz = Math.Sqrt(Math.Max(0, 1 - (fx * fx) - (fy * fy)));
                var light = (-0.55 * fx) - (0.65 * fy) + (0.52 * fz);
                var c = Dith(RockPal, 1.2 + (light * 2.4) - ((nx + ny) * 0.25), px, py);
                // moss in the shade of its top, specks of light on its sunny side
                if (fy < -0.3 && fx > 0.2 && Hash(px, py * 3) % 3 == 0) c = Dith(LeafPal, 2.6, px, py);
                if (light > 0.75 && Hash(px * 5, py) % 7 == 0) c = RockPal[4];
                Set(p, px, py, c, water);
            }
        }

        // the cracks: a dark line, the cell under it lit
        foreach (var (kx, ky) in RockCracks)
        {
            Set(p, cx + kx, cy + ky, RockPal[0], water);
            Set(p, cx + kx - 1, cy + ky + 1, RockPal[3], water);
        }

        // grass against its foot, pebbles beside it
        foreach (var (gx, gh) in RockTufts)
        {
            for (var k = 0; k < gh; k++) Set(p, cx + gx, cy + 5 - k, k == gh - 1 ? GrassPal[4] : GrassPal[3], water);
            Set(p, cx + gx + 1, cy + 5, GrassPal[2], water);
        }

        Set(p, cx + 10, cy + 4, RockPal[3], water); Set(p, cx + 11, cy + 4, RockPal[2], water); Set(p, cx + 10, cy + 5, RockPal[1], water); Set(p, cx + 11, cy + 5, RockPal[0], water);
        Set(p, cx - 11, cy + 5, RockPal[3], water); Set(p, cx - 11, cy + 6, RockPal[1], water);
    }

    /// <summary>Whether a cell (from the boulder's middle) is in it: an oval with its edge knocked about, flat where it sits on the ground.</summary>
    private static bool InRock(int dx, int dy, int seed)
    {
        if (dy > 5) return false;
        var angle = Math.Atan2(dy, dx);
        var bump = 1 + (0.08 * Math.Sin((angle * 3) + seed)) + (0.05 * Math.Sin((angle * 5) + (seed * 2)));
        double nx = dx / (8.5 * bump), ny = dy / (6.5 * bump);
        return (nx * nx) + (ny * ny) <= 1;
    }

    private static readonly (int X, int Y)[] RockCracks = [(-1, -4), (0, -3), (0, -2), (1, -1), (2, 0), (2, 1), (3, 2)];
    private static readonly (int X, int Gh)[] RockTufts = [(-8, 3), (-6, 2), (6, 3), (8, 2)];

    /// <summary>
    /// A stump of a cut tree: its cut top an oval of wood in rings (lit at its top left, a ring line every other band, a dark heart, a crack
    /// from the heart out), the bark round it; its side a cylinder of bark lit on its left and darkening round to its right in the dots, with
    /// furrows running down it; moss on its shady side; roots spreading at its foot; its shadow on the grass.
    /// </summary>
    private static void Stump(CapSheet p, int x, int y, int water)
    {
        int cx = x + 5, topY = y + 3;
        const double Rx = 5.5, Ry = 2.6;
        const int Side = 7;
        for (var dy = 0; dy <= 3; dy++) for (var dx = -3; dx <= 11; dx++) Shade(p, cx + dx, topY + Side + dy, 0.95 - (dy * 0.2) - (Math.Abs(dx - 4) * 0.04), water);

        // the roots: flares at its foot, lit on the left one, dark on the right
        foreach (var (dir, len) in Roots)
        {
            for (var k = 1; k <= len; k++)
            {
                var rx = cx + (dir * (6 + k));
                for (var ry = topY + Side - 2 + k; ry < topY + Side; ry++) Set(p, rx, ry, dir < 0 ? BarkPal[3] : BarkPal[1], water);
                Set(p, rx, topY + Side - 3 + k, BarkPal[0], water);
                Set(p, rx, topY + Side, BarkPal[0], water);
            }
        }

        // the side
        for (var dy = 0; dy <= Side; dy++)
        {
            var flare = dy >= Side - 1 ? 1 : 0;
            for (var dx = -6 - flare; dx <= 6 + flare; dx++)
            {
                int px = cx + dx, py = topY + dy;
                if (dx == -6 - flare || dx == 6 + flare || dy == Side) { Set(p, px, py, BarkPal[0], water); continue; }
                var v = 3.4 - ((dx + 6) / 12.0 * 2.8);
                if (Mod(dx + (dy / 3), 3) == 0) v -= 1;                        // the furrows
                var c = Dith(BarkPal, v, px, py);
                if (dx is >= 2 and <= 4 && dy is >= 1 and <= 3 && !(dx == 4 && dy == 3)) c = Dith(LeafPal, 3.6 - dy * 0.4, px, py);
                Set(p, px, py, c, water);
            }
        }

        // the cut top
        for (var dy = -3; dy <= 3; dy++)
        {
            for (var dx = -7; dx <= 7; dx++)
            {
                var d = Math.Sqrt((dx * dx / (Rx * Rx)) + (dy * dy / (Ry * Ry)));
                if (d > 1.05) continue;
                int px = cx + dx, py = topY + dy;
                uint c;
                if (d > 0.82) c = d > 0.97 ? BarkPal[0] : BarkPal[2];
                else if (d < 0.18) c = CutPal[1];
                else
                {
                    var band = (int)(d * 6);
                    c = band % 2 == 0 ? CutPal[2] : Dith(CutPal, 3.4 - ((dx + dy) * 0.12), px, py);
                }

                if (dx > 0 && dy == 0 && d < 0.8) c = CutPal[0];                // the crack from the heart
                Set(p, px, py, c, water);
            }
        }
    }

    private static readonly (int Dir, int Len)[] Roots = [(-1, 2), (1, 2)];

    /// <summary>
    /// A fallen log: a cylinder of bark lying on the grass, lit along its top and darkening to its underside in the dots, furrows running along
    /// it, a knot, the stub of a broken branch; moss on its back; its cut end an oval of rings; its far end broken off ragged; its shadow
    /// under it.
    /// </summary>
    private static void Log(CapSheet p, int x, int y, int water)
    {
        const int L = 25, R = 4;
        int x0 = x + 2, cy = y + 4;
        for (var i = -1; i <= L + 1; i++) { Shade(p, x0 + i, cy + R + 1, 0.95, water); Shade(p, x0 + i + 1, cy + R + 2, 0.45, water); }

        // the body
        for (var i = 1; i <= L; i++)
        {
            for (var dy = -R; dy <= R; dy++)
            {
                // its far end broken off ragged
                if (i > L - 3 && Hash(i, dy + 9) % 3 != 0 && Math.Abs(dy) < R) { if (i == L - 2) Set(p, x0 + i, cy + dy, CutPal[2], water); continue; }
                int px = x0 + i, py = cy + dy;
                if (Math.Abs(dy) == R) { Set(p, px, py, BarkPal[0], water); continue; }
                var v = 3.6 - ((dy + R) / (2.0 * R) * 3.0);
                var groove = dy + (int)Math.Round(Math.Sin((i * 0.45) + dy) * 0.8);
                if (Mod(groove, 3) == 0 && dy > -R + 1) v -= 1;
                var c = Dith(BarkPal, v, px, py);
                // moss on its back
                if (dy <= -R + 2 && (i is >= 5 and <= 9 || i is >= 16 and <= 19) && Hash(px, py) % 3 != 0) c = Dith(LeafPal, 3.4 + (dy * 0.3), px, py);
                Set(p, px, py, c, water);
            }
        }

        // a knot
        foreach (var (kx, ky, kc) in Knot) Set(p, x0 + 13 + kx, cy + ky, BarkPal[kc], water);

        // the stub of a broken branch on its top, its broken end lit
        for (var k = 1; k <= 3; k++)
        {
            Set(p, x0 + 10, cy - R - k, BarkPal[0], water);
            Set(p, x0 + 11, cy - R - k, k == 3 ? CutPal[3] : BarkPal[3], water);
            Set(p, x0 + 12, cy - R - k, k == 3 ? CutPal[2] : BarkPal[1], water);
            Set(p, x0 + 13, cy - R - k, BarkPal[0], water);
        }

        // its cut end: an oval of rings
        for (var dy = -R; dy <= R; dy++)
        {
            for (var dx = -3; dx <= 3; dx++)
            {
                var d = Math.Sqrt((dx * dx / 7.0) + (dy * dy / 17.0));
                if (d > 1.05) continue;
                int px = x0 + dx, py = cy + dy;
                uint c = d > 0.85 ? BarkPal[0] : d > 0.7 ? BarkPal[2] : d < 0.2 ? CutPal[1] : (int)(d * 5) % 2 == 0 ? CutPal[2] : Dith(CutPal, 3.5 - (dy * 0.15), px, py);
                Set(p, px, py, c, water);
            }
        }

    }

    private static readonly (int X, int Y, int C)[] Knot = [(0, -1, 0), (1, -1, 0), (-1, 0, 0), (0, 0, 3), (1, 0, 1), (2, 0, 0), (0, 1, 0), (1, 1, 0)];

    /// <summary>
    /// A berry field as Alola's routes have them: a frame of planks (lit on top, joints, nails, darker round its sides) with a post at each
    /// corner; soft soil in mounded rows (each lit along its top, darkening into its furrow in the dots), clods on it, damp dark patches where
    /// the plants are watered; a berry plant in each spot (leaves lit on top, swaying in the gusts, its berries hanging lit and shaded), a
    /// seedling here and there; its shadow on the grass.
    /// </summary>
    private static void Field(CapSheet p, int x0, int y0, int w, int h, int water, double s)
    {
        for (var x = 1; x <= w + 1; x++) { Shade(p, x0 + x, y0 + h, 0.9, water); Shade(p, x0 + x + 1, y0 + h + 1, 0.45, water); }
        for (var y = 3; y <= h; y++) Shade(p, x0 + w, y0 + y, 0.8, water);

        // the soil
        for (var y = 3; y < h - 3; y++)
        {
            for (var x = 3; x < w - 3; x++)
            {
                var ly = Mod(y - 3, 10);
                var c = Dith(SoilPal, 3.2 - (ly * 0.36), x0 + x, y0 + y);
                if (ly is >= 1 and <= 4 && Hash(x0 + x, y0 + y) % 17 == 0) c = SoilPal[3];          // a clod
                Set(p, x0 + x, y0 + y, c, water);
            }
        }

        // the frame: planks along the sides, lit on top, joints and nails
        for (var x = 0; x < w; x++)
        {
            for (var k = 0; k < 3; k++)
            {
                uint top = k == 0 ? WoodPal[4] : k == 1 ? Dith(WoodPal, 3.2 - (x / (double)w * 0.8), x0 + x, y0 + k) : WoodPal[1];
                uint bottom = k == 0 ? WoodPal[3] : k == 1 ? Dith(WoodPal, 2.2 - (x / (double)w * 0.8), x0 + x, y0 + h - 3 + k) : WoodPal[0];
                if (Mod(x, 14) == 13) { top = WoodPal[0]; bottom = WoodPal[0]; }
                Set(p, x0 + x, y0 + k, top, water);
                Set(p, x0 + x, y0 + h - 3 + k, bottom, water);
            }

            if (Mod(x, 14) is 2 or 11) { Set(p, x0 + x, y0 + 1, RockPal[3], water); Set(p, x0 + x, y0 + h - 2, RockPal[2], water); }
        }

        for (var y = 3; y < h - 3; y++)
        {
            for (var k = 0; k < 3; k++)
            {
                Set(p, x0 + k, y0 + y, k == 0 ? WoodPal[1] : k == 1 ? Dith(WoodPal, 3.4, x0 + k, y0 + y) : WoodPal[1], water);
                Set(p, x0 + w - 3 + k, y0 + y, k == 2 ? WoodPal[0] : k == 1 ? Dith(WoodPal, 1.8, x0 + w - 3 + k, y0 + y) : WoodPal[1], water);
            }
        }

        // a post at each corner, its cap lit
        foreach (var (cxp, cyp) in (ReadOnlySpan<(int, int)>)[(0, 0), (w - 4, 0), (0, h - 5), (w - 4, h - 5)])
        {
            for (var dy = -2; dy < 4; dy++)
            {
                for (var dx = 0; dx < 4; dx++)
                {
                    uint c = dx == 3 || dy == 3 ? WoodPal[0] : dy == -2 ? WoodPal[4] : dx == 0 ? WoodPal[4] : dx == 1 ? WoodPal[3] : WoodPal[2];
                    Set(p, x0 + cxp + dx, y0 + cyp + dy, c, water);
                }
            }
        }

        // the plants, a seedling here and there; the soil damp round each
        var rowIndex = 0;
        for (var py = y0 + 10; py < y0 + h - 3; py += 10, rowIndex++)
        {
            var berry = Berries[Mod(rowIndex + (x0 / 7), Berries.Length)];
            for (var px = x0 + 9; px < x0 + w - 6; px += 13)
            {
                for (var dx = -5; dx <= 5; dx++) for (var dy = -1; dy <= 0; dy++) if ((dx * dx / 25.0) + (dy * dy) <= 1) Set(p, px + dx, py + dy, Dith(SoilPal, 0.6 + (Math.Abs(dx) * 0.12), px + dx, py + dy), water);
                var sway = Wind(px, s) > 0.5 ? 1 : 0;
                if (Hash(px, py) % 5 == 0) Sprite(p, Seedling, LeafPal, px - 2 + sway, py - Seedling.Length, water);
                else Plant(p, px, py, sway, berry, water);
            }
        }
    }

    /// <summary>A berry plant: a fan of leaves lit on their tops in the dots, a stem, two bunches of berries hanging, each berry lit and shaded.</summary>
    private static void Plant(CapSheet p, int x, int foot, int sway, (uint Body, uint Lit, uint Dark) berry, int water)
    {
        for (var y = 0; y < PlantArt.Length; y++)
        {
            var lean = y < 3 ? sway : 0;
            for (var c = 0; c < PlantArt[y].Length; c++)
            {
                var ch = PlantArt[y][c];
                if (ch == '.') continue;
                int px = x - 4 + c + lean, py = foot - PlantArt.Length + y;
                var colour = ch switch { 'b' => berry.Body, 'l' => berry.Lit, 'd' => berry.Dark, '4' => Dith(LeafPal, 4.4, px, py), '3' => Dith(LeafPal, 3.3, px, py), _ => LeafPal[ch - '0'] };
                Set(p, px, py, colour, water);
            }
        }
    }

    private static readonly string[] PlantArt =
    [
        ".5..5..5.",
        "54.454.45",
        "443.4.344",
        ".4331334.",
        "lb31213lb",
        "bd..2..bd",
        ".lb121lb.",
        ".bd.2.bd.",
    ];

    private static readonly string[] Seedling =
    [
        ".4.4.",
        "43.34",
        "..2..",
        ".121.",
    ];

    /// <summary>
    /// A house of the route, seen from the front and above as the games draw them: a roof of red scalloped tiles in rows (each tile lit along
    /// its top, rounded at its foot, darker to the right in the dots), a ridge of round caps, barge boards at its ends and a fascia board along
    /// its eaves throwing a shadow on the wall; a brick chimney with a cap and smoke rising; a wall of siding boards (each lit along its top, a
    /// shadow line under it) between corner boards; a panelled door with a little window, a gold knob and a stone step; windows with green
    /// shutters, curtains, glass catching the light and a sill, a box of flowers under one; a lantern by the door that glows; a foundation of
    /// stones each shaded; a pot with a plant beside it; a mailbox; its shadow on the grass.
    /// </summary>
    private static void House(CapSheet p, int x0, int y0, int water, double s)
    {
        const int W = 44, RoofH = 22, WallH = 19;
        var wy = y0 + RoofH;
        // its shadow on the grass, to the right and below
        for (var y = 6; y < RoofH + WallH + 3; y++) for (var x = W; x < W + 6; x++) Shade(p, x0 + x, y0 + y, 0.95 - ((x - W) * 0.15), water);
        for (var x = -2; x < W + 6; x++) { Shade(p, x0 + x, wy + WallH + 2, 0.75, water); Shade(p, x0 + x + 1, wy + WallH + 3, 0.35, water); }

        // the chimney, its bricks and cap, and its smoke
        for (var y = -7; y < 4; y++)
        {
            for (var x = 30; x < 37; x++)
            {
                uint c;
                if (y <= -6) c = y == -7 ? RockPal[4] : RockPal[1];                                   // its cap
                else if (x == 30 || x == 36) c = Ink;
                else if (Mod(y, 3) == 2 || Mod(x + (Mod(Floor(y, 3), 2) * 2), 4) == 0) c = RockPal[3];  // the mortar
                else c = Dith(RoofPal, 2.6 - ((x - 30) * 0.25), x0 + x, y0 + y);
                Set(p, x0 + x, y0 + y, c, water);
            }
        }

        Set(p, x0 + 29, y0 - 7, Ink, water); Set(p, x0 + 37, y0 - 7, Ink, water);
        for (var n = 0; n < 3; n++)
        {
            var u = ((s * 0.45) + (n / 3.0)) % 1;
            var r = 1.5 + (u * 3);
            double cx = x0 + 33 + (u * 8) + (Math.Sin((u * 6) + n) * 1.5), cy = y0 - 10 - (u * 26);
            for (var dy = -4; dy <= 4; dy++)
            {
                for (var dx = -5; dx <= 5; dx++)
                {
                    var d = Math.Sqrt((dx * dx) + (dy * dy));
                    if (d > r) continue;
                    int px = (int)Math.Round(cx) + dx, py = (int)Math.Round(cy) + dy;
                    // it thins as it rises: fewer of its cells, in the dots
                    if ((CapSeaPalette.Bayer[((py & 3) * 4) + (px & 3)] + 0.5) / 16.0 > 1.15 - u) continue;
                    Set(p, px, py, dx + dy < -1 ? 0xFFFFFFu : 0xDCE6EEu, water);
                }
            }
        }

        // the roof
        for (var y = 0; y < RoofH; y++)
        {
            for (var x = -3; x < W + 3; x++)
            {
                uint c;
                if (y == 0 || x == -3 || x == W + 2) c = Ink;
                else if (x is -2 or -1 || x is W or W + 1) c = y % 4 == 0 ? WoodPal[1] : x is -2 or W ? WoodPal[4] : WoodPal[2];    // the barge boards
                else if (y <= 3)
                {
                    // the ridge: a row of round caps
                    var lx = Mod(x, 5);
                    c = y == 3 ? RoofPal[0] : lx == 4 ? RoofPal[1] : y == 1 && lx is 1 or 2 ? RoofPal[4] : Dith(RoofPal, 3.5 - (x / (double)W), x0 + x, y0 + y);
                }
                else if (y >= RoofH - 3)
                {
                    c = y == RoofH - 1 ? Ink : y == RoofH - 3 ? WoodPal[4] : WoodPal[2];                   // the fascia board
                }
                else
                {
                    // the tiles: rows of scallops, each set half a tile over the one above
                    var row = (y - 4) / 4;
                    var ly = (y - 4) % 4;
                    var sx = Mod(x + (row % 2 * 3), 6);
                    if (ly == 3) c = sx is 0 or 5 ? RoofPal[0] : RoofPal[1];
                    else if (sx == 0) c = RoofPal[1];
                    else if (ly == 0 && sx is 1 or 2) c = RoofPal[4];
                    else c = Dith(RoofPal, 3.3 - (x / (double)W * 1.4) - (ly * 0.35), x0 + x, y0 + y);
                }

                Set(p, x0 + x, y0 + y, c, water);
            }
        }

        // the wall: siding boards between corner boards, the eaves' shadow along its top
        for (var y = 0; y < WallH; y++)
        {
            for (var x = 0; x < W; x++)
            {
                uint c;
                if (x == 0 || x == W - 1) c = Ink;
                else if (x is 1 or 2) c = x == 1 ? WallPal[3] : WallPal[2];
                else if (x is W - 3 or W - 2) c = x == W - 3 ? WallPal[1] : WallPal[0];
                else
                {
                    var ly = y % 3;
                    c = ly == 2 ? WallPal[1] : ly == 0 ? WallPal[3] : Dith(WallPal, 2.9 - (x / (double)W * 1.4), x0 + x, wy + y);
                }

                if (y < 3 && x > 0 && x < W - 1) c = Dith(WallPal, y == 0 ? 0.3 : y == 1 ? 0.8 : 1.4, x0 + x, wy + y);
                Set(p, x0 + x, wy + y, c, water);
            }
        }

        // the foundation: stones of a few widths, each lit on top in the dots, mortar between
        for (var x = -1; x <= W; x++)
        {
            var joint = Mod(x * 7, 11) < 2 && Mod(x, 5) == 0;
            Set(p, x0 + x, wy + WallH, joint ? RockPal[1] : Dith(RockPal, 3.4, x0 + x, wy + WallH), water);
            Set(p, x0 + x, wy + WallH + 1, joint ? RockPal[0] : Dith(RockPal, 2.2, x0 + x, wy + WallH + 1), water);
        }

        // the door: its frame, panels, a little window, a gold knob, a stone step
        const int Dx = (W / 2) - 5;
        for (var y = 2; y < WallH; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                uint c;
                if (x == 0 || x == 9 || y == 2) c = Ink;
                else if (x is 1 or 8 || y == 3) c = x == 1 || y == 3 ? WoodPal[3] : WoodPal[1];
                else if (y is >= 5 and <= 7 && x is >= 3 and <= 6) c = y == 5 || x == 3 ? WoodPal[0] : (x - y) == -3 ? GlassLit : Glass;
                else if (y is >= 10 and <= 15 && x is >= 3 and <= 6) c = y == 10 || x == 3 ? WoodPal[1] : y == 15 || x == 6 ? WoodPal[4] : Dith(WoodPal, 2.3, x0 + Dx + x, wy + y);
                else c = Dith(WoodPal, 3.4 - (x * 0.15), x0 + Dx + x, wy + y);
                Set(p, x0 + Dx + x, wy + y, c, water);
            }
        }

        Set(p, x0 + Dx + 7, wy + 10, 0xFFF0A0u, water);
        Set(p, x0 + Dx + 7, wy + 11, 0xD8B030u, water);
        for (var x = -2; x < 12; x++)
        {
            Set(p, x0 + Dx + x, wy + WallH, x is -2 or 11 ? RockPal[1] : RockPal[4], water);
            Set(p, x0 + Dx + x, wy + WallH + 1, Dith(RockPal, 2.4, x0 + Dx + x, wy + WallH + 1), water);
            Set(p, x0 + Dx + x, wy + WallH + 2, RockPal[0], water);
        }

        // the windows, each with its shutters, curtains, glass and sill
        foreach (var wx in (ReadOnlySpan<int>)[4, W - 15])
        {
            for (var y = 3; y < 13; y++)
            {
                // the shutters, slatted
                foreach (var sx in (ReadOnlySpan<int>)[0, 9])
                {
                    for (var k = 0; k < 2; k++) Set(p, x0 + wx + sx + k, wy + y, y == 3 || y == 12 ? ShutterPal[0] : y % 2 == 0 ? ShutterPal[1] : ShutterPal[k == 0 ? 3 : 2], water);
                }

                for (var x = 2; x < 9; x++)
                {
                    uint c;
                    if (x == 2 || x == 8 || y == 3 || y == 12) c = WoodPal[1];
                    else if (x == 5 || y == 8) c = WoodPal[4];
                    else if ((x == 3 && y < 11) || (x == 4 && y < 6) || (x == 7 && y < 11) || (x == 6 && y < 6)) c = y < 5 || x is 3 or 7 ? 0xF0A8C0u : 0xD07898u;   // the curtains
                    else c = (x + y) % 5 == 0 ? GlassLit : Dith(GlassRamp, 1.3 - ((y - 4) * 0.08), x0 + wx + x, wy + y);
                    Set(p, x0 + wx + x, wy + y, c, water);
                }
            }

            for (var x = 1; x < 10; x++) { Set(p, x0 + wx + x, wy + 13, WoodPal[4], water); Set(p, x0 + wx + x, wy + 14, WoodPal[1], water); }
        }

        // a box of flowers under the left window
        for (var x = 5; x < 13; x++) { Set(p, x0 + x, wy + 15, WoodPal[3], water); Set(p, x0 + x, wy + 16, WoodPal[1], water); }
        for (var n = 0; n < 4; n++)
        {
            var fx = x0 + 6 + (n * 2);
            Set(p, fx, wy + 13, n % 2 == 0 ? 0xF84860u : 0xFFD040u, water);
            Set(p, fx, wy + 14, GrassPal[3], water);
            Set(p, fx + 1, wy + 14, GrassPal[2], water);
        }

        // the lantern by the door, glowing and flickering a little
        var lamp = x0 + Dx + 11;
        var glow = Math.Sin(s * 7) > 0.6 ? 0xFFF4C0u : 0xFFE070u;
        Set(p, lamp, wy + 4, Ink, water); Set(p, lamp + 1, wy + 4, Ink, water);
        Set(p, lamp, wy + 5, 0x302018u, water); Set(p, lamp + 1, wy + 5, 0x302018u, water);
        Set(p, lamp, wy + 6, glow, water); Set(p, lamp + 1, wy + 6, 0xE8B040u, water);
        Set(p, lamp, wy + 7, 0xE8B040u, water); Set(p, lamp + 1, wy + 7, 0xB07820u, water);
        Set(p, lamp, wy + 8, Ink, water); Set(p, lamp + 1, wy + 8, Ink, water);

        // a pot with a plant beside the house
        foreach (var (px, py, ch) in PotAt)
        {
            uint c = ch switch { 'p' => 0xC86A3Cu, 'P' => 0xE8905Au, 'q' => 0x8A4224u, 'g' => GrassPal[3], 'G' => GrassPal[4], _ => LeafPal[1] };
            Set(p, x0 - 9 + px, wy + 13 + py, c, water);
        }

        // the mailbox beside the house: a post lit on its left, a box rounded on top, its flag up
        var mx = x0 + W + 4;
        for (var y = 8; y < WallH + 2; y++) { Set(p, mx + 2, wy + y, WoodPal[3], water); Set(p, mx + 3, wy + y, WoodPal[1], water); }
        for (var y = 2; y < 8; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                if (y == 2 && x is 0 or 5) continue;
                Set(p, mx + x, wy + y, x == 0 || x == 5 || y == 2 || y == 7 ? 0x5A0E0Eu : y == 3 || x == 1 ? 0xFF7060u : Dith(MailRamp, 1.6 - (x * 0.2), mx + x, wy + y), water);
            }
        }

        Set(p, mx + 6, wy + 3, 0xFFD040u, water); Set(p, mx + 6, wy + 4, 0xFFD040u, water); Set(p, mx + 7, wy + 3, 0xFFD040u, water);
    }

    private static readonly (int X, int Y, char C)[] PotAt =
    [
        (2, -3, 'G'), (4, -3, 'G'), (1, -2, 'G'), (2, -2, 'g'), (3, -2, 'G'), (4, -2, 'g'), (5, -2, 'G'),
        (0, -1, 'G'), (1, -1, 'g'), (2, -1, 'l'), (3, -1, 'g'), (4, -1, 'l'), (5, -1, 'g'), (6, -1, 'l'),
        (1, 0, 'l'), (2, 0, 'g'), (3, 0, 'l'), (4, 0, 'g'), (5, 0, 'l'),
        (0, 1, 'P'), (1, 1, 'P'), (2, 1, 'P'), (3, 1, 'P'), (4, 1, 'p'), (5, 1, 'p'), (6, 1, 'q'),
        (0, 2, 'q'), (1, 2, 'q'), (2, 2, 'q'), (3, 2, 'q'), (4, 2, 'q'), (5, 2, 'q'), (6, 2, 'q'),
        (1, 3, 'P'), (2, 3, 'p'), (3, 3, 'p'), (4, 3, 'p'), (5, 3, 'q'),
        (1, 4, 'P'), (2, 4, 'p'), (3, 4, 'p'), (4, 4, 'q'), (5, 4, 'q'),
        (2, 5, 'p'), (3, 5, 'q'), (4, 5, 'q'),
    ];

    // ---------------------------------------------------------------- the Pidgey towing the banner

    /// <summary>
    /// What the banner says, over a route's first encounter and over an allowed static alike (asked 2026-10-20, first «POKÉMON DE RUTA»). The
    /// one thing the abyss's window writes, by the player's choice the same day: the rule that the deep writes no letters (2026-10-10) keeps for
    /// everything else, so the banner does not count for <see cref="CapTextProbe"/> there.
    /// </summary>
    private const string BannerText = "POKÉMON CAPTURABLE";

    /// <summary>What the banner says over a duplicate (asked 2026-10-20): the player may take it (it is then the route's encounter) or leave it.</summary>
    private const string DuplicateText = "DUPLICADO, TÚ ELIGES";

    /// <summary>When the Pidgey comes in over the right of the party's sky, and its speed in cells a second, across both windows and off the left.</summary>
    private const double FlyFrom = 1.8, FlySpeed = 56;

    private static readonly uint[] ClothPal = [0x9C8C72u, 0xC4B698u, 0xE2D8BEu, 0xF6F0E0u, 0xFFFDF6u];
    private static readonly uint[] TrimPal = [0x6A1414u, 0xA82820u, 0xD83A30u, 0xF06A58u];

    /// <summary>The duplicate's trim: amber, for a choice rather than a yes.</summary>
    private static readonly uint[] AmberPal = [0x6A3A08u, 0xA86010u, 0xE09A20u, 0xF8C860u];
    private const uint Rope = 0x5A3E22u, RopeLit = 0x9A7448u, BannerInk = 0x2E1C12u;

    /// <summary>The banners' letters as cells (from the top left of the line, the accents above it), worked out once from the pixel font.</summary>
    private static (int X, int Y)[]? _letters, _duplicateLetters;

    private static (int X, int Y)[] BannerLetters(bool duplicate)
    {
        if ((duplicate ? _duplicateLetters : _letters) is { } done) return done;
        var cells = new List<(int X, int Y)>();
        PixelFont.Draw(duplicate ? DuplicateText : BannerText, 0, 0, (x, y) => cells.Add((x, y - PixelFont.Above)));
        return duplicate ? _duplicateLetters = [.. cells] : _letters = [.. cells];
    }

    /// <summary>Where the Pidgey flies at a moment (its middle): across the sky from right to left, rising and falling a little as it beats its wings.</summary>
    private static (double X, double Y) FlightAt(CapWorldLayout l, double s, int world)
    {
        var x = world + 24 - ((s - FlyFrom) * FlySpeed);
        // each downstroke lifts it, between them it sinks a little; and the path itself drifts gently up and down
        var y = (l.SurfaceY * 0.4) - (Math.Sin(s * 2 * Math.PI * 1.5) * 1.6) + (Math.Sin(s * 0.9) * 3);
        return (x, y);
    }

    /// <summary>
    /// A Pidgey towing a banner across the party's sky from right to left, as a banner is towed (2026-10-20, «more realistic and physically
    /// reasonable»): it holds the tow line in its talons, nose a little up with the pull; the line runs back, nearly taut with the drag, to a
    /// bridle whose two lines hold the top and the foot of a pole along the banner's leading edge, weighted at its foot so it hangs upright and
    /// swings a beat behind the bird. The cloth streams behind the pole: still at the pole, a wave running back along it that grows towards its
    /// free end (each column lifted and dropped), lit where it faces up to the sun and shaded where it turns away, in the sky's dots; its red
    /// trims, its letters and its swallowtail end all ride the wave, the tails snapping hardest. The whole banner follows the bird's rise and fall
    /// late, as a towed load does.
    /// </summary>
    private static void Flyover(CapSheet p, CapWorldLayout l, CapSeaContext ctx, double s, bool duplicate)
    {
        if (s < FlyFrom || !CritterArt.All.TryGetValue(16, out var pidgey)) return;
        var textW = CapGlyphs.Width(duplicate ? DuplicateText : BannerText, true);
        var trim = duplicate ? AmberPal : TrimPal;
        const int Bh = 16, Line = 16, Bridle = 8, Tail = 7;
        var bw = textW + 16;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;

        // in world columns, so it crosses the party's window and then the abyss's; drawn only where this window shows it
        var (fx, fy) = FlightAt(l, s, world);
        int px = (int)Math.Round(fx) - ctx.WorldX, py = (int)Math.Round(fy);
        if (px > l.Cols + 30 || px + Line + Bridle + bw + Tail + 30 < 0) return;
        var letters = BannerLetters(duplicate);
        if (l.Panel == CapPanelKind.Party) CapTextProbe.Hit();
        // the talons, under its body towards its tail
        int hx = px + 6, hy = py + 7;

        // the banner's leading edge follows where the bird was a moment ago, hanging a little below its line
        var (_, lagY) = FlightAt(l, s - 0.3, world);
        var poleX = hx + Line + Bridle;
        var poleTop = (int)Math.Round(lagY + 3);
        // the pole swings a beat behind: its foot trails back and forth a cell
        var swing = Math.Sin((s * 2 * Math.PI * 1.5) - 1.2);
        var footShift = (int)Math.Round(swing * 0.8);

        // the tow line, nearly taut: a slight sag; then the bridle to the pole's top and foot
        var knotX = hx + Line;
        var knotY = poleTop + (Bh / 2) - 1;
        for (var x = hx; x <= knotX; x++)
        {
            var k = (x - hx) / (double)(knotX - hx);
            var y = (int)Math.Round(hy + ((knotY - hy) * k) + (Math.Sin(k * Math.PI) * 1.2));
            p.Set(x, y, Rope);
        }

        foreach (var (endX, endY) in (ReadOnlySpan<(int, int)>)[(poleX, poleTop - 1), (poleX + footShift, poleTop + Bh)])
        {
            for (var x = knotX; x <= endX; x++)
            {
                var k = (x - knotX) / (double)Math.Max(1, endX - knotX);
                p.Set(x, (int)Math.Round(knotY + ((endY - knotY) * k)), Rope);
            }
        }

        p.Set(knotX, knotY, RopeLit);

        // the cloth: column by column from the pole back, each lifted by the wave running along it
        var length = bw + Tail;
        for (var c = 0; c < length; c++)
        {
            var reach = c / (double)length;
            var amp = 0.3 + (reach * reach * reach * 3.0);
            var phase = (c * 0.22) - (s * 2 * Math.PI * 1.6);
            var lift = Math.Sin(phase) * amp;
            var slope = Math.Cos(phase) * amp * 0.22;
            // a little droop towards the free end, where nothing holds it up but the wind
            var dy = (int)Math.Round(lift + (reach * 1.5));
            var x = poleX + 1 + c + (int)Math.Round(footShift * (1 - reach) * 0.5);
            // the swallowtail: a V cut into its free end, deeper towards the end
            var cut = c - bw;
            for (var r = 0; r < Bh; r++)
            {
                if (cut >= 0 && Math.Abs(r - ((Bh - 1) / 2.0)) < cut * 0.95) continue;
                var y = poleTop + r + dy;
                uint colour;
                if (r <= 2)
                {
                    colour = r == 0 ? trim[0] : Dith(trim, 2.4 - (slope * 2) - (r * 0.3), x, y);
                }
                else if (r >= Bh - 3)
                {
                    colour = r == Bh - 1 ? trim[0] : Dith(trim, 1.7 - (slope * 2) - ((r - Bh + 3) * 0.3), x, y);
                }
                else
                {
                    // lit where the cloth faces up to the sun, shaded where it turns down; a touch darker towards its foot
                    colour = Dith(ClothPal, 2.6 - (slope * 3.2) - ((r - 3) * 0.06), x, y);
                }

                p.Set(x, y, colour);
            }

            // the letters on this column, riding the wave with the cloth
            var lx = c - 8;
            if (lx < -1 || lx > textW) continue;
            foreach (var (gx, gy) in letters)
            {
                if (gx == lx) p.Set(x, poleTop + 5 + gy + dy, BannerInk);
            }
        }

        // the pole: lit on its left, its cap, and the weight at its foot
        for (var r = -2; r <= Bh + 1; r++)
        {
            var shift = (int)Math.Round(footShift * Math.Clamp((r + 2) / (double)(Bh + 3), 0, 1));
            p.Set(poleX - 1 + shift, poleTop + r, Rope);
            p.Set(poleX + shift, poleTop + r, RopeLit);
        }

        p.Set(poleX - 1, poleTop - 3, RopeLit); p.Set(poleX, poleTop - 3, RopeLit);
        for (var k = 0; k < 3; k++)
        {
            p.Set(poleX - 2 + k + footShift, poleTop + Bh + 2, RockPal[k == 0 ? 3 : 1]);
            p.Set(poleX - 2 + k + footShift, poleTop + Bh + 3, RockPal[0]);
        }

        // the bird over its line: nose up a little with the pull
        CapVisitors.Centred(p, pidgey, s, px, py, 0.55, false, 0xFFFFFFu, 0, 4);
        // its talons gripping the line
        p.Set(hx, hy, 0xE89838u); p.Set(hx + 1, hy, 0xC07020u); p.Set(hx, hy + 1, Rope);
    }

    // ---------------------------------------------------------------- the water's edge, the Pokémon, the sign

    /// <summary>The line where the water stands: a curl of foam along it, white flecks running, the grass darkened by the wet just above.</summary>
    private static void Shore(CapSheet p, CapWorldLayout l, int water, double s)
    {
        for (var x = 0; x < l.Cols; x++)
        {
            var curl = (int)Math.Round(Math.Sin((x * 0.3) + (s * 5)) * 1.2);
            var y = water + curl;
            if (y - 3 >= l.Rows) continue;
            p.Set(x, y - 3, Wet);
            p.Set(x, y - 2, Mod(x + (int)(s * 8), 9) == 0 ? Foam : Wet);
            p.Set(x, y - 1, Mod(x + (int)(s * 10), 6) < 3 ? Foam : FoamLo);
            p.Set(x, y, Foam);
        }
    }

    /// <summary>
    /// The wild Pokémon in the tall grass in the middle of the abyss's window: a patch round it, the Pokémon standing in it with a shadow under it,
    /// the front blades over its feet; it hops now and then and the grass rustles as it lands. When the battle has its outcome (2026-10-20,
    /// detailed the same day): caught, a spinning Poké Ball comes flying in with a streak behind it, opens, and a red beam takes the Pokémon
    /// in, red all over and shrinking up into it; the ball drops into the grass kicking up dust, wobbles three times tilting each way, and clicks
    /// shut with a flash and stars; fainted, it flashes white twice and sinks swaying into the grass, dust and torn blades flying; fled, it
    /// springs up and vanishes in a burst of smoke, leaves drifting down after.
    /// </summary>
    private static void Wild(CapSheet p, CapWorldLayout l, CapSeaContext ctx, int top, CapEncounterTurn turn, int water, double s)
    {
        // on the tile grid: a patch five tiles wide, three high, under the path, its middle where the Pokémon stands
        var (tx0, ty0) = WildTile(l, top, ctx);
        var x0 = (tx0 * Tile) - ctx.WorldX;
        var py = top + (ty0 * Tile) + 20;
        var cx = x0 + 40;
        Tall(p, x0, top + (ty0 * Tile), 5, 3, 99, water, s);
        var outcome = turn.Outcome;
        var u = outcome == CapEncounterOutcome.None ? -1 : s + turn.Start - turn.OutcomeAt;
        var still = outcome == CapEncounterOutcome.None;

        if (ToastPixels.SpritePixels.From(turn.Sprite) is { } sprite)
        {
            var beat = s % 1.6;
            var hop = still && beat < 0.35 ? Math.Sin(beat / 0.35 * Math.PI) * 6 : 0;
            var foot = py + 8 - (int)Math.Round(hop);
            double scale = 1, mix = 0;
            uint tint = 0xFFFFFFu;
            int sink = 0, shift = 0, rise = 0;
            var shown = true;
            switch (outcome)
            {
                case CapEncounterOutcome.Caught:
                    // red all over in the beam, then shrinking up into the ball
                    shown = u < 0.86;
                    tint = 0xF03838u;
                    mix = Math.Clamp((u - 0.44) / 0.1, 0, 1);
                    var take = Math.Clamp((u - 0.56) / 0.3, 0, 1);
                    scale = 1 - (take * take);
                    rise = (int)Math.Round(take * (foot - (py - 12)));
                    break;
                case CapEncounterOutcome.Fainted:
                    // two white flashes, then it sinks swaying a cell each way, darkening
                    var down = Math.Clamp((u - 0.4) / 0.85, 0, 1);
                    shown = u < 1.3;
                    mix = u < 0.4 ? ((int)(u * 10) % 2 == 0 ? 0.85 : 0) : down * 0.35;
                    tint = u < 0.4 ? 0xFFFFFFu : 0x203018u;
                    sink = (int)Math.Round(down * down * (sprite.Height + 2));
                    shift = u >= 0.4 ? (int)Math.Round(Math.Sin(u * 18) * (1 - down)) : 0;
                    break;
                case CapEncounterOutcome.Fled:
                    // it springs up, whitening, and is gone in the smoke
                    shown = u < 0.3;
                    rise = (int)Math.Round(Math.Sin(Math.Clamp(u / 0.3, 0, 1) * Math.PI) * 5);
                    mix = Math.Clamp(u / 0.25, 0, 1) * 0.7;
                    break;
            }

            if (shown && scale > 0.05)
            {
                // its shadow, smaller while it is up
                var sw = (int)(((sprite.Width / 3) - (hop / 2) - (rise / 3)) * scale);
                for (var dx = -sw; dx <= sw; dx++) Set(p, cx + dx, py + 9, TallPal[0], water);
                DrawWild(p, sprite, cx + shift, foot - rise, scale, tint, mix, sink, py + 8, water);
            }
        }

        // the front blades over its feet, rustling as it lands, or as it goes
        var rustle = (still && s % 1.6 is > 0.35 and < 0.6) || (!still && u is > 0 and < 1.2 && (int)(u * 12) % 2 == 0) ? 1 : 0;
        for (var r = 0; r < TallStrip.Length; r++)
        {
            for (var dx = -20; dx < 20; dx++)
            {
                var ch = TallStrip[r][Mod(dx + 40 + (rustle * (r % 2)), 16)];
                if (ch == '.') continue;
                Set(p, cx + dx, py + 4 + r, TallPal[ch - '0'], water - 1);
            }
        }

        switch (outcome)
        {
            case CapEncounterOutcome.Caught: CatchScene(p, cx, py, u, water); break;
            case CapEncounterOutcome.Fainted:
                Dust(p, cx, py + 6, u - 0.45, water);
                Blades(p, cx, py + 4, u - 0.45, water);
                break;
            case CapEncounterOutcome.Fled:
                Smoke(p, cx, py - 8, u, water);
                Leaves(p, cx, py - 20, u - 0.45, water);
                break;
        }
    }

    /// <summary>The wild sprite at a scale (nearest cell), its foot at a point, tinted, sunk a number of cells and cut off at the grass line.</summary>
    private static void DrawWild(CapSheet p, ToastPixels.SpritePixels sprite, int cx, int foot, double scale, uint tint, double mix, int sink, int clip, int water)
    {
        int w = Math.Max(1, (int)Math.Round(sprite.Width * scale)), h = Math.Max(1, (int)Math.Round(sprite.Height * scale));
        int left = cx - (w / 2), topRow = foot - h + sink;
        for (var sy = 0; sy < h; sy++)
        {
            var y = topRow + sy;
            if (y > clip) break;
            var srcY = Math.Min(sprite.Height - 1, (int)(sy / scale));
            for (var sx = 0; sx < w; sx++)
            {
                var i = ((srcY * sprite.Width) + Math.Min(sprite.Width - 1, (int)(sx / scale))) * 4;
                if (sprite.Bgra[i + 3] < 128) continue;
                var c = CapSheet.Rgb(sprite.Bgra[i + 2], sprite.Bgra[i + 1], sprite.Bgra[i]);
                Set(p, left + sx, y, mix > 0 ? CapSheet.Mix(c, tint, mix) : c, water - 1);
            }
        }
    }

    /// <summary>How long the outcome plays before the sea comes back up: the ball's three wobbles and its click take longest.</summary>
    public static double Hold(CapEncounterOutcome outcome) => outcome switch
    {
        CapEncounterOutcome.Caught => 3.0,
        CapEncounterOutcome.Fainted => 1.8,
        CapEncounterOutcome.Fled => 1.8,
        _ => 0,
    };

    private static readonly uint[] SmokePal = [0x6A7280u, 0x9AA2AEu, 0xC8CED8u, 0xEEF2F6u, 0xFFFFFFu];
    private static readonly uint[] DustPal = [0x8A7458u, 0xA8916Eu, 0xC6B08Au, 0xE0CEA8u];

    /// <summary>
    /// The capture: a Poké Ball flies in from the upper right on an arc, spinning, a fading streak behind it; over the Pokémon it opens, its
    /// inside glowing, and a red beam spreads down with sparks running up it while the Pokémon is taken in; it shuts and drops into the grass
    /// with a bounce and a puff of dust; it wobbles three times, tilting each way; it clicks, a flash from its button, a ring and three stars.
    /// </summary>
    private static void CatchScene(CapSheet p, int cx, int py, double u, int water)
    {
        if (u < 0) return;
        int hx = cx, hy = py - 18;                 // where it hangs over the Pokémon, open
        int rx = cx, ry = py + 2;                  // where it rests in the grass
        if (u < 0.44)
        {
            // the streak: where it was a moment before, thinning
            for (var n = 6; n >= 0; n--)
            {
                var k = Math.Max(0, (u - (n * 0.025)) / 0.44);
                var (fx, fy) = Flight(hx, hy, k);
                if (n == 0)
                {
                    Ball(p, fx, fy, 0, u * 26, false, water);
                    continue;
                }

                for (var d = -1; d <= 1; d++)
                {
                    if ((CapSeaPalette.Bayer[(((fy + d) & 3) * 4) + (fx & 3)] + 0.5) / 16.0 < 0.75 - (n * 0.11)) Set(p, fx, fy + d, n < 3 ? 0xFFFFFFu : 0xFFD8D0u, water);
                }
            }

            return;
        }

        if (u < 0.9)
        {
            // open over it: the beam spreading down, brightest at its core, sparks running up it
            var open = Math.Min(1, (u - 0.44) / 0.08) * (u > 0.8 ? 1 - ((u - 0.8) / 0.1) : 1);
            var reach = Math.Min(1, (u - 0.44) / 0.12);
            var bottom = hy + 6 + (int)Math.Round(reach * (py + 6 - hy - 6));
            for (var y = hy + 6; y < bottom; y++)
            {
                var spread = 1 + ((y - hy - 6) / 3);
                for (var dx = -spread; dx <= spread; dx++)
                {
                    var edge = Math.Abs(dx) / (double)(spread + 1);
                    var bayer = (CapSeaPalette.Bayer[((y & 3) * 4) + ((hx + dx) & 3)] + 0.5) / 16.0;
                    if (bayer > 0.85 - (edge * 0.6)) continue;
                    Set(p, hx + dx, y, edge < 0.25 ? 0xFFE0D8u : edge < 0.6 ? 0xFF7060u : 0xD02828u, water);
                }
            }

            for (var n = 0; n < 5; n++)
            {
                var k = ((u * 3) + (n * 0.2)) % 1;
                int sx = hx + (int)Math.Round(Math.Sin((n * 2.3) + (u * 9)) * (1 - k) * 8), sy = bottom - (int)Math.Round(k * (bottom - hy - 6));
                Set(p, sx, sy, 0xFFFFFFu, water);
            }

            Ball(p, hx, hy, (int)Math.Round(open * 3), 0, false, water);
            return;
        }

        if (u < 1.22)
        {
            // it drops into the grass and bounces once, dust kicked up where it lands
            var k = (u - 0.9) / 0.32;
            var y = k < 0.65 ? hy + (int)Math.Round((ry - hy) * (k / 0.65) * (k / 0.65)) : ry - (int)Math.Round(Math.Sin((k - 0.65) / 0.35 * Math.PI) * 3);
            if (k > 0.6) Landing(p, rx, ry + 5, (k - 0.6) / 0.4, water);
            BallShadow(p, rx, ry + 6, water);
            Ball(p, rx, y, 0, 0, false, water);
            return;
        }

        // the three wobbles, tilting each way, then the click
        var tilt = 0.0;
        foreach (var at in (ReadOnlySpan<double>)[1.4, 1.85, 2.3])
        {
            var w = u - at;
            if (w is >= 0 and < 0.36) tilt = Math.Sin(w / 0.36 * Math.PI * 2) * 0.45;
        }

        var clicked = u >= 2.75;
        BallShadow(p, rx, ry + 6, water);
        Ball(p, rx + (int)Math.Round(tilt * 3), ry, 0, tilt, clicked && u < 2.9, water);
        if (!clicked) return;

        // the click: a ring from the button, three stars rising and fading
        var t = u - 2.75;
        if (t < 0.2)
        {
            var radius = 4 + (t * 40);
            for (var a = 0; a < 28; a++)
            {
                var ang = a / 28.0 * Math.PI * 2;
                Set(p, rx + (int)Math.Round(Math.Cos(ang) * radius), ry + (int)Math.Round(Math.Sin(ang) * radius * 0.6), 0xFFF4C0u, water);
            }
        }

        for (var n = 0; n < 3; n++)
        {
            var sx = rx + ((n - 1) * 9) + (int)Math.Round((n - 1) * t * 12);
            var sy = ry - 9 - (int)Math.Round(t * 24) - (n == 1 ? 4 : 0);
            var keep = 1 - Math.Clamp((t - 0.3) / 0.3, 0, 1);
            Star(p, sx, sy, keep, water);
        }
    }

    /// <summary>Where the thrown ball is along its flight (0 thrown, 1 over the Pokémon): in from the upper right, on an arc.</summary>
    private static (int X, int Y) Flight(int hx, int hy, double k) =>
        (hx + (int)Math.Round((1 - k) * 90), hy - (int)Math.Round((1 - k) * 75) - (int)Math.Round(Math.Sin(k * Math.PI) * 20));

    /// <summary>
    /// A Poké Ball centred on a point: red over white, lit at its top left and shaded at its lower right in the dots, a shine on its red, a black
    /// band and a button with a ring, an ink rim; turned by an angle (its band tilting, as it spins or wobbles); its top half lifted a few cells
    /// when open, its inside glowing; its button lit when it clicks.
    /// </summary>
    private static void Ball(CapSheet p, int x, int y, int open, double angle, bool flash, int water)
    {
        const double R = 5.6;
        double sin = Math.Sin(angle), cos = Math.Cos(angle);
        if (open > 0)
        {
            // its inside, glowing, between the halves
            for (var dy = -open; dy <= 0; dy++) for (var dx = -4; dx <= 4; dx++) Set(p, x + dx, y + dy, Math.Abs(dx) < 3 ? 0xFFF0E0u : 0xF06050u, water);
        }

        for (var dy = -6; dy <= 6; dy++)
        {
            for (var dx = -6; dx <= 6; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > R + 0.3) continue;
                // across the band, in the ball's own turn
                var across = (dy * cos) - (dx * sin);
                var lift = across < 0 ? open : 0;
                uint c;
                if (d > R - 0.8) c = 0x141414u;
                else if (Math.Abs(across) < 0.9) c = Math.Abs(across) < 0.4 ? 0x141414u : 0x2A2A2Au;
                else if (across < 0) c = Dith(BallRed, 2.7 - ((dx + dy) * 0.2), x + dx, y + dy);
                else c = Dith(BallWhite, 2.7 - ((dx + dy) * 0.2), x + dx, y + dy);
                if (across < -1 && dx is -3 or -2 && dy is -4 or -3 && !(dx == -2 && dy == -3)) c = 0xFFFFFFu;       // the shine
                Set(p, x + dx, y + dy - lift, c, water);
            }
        }

        // the button, turned with the ball
        int bx = x, by = y;
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                var m = Math.Abs(dx) + Math.Abs(dy);
                if (m > 3 || open > 0) continue;
                Set(p, bx + dx, by + dy, m == 3 || (Math.Abs(dx) == 2 || Math.Abs(dy) == 2) ? 0x141414u : flash ? 0xFFF4A0u : m == 0 ? 0xD8DCE4u : 0xFFFFFFu, water);
            }
        }
    }

    private static readonly uint[] BallRed = [0x7A0E0Eu, 0xB82424u, 0xE03C3Cu, 0xFF7A68u];
    private static readonly uint[] BallWhite = [0x8C929Cu, 0xC2C6CEu, 0xEAECF0u, 0xFFFFFFu];

    /// <summary>The ball's shadow on the grass: an oval in the dots.</summary>
    private static void BallShadow(CapSheet p, int x, int y, int water)
    {
        for (var dy = -1; dy <= 1; dy++) for (var dx = -6; dx <= 7; dx++) if ((dx * dx / 49.0) + (dy * dy / 2.0) <= 1) Shade(p, x + dx, y + dy, 0.9 - (Math.Abs(dx) * 0.06), water);
    }

    /// <summary>Dust kicked up where the ball lands: two little puffs either side, low and spreading.</summary>
    private static void Landing(CapSheet p, int x, int y, double k, int water)
    {
        if (k is < 0 or > 1) return;
        for (var side = -1; side <= 1; side += 2)
        {
            Puff(p, x + (side * (5 + (int)Math.Round(k * 6))), y - (int)Math.Round(k * 3), 1.5 + (k * 2), 1 - k, DustPal, water);
        }
    }

    /// <summary>A four-pointed star: a bright heart, its points, as much of it kept as <paramref name="keep"/> says, in the dots.</summary>
    private static void Star(CapSheet p, int x, int y, double keep, int water)
    {
        foreach (var (dx, dy, c) in StarCells)
        {
            int px = x + dx, py = y + dy;
            if ((CapSeaPalette.Bayer[((py & 3) * 4) + (px & 3)] + 0.5) / 16.0 > keep) continue;
            Set(p, px, py, c, water);
        }
    }

    private static readonly (int X, int Y, uint C)[] StarCells =
    [
        (0, 0, 0xFFFFFFu), (-1, 0, 0xFFF0A0u), (1, 0, 0xFFF0A0u), (0, -1, 0xFFF0A0u), (0, 1, 0xFFF0A0u),
        (-2, 0, 0xF8C840u), (2, 0, 0xF8C840u), (0, -2, 0xF8C840u), (0, 2, 0xF8C840u), (0, -3, 0xE0A020u), (0, 3, 0xE0A020u),
    ];

    /// <summary>Dust rising from where the Pokémon sank: earthy puffs drifting up and thinning out.</summary>
    private static void Dust(CapSheet p, int cx, int y, double u, int water)
    {
        if (u is < 0 or > 1.2) return;
        for (var n = 0; n < 6; n++)
        {
            var k = Math.Clamp((u - (n * 0.07)) / 0.85, 0, 1);
            if (k <= 0 || k >= 1) continue;
            int px = cx + ((n - 2) * 7) - 3 + (int)Math.Round((n - 2.5) * k * 6), pyy = y - (int)Math.Round(k * 9);
            Puff(p, px, pyy, 1.5 + (k * 3), 1 - k, DustPal, water);
        }
    }

    /// <summary>Torn blades flying up from where the Pokémon sank, turning over as they fall back.</summary>
    private static void Blades(CapSheet p, int cx, int y, double u, int water)
    {
        if (u is < 0 or > 1) return;
        for (var n = 0; n < 6; n++)
        {
            var dir = (n - 2.5) / 2.5;
            double bx = cx + (dir * u * 30), by = y - (Math.Sin(Math.Min(1, u * 1.4) * Math.PI) * (10 + (n % 3 * 4))) + (u * 6);
            int ix = (int)Math.Round(bx), iy = (int)Math.Round(by);
            var flip = (int)(u * 14 + n) % 2 == 0;
            Set(p, ix, iy, TallPal[3], water);
            Set(p, ix + (flip ? 1 : 0), iy + (flip ? 0 : 1), TallPal[2], water);
        }
    }

    /// <summary>The smoke it flees in: puffs bursting out from it, rising and spreading, thinning away in the dots.</summary>
    private static void Smoke(CapSheet p, int cx, int cy, double u, int water)
    {
        if (u is < 0 or > 1.6) return;
        foreach (var (ox, oy, delay) in SmokePuffs)
        {
            var k = Math.Clamp((u - delay) / 1.3, 0, 1);
            if (k <= 0 || k >= 1) continue;
            var ease = 1 - ((1 - k) * (1 - k));
            int px = cx + (int)Math.Round(ox * (4 + (ease * 13))), pyy = cy + (int)Math.Round(oy * (3 + (ease * 9))) - (int)Math.Round(k * 12);
            Puff(p, px, pyy, 3 + (ease * 5.5), 1.15 - k, SmokePal, water);
        }

        // a burst of streaks out from it at the first instant
        if (u < 0.22)
        {
            for (var a = 0; a < 8; a++)
            {
                var ang = a / 8.0 * Math.PI * 2;
                for (var d = 6 + (int)(u * 60); d < 10 + (int)(u * 70); d++) Set(p, cx + (int)Math.Round(Math.Cos(ang) * d), cy + (int)Math.Round(Math.Sin(ang) * d * 0.7), 0xFFFFFFu, water);
            }
        }
    }

    private static readonly (double X, double Y, double Delay)[] SmokePuffs =
    [
        (0, 0, 0), (-1, -0.3, 0.03), (1, -0.4, 0.05), (-0.7, 0.6, 0.08), (0.8, 0.5, 0.1), (0, -1, 0.12), (-1.3, 0.1, 0.16), (1.3, 0, 0.18),
        (-0.4, -1.2, 0.22), (0.5, -1.1, 0.24),
    ];

    /// <summary>Leaves drifting down where it was, swinging side to side as they fall.</summary>
    private static void Leaves(CapSheet p, int cx, int y, double u, int water)
    {
        if (u is < 0 or > 1.3) return;
        for (var n = 0; n < 4; n++)
        {
            var k = Math.Clamp((u - (n * 0.12)) / 1.0, 0, 1);
            if (k <= 0 || k >= 1) continue;
            int lx = cx + ((n - 1) * 9) + (int)Math.Round(Math.Sin((k * 9) + n) * 4), ly = y + (int)Math.Round(k * 26);
            var flip = Math.Sin((k * 9) + n) > 0;
            Set(p, lx, ly, LeafPal[4], water);
            Set(p, lx + (flip ? 1 : -1), ly, LeafPal[3], water);
            Set(p, lx, ly + 1, LeafPal[2], water);
        }
    }

    /// <summary>A puff: a ball of a ramp lit at its top left and darker below, its cells kept by the dither as much as it is still there.</summary>
    private static void Puff(CapSheet p, int x, int y, double r, double keep, uint[] ramp, int water)
    {
        var ir = (int)Math.Ceiling(r);
        for (var dy = -ir; dy <= ir; dy++)
        {
            for (var dx = -ir; dx <= ir; dx++)
            {
                var d = Math.Sqrt((dx * dx) + (dy * dy));
                if (d > r) continue;
                int px = x + dx, py = y + dy;
                if ((CapSeaPalette.Bayer[((py & 3) * 4) + (px & 3)] + 0.5) / 16.0 > keep) continue;
                var v = (ramp.Length - 1) * (0.62 - ((dx + dy) / r * 0.28) - (d > r - 1 && dy > 0 ? 0.3 : 0));
                Set(p, px, py, Dith(ramp, v, px, py), water);
            }
        }
    }
}
