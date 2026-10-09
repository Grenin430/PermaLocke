namespace PermaLocke.App.Views;

/// <summary>
/// Numbers that depend on an item's seed and on nothing else (2026-10-09): the same seed always gives the same ones, so any
/// frame of any animation can be made again. Nothing here is random.
/// </summary>
public readonly struct ItemSeed
{
    private readonly uint _key;

    public ItemSeed(int seed)
    {
        _key = Mix(unchecked((uint)seed) + 0x9E3779B9);
    }

    public uint Key => _key;

    /// <summary>A number from 0 to 1 for a purpose, told apart by its salt.</summary>
    public double Unit(int salt) => Mix(_key + unchecked((uint)salt)) / (double)uint.MaxValue;

    /// <summary>A whole number from 0 to <paramref name="count"/> - 1 for a purpose.</summary>
    public int Pick(int salt, int count) => (int)(Mix(_key + unchecked((uint)salt)) % (uint)count);

    /// <summary>True for one seed in <paramref name="outOf"/>.</summary>
    public bool OneIn(int salt, int outOf) => Pick(salt, outOf) == 0;

    /// <summary>A number from 0 to 1 that changes thirty times a second: a tremble that is not random.</summary>
    public double Noise(double t, int salt) =>
        Mix(_key + unchecked((uint)(((int)Math.Floor(t * 30) * 31) + salt))) / (double)uint.MaxValue;

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

/// <summary>
/// The colours of a scene (2026-10-09): the item's own colour in four lightnesses and five turned a little round the colour
/// wheel. One family of colours per item, so that a scene is coherent whatever it is.
/// </summary>
public readonly struct ItemPrism
{
    public readonly uint Deep;
    public readonly uint Base;
    public readonly uint Light;
    public readonly uint Pale;
    private readonly uint _h0;
    private readonly uint _h1;
    private readonly uint _h2;
    private readonly uint _h3;
    private readonly uint _h4;

    /// <param name="tint">Opaque BGRA, from <see cref="ItemTint.Of"/>, or 0 for the fallback.</param>
    /// <param name="fallback">What to use when the item has no colour of its own.</param>
    public ItemPrism(uint tint, uint fallback)
    {
        var colour = tint != 0 ? tint : fallback;
        Deep = ItemTint.Shade(colour, 0, 0.28);
        Base = ItemTint.Shade(colour, 0, 0.52);
        Light = ItemTint.Shade(colour, 0, 0.72);
        Pale = ItemTint.Shade(colour, 0, 0.90, 0.6);
        _h0 = ItemTint.Shade(colour, -60, 0.62);
        _h1 = ItemTint.Shade(colour, -30, 0.68);
        _h2 = ItemTint.Shade(colour, 0, 0.74);
        _h3 = ItemTint.Shade(colour, 30, 0.68);
        _h4 = ItemTint.Shade(colour, 60, 0.62);
    }

    public uint Hue(int i) => (((i % 5) + 5) % 5) switch { 0 => _h0, 1 => _h1, 2 => _h2, 3 => _h3, _ => _h4 };
}

/// <summary>
/// The small drawing pieces the styles share (2026-10-09): a cell that leaves the scene is not drawn instead of being asked,
/// and every piece is a loop over game cells, with no allocation.
/// </summary>
public static class ItemFx
{
    /// <summary>
    /// How long each of the frames of a flash or a click lasts, whatever the frame rate: a little more than one frame at 30 a
    /// second, so that two are seen at 30, 45 and 60.
    /// </summary>
    public const double FlashPhase = 0.035;

    /// <summary>A cell that is left off when it falls outside of the scene, instead of being asked of the canvas.</summary>
    public static void Dot(ItemCanvas c, int gx, int gy, uint colour)
    {
        if (c.Contains(gx, gy)) c.Put(gx, gy, colour);
    }

    public static void Block(ItemCanvas c, int gx, int gy, int size, uint colour)
    {
        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
            {
                Dot(c, gx + dx, gy + dy, colour);
            }
        }
    }

    /// <summary>A straight line of cells between two, as thick as asked.</summary>
    public static void Segment(ItemCanvas c, int x0, int y0, int x1, int y1, int size, uint colour)
    {
        var steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (var i = 0; i <= steps; i++)
        {
            var f = steps == 0 ? 0 : i / (double)steps;
            Block(c, (int)Math.Round(x0 + ((x1 - x0) * f)), (int)Math.Round(y0 + ((y1 - y0) * f)), size, colour);
        }
    }

    /// <summary>
    /// A ring of dashes, round or flattened, whole or only an arc, faded through the dither.
    /// </summary>
    /// <param name="density">0 to 16: how many of every sixteen cells of the dither are on.</param>
    /// <param name="segments">How many dashes it is made of; 0 for a continuous ring.</param>
    /// <param name="turn">How far round the dashes are turned, in whole turns.</param>
    /// <param name="flatten">Height over width: 1 is a circle, 0.25 a ripple on the ground.</param>
    /// <param name="from">Start of the arc, in turns (0 is to the right, 0.25 down).</param>
    /// <param name="to">End of the arc, in turns.</param>
    public static void Ring(ItemCanvas c, double cx, double cy, double radius, uint colour, double density, int thickness = 1,
        int segments = 0, double turn = 0, double flatten = 1, double from = 0, double to = 1)
    {
        if (radius < 0.5 || density <= 0) return;

        for (var layer = 0; layer < thickness; layer++)
        {
            var r = radius - layer;
            var steps = (int)(2 * Math.PI * Math.Max(r, 1) * 1.5) + 8;

            for (var s = 0; s < steps; s++)
            {
                var f = from + ((to - from) * s / steps);
                if (segments > 0 && ((f * segments) + turn) % 1.0 >= 0.72) continue;

                var gx = (int)Math.Round(cx + (r * Math.Cos(2 * Math.PI * f)));
                var gy = (int)Math.Round(cy + (r * flatten * Math.Sin(2 * Math.PI * f)));
                if (!c.Contains(gx, gy) || ItemCanvas.Bayer[gy & 3, gx & 3] >= density) continue;

                c.Put(gx, gy, colour);
            }
        }
    }

    /// <summary>A plus sign of cells, with a brighter centre: the glint of the game.</summary>
    public static void Cross(ItemCanvas c, int x, int y, int arm, uint colour, uint centre = 0)
    {
        for (var d = 1; d <= arm; d++)
        {
            Dot(c, x + d, y, colour);
            Dot(c, x - d, y, colour);
            Dot(c, x, y + d, colour);
            Dot(c, x, y - d, colour);
        }

        Dot(c, x, y, centre != 0 ? centre : colour);
    }

    /// <summary>A star: the plus and the four diagonals, shorter.</summary>
    public static void Star(ItemCanvas c, int x, int y, int arm, uint colour, uint centre = 0)
    {
        Cross(c, x, y, arm, colour, centre);
        var d = (int)Math.Round(arm * 0.7);
        for (var k = 1; k <= d; k++)
        {
            Dot(c, x + k, y + k, colour);
            Dot(c, x - k, y + k, colour);
            Dot(c, x + k, y - k, colour);
            Dot(c, x - k, y - k, colour);
        }
    }

    /// <summary>A flat disc of cells, through the dither.</summary>
    public static void Disc(ItemCanvas c, double cx, double cy, double radius, uint colour, double density = 16)
    {
        for (var gy = (int)Math.Floor(cy - radius); gy <= (int)Math.Ceiling(cy + radius); gy++)
        {
            for (var gx = (int)Math.Floor(cx - radius); gx <= (int)Math.Ceiling(cx + radius); gx++)
            {
                var d = Math.Sqrt(((gx - cx) * (gx - cx)) + ((gy - cy) * (gy - cy)));
                if (d > radius || !c.Contains(gx, gy) || ItemCanvas.Bayer[gy & 3, gx & 3] >= density) continue;

                c.Put(gx, gy, colour);
            }
        }
    }

    /// <summary>A patch of ground in dither, in a colour: the cheap stage under the bag that every category has.</summary>
    public static void Ground(ItemCanvas c, double cx, int y, double rx, double ry, uint colour, double density = 9)
    {
        for (var gy = (int)Math.Floor(y - ry); gy <= (int)Math.Ceiling(y + ry); gy++)
        {
            for (var gx = (int)Math.Floor(cx - rx); gx <= (int)Math.Ceiling(cx + rx); gx++)
            {
                var d = (((gx + 0.5 - cx) / rx) * ((gx + 0.5 - cx) / rx)) + (((gy + 0.5 - y) / ry) * ((gy + 0.5 - y) / ry));
                if (d > 1 || !c.Contains(gx, gy)) continue;
                if (ItemCanvas.Bayer[gy & 3, gx & 3] < density * (1 - d)) c.Put(gx, gy, colour);
            }
        }
    }

    /// <summary>Under-damped spring: 1 at rest, <c>1 - amplitude</c> at the start, overshooting and settling.</summary>
    public static double Spring(double tau, double amplitude, double decay, double speed) =>
        tau <= 0 ? 1 - amplitude : 1 - (amplitude * Math.Exp(-decay * tau) * Math.Cos(speed * tau));

    /// <summary>Where the bag stands when the scene is coming in: it rises six rows with the dither.</summary>
    public static int Rise(double t, double fadeIn) =>
        t < fadeIn ? (int)Math.Round((1 - ItemCanvas.Ease(t / fadeIn)) * 6) : 0;

    /// <summary>
    /// The plate, coming in from the left through the dither at a moment and being the last thing drawn so that nothing covers it.
    /// </summary>
    public static void PlateIn(ItemCanvas c, ItemScene.Item item, double t, double start, int left, int top, string? caption = null,
        uint accent = 0, uint accentLight = 0, IReadOnlyList<(int X, int Y)>? badge = null, bool ornate = false)
    {
        var u = Math.Clamp((t - start) / 0.4, 0, 1);
        if (u <= 0) return;

        var kept = c.Alpha;
        c.Alpha = Math.Min(kept, ItemCanvas.Smooth(u));
        c.Plate(item, left, top, -(int)Math.Round((1 - ItemCanvas.Ease(u)) * 6), caption, accent, accentLight, badge, ornate);
        c.Alpha = kept;
    }
}
