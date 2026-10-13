using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// Pokémon that pass by (singleplayer, 2026-10-13): swimmers under the surface, from a Magikarp to a Kyogre, and fliers in the sky, from a
/// Wingull to a Rayquaza, drawn from the cartridge's own icons. They cross the whole sea behind the screens of the game, like the whale did
/// (one window sees them go in and the other sees them come out). A few lanes, each a slot of <see cref="CapFauna.EventAt"/> on the seed of the
/// run and the time: one or two at once at most, the commons often, the legends rarely and twice the size where there is room.
/// </summary>
public static class CapVisitors
{
    /// <summary>The cartridge's icon of a species, set once by whoever has the sprites (the badge, the lab). Null draws nothing.</summary>
    public static Func<int, BitmapSource?>? Icons { get; set; }

    /// <param name="Groups">Species that pass in a little group of three (a school, a flock).</param>
    /// <param name="Big">Drawn at twice the size where the window has room.</param>
    public sealed record Lane(int Kind, double Period, double Duration, int Chance, bool Flies, int[] Pool, bool Big = false);

    private static readonly HashSet<int> Groups = [129, 456, 370, 746, 223, 278, 16, 21, 333, 41];

    /// <summary>Long bodies that wave as they go.</summary>
    private static readonly HashSet<int> Serpents = [130, 350, 384, 147, 148, 230, 117];

    public static readonly Lane[] Lanes =
    [
        // The sea: commons often, then the less common with a rare among them, and the great ones now and then.
        new(10, 55, 32, 75, false, [129, 129, 118, 116, 456, 370, 223, 222, 211, 746, 779, 170, 318]),
        new(11, 95, 42, 60, false, [119, 226, 320, 319, 457, 224, 171, 369, 121, 117, 131, 130, 350, 230, 781, 490]),
        new(12, 300, 70, 55, false, [382, 382, 249, 788, 321, 131], Big: true),
        // The sky.
        new(13, 45, 14, 70, true, [278, 278, 16, 21, 731, 12, 333, 163, 41]),
        new(14, 120, 20, 55, true, [279, 18, 22, 733, 334, 227, 398, 628, 169, 741, 468, 149, 373, 6, 142, 380, 381]),
        new(15, 330, 26, 45, true, [384, 384, 250, 144, 145, 146, 249], Big: true)
    ];

    private sealed record Sprite(int W, int H, uint[] Rgb, bool[] Solid, (int X, int Y)[] Edge);

    private static readonly Dictionary<(int Species, int Scale), Sprite?> Cache = [];

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double t, double phase)
    {
        if (l.Mode is CapMode.Compact or CapMode.Strip) return;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;
        var roomy = l.Mode == CapMode.Column && !l.Dense;

        foreach (var lane in Lanes)
        {
            if (CapFauna.EventAt(ctx.Seed, lane.Kind, t, lane.Period, lane.Duration, lane.Chance) is not { Active: true } e) continue;
            var species = lane.Pool[e.Variant % lane.Pool.Length];
            var scale = lane.Big ? (roomy ? 2 : 1) : (roomy ? 1 : -2);
            if (Get(species, scale) is not { } s) continue;

            var dir = (e.Variant / 7) % 2 == 0 ? 1 : -1;
            var count = Groups.Contains(species) ? 3 : 1;
            var length = (s.W + 6) * count;
            var lead = Across(e.Progress, dir, world, length) - ctx.WorldX;

            int top;
            if (lane.Flies)
            {
                var room = l.SurfaceY - s.H - 10;
                if (room < 2) continue;
                top = 2 + ((e.Variant / 3) % room);
            }
            else
            {
                var room = l.FloorTop - 18 - s.H - (l.SurfaceY + 8);
                if (room < 2) continue;
                top = l.SurfaceY + 8 + ((e.Variant / 3) % room);
            }

            for (var i = 0; i < count; i++)
            {
                var x = lead - (dir > 0 ? s.W : 0) - (dir * i * (s.W + 6));
                var y = top + (i == 0 ? 0 : ((i % 2 == 0 ? 1 : -1) * 5)) ;
                if (x + s.W < -2 || x > l.Cols + 2) continue;
                var beat = phase + (i * 0.7) + (e.Variant % 5);
                if (lane.Flies) Fly(p, s, x, y, dir, pal, ctx, beat, species, lane.Big);
                else Swim(p, l, s, x, y, dir, pal, beat, species);
            }
        }
    }

    /// <summary>The shark of a sea in danger, now a Sharpedo: dark, slow, for good while the danger lasts.</summary>
    public static void Prowl(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double phase, double strength)
    {
        if (l.Mode is CapMode.Compact or CapMode.Strip) return;
        if (Get(319, l.Mode == CapMode.Column && !l.Dense ? 2 : 1) is not { } s) return;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;
        var x = (int)((phase * 3.2) % (world + (2 * s.W))) - s.W - ctx.WorldX;
        var y = l.SurfaceY + 30 + (int)(Math.Sin(phase * 0.2) * 12);
        var dark = CapSheet.Mix(pal.Water[6], 0x000000u, 0.4);
        Draw(p, s, x, y, flip: true, (int)(200 * strength), dark, 0.55, phase * 2, 1, pal.Ink);
    }

    private static void Swim(CapSheet p, CapWorldLayout l, Sprite s, int x, int y, int dir, CapSeaPalette pal, double beat, int species)
    {
        // Deeper is bluer: the water between the eye and the Pokémon.
        var depth = Math.Clamp((y - l.SurfaceY) / (double)Math.Max(1, l.FloorTop - l.SurfaceY), 0, 1);
        var bob = (int)Math.Round(Math.Sin(beat * 1.3) * 1.5);
        // Behind the party they are faint shapes in the water, so the words in front stay easy to read.
        var party = l.Panel == CapPanelKind.Party;
        Draw(p, s, x, y + bob, flip: dir > 0, party ? 120 : 225, pal.Water[5], (party ? 0.45 : 0.12) + (0.3 * depth), beat * 3, Serpents.Contains(species) ? 2 : 1, pal.Ink);
        if (party) return;

        // Bubbles from the mouth now and then, rising and fading.
        var mouth = dir > 0 ? x + s.W : x;
        for (var k = 0; k < 3; k++)
        {
            var age = ((beat * 0.45) + (k / 3.0)) % 1.0;
            if (age > 0.75) continue;
            p.Set(mouth + (int)Math.Round(Math.Sin((age * 8) + k) * 1.5), y + (s.H / 3) - (int)(age * 16), pal.Snow, (int)(170 * (1 - age)));
        }
    }

    private static void Fly(CapSheet p, Sprite s, int x, int y, int dir, CapSeaPalette pal, CapSeaContext ctx, double beat, int species, bool big)
    {
        // Wings beat as a bob; at night the fliers are shapes against the sky.
        var bob = (int)Math.Round(Math.Sin(beat * (big ? 2.2 : 5)) * (big ? 2.5 : 1.5));
        var night = CapSeaPalette.Night(ctx.SkyHour);
        Draw(p, s, x, y + bob, flip: dir > 0, 255, 0x141C38u, 0.6 * night, beat * 2.5, Serpents.Contains(species) ? 2 : 0, pal.Ink);
    }

    /// <summary>A sprite with its dark outline, flipped to face where it goes, each column lifted by a wave (a tail, a serpent's body).</summary>
    private static void Draw(CapSheet p, Sprite s, int left, int top, bool flip, int alpha, uint tint, double tintAmount, double wavePhase, int wave, uint ink)
    {
        int Lift(int x) => wave == 0 ? 0 : (int)Math.Round(Math.Sin(wavePhase - (x * 0.3)) * wave * (0.3 + (0.7 * (flip ? 1 - (x / (double)s.W) : x / (double)s.W))));
        int Col(int x) => flip ? s.W - 1 - x : x;

        foreach (var (ex, ey) in s.Edge)
        {
            var cx = Col(ex);
            p.Set(left + cx, top + ey + Lift(cx), ink, alpha * 3 / 4);
        }

        for (var y = 0; y < s.H; y++)
        {
            for (var x = 0; x < s.W; x++)
            {
                var at = (y * s.W) + x;
                if (!s.Solid[at]) continue;
                var cx = Col(x);
                p.Set(left + cx, top + y + Lift(cx), tintAmount > 0 ? CapSheet.Mix(s.Rgb[at], tint, tintAmount) : s.Rgb[at], alpha);
            }
        }
    }

    private static int Across(double progress, int dir, int world, int length) =>
        dir > 0 ? -length + (int)(progress * (world + (2 * length))) : world + length - (int)(progress * (world + (2 * length)));

    /// <summary>The icon cut to what is drawn in it, at a scale: 2 doubles it, -2 halves it, 1 leaves it.</summary>
    private static Sprite? Get(int species, int scale)
    {
        if (Cache.TryGetValue((species, scale), out var known)) return known;
        if (ToastPixels.SpritePixels.From(Icons?.Invoke(species)) is not { } px) return null;

        int x0 = px.Width, y0 = px.Height, x1 = -1, y1 = -1;
        for (var y = 0; y < px.Height; y++)
        {
            for (var x = 0; x < px.Width; x++)
            {
                if (!px.Solid(x, y)) continue;
                x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
            }
        }

        if (x1 < 0) return Cache[(species, scale)] = null;

        double step = scale < 0 ? -scale : 1.0 / scale;
        var w = Math.Max(1, (int)Math.Ceiling((x1 - x0 + 1) / step));
        var h = Math.Max(1, (int)Math.Ceiling((y1 - y0 + 1) / step));
        var rgb = new uint[w * h];
        var solid = new bool[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var sx = x0 + (int)(x * step);
                var sy = y0 + (int)(y * step);
                if (!px.Solid(sx, sy)) continue;
                var c = px.At(sx, sy);
                rgb[(y * w) + x] = ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                solid[(y * w) + x] = true;
            }
        }

        bool On(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && solid[(y * w) + x];
        var edge = new List<(int, int)>();
        for (var y = -1; y <= h; y++)
        {
            for (var x = -1; x <= w; x++)
            {
                if (!On(x, y) && (On(x - 1, y) || On(x + 1, y) || On(x, y - 1) || On(x, y + 1))) edge.Add((x, y));
            }
        }

        return Cache[(species, scale)] = new Sprite(w, h, rgb, solid, [.. edge]);
    }
}
