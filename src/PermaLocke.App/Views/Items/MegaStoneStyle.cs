using static PermaLocke.App.Views.ItemFx;

namespace PermaLocke.App.Views;

/// <summary>
/// A Mega Stone going into the bag (2026-10-09): the event of the whole system, the longest animation there is (4.5 s) and the
/// only one with a resonance. The bag crouches and springs open, the stone rises out of it with an overshoot and hangs, two
/// or three strands of light wind round it in front and behind while it beats faster and faster (its outline pulses, a ring of
/// dots opens with every beat); the strands close on it, two flashes of white, rings of shock sweep the scene and a glyph is
/// drawn and dissolves; the stone drops into the bag with weight, the bag swallows it, rebounds and settles, a mark of impact,
/// a ripple on the ground and a dome of dashes, and the sparks that are left bounce once and go out one by one. The background
/// is never lit or darkened: everything drawn belongs to the stone, the bag, the plate or the rings and sparks round them.
/// </summary>
/// <remarks>
/// <para>
/// Everything is a function of the moment and the item: the icon's own colour tints the whole scene (the stones of the mod
/// included, their icons are in the cartridge like the others'), and the item's seed changes the rhythm of the heartbeat, the
/// turns and number of the strands, their direction, the gaps and orientation of the rings, where the glyph is traced from and
/// the order in which the sparks go out. The glyph itself is one of five families, taken from the hue of the stone. The same
/// moment of the same item is always the same frame.
/// </para>
/// <para>
/// 112 pixels of the game high, 48 more than the common scene, because the strands, the rings and the glyph need the room.
/// Only the height grows: the pixel is still computed from the width, so the scaling is the same and the bag still has the
/// same place beside the emulator's bottom screen.
/// </para>
/// <para>
/// The two flashes and the mark of impact last <see cref="FlashPhase"/> seconds each, whatever the frame rate: with more than
/// 28.6 frames a second there is at least one frame in each, so that at 30 they are not skipped.
/// </para>
/// </remarks>
public sealed class MegaStoneStyle : ItemStyle
{
    public static MegaStoneStyle Instance { get; } = new();

    public const int SceneHeight = 112;

    /// <summary>
    /// How long each of the two frames of white lasts, and each of the two of the mark of impact: a little more than one frame
    /// at 30 a second (33.3 ms), so that two frames are seen at 30, 45 and 60 a second.
    /// </summary>
    public const double FlashPhase = 0.035;

    /// <summary>Axis of the bag and the stone, and where every ring comes from.</summary>
    public const int Cx = 48;
    public const double RestY = 52;

    private const double MouthY = 82;
    private const int BagBottom = 100;
    private const int BagLeft = Cx - 12;
    private const int Ground = 103;
    private const int PlateLeft = 66;
    private const int PlateTop = 44;

    /// <summary>How far into the anticipation the stone starts to come out.</summary>
    private const double EmergeAt = 0.45;

    private const int Samples = 40;

    private static readonly uint Fallback = ItemCanvas.Bgra(0xC8, 0x80, 0xF0);
    private static readonly double[] RingReach = [30, 40, 46];
    private static readonly (int X, int Y)[][] Glyphs = BuildGlyphs();
    private static readonly (int X, int Y)[][] Badges = BuildBadges();

    public override int Height => SceneHeight;

    /// <summary>
    /// The moment the strands close on the stone and the white comes: where the climax ends and the rest begins. Public so that
    /// the tests look for the flash where it is.
    /// </summary>
    public static double ImpactAt(ItemPhases p) => p.ClimaxAt + (0.72 * p.Climax);

    /// <summary>The family of glyph a stone gets, 0 to 4, from the hue of its tint: no table of stones.</summary>
    public static int Family(uint tint)
    {
        if (tint == 0 || ItemTint.Saturation(tint) < 0.22) return 4;

        var hue = ItemTint.Hue(tint);
        return hue < 75 || hue >= 335 ? 0 : hue < 165 ? 1 : hue < 255 ? 2 : 3;
    }

    /// <summary>
    /// The family of glyph of an item: from the type of the Pokémon the stone is for, read from the cartridge (the mega
    /// evolution and personal tables), and from the hue of its tint when that could not be read.
    /// </summary>
    public static int FamilyOf(ItemScene.Item item) =>
        item.Kind is >= 0 and < 18 ? FamilyOfType[item.Kind] : Family(item.Tint);

    /// <summary>Each of the game's eighteen types, by its number, in the family of glyph that suits it.</summary>
    private static readonly int[] FamilyOfType = [4, 0, 2, 3, 1, 3, 1, 3, 4, 0, 2, 1, 0, 3, 2, 0, 3, 3];

    /// <summary>How many strands of light a seed gives its stone: three in one of four, two in the rest.</summary>
    public static int StrandsFor(int seed) => Look.Of(seed).Strands;

    /// <summary>
    /// Which of five scores a seed plays: 0 the usual one, two of every three; 2 a double beat (lub-dub); 3 the rings leave before the white;
    /// 4 the glyph is traced before it; 5 a beat that starts slow and ends frantic.
    /// </summary>
    public static int VariantFor(int seed) => Look.Of(seed).Variant;

    public override void Draw(ItemCanvas c, ItemScene.Item item, ItemPhases p, double t)
    {
        var look = Look.Of(item.Seed);
        var prism = new ItemPrism(item.Tint, Fallback);
        var family = FamilyOf(item);

        var impact = ImpactAt(p);
        var fallStart = impact + 0.26;
        var fallEnd = fallStart + 0.32;
        var a = Math.Clamp((t - p.In) / p.Anticipation, 0, 1);
        var conv = Math.Clamp((t - p.ClimaxAt) / (impact - p.ClimaxAt), 0, 1);
        var since = t - impact;
        var landed = t - fallEnd;
        var flash = since is >= 0 and < 2 * FlashPhase ? (since < FlashPhase ? 1 : 2) : 0;
        var mark = landed is >= 0 and < 2 * FlashPhase ? (landed < FlashPhase ? 1 : 2) : 0;

        // The heartbeat: quickens from the start of the resonance and keeps the pace of its end until the impact.
        var pulse = 0.0;
        var beat = 0;
        if (t >= p.ResonanceAt && t < impact)
        {
            var s = t - p.ResonanceAt;
            var spread = look.BeatEnd - look.BeatStart;
            var phase = look.BeatStart * s;
            phase += s <= p.Resonance
                ? 0.5 * spread * s * s / p.Resonance
                : (0.5 * spread * p.Resonance) + (spread * (s - p.Resonance));
            beat = (int)Math.Floor(phase);
            var along = phase - beat;
            pulse = Math.Exp(-along * 4.5);

            // Lub-dub: a second, weaker knock right behind the first.
            if (look.Variant == 2 && along > 0.28) pulse = Math.Max(pulse, 0.8 * Math.Exp(-(along - 0.28) * 5));
        }

        // The stone: where it is, how it is squashed, and how white.
        var stone = false;
        var x = (double)Cx;
        var y = RestY;
        var sx = 1.0;
        var sy = 1.0;
        var mix = 0.0;
        var shine = -1.0;

        if (a >= EmergeAt && t < fallEnd)
        {
            stone = true;

            if (t < p.ResonanceAt)
            {
                var e = (a - EmergeAt) / (1 - EmergeAt);
                var scale = 0.4 + (0.6 * ItemCanvas.Ease(e));
                y = MouthY + ((RestY - MouthY) * ItemCanvas.Back(e));
                sx = scale * (1 - (0.14 * (1 - e)));
                sy = scale * (1 + (0.20 * (1 - e)));
                shine = (e - 0.4) * 2.2;
            }
            else if (t < impact)
            {
                y = RestY + Math.Round(Math.Sin((t - p.ResonanceAt) * 5.0));
                mix = Math.Min(1, (pulse * 0.85) + (conv * 0.4));

                // It shakes more and more as the strands close on it.
                x += Math.Round((Noise(look, t, 1) - 0.5) * 4 * conv * conv);
                y += Math.Round((Noise(look, t, 2) - 0.5) * 4 * conv * conv);
            }
            else if (t < fallStart)
            {
                y = RestY - Math.Round(2 * ItemCanvas.Ease(since / 0.2));
                mix = 0.8 * (1 - Math.Clamp(since / 0.45, 0, 1));
            }
            else
            {
                var g = (t - fallStart) / (fallEnd - fallStart);
                var from = RestY - 2;
                y = from + ((BagBottom - 2 - from) * g * g);
                sx = 1 - (0.15 * g);
                sy = 1 + (0.25 * g);
            }

            if (flash > 0) mix = 1;
        }

        // The bag: the crouch before it opens, the shiver with the heartbeat, and when the stone is in, the swallow, the
        // rebound, the stretch, the relax and the stillness.
        var bagY = 1.0;
        var open = false;
        var shiver = 0;
        uint stitch = 0;
        var bulgeRow = -1.0;
        var bulge = 0.0;
        var offset = t < p.In ? (int)Math.Round((1 - ItemCanvas.Ease(t / p.In)) * 6) : 0;

        if (t >= p.In && t < p.ResonanceAt)
        {
            bagY = a < 0.40 ? 1 - (0.20 * ItemCanvas.Smooth(a / 0.40))
                : a < 0.55 ? 0.80 + (0.34 * ItemCanvas.Smooth((a - 0.40) / 0.15))
                : 1 + (0.14 * (1 - ItemCanvas.Ease((a - 0.55) / 0.45)));
            open = a >= EmergeAt;
        }
        else if (t >= p.ResonanceAt && t < fallEnd)
        {
            open = true;
            if (t < impact && pulse > 0.35)
            {
                shiver = (beat & 1) == 0 ? 1 : -1;
                stitch = prism.Light;
            }
        }
        else if (t >= fallEnd)
        {
            bagY = landed < 0.08 ? 1 - (0.18 * ItemCanvas.Ease(landed / 0.08))
                : landed < 0.30 ? 0.82 + (0.28 * ItemCanvas.Back((landed - 0.08) / 0.22))
                : landed < 0.55 ? 1.10 - (0.13 * ItemCanvas.Smooth((landed - 0.30) / 0.25))
                : landed < 0.85 ? 0.97 + (0.03 * ItemCanvas.Smooth((landed - 0.55) / 0.30))
                : 1.0;
            shiver = landed < 0.5 ? (int)Math.Round(Math.Sin(40 * landed) * Math.Exp(-7 * landed) * 1.6) : 0;
            open = landed < 0.10;

            // The swallow: the leather swells and the swelling goes down the bag.
            if (landed is >= 0.04 and < 0.30)
            {
                var u = (landed - 0.04) / 0.26;
                bulgeRow = 5 + (13 * u);
                bulge = 0.20 * Math.Sin(Math.PI * u);
            }

            // The buckle and the stitching take the stone's colours for a moment, flickering through the prism.
            if (landed < 0.6) stitch = prism.Hue((int)(landed * 14));
        }

        var bagX = 1 + ((1 - bagY) * 0.6);

        c.ShadowPatch(Cx, BagBottom + offset, 13 + (int)Math.Round((1 - bagY) * 8));
        Ripple(c, prism, landed);

        // In one score of six the rings leave while the strands are still closing, before the white.
        Rings(c, prism, look, look.Variant == 3 ? since + 0.30 : since);

        var grow = ItemCanvas.Smooth((t - (p.In + (0.55 * p.Anticipation))) / ((0.25 * p.Resonance) + (0.45 * p.Anticipation)));
        var beating = stone && t >= p.ResonanceAt && t < impact;

        if (t < impact) Strands(c, prism, look, p, t, grow, conv, pulse, x, y, front: false);
        if (beating)
        {
            Halo(c, prism, x, y, pulse, beat);

            // The outline of the stone in two or three tones: on the beat it is pale and thick, and it falls through the rest.
            if (pulse > 0.08)
            {
                var tone = pulse > 0.66 ? prism.Pale : pulse > 0.33 ? prism.Light : prism.Base;
                c.IconOutline(item, x, y, sx, sy, tone, pulse > 0.66 ? 2 : 1);
            }
        }

        if (stone) c.Icon(item, x, y, sx, sy, shine, ItemCanvas.White, mix);
        if (t < impact) Strands(c, prism, look, p, t, grow, conv, pulse, x, y, front: true);

        c.Bag(BagLeft, BagBottom + offset, open, bagX, bagY, shiver, stitch, bulgeRow, bulge);

        Landing(c, prism, landed);
        Sparks(c, prism, look, landed);
        Mark(c, prism, mark);
        GlyphAt(c, prism, look, family, look.Variant == 4 ? since + 0.40 : since);
        if (flash > 0) Flash(c, flash);

        // The plate comes in on its own, from the left and through the dither, and is the last thing drawn: nothing covers it.
        var plateIn = Math.Clamp((t - 0.35) / 0.45, 0, 1);
        if (plateIn > 0)
        {
            var kept = c.Alpha;
            c.Alpha = Math.Min(kept, ItemCanvas.Smooth(plateIn));
            c.Plate(item, PlateLeft, PlateTop, -(int)Math.Round((1 - ItemCanvas.Ease(plateIn)) * 6), "MEGAPIEDRA", prism.Base,
                prism.Pale, Badges[family]);
            c.Alpha = kept;
        }
    }

    /// <summary>
    /// The strands of light round the stone. The back half goes behind it and the front half in front, which is all the
    /// depth there is: the nearer part is two cells thick, the farther one a cell and darker.
    /// </summary>
    private static void Strands(ItemCanvas c, ItemPrism prism, Look look, ItemPhases p, double t, double grow, double conv,
        double pulse, double stoneX, double stoneY, bool front)
    {
        // What surrounds the stone breathes with it, since the stone itself does not change size.
        var radius = 18 * grow * (1 - (conv * conv * conv)) * (1 + (0.22 * pulse));
        if (radius < 0.6) return;

        var span = 46 * grow * (1 - (0.9 * conv * conv));
        var tau = Math.Max(0, t - (p.In + (0.55 * p.Anticipation)));
        var phase = look.Spin * ((0.55 * tau) + (0.22 * tau * tau) + (1.6 * conv * conv * conv));

        for (var strand = 0; strand < look.Strands; strand++)
        {
            int lastX = 0, lastY = 0;
            var lastFront = false;

            for (var k = 0; k < Samples; k++)
            {
                var v = k / (Samples - 1.0);
                var angle = 2 * Math.PI * ((look.Turns * v) + phase + (strand / (double)look.Strands));
                var nearer = Math.Cos(angle) > 0;
                var gx = (int)Math.Round(stoneX + (radius * Math.Sin(angle)));
                var gy = (int)Math.Round(stoneY + ((v - 0.5) * span));

                // The first strand is pale and the others run through the colours of the prism along their length.
                var colour = strand == 0 ? (nearer ? prism.Pale : prism.Light) : prism.Hue((k / 3) + (strand * 2) + (int)Math.Floor(tau * 5));
                if (!nearer) colour = ItemCanvas.Lerp(colour, prism.Deep, 0.5);

                // A segment is drawn on the side of the stone it is on, joined to the point before it when both are on that side.
                if (nearer == front)
                {
                    if (k > 0 && lastFront == nearer) Segment(c, lastX, lastY, gx, gy, front ? 2 : 1, colour);
                    else Block(c, gx, gy, front ? 2 : 1, colour);
                }

                lastX = gx;
                lastY = gy;
                lastFront = nearer;
            }
        }
    }

    /// <summary>
    /// The beat of the stone seen from outside: a ring of dots that opens from the stone with every beat, whole at the start
    /// of it and thinning out as it goes, with the gaps on the other half every other beat.
    /// </summary>
    private static void Halo(ItemCanvas c, ItemPrism prism, double x, double y, double pulse, int beat)
    {
        if (pulse < 0.05) return;

        // Two rings of the beat, one behind the other: the first is whole and thick at the peak, so that it carries on its own
        // in a single frame, and both thin out into dashes and dither as the beat is spent.
        var spent = 1 - pulse;
        var peak = spent < 0.35;
        const int Steps = 84;

        for (var ring = 0; ring < 2; ring++)
        {
            var radius = 13 + (ring * 5) + (17 * ItemCanvas.Ease(spent));
            var density = Math.Min(16, (pulse * 24) - (ring * 6));
            if (density <= 0) continue;

            for (var s = 0; s < Steps; s++)
            {
                // Whole on the peak; later the gaps are on one half and on the other every other beat.
                if (!peak && ((s + (beat & 1)) & 3) == 3) continue;

                var f = s / (double)Steps;
                var gx = (int)Math.Round(x + (radius * Math.Cos(2 * Math.PI * f)));
                var gy = (int)Math.Round(y + (radius * Math.Sin(2 * Math.PI * f)));
                if (!c.Contains(gx, gy) || ItemCanvas.Bayer[gy & 3, gx & 3] >= density) continue;

                c.Put(gx, gy, ring == 0 && spent < 0.3 ? prism.Pale : (s & 1) == 0 ? prism.Light : prism.Base);
                if (peak && ring == 0)
                {
                    // A second cell outwards on the peak: a ring two cells thick.
                    Dot(c, gx + Math.Sign(gx - (int)Math.Round(x)), gy + Math.Sign(gy - (int)Math.Round(y)), prism.Light);
                }
            }
        }
    }

    /// <summary>The rings of shock: dashed, each turned a little more, whole while they travel and gone through the dither after.</summary>
    private static void Rings(ItemCanvas c, ItemPrism prism, Look look, double since)
    {
        for (var ring = 0; ring < RingReach.Length; ring++)
        {
            var u = (since - (ring * 0.09)) / 0.85;
            if (u <= 0 || u >= 1) continue;

            var radius = RingReach[ring] * ItemCanvas.Ease(u);
            var density = u < 0.5 ? 16 : (1 - ((u - 0.5) / 0.5)) * 16;
            var colour = ring == 0 ? (u < 0.2 ? ItemCanvas.White : prism.Pale) : ring == 1 ? prism.Light : prism.Base;
            var thickness = 3 - ring;
            var steps = (int)(2 * Math.PI * radius * 1.5) + 8;

            for (var layer = 0; layer < thickness; layer++)
            {
                var r = radius - layer;
                for (var s = 0; s < steps; s++)
                {
                    var f = s / (double)steps;
                    if (((f * look.Segments) + look.Turn + (u * 0.4 * (ring + 1))) % 1.0 >= 0.72) continue;

                    var gx = (int)Math.Round(Cx + (r * Math.Cos(2 * Math.PI * f)));
                    var gy = (int)Math.Round(RestY + (r * Math.Sin(2 * Math.PI * f)));
                    if (!c.Contains(gx, gy) || ItemCanvas.Bayer[gy & 3, gx & 3] >= density) continue;

                    c.Put(gx, gy, colour);
                }
            }
        }
    }

    /// <summary>The second, smaller wave, after the rebound: a ripple on the ground round the bag, flat, in dashes.</summary>
    private static void Ripple(ItemCanvas c, ItemPrism prism, double landed)
    {
        const double Start = 0.12, Length = 0.65;
        var u = (landed - Start) / Length;
        if (u <= 0 || u >= 1) return;

        var rx = 6 + (32 * ItemCanvas.Ease(u));
        var ry = Math.Max(1.2, rx * 0.2);
        var density = u < 0.45 ? 16 : (1 - ((u - 0.45) / 0.55)) * 16;
        var colour = u < 0.25 ? prism.Pale : prism.Light;
        var steps = (int)(2 * Math.PI * rx * 1.2) + 8;

        for (var s = 0; s < steps; s++)
        {
            var f = s / (double)steps;
            if ((f * 12) % 1.0 >= 0.7) continue;

            var gx = (int)Math.Round(Cx + (rx * Math.Cos(2 * Math.PI * f)));
            var gy = (int)Math.Round(BagBottom + 1 + (ry * Math.Sin(2 * Math.PI * f)));
            if (!c.Contains(gx, gy) || ItemCanvas.Bayer[gy & 3, gx & 3] >= density) continue;

            c.Put(gx, gy, colour);
        }
    }

    /// <summary>The bag takes the blow: a dome of dashes opens over its mouth, half the height of a ring.</summary>
    private static void Landing(ItemCanvas c, ItemPrism prism, double landed)
    {
        const double Length = 0.45;
        if (landed < 0 || landed >= Length) return;

        var u = landed / Length;
        var radius = 6 + (18 * ItemCanvas.Ease(u));
        var density = (1 - u) * 16;
        var colour = u < 0.3 ? prism.Pale : prism.Light;
        var steps = (int)(Math.PI * radius * 1.5) + 6;

        for (var s = 0; s <= steps; s++)
        {
            if (s % 4 == 3) continue;

            var angle = Math.PI + (Math.PI * s / steps);
            var gx = (int)Math.Round(Cx + (radius * Math.Cos(angle)));
            var gy = (int)Math.Round(MouthY + 2 + (0.6 * radius * Math.Sin(angle)));
            if (!c.Contains(gx, gy) || ItemCanvas.Bayer[gy & 3, gx & 3] >= density) continue;

            c.Put(gx, gy, colour);
        }
    }

    /// <summary>
    /// The mark of impact, the sound of the blow as the comics draw it: rays from where the stone touches the bottom of the bag
    /// and a tick on each side, two frames, white and then in the stone's colour and smaller.
    /// </summary>
    private static void Mark(ItemCanvas c, ItemPrism prism, int mark)
    {
        if (mark == 0) return;

        const int Y = 90;
        var reach = mark == 1 ? 7 : 5;
        var colour = mark == 1 ? ItemCanvas.White : prism.Pale;

        for (var d = 2; d <= reach; d++)
        {
            var diagonal = (int)Math.Round(d * 0.75);
            Dot(c, Cx + d, Y, colour);
            Dot(c, Cx - d, Y, colour);
            Dot(c, Cx, Y + d, colour);
            Dot(c, Cx, Y - d, colour);
            Dot(c, Cx + diagonal, Y + diagonal, colour);
            Dot(c, Cx - diagonal, Y + diagonal, colour);
            Dot(c, Cx + diagonal, Y - diagonal, colour);
            Dot(c, Cx - diagonal, Y - diagonal, colour);
        }

        for (var k = 10; k <= 13; k++)
        {
            Dot(c, Cx - k, Y, prism.Light);
            Dot(c, Cx + k, Y, prism.Light);
        }
    }

    /// <summary>
    /// The glyph over the stone, one of five families by the hue of the stone (sun, sprout, rings, crystal and the plain double
    /// diamond), traced one cell at a time in the colours of the prism, held for an instant and dissolved through the dither.
    /// </summary>
    private static void GlyphAt(ItemCanvas c, ItemPrism prism, Look look, int family, double since)
    {
        const double Start = 0.07, Trace = 0.25, Hold = 0.55, Gone = 0.95;
        if (since < Start || since >= Gone) return;

        var glyph = Glyphs[family];
        var drawn = (int)(Math.Clamp((since - Start) / Trace, 0, 1) * glyph.Length);
        var density = since < Hold ? 16 : (1 - ItemCanvas.Smooth((since - Hold) / (Gone - Hold))) * 16;
        var first = look.Corner * (glyph.Length / 4);

        for (var n = 0; n < drawn; n++)
        {
            // It starts from the cell the seed says and goes round.
            var (gx, gy) = glyph[(n + first) % glyph.Length];
            gx += Cx;
            gy += 21;

            if (!c.Contains(gx, gy) || ItemCanvas.Bayer[gy & 3, gx & 3] >= density) continue;

            c.Put(gx, gy, n >= drawn - 3 && drawn < glyph.Length ? ItemCanvas.White : prism.Hue(n / 3));
        }
    }

    /// <summary>
    /// The sparks that are left: they scatter from the bag's mouth, fall to the ground, bounce once, settle, and go out one by
    /// one, in the order the seed says.
    /// </summary>
    private static void Sparks(ItemCanvas c, ItemPrism prism, Look look, double tau)
    {
        if (tau < 0) return;

        const int Count = 14;
        const double Gravity = 90;

        for (var i = 0; i < Count; i++)
        {
            // Each one leaves a moment after the one before and goes out in the order the seed says, the last one a second later.
            var own = tau - (i * 0.012);
            var life = 0.60 + (((i * look.SparkStep) % Count) * 0.05);
            if (own < 0 || own >= life) continue;

            var vx = (i - 6.5) * 4.6;
            var vy = -(46 + (((i * 37) % 17) * 1.8));

            // Up and down to the ground, a rebound that gives back two fifths of the speed, and rest.
            var hits = (-vy + Math.Sqrt((vy * vy) + (4 * Gravity * (Ground - MouthY)))) / (2 * Gravity);
            double row;
            if (own < hits)
            {
                row = MouthY + (vy * own) + (Gravity * own * own);
            }
            else
            {
                var back = -0.4 * (vy + (2 * Gravity * hits));
                var u = own - hits;
                row = Math.Min(Ground, Ground + (back * u) + (Gravity * u * u));
            }

            var gx = (int)Math.Round(Cx + (vx * Math.Min(own, 0.55)));
            var gy = (int)Math.Round(row);

            if (!c.Contains(gx, gy)) continue;
            if (own > life * 0.7 && (((int)(own * 30)) & 1) == 0) continue;

            var colour = own < 0.07 ? ItemCanvas.White : (i & 1) == 0 ? prism.Light : prism.Hue(i);
            Block(c, gx, gy, own < life * 0.7 ? 2 : 1, colour);

            // The first ones have arms, like the sparks of the game.
            if (own < 0.12 && i % 4 == 0)
            {
                Dot(c, gx - 2, gy, prism.Pale);
                Dot(c, gx + 3, gy, prism.Pale);
            }
        }
    }

    /// <summary>
    /// Two frames of white: a disc of it, then a broken ring. Each lasts <see cref="FlashPhase"/> seconds, so that at 30 frames
    /// a second the player still sees two.
    /// </summary>
    private static void Flash(ItemCanvas c, int frame)
    {
        var reach = frame == 1 ? 28 : 36;
        var inner = frame == 1 ? 0 : 24;

        for (var gy = (int)RestY - reach; gy <= (int)RestY + reach; gy++)
        {
            for (var gx = Cx - reach; gx <= Cx + reach; gx++)
            {
                var d = Math.Sqrt(((gx - Cx) * (gx - Cx)) + ((gy - RestY) * (gy - RestY)));
                if (d > reach || d < inner || !c.Contains(gx, gy)) continue;
                if (frame == 2 && ItemCanvas.Bayer[gy & 3, gx & 3] >= 9) continue;

                c.Put(gx, gy, ItemCanvas.White);
            }
        }
    }

    /// <summary>The five glyphs, each a list of cells in the order they are traced, round the point (0, 0).</summary>
    private static (int X, int Y)[][] BuildGlyphs()
    {
        static void Diamond(List<(int X, int Y)> cells, int r)
        {
            for (var i = 0; i < r; i++) cells.Add((i, -r + i));
            for (var i = 0; i < r; i++) cells.Add((r - i, i));
            for (var i = 0; i < r; i++) cells.Add((-i, r - i));
            for (var i = 0; i < r; i++) cells.Add((-r + i, -i));
        }

        static void Circle(List<(int X, int Y)> cells, int r)
        {
            var ring = new List<(int X, int Y)>();
            for (var y = -r - 1; y <= r + 1; y++)
            {
                for (var x = -r - 1; x <= r + 1; x++)
                {
                    if (Math.Abs(Math.Sqrt((x * x) + (y * y)) - r) < 0.5) ring.Add((x, y));
                }
            }

            cells.AddRange(ring.OrderBy(cell => Math.Atan2(cell.Y, cell.X)));
        }

        // The sun: a diamond with a ray out of each corner.
        var sun = new List<(int X, int Y)>();
        Diamond(sun, 6);
        for (var d = 8; d <= 12; d++)
        {
            sun.Add((d, d));
            sun.Add((-d, d));
            sun.Add((d, -d));
            sun.Add((-d, -d));
        }

        sun.Add((0, 0));

        // The sprout: a diamond round a stem with two leaves.
        var sprout = new List<(int X, int Y)>();
        Diamond(sprout, 9);
        for (var y = 6; y >= -2; y--) sprout.Add((0, y));
        foreach (var (lx, ly) in new[] { (1, -3), (2, -4), (3, -5), (4, -5), (-1, -3), (-2, -4), (-3, -5), (-4, -5) }) sprout.Add((lx, ly));

        // The rings: two circles and a point.
        var rings = new List<(int X, int Y)>();
        Circle(rings, 9);
        Circle(rings, 4);
        rings.Add((0, 0));

        // The crystal: a diamond with an X inside it.
        var crystal = new List<(int X, int Y)>();
        Diamond(crystal, 9);
        for (var d = 1; d <= 6; d++)
        {
            crystal.Add((d, d));
            crystal.Add((-d, d));
            crystal.Add((d, -d));
            crystal.Add((-d, -d));
        }

        crystal.Add((0, 0));

        // The plain one, for a stone with no colour of its own: a diamond in a diamond, four marks and a point.
        var plain = new List<(int X, int Y)>();
        Diamond(plain, 9);
        Diamond(plain, 4);
        plain.AddRange([(0, -13), (13, 0), (0, 13), (-13, 0), (0, 0)]);

        return [[.. sun], [.. sprout], [.. rings], [.. crystal], [.. plain]];
    }

    /// <summary>The same five, as marks of five by five for the corner of the plate.</summary>
    private static (int X, int Y)[][] BuildBadges() =>
    [
        [(2, 0), (2, 1), (2, 2), (2, 3), (2, 4), (0, 2), (1, 2), (3, 2), (4, 2), (0, 0), (4, 0), (0, 4), (4, 4)],
        [(2, 4), (2, 3), (2, 2), (1, 1), (0, 0), (3, 1), (4, 0)],
        [(1, 0), (2, 0), (3, 0), (0, 1), (4, 1), (0, 2), (4, 2), (0, 3), (4, 3), (1, 4), (2, 4), (3, 4)],
        [(2, 0), (1, 1), (3, 1), (0, 2), (4, 2), (1, 3), (3, 3), (2, 4), (2, 2)],
        [(2, 1), (1, 2), (3, 2), (2, 3), (2, 2)]
    ];

    /// <summary>A number from 0 to 1 that depends on the item's seed and on the thirtieth of a second, so a tremble is never random.</summary>
    private static double Noise(Look look, double t, int salt) =>
        Look.Mix(look.Key + (uint)(((int)Math.Floor(t * 30) * 31) + salt)) / (double)uint.MaxValue;

    /// <summary>What the item's seed decides.</summary>
    private readonly record struct Look(uint Key, double Turns, int Spin, double BeatStart, double BeatEnd, int Segments,
        double Turn, int Corner, int SparkStep, int Strands, int Variant)
    {
        public static Look Of(int seed)
        {
            var key = Mix(unchecked((uint)seed) + 0x9E3779B9);
            var roll = (int)(Mix(key + 10) % 12);
            var variant = roll < 8 ? 0 : roll - 6;

            return new Look(
                key,
                Turns: 1.5 + (0.5 * (Mix(key + 1) % 4)),
                Spin: (Mix(key + 2) & 1) == 0 ? 1 : -1,
                BeatStart: (variant == 5 ? 0.8 : 1.5) + ((Mix(key + 3) % 100) / 100.0 * 0.9),
                BeatEnd: (variant == 5 ? 11.0 : 7.0) + ((Mix(key + 4) % 100) / 100.0 * 2.5),
                Segments: 10 + (int)(Mix(key + 5) % 5),
                Turn: (Mix(key + 6) % 1000) / 1000.0,
                Corner: (int)(Mix(key + 7) % 4),
                SparkStep: (Mix(key + 8) % 3) switch { 0 => 3, 1 => 9, _ => 11 },
                Strands: Mix(key + 9) % 4 == 0 ? 3 : 2,
                Variant: variant);
        }

        public static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7FEB352D;
                x ^= x >> 15;
                x *= 0x846CA68B;
                x ^= x >> 16;
                return x;
            }
        }
    }

}
