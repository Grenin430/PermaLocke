namespace PermaLocke.App.Views;

/// <summary>
/// Pokémon that pass by (singleplayer, 2026-10-13): swimmers under the surface, from a Magikarp to a Kyogre, and fliers in the sky, from a
/// Wingull to a Rayquaza, drawn by hand in profile (<see cref="CritterArt"/>) and animated pixel by pixel: a fish beats the back half of its
/// body, a serpent waves all of it, a bird beats its wings round its shoulders. They cross the whole sea behind the screens of the game, like the
/// whale did (one window sees them go in and the other sees them come out). A few lanes, each a slot of <see cref="CapFauna.EventAt"/> on the seed
/// of the run and the time: one or two at once at most, the commons often, the legends rarely and twice the size where there is room.
/// </summary>
public static class CapVisitors
{
    /// <param name="Big">Drawn at twice the size where the window has room.</param>
    public sealed record Lane(int Kind, double Period, double Duration, int Chance, bool Flies, int[] Pool, bool Big = false);

    /// <summary>Species that pass in a little group of three (a school, a flock).</summary>
    private static readonly HashSet<int> Groups = [129, 456, 370, 746, 223, 278, 16, 21, 333, 41];

    public static readonly Lane[] Lanes =
    [
        // The sea: commons often, then the less common with a rare among them, and the great ones now and then.
        new(10, 55, 32, 75, false, [129, 129, 118, 116, 456, 370, 223, 211, 746, 779, 170, 318]),
        new(11, 95, 42, 60, false, [119, 226, 320, 319, 457, 171, 369, 117, 131, 130, 350, 230, 490]),
        new(12, 300, 70, 55, false, [382, 382, 249, 321, 131], Big: true),
        // The sky.
        new(13, 45, 14, 70, true, [278, 278, 16, 21, 731, 12, 333, 41]),
        new(14, 120, 20, 55, true, [279, 18, 22, 733, 334, 227, 398, 169, 468, 149, 373, 6, 142, 380, 381]),
        new(15, 330, 26, 45, true, [384, 384, 250, 144, 145, 146, 249], Big: true)
    ];

    /// <summary>One pixel of a drawing, already lit (top edge) and shaded (bottom edge).</summary>
    private readonly record struct Dot(int X, int Y, uint Rgb, bool Wing);

    /// <param name="Frames">The hand-posed frames, when the drawing has them; empty otherwise.</param>
    private sealed record Sprite(CritterArt Art, int W, int H, Dot[] Dots, Dot[][] Frames);

    private static readonly Dictionary<int, Sprite?> Cache = [];

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double t, double phase)
    {
        if (l.Mode is CapMode.Compact or CapMode.Strip) return;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;
        var roomy = l.Mode == CapMode.Column && !l.Dense;

        foreach (var lane in Lanes)
        {
            if (CapFauna.EventAt(ctx.Seed, lane.Kind, t, lane.Period, lane.Duration, lane.Chance) is not { Active: true } e) continue;
            var species = lane.Pool[e.Variant % lane.Pool.Length];
            if (Get(species) is not { } s) continue;

            var scale = lane.Big && roomy ? 2 : 1;
            var (w, h) = (s.W * scale, s.H * scale);
            var dir = (e.Variant / 7) % 2 == 0 ? 1 : -1;
            var count = Groups.Contains(species) ? 3 : 1;
            var length = (w + 6) * count;
            var lead = Across(e.Progress, dir, world, length) - ctx.WorldX;

            int top;
            if (lane.Flies)
            {
                var room = l.SurfaceY - h - 10;
                if (room < 2) continue;
                top = 3 + ((e.Variant / 3) % room);
            }
            else
            {
                var room = l.FloorTop - 18 - h - (l.SurfaceY + 8);
                if (room < 2) continue;
                top = l.SurfaceY + 8 + ((e.Variant / 3) % room);
            }

            for (var i = 0; i < count; i++)
            {
                var x = lead - (dir > 0 ? w : 0) - (dir * i * (w + 6));
                var y = top + (i == 0 ? 0 : (i % 2 == 0 ? 1 : -1) * 5);
                if (x + w < -4 || x > l.Cols + 4) continue;
                var beat = phase + (i * 0.7) + (e.Variant % 5);

                if (lane.Flies)
                {
                    // At night the fliers are shapes against the sky.
                    var night = CapSeaPalette.Night(ctx.SkyHour);
                    Draw(p, s, x, y, scale, dir > 0, 255, 0x141C38u, 0.6 * night, beat, lane.Big, pal.Ink);
                    continue;
                }

                // Deeper is bluer: the water between the eye and the Pokémon. Behind the party they are faint shapes, so the words stay easy to read.
                var depth = Math.Clamp((y - l.SurfaceY) / (double)Math.Max(1, l.FloorTop - l.SurfaceY), 0, 1);
                var party = l.Panel == CapPanelKind.Party;
                Draw(p, s, x, y, scale, dir > 0, party ? 120 : 255, pal.Water[5], (party ? 0.45 : 0.1) + (0.3 * depth), beat, lane.Big, pal.Ink);
                if (!party) Bubbles(p, pal, dir > 0 ? x + w : x, y + (h / 3), beat);
            }
        }
    }

    /// <summary>The shark of a sea in danger, now a Sharpedo: dark, slow, for good while the danger lasts.</summary>
    public static void Prowl(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double phase, double strength)
    {
        if (l.Mode is CapMode.Compact or CapMode.Strip || Get(319) is not { } s) return;
        var scale = l.Mode == CapMode.Column && !l.Dense ? 2 : 1;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;
        var x = (int)((phase * 3.2) % (world + (2 * s.W * scale))) - (s.W * scale) - ctx.WorldX;
        var y = l.SurfaceY + 30 + (int)(Math.Sin(phase * 0.2) * 12);
        Draw(p, s, x, y, scale, flip: true, (int)(230 * strength), CapSheet.Mix(pal.Water[6], 0x000000u, 0.4), 0.6, phase * 0.7, big: true, pal.Ink);
    }

    private static void Bubbles(CapSheet p, CapSeaPalette pal, int mouth, int y, double beat)
    {
        for (var k = 0; k < 3; k++)
        {
            var age = ((beat * 0.45) + (k / 3.0)) % 1.0;
            if (age > 0.75) continue;
            p.Set(mouth + (int)Math.Round(Math.Sin((age * 8) + k) * 1.5), y - (int)(age * 16), pal.Snow, (int)(170 * (1 - age)));
        }
    }

    /// <summary>
    /// One frame of a drawing: each pixel moved by the motion of its kind (the wave along the body, the beat of the wings), facing where it goes,
    /// at its scale, with a dark outline under it when it is drawn solid.
    /// </summary>
    private static void Draw(CapSheet p, Sprite s, int left, int top, int scale, bool flip, int alpha, uint tint, double tintAmount, double beat, bool big,
        uint ink)
    {
        var art = s.Art;

        // Hand-posed frames carry their own outline and shading: the frame of the moment, facing where it goes, with a gentle drift up and down.
        if (s.Frames.Length > 0)
        {
            var frame = s.Frames[(int)Math.Floor(Math.Abs(beat) * art.Fps) % s.Frames.Length];
            var drift = (int)Math.Round(Math.Sin(beat * 1.3) * 1.0);
            foreach (var d in frame)
            {
                var x = flip ? s.W - 1 - d.X : d.X;
                p.Rect(left + (x * scale), top + ((d.Y + drift) * scale), scale, scale, tintAmount > 0 ? CapSheet.Mix(d.Rgb, tint, tintAmount) : d.Rgb, alpha);
            }

            return;
        }

        // The wings: drawn raised, they swing down through the shoulder line and back; drawn open, they close and open. Big ones beat slowly.
        var swing = Math.Cos(beat * (big ? 3.6 : art.Motion == CritterMotion.Bob ? 9 : 7.5));
        var fold = art.Flap switch { "up" => swing, "sym" => 0.6 + (0.4 * swing), _ => 1.0 };
        var bob = art.Flap is null ? Math.Sin(beat * 1.6) * 1.2 : -Math.Sin(beat * (big ? 3.6 : 7.5)) * 0.8;
        var (amp, k, speed) = art.Motion switch
        {
            CritterMotion.Serpent => (big ? 2.4 : 2.0, 0.32, 3.2),
            CritterMotion.Fish => (1.2, 0.55, 5.0),
            _ => (0.0, 0.0, 0.0)
        };

        (int X, int Y) Place(Dot d)
        {
            var y = d.Y + bob;
            if (d.Wing) y = art.Pivot + ((d.Y - art.Pivot) * fold) + bob;
            else if (amp > 0)
            {
                // Head steady, the wave grows towards the tail (a fish), or runs the whole length (a serpent).
                var u = d.X / (double)Math.Max(1, s.W - 1);
                var weight = art.Motion == CritterMotion.Serpent ? 0.35 + (0.65 * u) : Math.Clamp((u - 0.35) / 0.65, 0, 1);
                y += Math.Sin((beat * speed) - (d.X * k)) * amp * weight;
            }

            var x = flip ? s.W - 1 - d.X : d.X;
            return (left + (x * scale), top + (int)Math.Round(y * scale));
        }

        if (alpha >= 200)
        {
            foreach (var d in s.Dots)
            {
                var (x, y) = Place(d);
                p.Rect(x - 1, y, scale + 2, scale, ink, 220);
                p.Rect(x, y - 1, scale, scale + 2, ink, 220);
            }
        }

        // Raised, the wing is behind the body; on the way down it is the near wing, in front of it.
        var wingsFirst = fold > 0.2;
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var d in s.Dots)
            {
                if (d.Wing != ((pass == 0) == wingsFirst)) continue;
                var (x, y) = Place(d);
                p.Rect(x, y, scale, scale, tintAmount > 0 ? CapSheet.Mix(d.Rgb, tint, tintAmount) : d.Rgb, alpha);
            }
        }
    }

    /// <summary>Every drawing in a few moments of its motion, one row each, for the lab to look at (<c>--bichos</c>).</summary>
    public static CapSheet Gallery(int frames = 6)
    {
        var sprites = CritterArt.All.Keys.Select(Get).OfType<Sprite>().ToList();
        var cellW = sprites.Max(s => s.W) + 6;
        var cellH = sprites.Max(s => s.H) + 8;
        var sheet = new CapSheet(cellW * frames, cellH * sprites.Count);
        var pal = CapSeaPalette.Of(CapMood.Calm);
        sheet.Rect(0, 0, sheet.W, sheet.H, 0x2A6A90u);
        for (var r = 0; r < sprites.Count; r++)
        {
            for (var f = 0; f < frames; f++)
            {
                Draw(sheet, sprites[r], (f * cellW) + 3, (r * cellH) + 4, 1, false, 255, 0, 0, f * 0.11, false, pal.Ink);
            }
        }

        return sheet;
    }

    private static int Across(double progress, int dir, int world, int length) =>
        dir > 0 ? -length + (int)(progress * (world + (2 * length))) : world + length - (int)(progress * (world + (2 * length)));

    /// <summary>A drawing read into its pixels, lit along the top edge and shaded along the bottom one.</summary>
    private static Sprite? Get(int species)
    {
        if (Cache.TryGetValue(species, out var known)) return known;
        if (!CritterArt.All.TryGetValue(species, out var art)) return Cache[species] = null;

        if (art.Frames is { Count: > 0 } posed)
        {
            var colours = art.Palette.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToDictionary(entry => entry[0], entry => Convert.ToUInt32(entry[2..], 16));
            var frames = posed.Select(f => f.Replace("\r", string.Empty).Split('\n')).ToList();
            var dotsOf = frames.Select(lines => lines.SelectMany((r, y) => r.Select((c, x) => (c, x, y)))
                .Where(q => q.c != '.' && colours.ContainsKey(q.c)).Select(q => new Dot(q.x, q.y, colours[q.c], false)).ToArray()).ToArray();
            return Cache[species] = new Sprite(art, frames.Max(r => r.Max(l => l.Length)), frames.Max(r => r.Length), dotsOf[0], dotsOf);
        }

        var palette = art.Palette.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToDictionary(entry => entry[0], entry => Convert.ToUInt32(entry[2..], 16));
        var rows = art.Rows.Replace("\r", string.Empty).Split('\n');
        var w = rows.Max(r => r.Length);
        bool On(int x, int y) => y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length && rows[y][x] != '.';

        var dots = new List<Dot>();
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                var c = rows[y][x];
                if (c == '.' || !palette.TryGetValue(char.ToLowerInvariant(c), out var rgb)) continue;
                if (char.ToLowerInvariant(c) != 'k')
                {
                    if (!On(x, y - 1)) rgb = CapSheet.Lighter(rgb, 0.18);
                    else if (!On(x, y + 1)) rgb = CapSheet.Darker(rgb, 0.22);
                }

                dots.Add(new Dot(x, y, rgb, char.IsUpper(c)));
            }
        }

        return Cache[species] = new Sprite(art, w, rows.Length, [.. dots], []);
    }
}
