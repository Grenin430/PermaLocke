using System.Windows.Media.Imaging;

namespace PermaLocke.App.Views;

/// <summary>
/// Pokémon that pass by (singleplayer, 2026-10-13): swimmers under the surface, from a Magikarp to a Kyogre, and fliers in the sky, from a
/// Wingull to a Rayquaza, drawn by hand in profile (<see cref="CritterArt"/>) and animated pixel by pixel: a fish beats the back half of its
/// body, a serpent waves all of it, a bird beats its wings round its shoulders. They cross the whole sea behind the screens of the game, like the
/// whale did (one window sees them go in and the other sees them come out). A few lanes, each a slot of <see cref="CapFauna.EventAt"/> on the seed
/// of the run and the time: one or two at once at most, the commons often, the legends rarely and twice the size where there is room.
/// Drawings from sheets are painted at two thirds of their size (2026-10-14).
/// </summary>
public static class CapVisitors
{
    /// <param name="Big">Drawn at twice the size where the window has room.</param>
    public sealed record Lane(int Kind, double Period, double Duration, int Chance, bool Flies, int[] Pool, bool Big = false);

    /// <summary>Species that pass in a little group of three (a school, a flock).</summary>
    private static readonly HashSet<int> Groups = [456, 370, 746, 223, 278, 16, 21, 333, 41];

    /// <summary>For the lab only: every lane that comes out draws this species, to look at one drawing in the sea.</summary>
    public static int? ForcedSpecies { get; set; }

    /// <summary>For the lab only: every lane passes at once, each with the next of these species that suits it (sea or sky).</summary>
    public static int[]? ForcedAll { get; set; }

    // Spearow and Rayquaza taken out of the skies (2026-10-16, the player's wish); their drawings stay.
    // Periods halved (2026-10-14): the visitors come by twice as often, now that they are drawn smaller.
    public static readonly Lane[] Lanes =
    [
        // The sea: commons often, then the less common with a rare among them, and the great ones now and then.
        new(10, 34, 32, 75, false, [129, 129, 118, 116, 456, 370, 223, 211, 746, 779, 170, 318]),
        new(11, 48, 42, 60, false, [119, 226, 320, 319, 457, 171, 369, 117, 131, 130, 350, 230, 490]),
        new(12, 150, 70, 55, false, [382, 382, 249, 321, 131], Big: true),
        // The sky.
        new(13, 22, 14, 70, true, [278, 278, 16, 731, 12, 333, 41]),
        new(14, 60, 20, 55, true, [279, 18, 22, 733, 334, 227, 398, 169, 468, 149, 6, 142, 380, 381]),
        new(15, 165, 26, 45, true, [250, 144, 145, 146, 249], Big: true)
    ];

    /// <summary>Every drawing from a sheet is painted at two thirds of its size (2026-10-14), keeping its thin joins (<see cref="Pick"/>).</summary>
    private const double Shrink = 2.0 / 3.0;

    /// <summary>The side of a cell once shrunk.</summary>
    private static int Shrunk(int cell) => (int)Math.Ceiling(cell * Shrink);

    /// <summary>
    /// The sheet pixel (an index into its BGRA bytes) that an output pixel of a drawing shrunk to two thirds shows, or -1 for none. The
    /// output pixel is on if the drawing covers at least 30 % of the patch of sheet under it, and it shows the colour that fills most of that
    /// patch, a dark outline counting half as much again. Plain nearest neighbour dropped the thin joins (a neck, an antenna, a tail) in
    /// thirty species; this keeps them (2026-10-14). A lone pixel, one with no neighbour of its own colour (an eye, its glint), counts
    /// two and a half times: otherwise it won or lost by where the frame fell on the grid, and the eye blinked as the body moved (2026-10-17).
    /// That still lost now and then, when the pixel fell across four output pixels and had too little of each (2026-10-20, the player saw
    /// eyes blink on some swimmers and fliers): now a tiny pixel (one with at most one neighbour of its own colour) always shows in the output
    /// pixel its middle falls in, the darkest if two do, so every eye of the sheet is in every frame.
    /// </summary>
    internal static int Pick(ToastPixels.SpritePixels sheet, int baseX, int baseY, int cell, int ox, int oy, double scale = Shrink)
    {
        double x0 = ox / scale, x1 = (ox + 1) / scale, y0 = oy / scale, y1 = (oy + 1) / scale;
        double total = 0, covered = 0, bestScore = 0;
        int best = -1, eye = -1, eyeSum = int.MaxValue;
        for (var y = (int)Math.Floor(y0); y < Math.Min(cell, (int)Math.Ceiling(y1)); y++)
        {
            var wy = Math.Min(y + 1, y1) - Math.Max(y, y0);
            for (var x = (int)Math.Floor(x0); x < Math.Min(cell, (int)Math.Ceiling(x1)); x++)
            {
                var w = wy * (Math.Min(x + 1, x1) - Math.Max(x, x0));
                total += w;
                var at = (((baseY + y) * sheet.Width) + baseX + x) * 4;
                if (sheet.Bgra[at + 3] < 128) continue;
                covered += w;
                var score = sheet.Bgra[at] + sheet.Bgra[at + 1] + sheet.Bgra[at + 2] < 150 ? w * 1.5 : w;
                if (Lone(sheet, baseX, baseY, cell, x, y)) score *= 2.5;
                var sum = sheet.Bgra[at] + sheet.Bgra[at + 1] + sheet.Bgra[at + 2];
                if (x + 0.5 >= x0 && x + 0.5 < x1 && y + 0.5 >= y0 && y + 0.5 < y1 && sum < eyeSum && Tiny(sheet, baseX, baseY, cell, x, y))
                {
                    eye = at;
                    eyeSum = sum;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = at;
                }
            }
        }

        if (eye >= 0) return eye;
        return total > 0 && covered / total >= 0.30 ? best : -1;
    }

    /// <summary>Whether a drawn pixel of a cell has at most one neighbour, of the eight round it, of exactly its colour: an eye, a pupil, a glint.</summary>
    private static bool Tiny(ToastPixels.SpritePixels sheet, int baseX, int baseY, int cell, int x, int y)
    {
        var at = (((baseY + y) * sheet.Width) + baseX + x) * 4;
        var same = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if ((dx | dy) == 0 || x + dx < 0 || y + dy < 0 || x + dx >= cell || y + dy >= cell) continue;
                var n = (((baseY + y + dy) * sheet.Width) + baseX + x + dx) * 4;
                if (sheet.Bgra[n + 3] >= 128 && sheet.Bgra[n] == sheet.Bgra[at] && sheet.Bgra[n + 1] == sheet.Bgra[at + 1]
                    && sheet.Bgra[n + 2] == sheet.Bgra[at + 2] && ++same > 1) return false;
            }
        }

        return true;
    }

    /// <summary>Whether a drawn pixel of a cell has no neighbour, of the eight round it, of exactly its colour.</summary>
    private static bool Lone(ToastPixels.SpritePixels sheet, int baseX, int baseY, int cell, int x, int y)
    {
        var at = (((baseY + y) * sheet.Width) + baseX + x) * 4;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if ((dx | dy) == 0 || x + dx < 0 || y + dy < 0 || x + dx >= cell || y + dy >= cell) continue;
                var n = (((baseY + y + dy) * sheet.Width) + baseX + x + dx) * 4;
                if (sheet.Bgra[n + 3] >= 128 && sheet.Bgra[n] == sheet.Bgra[at] && sheet.Bgra[n + 1] == sheet.Bgra[at + 1]
                    && sheet.Bgra[n + 2] == sheet.Bgra[at + 2]) return false;
            }
        }

        return true;
    }

    /// <summary>One pixel of a drawing, already lit (top edge) and shaded (bottom edge).</summary>
    private readonly record struct Dot(int X, int Y, uint Rgb, bool Wing);

    /// <param name="Frames">The hand-posed frames, when the drawing has them; empty otherwise.</param>
    private sealed record Sprite(CritterArt Art, int W, int H, Dot[] Dots, Dot[][] Frames);

    private static readonly Dictionary<int, Sprite?> Cache = [];

    private static readonly Dictionary<string, ToastPixels.SpritePixels?> Sheets = [];

    /// <summary>A sheet of frames shipped inside the app, decoded once.</summary>
    private static ToastPixels.SpritePixels? SheetOf(string name)
    {
        // panels (and tests) paint from more than one thread; the cache is a plain dictionary
        lock (Sheets) return LoadSheet(name);
    }

    private static ToastPixels.SpritePixels? LoadSheet(string name)
    {
        if (Sheets.TryGetValue(name, out var known)) return known;
        using var stream = typeof(CapVisitors).Assembly.GetManifestResourceStream($"PermaLocke.App.Critters.{name}.png");
        if (stream is null) return Sheets[name] = null;
        var frame = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        frame.Freeze();
        return Sheets[name] = ToastPixels.SpritePixels.From(frame);
    }

    /// <summary>
    /// A Pokémon with a sheet swims (or flies) a path of its own: height that drifts on two slow waves that never line up the same way, a
    /// little push on every beat and a glide after it, and the body tilted to where it is going, from the frames turned for that tilt. Each
    /// species has its own drift, sway and push; each passing its own phase, so no two beat together.
    /// </summary>
    private static void Swimmer(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, CritterArt art, Lane lane, (double Progress, int Variant) e,
        int dir, int world, bool party)
    {
        if (SheetOf(art.Sheet!) is not { } sheet || art.Angles is not { Length: > 0 } angles) return;
        var cell = art.Cell;
        var size = Shrunk(cell);
        var count = Math.Max(1, sheet.Width / cell);
        var tau = (e.Progress * lane.Duration) + ((e.Variant % 13) * 0.173);
        var loop = count / art.Fps;
        var speed = (world + (2.0 * size)) / lane.Duration;

        // the push: a little ahead on the stroke, a little behind on the glide; the frame of the beat is the same clock
        var Surge = art.Surge;
        var beat = 2 * Math.PI * tau / loop;
        var x = Across(e.Progress, dir, world, size) + (dir * Surge * Math.Sin(beat)) - ctx.WorldX + (dir > 0 ? -size / 2.0 : size / 2.0);
        if (x + (size / 2.0) < -4 || x - (size / 2.0) > l.Cols + 4) return;

        // depth: two slow waves with the run's own phases
        double w1 = 2 * Math.PI / 13.0, w2 = 2 * Math.PI / 4.3, a1 = art.Drift, a2 = art.Bob, p1 = e.Variant % 7, p2 = e.Variant % 11;
        var half = Math.Max(4, art.Height * Shrink / 2.0);
        var top = lane.Flies ? 3 + half : l.SurfaceY + 6 + half;
        var bottom = lane.Flies ? l.SurfaceY - 4 - half : l.FloorTop - 16 - half;
        if (bottom <= top) return;
        var mid = top + ((e.Variant / 3) % Math.Max(1, (int)(bottom - top)));
        var y = Math.Clamp(mid + (a1 * Math.Sin((w1 * tau) + p1)) + (a2 * Math.Sin((w2 * tau) + p2)), top, bottom);

        // the tilt follows where it is going on the slow wave only, at its mean speed: the push of each beat and the short sway would make
        // it rock between two tilts; down is positive, and the drawing already points its nose down by its pose
        var vy = a1 * w1 * Math.Cos((w1 * tau) + p1);
        var heading = Math.Atan2(vy, Math.Max(1, speed)) * 180 / Math.PI;
        var lift = Math.Clamp(art.Pose - heading, angles[0], angles[^1]);
        var row = 0;
        for (var k = 1; k < angles.Length; k++) if (Math.Abs(angles[k] - lift) < Math.Abs(angles[row] - lift)) row = k;
        var col = (int)Math.Floor(tau * art.Fps) % count;

        var left = (int)Math.Round(x - (size / 2.0));
        var topY = (int)Math.Round(y - (size / 2.0));
        var depth = Math.Clamp((y - l.SurfaceY) / Math.Max(1, l.FloorTop - l.SurfaceY), 0, 1);
        // in the sky: full colour by day, a shape against it at night; under water: bluer with depth, faint behind the party
        var night = CapSeaPalette.Night(ctx.SkyHour);
        var alpha = party && !lane.Flies ? 120 : 255;
        var tint = lane.Flies ? 0x141C38u : pal.Water[5];
        var tintAmount = lane.Flies ? 0.6 * night : (party ? 0.45 : 0.08) + (0.25 * depth);
        // a shiny in the battle (2026-10-17): it passes in its shiny colours, whole, over the rainbow, with sparkles behind it
        var shiny = ctx.Shiny >= 0;
        if (shiny) (alpha, tintAmount) = (255, 0);
        for (var oy = 0; oy < size; oy++)
        {
            for (var ox = 0; ox < size; ox++)
            {
                var at = Pick(sheet, col * cell, row * cell, cell, ox, oy);
                if (at < 0) continue;
                var rgb = ((uint)sheet.Bgra[at + 2] << 16) | ((uint)sheet.Bgra[at + 1] << 8) | sheet.Bgra[at];
                var dx = dir > 0 ? size - 1 - ox : ox;
                p.Rect(left + dx, topY + oy, 1, 1, shiny ? CapShiny.Shiny(rgb) : tintAmount > 0 ? CapSheet.Mix(rgb, tint, tintAmount) : rgb, alpha);
                if (shiny) CapShiny.Keep(left + dx, topY + oy);
            }
        }

        if (shiny) CapShiny.Trail(p, left + (size / 2), topY + (size / 2), size, dir, ctx.Shiny);

        if (!party && !lane.Flies) Bubbles(p, pal, (int)Math.Round(x + (dir * size * 0.33)), topY + (size / 2), beat);
    }

    /// <summary>Lab selection: retain the existing lane assignments, but include every species when a batch exceeds its sea or sky lanes.</summary>
    internal static int[] ForcedLaneSpecies(int[] all, int laneIndex)
    {
        var flies = Lanes[laneIndex].Flies;
        var suits = all.Where(id => Lanes.Any(other => other.Flies == flies && other.Pool.Contains(id % 10000))).ToArray();
        if (suits.Length == 0) return [];
        var laneCount = Lanes.Count(lane => lane.Flies == flies);
        if (suits.Length <= laneCount) return [suits[laneIndex % suits.Length]];
        return [.. suits.Where((_, index) => index % laneCount == laneIndex % laneCount)];
    }

    public static void Paint(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, double t, double phase)
    {
        if (l.Mode is CapMode.Compact or CapMode.Strip || ctx.Parade) return;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;
        var roomy = l.Mode == CapMode.Column && !l.Dense;

        foreach (var lane in Lanes)
        {
            var e = CapFauna.EventAt(ctx.Seed, lane.Kind, t, lane.Period, lane.Duration, lane.Chance);
            if (ForcedAll is { Length: > 0 } all)
            {
                var k = Array.IndexOf(Lanes, lane);
                var suits = ForcedLaneSpecies(all, k);
                for (var extra = 0; extra < suits.Length; extra++)
                {
                    var progress = ((t / lane.Duration) + (k * 0.37) + (extra * 0.19)) % 1.0;
                    Swimmer(p, l, pal, ctx, CritterArt.All[suits[extra]], lane, (progress, 17 + (k * 29) + (extra * 11)),
                        k % 2 == 0 ? 1 : -1, world, l.Panel == CapPanelKind.Party);
                }
                continue;
            }

            if (!e.Active) continue;
            // a Mega form (10000 + the species) goes where its species goes
            var forcedHere = ForcedSpecies is { } forced && Lanes.Any(other => other.Flies == lane.Flies && other.Pool.Contains(forced % 10000));
            var species = forcedHere ? ForcedSpecies!.Value : lane.Pool[e.Variant % lane.Pool.Length];
            // once the Mega Evolution is unlocked (the sixth trial), those that have a Mega form pass in it (2026-10-16)
            if (ctx.Megas && !forcedHere && CritterArt.All.ContainsKey(10000 + species)) species += 10000;
            if (CritterArt.All.TryGetValue(species, out var posed) && posed.Sheet is not null)
            {
                Swimmer(p, l, pal, ctx, posed, lane, (e.Progress, e.Variant), (e.Variant / 7) % 2 == 0 ? 1 : -1, world, l.Panel == CapPanelKind.Party);
                continue;
            }

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
        if (l.Mode is CapMode.Compact or CapMode.Strip) return;
        if (CritterArt.All.TryGetValue(319, out var shark) && shark.Sheet is not null)
        {
            ProwlSheet(p, l, pal, ctx, shark, phase, strength);
            return;
        }

        if (Get(319) is not { } s) return;
        var scale = l.Mode == CapMode.Column && !l.Dense ? 2 : 1;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;
        var x = (int)((phase * 3.2) % (world + (2 * s.W * scale))) - (s.W * scale) - ctx.WorldX;
        var y = l.SurfaceY + 30 + (int)(Math.Sin(phase * 0.2) * 12);
        Draw(p, s, x, y, scale, flip: true, (int)(230 * strength), CapSheet.Mix(pal.Water[6], 0x000000u, 0.4), 0.6, phase * 0.7, big: true, pal.Ink);
    }

    /// <summary>
    /// The danger shark from its sheet (2026-10-14): level (the row nearest no tilt), its own swim at its pace, slow across the world and dark
    /// with the water, as strong as the danger.
    /// </summary>
    private static void ProwlSheet(CapSheet p, CapWorldLayout l, CapSeaPalette pal, CapSeaContext ctx, CritterArt art, double phase, double strength)
    {
        if (SheetOf(art.Sheet!) is not { } sheet || art.Angles is not { Length: > 0 } angles) return;
        var cell = art.Cell;
        var count = Math.Max(1, sheet.Width / cell);
        var row = 0;
        for (var k = 1; k < angles.Length; k++) if (Math.Abs(angles[k]) < Math.Abs(angles[row])) row = k;
        var col = (int)Math.Floor(phase * art.Fps) % count;
        var world = ctx.WorldWidth > 0 ? ctx.WorldWidth : l.Cols;
        var size = Shrunk(cell);
        var left = (int)((phase * 3.2) % (world + (2 * size))) - size - ctx.WorldX;
        var top = l.SurfaceY + 18 + (int)(Math.Sin(phase * 0.2) * 12);
        var alpha = (int)(230 * strength);
        var tint = CapSheet.Mix(pal.Water[6], 0x000000u, 0.4);
        for (var oy = 0; oy < size; oy++)
        {
            for (var ox = 0; ox < size; ox++)
            {
                var at = Pick(sheet, col * cell, row * cell, cell, ox, oy);
                if (at < 0) continue;
                var rgb = ((uint)sheet.Bgra[at + 2] << 16) | ((uint)sheet.Bgra[at + 1] << 8) | sheet.Bgra[at];
                // it swims to the right, so the drawing (facing left) is turned round
                p.Rect(left + (size - 1 - ox), top + oy, 1, 1, CapSheet.Mix(rgb, tint, 0.6), alpha);
            }
        }
    }

    /// <summary>
    /// A drawing from its sheet at any <paramref name="scale"/>, centred on a point, at its own pace (the Mega parade, 2026-10-16): turned by
    /// <paramref name="tilt"/> degrees (nose up positive) as far as its sheet has frames for, its colours mixed towards <paramref name="mixTo"/>
    /// by <paramref name="mix"/>, and only <paramref name="keep"/> of its cells drawn (an ordered dither, for coming and going).
    /// </summary>
    internal static void Centred(CapSheet p, CritterArt art, double tau, int cx, int cy, double scale, bool faceRight, uint mixTo = 0xFFFFFF, double mix = 0,
        double tilt = 0, double keep = 1)
    {
        if (scale < 0.04 || keep <= 0 || art.Sheet is null || SheetOf(art.Sheet) is not { } sheet || art.Angles is not { Length: > 0 } angles) return;
        var cell = art.Cell;
        var count = Math.Max(1, sheet.Width / cell);
        var row = 0;
        var lift = art.Pose + tilt;
        for (var k = 1; k < angles.Length; k++) if (Math.Abs(angles[k] - lift) < Math.Abs(angles[row] - lift)) row = k;
        var col = (int)Math.Floor(Math.Max(0, tau) * art.Fps) % count;
        var size = (int)Math.Ceiling(cell * scale);
        int left = cx - (size / 2), top = cy - (size / 2);
        for (var oy = 0; oy < size; oy++)
        {
            for (var ox = 0; ox < size; ox++)
            {
                var at = Pick(sheet, col * cell, row * cell, cell, ox, oy, scale);
                if (at < 0) continue;
                int x = left + (faceRight ? size - 1 - ox : ox), y = top + oy;
                if (keep < 1 && Dither[y & 3, x & 3] >= keep * 16) continue;
                var rgb = ((uint)sheet.Bgra[at + 2] << 16) | ((uint)sheet.Bgra[at + 1] << 8) | sheet.Bgra[at];
                p.Set(x, y, mix > 0 ? CapSheet.Mix(rgb, mixTo, mix) : rgb);
            }
        }
    }

    private static readonly int[,] Dither = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

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
        if (!CritterArt.All.TryGetValue(species, out var art) || art.Sheet is not null) return Cache[species] = null;

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
