using PermaLocke.App.Views;

namespace PermaLocke.PixelCheck;

/// <summary>
/// Prototypes for the rework of the item animation (2026-10-09): an item just picked up, bought or given goes into the bag.
/// Six looks, each a 176 x 72 scene in pixels of the game, drawn at four moments of the same timeline the app uses
/// (<see cref="ItemTimeline"/>), next to the animation as it is today. Nothing here is shipped: it exists to choose.
/// </summary>
public sealed partial class NotificationPrototypes
{
    private sealed record ItemSample(string Name, string Amount, bool Potion);

    private const int IW = 176, IH = 72;

    private static readonly ItemSample[] ItemSamples = [new("MT85 - VUELO", "NUEVO", false), new("POCIÓN", "X3", true)];

    /// <summary>Coming out, resting, dropping in and fading: the four moments each look is drawn at.</summary>
    private static readonly double[] ItemMoments = [0.5, 1.1, 1.7, 2.1];

    private delegate Pix ItemLook(ItemSample item, double t);

    private static readonly (string Name, string Title, ItemLook Look)[] ItemLooks =
    [
        ("A", "COFRE DEL TESORO", ItemChest),
        ("B", "MOCHILA DE EXPLORADOR", ItemPack),
        ("C", "PLACA DE LATON", ItemBrass),
        ("D", "PERGAMINO Y LACRE", ItemScroll),
        ("E", "POKE BALL QUE ABRE", ItemBall),
        ("F", "CASILLAS DEL INVENTARIO", ItemSlots)
    ];

    private static readonly int[,] ItemBayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    // ---------------------------------------------------------------- shared kit

    private static double Ease3(double p) => 1 - Math.Pow(1 - Math.Clamp(p, 0, 1), 3);

    private static double Smooth(double p)
    {
        p = Math.Clamp(p, 0, 1);
        return p * p * (3 - (2 * p));
    }

    private static int SmallWidth(string text) => text.Length == 0 ? 0 : text.Sum(c => c == ' ' ? 3 : 4) - 1;

    /// <summary>0 before the item comes out, 1 once it rests: the climb out of the bag.</summary>
    private static double Rise(double t) => Ease3((t - ItemTimeline.Pop) / (ItemTimeline.Shown - ItemTimeline.Pop));

    /// <summary>0 while it rests, up to 1 as it lands inside.</summary>
    private static double Fall(double t) => Math.Clamp((t - ItemTimeline.Drop) / (ItemTimeline.Inside - ItemTimeline.Drop), 0, 1);

    private static bool Out(double t) => t >= ItemTimeline.Pop && t < ItemTimeline.Inside;

    private static Pix ItemFade(Pix p, double t)
    {
        var alpha = t < ItemTimeline.In ? t / ItemTimeline.In
            : t > ItemTimeline.Out ? 1 - ((t - ItemTimeline.Out) / ItemTimeline.Fade)
            : 1;
        if (alpha >= 1) return p;
        for (var y = 0; y < p.H; y++)
        {
            for (var x = 0; x < p.W; x++)
            {
                if (ItemBayer[y & 3, x & 3] < alpha * 16) continue;
                var i = ((y * p.W) + x) * 4;
                p.B[i] = p.B[i + 1] = p.B[i + 2] = p.B[i + 3] = 0;
            }
        }

        return p;
    }

    /// <summary>The cartridge icon of the item, drawn from scratch at 16 x 16: a TM disc or a potion.</summary>
    private static Pix ItemIcon(ItemSample item)
    {
        var p = new Pix(16, 16);
        if (item.Potion)
        {
            p.Rect(6, 0, 4, 2, 0x8A5A2A);
            p.Rect(7, 0, 2, 1, 0xC08A4A);
            p.Rect(6, 2, 4, 4, 0xDDEBF4);
            p.Rect(6, 2, 1, 4, 0xFFFFFF);
            for (var y = 5; y < 16; y++)
            {
                var half = y < 7 ? 3 + (y - 5) : y > 13 ? 7 - (y - 13) * 2 : 6;
                for (var x = 8 - half; x < 8 + half; x++)
                {
                    var edge = x == 8 - half || x == 8 + half - 1 || y == 15;
                    var liquid = y >= 8;
                    p.Set(x, y, edge ? 0x2A1840 : liquid ? Mix(0xE070FF, 0x7A28C0, (y - 8) / 8.0) : 0xDDEBF4);
                }
            }

            p.Rect(4, 9, 1, 3, 0xFFFFFF, 220);
            p.Set(5, 8, 0xFFFFFF, 220);
            p.Rect(9, 12, 3, 1, 0xFFB0FF);
            return p;
        }

        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var d = Math.Sqrt(Math.Pow(x - 7.5, 2) + Math.Pow(y - 7.5, 2));
                if (d > 7.6) continue;
                uint c;
                if (d > 6.6) c = 0x3A2040;
                else if (d < 1.9) c = 0x2A1838;
                else if (d < 3.4) c = 0xFFF6FA;
                else if (d < 5.0) c = (x + y) % 5 == 0 ? 0xFF8CB8u : 0xE0508Cu;
                else c = Mix(0xFFEAF2, 0xD89AB8, (x + y) / 30.0);
                p.Set(x, y, c);
            }
        }

        for (var a = 3.4; a < 4.6; a += 0.4) p.Set((int)(7.5 - (a * 1.2)), (int)(7.5 - (a * 1.0)), 0xFFFFFF);
        p.Set(3, 3, 0xFFFFFF);
        p.Set(4, 2, 0xFFFFFF);
        return p;
    }

    private static void PutIcon(Pix p, Pix icon, double cx, double cy, double sx, double sy, double shine = -1)
    {
        if (sx <= 0.05 || sy <= 0.05) return;
        var w = icon.W * sx;
        var h = icon.H * sy;
        var left = cx - (w / 2);
        var top = cy - (h / 2);
        for (var gy = (int)Math.Floor(top); gy < (int)Math.Ceiling(top + h); gy++)
        {
            for (var gx = (int)Math.Floor(left); gx < (int)Math.Ceiling(left + w); gx++)
            {
                var ix = (int)Math.Floor((gx + 0.5 - left) / sx);
                var iy = (int)Math.Floor((gy + 0.5 - top) / sy);
                if (ix < 0 || iy < 0 || ix >= icon.W || iy >= icon.H) continue;
                var i = ((iy * icon.W) + ix) * 4;
                if (icon.B[i + 3] < 128) continue;
                var c = Rgb(icon.B[i + 2], icon.B[i + 1], icon.B[i]);
                if (shine is > 0 and < 1.3 && Math.Abs(((ix + iy) / (double)(icon.W + icon.H)) - shine) < 0.08) c = Mix(c, 0xFFFFFF, 0.7);
                p.Set(gx, gy, c);
            }
        }
    }

    /// <summary>A soft glow with real transparency: strongest in the middle, falling off with the square of the distance.</summary>
    private static void Glow(Pix p, double cx, double cy, double rx, double ry, uint colour, int strongest)
    {
        for (var y = (int)(cy - ry) - 1; y <= cy + ry + 1; y++)
        {
            for (var x = (int)(cx - rx) - 1; x <= cx + rx + 1; x++)
            {
                var d = Math.Sqrt(Math.Pow((x + 0.5 - cx) / rx, 2) + Math.Pow((y + 0.5 - cy) / ry, 2));
                if (d < 1) p.Set(x, y, colour, (int)(strongest * (1 - d) * (1 - d)));
            }
        }
    }

    private static void Star(Pix p, int x, int y, int arm, uint colour, int alpha = 255)
    {
        p.Set(x, y, 0xFFFFFF, alpha);
        for (var k = 1; k <= arm; k++)
        {
            p.Set(x - k, y, colour, alpha);
            p.Set(x + k, y, colour, alpha);
            p.Set(x, y - k, colour, alpha);
            p.Set(x, y + k, colour, alpha);
        }
    }

    /// <summary>Four-point sparks thrown out of a point, fading as they go.</summary>
    private static void Sparks(Pix p, double cx, double cy, double since, int count, double reach, uint colour)
    {
        if (since < 0 || since > 0.5) return;
        for (var i = 0; i < count; i++)
        {
            var a = (i / (double)count * Math.PI * 2) + 0.6;
            var r = 3 + (reach * Ease3(since / 0.45));
            Star(p, (int)Math.Floor(cx + (Math.Cos(a) * r)), (int)Math.Floor(cy + (Math.Sin(a) * r * 0.8)), since < 0.2 || i % 2 == 0 ? 1 : 0, colour);
        }
    }

    /// <summary>A soft dithered ellipse, dense in the middle: shadows, light and glow.</summary>
    private static void Haze(Pix p, double cx, double cy, double rx, double ry, uint colour, double strength = 1)
    {
        for (var y = (int)(cy - ry) - 1; y <= cy + ry + 1; y++)
        {
            for (var x = (int)(cx - rx) - 1; x <= cx + rx + 1; x++)
            {
                var d = Math.Pow((x + 0.5 - cx) / rx, 2) + Math.Pow((y + 0.5 - cy) / ry, 2);
                if (d <= 1 && ItemBayer[y & 3 & 3, x & 3] < (1 - d) * 16 * strength) p.Set(x, y, colour);
            }
        }
    }

    private static void Text2(Pix p, string text, int x, int y, uint ink, uint? shadow = null)
    {
        if (shadow is { } s) p.Small(text, x + 1, y + 1, s);
        p.Small(text, x, y, ink);
    }

    /// <summary>The plate beside the scene in the app's own dark look: gold edge, name, where it went and how many.</summary>
    private static void DarkTag(Pix p, int x, int y, ItemSample s, uint accent, uint accentLight, int slide = 0)
    {
        var w = Math.Max(SmallWidth(s.Name), SmallWidth("A LA MOCHILA") + 4 + SmallWidth(s.Amount)) + 12;
        x += slide;
        p.Notched(x + 1, y + 1, w, 24, 0x14101C, 1);
        p.Notched(x, y, w, 24, 0x0B0910, 1);
        p.Notched(x + 1, y + 1, w - 2, 22, 0x1C1530, 1);
        p.HLine(x + 3, y + 1, w - 6, 0x3A2E5C);
        p.HLine(x + 3, y + 22, w - 6, 0x100C1C);
        p.Rect(x + 1, y + 1, 2, 22, accent);
        p.Rect(x + 1, y + 1, 2, 3, accentLight);
        Text2(p, s.Name, x + 6, y + 5, 0xF4F0FF, 0x100C1C);
        Text2(p, "A LA MOCHILA", x + 6, y + 14, 0x9A90B8, 0x100C1C);
        Text2(p, s.Amount, x + 6 + SmallWidth("A LA MOCHILA") + 4, y + 14, 0xFFDC7A, 0x100C1C);
    }

    private static double PlateSlide(double t) => (1 - Ease3((t - 0.2) / 0.3)) * -9;

    // ---------------------------------------------------------------- A: the treasure chest

    private static Pix ItemChest(ItemSample s, double t)
    {
        var p = new Pix(IW, IH);
        const int cx = 28;
        var opened = Smooth((t - ItemTimeline.Pop) / 0.18) * (1 - Smooth((t - ItemTimeline.Inside - 0.02) / 0.1));
        var fall = Fall(t);
        var hop = t is >= ItemTimeline.Inside and < ItemTimeline.Inside + 0.18 ? (int)Math.Round(Math.Sin((t - ItemTimeline.Inside) / 0.18 * Math.PI) * 3) : 0;

        // Shadow and the light pouring out of the open lid.
        Haze(p, cx, 66, 21, 3.2, 0x0F0C16);
        if (opened > 0.05)
        {
            for (var y = 40; y > 4; y--)
            {
                var reach = (40 - y) / 36.0;
                var half = 4 + (int)(reach * 9);
                for (var x = cx - half; x <= cx + half; x++)
                {
                    var edge = 1 - (Math.Abs(x - cx) / (double)(half + 1));
                    if (ItemBayer[y & 3, x & 3] < edge * (1 - reach) * 9 * opened) p.Set(x, y, 0xFFE9A0);
                }
            }
        }

        var top = 41 - hop;
        // Open lid, standing behind: its red lining facing us.
        if (opened > 0.05)
        {
            var lift = (int)(opened * 17);
            var y0 = top - 5 - lift;
            var h = 17 + (int)(opened * 3);
            for (var yy = 0; yy < h; yy++)
            {
                // Narrower towards the top: the lid is leaning back.
                var inset = (int)((h - 1 - yy) * 0.12) + (yy < 2 ? 2 - yy : 0);
                for (var xx = inset; xx < 30 - inset; xx++)
                {
                    var rim = yy < 2 || xx < inset + 2 || xx >= 30 - inset - 2 || yy >= h - 3;
                    var lining = Mix(0x9A2E40, 0x5A1422, yy / (double)h);
                    p.Set(cx - 15 + xx, y0 + yy, rim ? (yy == 0 || xx == inset || xx == 29 - inset ? 0x1E1008u : Mix(0xB07434, 0x7A4A1C, yy / (double)h)) : lining);
                }
            }

            for (var x = cx - 10; x < cx + 11; x += 4) p.Set(x, y0 + 6, 0xE0A83A);
            p.Rect(cx - 6, y0 + 10, 13, 1, 0xE0A83A, 140);
        }

        // Body: planks, iron straps, rivets and the lock.
        p.Notched(cx - 18, top, 36, 24, 0x1E1008, 2);
        for (var y = 0; y < 22; y++)
        {
            var plank = y / 6;
            for (var x = 0; x < 34; x++)
            {
                var c = Mix(0xB07434, 0x7A4A1C, x / 34.0);
                if (y % 6 == 5) c = Darker(c, 0.32);
                else if (y % 6 == 0) c = Lighter(c, 0.14);
                if ((StableHash($"{x}:{y}:{plank}") & 31) == 0) c = Darker(c, 0.2);
                if (x < 2) c = Lighter(c, 0.18);
                if ((x == 0 || x == 33) && (y == 0 || y == 21)) continue;
                p.Set(cx - 17 + x, top + 1 + y, c);
            }
        }

        foreach (var sx in new[] { cx - 14, cx + 9 })
        {
            p.Rect(sx, top, 5, 24, 0x2A2E3C);
            p.Rect(sx + 1, top + 1, 3, 22, 0x6C7488);
            p.Rect(sx + 1, top + 1, 1, 22, 0xA8B0C4);
            for (var ry = 4; ry < 22; ry += 7) p.Set(sx + 2, top + ry, 0x2A2E3C);
        }

        p.Rect(cx - 4, top + 5, 9, 9, 0x4A3010);
        p.Rect(cx - 3, top + 6, 7, 7, 0xE0A83A);
        p.Rect(cx - 3, top + 6, 7, 1, 0xFFEAA0);
        p.Rect(cx, top + 8, 1, 3, 0x3A2008);
        p.Set(cx - 1, top + 8, 0x3A2008);
        p.Set(cx + 1, top + 8, 0x3A2008);

        if (opened < 0.95)
        {
            // Closed lid, domed, with the straps running over it.
            var y0 = top - 11 + (int)(opened * 6);
            p.Notched(cx - 18, y0, 36, 12, 0x1E1008, 4);
            for (var y = 0; y < 10; y++)
            {
                var inset = y < 3 ? 3 - y : 0;
                for (var x = inset; x < 34 - inset; x++) p.Set(cx - 17 + x, y0 + 1 + y, Mix(Lighter(0xB07434, 0.2), 0x7A4A1C, (y / 10.0) * 0.8 + (x / 34.0) * 0.2));
            }

            foreach (var sx in new[] { cx - 14, cx + 9 }) p.Rect(sx + 1, y0 + 1, 3, 10, 0x6C7488);
            p.Rect(cx - 4, y0 + 8, 9, 3, 0xE0A83A);
        }
        else
        {
            p.Rect(cx - 16, top - 1, 32, 3, 0x1A0C08);
        }

        // The item climbs out, rests with a glint, and drops back in.
        if (Out(t))
        {
            var rise = Rise(t);
            var y = t < ItemTimeline.Shown ? 40 - (22 * rise) - (Math.Sin(rise * Math.PI) * 5)
                : t < ItemTimeline.Drop ? 18 + Math.Round(Math.Sin((t - ItemTimeline.Shown) * 4.5) * 1.5)
                : 18 - (Math.Sin(fall * Math.PI) * 3) + (22 * fall * fall);
            var scale = t < ItemTimeline.Shown ? 0.4 + (0.6 * rise) : 1 - (fall * 0.6);
            PutIcon(p, ItemIcon(s), cx, y, scale, scale, (t - ItemTimeline.Shown + 0.05) / 0.35);
            Sparks(p, cx, 22, t - ItemTimeline.Pop, 6, 16, 0xFFDC7A);
        }

        for (var i = 0; i < 7 && opened > 0.3; i++)
        {
            var phase = (t * 0.7) + (i * 0.37);
            var my = 36 - (int)((phase % 1.0) * 30);
            p.Set(cx - 8 + ((i * 5) % 17) + (int)Math.Round(Math.Sin(phase * 6)), my, 0xFFF0B0, (int)(200 * (1 - (phase % 1.0))));
        }

        Sparks(p, cx, 38, t - ItemTimeline.Inside, 7, 12, 0xFFDC7A);

        // The brass plate, slid in from the left.
        var width = Math.Max(SmallWidth(s.Name), SmallWidth("A LA MOCHILA") + 4 + SmallWidth(s.Amount)) + 16;
        var px = 56 + (int)PlateSlide(t);
        Brass(p, px, 22, width, 27);
        p.Small(s.Name, px + 9, 26, 0x3A1E08);
        p.Small("A LA MOCHILA", px + 9, 35, 0x6A3E10);
        Chip(p, px + 9 + SmallWidth("A LA MOCHILA") + 3, 34, s.Amount, 0x2A1A08, 0xFFDC7A, 0x6A4A18);
        return ItemFade(p, t);
    }

    // ---------------------------------------------------------------- B: the explorer's pack

    private static void DrawPack(Pix p, int x, int bottom, bool open, int squash)
    {
        var top = bottom - 28 + squash;
        // Shoulder straps and the carrying loop.
        p.Rect(x + 8, top - 4, 2, 5, 0x2A1810);
        p.Rect(x + 20, top - 4, 2, 5, 0x2A1810);
        p.Rect(x + 8, top - 5, 14, 2, 0x2A1810);
        p.Rect(x + 9, top - 4, 12, 1, 0x7A4A28);
        // Body.
        p.Notched(x, top, 30, 28 - squash, 0x14281E, 4);
        for (var y = 0; y < 26 - squash; y++)
        {
            for (var x2 = 0; x2 < 28; x2++)
            {
                var inset = y < 3 ? 3 - y : 0;
                if (x2 < inset || x2 >= 28 - inset) continue;
                var c = Mix(0x3F8A6A, 0x1F5A44, y / 26.0);
                if (x2 < 2) c = Lighter(c, 0.22);
                if (x2 > 25) c = Darker(c, 0.25);
                if ((x2 + y) % 6 == 0 && y > 10) c = Darker(c, 0.1);
                p.Set(x + 1 + x2, top + 1 + y, c);
            }
        }

        // Front pocket with its zip.
        p.Notched(x + 4, top + 14 - squash, 22, 11, 0x14281E, 2);
        p.Notched(x + 5, top + 15 - squash, 20, 9, 0x2C7254, 2);
        for (var zx = x + 6; zx < x + 24; zx += 2) p.Set(zx, top + 17 - squash, 0xD8E8D0);
        p.Rect(x + 22, top + 18 - squash, 2, 3, 0xE0A83A);
        // Flap.
        if (open)
        {
            p.Rect(x + 2, top + 1, 26, 5, 0x0C1812);
            p.Rect(x + 4, top + 2, 22, 2, 0x1A3A2C);
            p.Notched(x + 2, top - 10, 26, 9, 0x4A200C, 2);
            p.Notched(x + 3, top - 9, 24, 7, 0xC8602C, 2);
            for (var fx = x + 5; fx < x + 26; fx += 2) p.Set(fx, top - 3, 0xF2A064);
        }
        else
        {
            p.Notched(x + 1, top, 28, 13, 0x4A200C, 3);
            p.Notched(x + 2, top + 1, 26, 11, 0xC8602C, 3);
            p.HLine(x + 4, top + 2, 22, 0xF2A064);
            for (var fx = x + 4; fx < x + 26; fx += 2) p.Set(fx, top + 10, 0xF2A064);
            p.Rect(x + 12, top + 7, 6, 6, 0x6A4A10);
            p.Rect(x + 13, top + 8, 4, 4, 0xE0A83A);
            p.Set(x + 14, top + 9, 0xFFEAA0);
            p.Set(x + 14, top + 10, 0x3A2008);
        }
    }

    private static Pix ItemPack(ItemSample s, double t)
    {
        var p = new Pix(IW, IH);
        const int bx = 8;
        const int bottom = 62;
        var open = t >= ItemTimeline.Pop - 0.05 && t < ItemTimeline.Inside + 0.05;
        var squash = t is >= ItemTimeline.Inside and < ItemTimeline.Inside + 0.16 ? (int)Math.Round(Math.Sin((t - ItemTimeline.Inside) / 0.16 * Math.PI) * 3) : 0;
        Haze(p, bx + 15, bottom + 1, 17, 3, 0x0F0C16);
        DrawPack(p, bx, bottom, open, squash);

        // The item flies out of the open flap in an arc, rests, and flies back.
        if (Out(t))
        {
            var mouth = (X: bx + 15.0, Y: 36.0);
            var rest = (X: 54.0, Y: 18.0);
            double f = t < ItemTimeline.Shown ? Rise(t) : t < ItemTimeline.Drop ? 1 : 1 - Fall(t);
            var arc = Math.Sin(f * Math.PI) * 12;
            var x = mouth.X + ((rest.X - mouth.X) * f);
            var y = mouth.Y + ((rest.Y - mouth.Y) * f) - arc;
            var scale = 0.4 + (0.6 * f);
            if (t >= ItemTimeline.Shown && t < ItemTimeline.Drop) y += Math.Round(Math.Sin((t - ItemTimeline.Shown) * 4.5) * 1.5);
            // A trail of dust behind it while it moves.
            if (f is > 0.02 and < 0.98)
            {
                for (var k = 1; k <= 4; k++)
                {
                    var ff = Math.Clamp(f + (t < ItemTimeline.Drop ? -k * 0.07 : k * 0.07), 0, 1);
                    p.Set((int)(mouth.X + ((rest.X - mouth.X) * ff)), (int)(mouth.Y + ((rest.Y - mouth.Y) * ff) - (Math.Sin(ff * Math.PI) * 12)), 0xFFE9A0, 200 - (k * 40));
                }
            }

            // A ring where it hangs, pulsing while it rests.
            if (t is >= ItemTimeline.Shown and < ItemTimeline.Drop)
            {
                var pulse = ((t - ItemTimeline.Shown) * 1.4) % 1.0;
                for (var a = 0; a < 40; a++)
                {
                    var ang = a / 40.0 * Math.PI * 2;
                    p.Set((int)(rest.X + (Math.Cos(ang) * (10 + (pulse * 6)))), (int)(rest.Y + (Math.Sin(ang) * (10 + (pulse * 6)) * 0.8)), 0xFFE9A0, (int)(150 * (1 - pulse)));
                }
            }

            PutIcon(p, ItemIcon(s), x, y, scale, scale, (t - ItemTimeline.Shown + 0.05) / 0.35);
        }

        Sparks(p, bx + 15, 30, t - ItemTimeline.Inside, 6, 13, 0xFFDC7A);

        // The pocket tabs of the game's bag over the plate, the item's own one lit.
        string[] tabs = ["OBJ", "MED", "MT", "BAY", "CLV"];
        var current = s.Potion ? 1 : 2;
        var tx = 70 + (int)PlateSlide(t);
        for (var i = 0; i < tabs.Length; i++)
        {
            var lit = i == current;
            var tw = SmallWidth(tabs[i]) + 4;
            p.Notched(tx, 15, tw, 8, lit ? 0xFFEAA0u : 0x0B0910u, 1);
            p.Notched(tx + 1, 16, tw - 2, 6, lit ? 0xE0A83Au : 0x2A2244u, 1);
            p.Small(tabs[i], tx + 2, 16, lit ? 0x2A1808u : 0x9A90B8u);
            tx += tw + 1;
        }

        DarkTag(p, 70, 25, s, 0x3F8A6A, 0xA8E8C8, (int)PlateSlide(t));
        return ItemFade(p, t);
    }

    // ---------------------------------------------------------------- C: the brass plaque, like the level cap

    private static Pix ItemBrass(ItemSample s, double t)
    {
        var p = new Pix(IW, IH);
        var slide = (int)PlateSlide(t) * 2;
        var x = 8 + slide;
        var width = Math.Max(SmallWidth(s.Name), SmallWidth("A LA MOCHILA") + 4 + SmallWidth(s.Amount)) + 82;
        // Walnut frame behind the plate, like the level cap's board.
        p.Notched(x - 2, 11, width + 4, 48, 0x1A0C06, 3);
        p.Notched(x - 1, 12, width + 2, 46, 0x5A3A22, 3);
        for (var yy = 13; yy < 57; yy++)
        {
            for (var xx = x; xx < x + width; xx++)
            {
                if ((StableHash($"w{xx}:{yy}") & 15) == 0) p.Set(xx, yy, 0x3E2614, 150);
            }
        }

        Brass(p, x + 3, 15, width - 6, 40);

        // The window holding the item: walnut socket, lit from above.
        const int sx = 14;
        var wx = sx + slide;
        p.Notched(wx, 20, 30, 30, 0x1A0C06, 3);
        p.Notched(wx + 1, 21, 28, 28, 0x6A4424, 3);
        p.Notched(wx + 3, 23, 24, 24, 0x120C18, 2);
        p.VGrad(wx + 4, 24, 22, 22, 0x2A2038, 0x120C18);
        Glow(p, wx + 15, 33, 12, 16, 0xFFE9A0, 120);
        Haze(p, wx + 15, 43, 9, 3, 0x000000);
        var inside = t >= ItemTimeline.Pop && t < ItemTimeline.Drop;
        if (inside || (t >= ItemTimeline.Drop && t < ItemTimeline.Drop + 0.25))
        {
            var rise = Rise(t);
            var y = 35 + (t < ItemTimeline.Shown ? (1 - rise) * 8 : Math.Round(Math.Sin((t - ItemTimeline.Shown) * 4.5) * 1.2));
            var scale = t < ItemTimeline.Shown ? 0.4 + (0.6 * rise) : 1;
            var leave = Math.Clamp((t - ItemTimeline.Drop) / 0.25, 0, 1);
            PutIcon(p, ItemIcon(s), wx + 15, y - (leave * 6), scale * (1 - leave), scale * (1 - leave), (t - ItemTimeline.Shown + 0.05) / 0.35);
        }

        // Laurel hugging the window and a star at the top.

        // Engraved text.
        var tx = wx + 38;
        Text2(p, s.Name, tx, 24, 0x3A1E08, 0xFFE8A8);
        p.HLine(tx, 32, width - 58, 0x8A5C18);
        p.HLine(tx, 33, width - 58, 0xFFE8A8, 140);
        Text2(p, "A LA MOCHILA", tx, 38, 0x6A3E10, 0xFFE8A8);
        Chip(p, tx + SmallWidth("A LA MOCHILA") + 3, 37, s.Amount, 0x2A1A08, 0xFFDC7A, 0x6A4A18);

        // The bag, small at the end of the plate: the item flies into it.
        var bx = x + width - 22;
        var hop = t is >= ItemTimeline.Inside and < ItemTimeline.Inside + 0.18 ? (int)Math.Round(Math.Sin((t - ItemTimeline.Inside) / 0.18 * Math.PI) * 2) : 0;
        MiniBag(p, bx, 36 - hop);
        if (t >= ItemTimeline.Drop && t < ItemTimeline.Inside)
        {
            var f = Fall(t);
            var fx = (wx + 15) + (((bx + 7) - (wx + 15)) * f);
            var fy = 30 + ((40 - 30) * f) - (Math.Sin(f * Math.PI) * 14);
            PutIcon(p, ItemIcon(s), fx, fy, 1 - (f * 0.65), 1 - (f * 0.65));
        }

        Sparks(p, bx + 7, 36, t - ItemTimeline.Inside, 5, 9, 0xFFDC7A);

        // One sheen crossing the plate when the item comes to rest.
        var sheen = (t - ItemTimeline.Shown) / 0.5;
        if (sheen is > 0 and < 1)
        {
            var sxx = x + 3 + (int)(sheen * (width - 6));
            for (var yy = 16; yy < 54; yy++) for (var k = 0; k < 4; k++) p.Set(sxx + k - ((yy - 16) / 3), yy, 0xFFFFFF, 70);
        }

        return ItemFade(p, t);
    }

    private static void MiniBag(Pix p, int x, int y)
    {
        string[] art =
        [
            "....oooooo....",
            "...odddddddo..",
            "..ooooooooooo.",
            ".oFFFFFgFFFFFo",
            ".oFFFFFgFFFFFo",
            ".ommmmmkmmmmmo",
            ".ommppppppppmo",
            ".ommpqqqqqqpmo",
            ".ommppppppppmo",
            ".ommmmmmmmmmmo",
            "..ooooooooooo.",
        ];
        var key = new Dictionary<char, uint>
        {
            ['o'] = 0x1A0C06, ['d'] = 0x7A321A, ['F'] = 0xC05C30, ['g'] = 0xFFDC7A, ['m'] = 0xB85228, ['k'] = 0x8E601C, ['p'] = 0x9A4422, ['q'] = 0x863A1C
        };
        p.Art(art, x, y, key);
    }

    // ---------------------------------------------------------------- D: the parchment and its seal

    private static Pix ItemScroll(ItemSample s, double t)
    {
        var p = new Pix(IW, IH);
        var width = Math.Max(SmallWidth(s.Name), SmallWidth("A LA MOCHILA") + 4 + SmallWidth(s.Amount)) + 56;
        // The parchment rolls itself up as the item goes in.
        var roll = Smooth((t - ItemTimeline.Drop) / (ItemTimeline.Inside - ItemTimeline.Drop + 0.2));
        var open = Math.Clamp(Ease3((t - 0.1) / 0.35), 0, 1) * (1 - roll);
        var sheet = Math.Max(10, (int)(width * open));
        const int top = 16;
        const int height = 38;
        var left = 10;

        var layer = new Pix(IW, IH);
        Sheet(layer, left + 6, top, sheet, height, 7);
        Scorch(layer, left + 7, top + 1, Math.Max(2, sheet - 2), height - 2, 2, 31);
        if (sheet > 50)
        {
            // The stamp: a ring of ink the item is pressed into.
            for (var a = 0; a < 90; a++)
            {
                var ang = a / 90.0 * Math.PI * 2;
                layer.Set(left + 25 + (int)Math.Round(Math.Cos(ang) * 12.5), top + 19 + (int)Math.Round(Math.Sin(ang) * 12.5), a % 7 == 0 ? 0xB89A60u : 0x7A5A28u);
            }

            Glow(layer, left + 25, top + 19, 12, 12, 0xD8BE84, 130);
            Text2(layer, s.Name, left + 44, top + 9, 0x3A2410);
            layer.Small("A LA MOCHILA", left + 44, top + 18, 0x7A5A28);
            Chip(layer, left + 44 + SmallWidth("A LA MOCHILA") + 3, top + 17, s.Amount, 0x3A2410, 0xFFDC7A, 0x6A4A18);
            for (var xx = left + 44; xx < left + 44 + SmallWidth(s.Name); xx += 2) layer.Set(xx, top + 16, 0xA88850, 150);
        }

        // Clip the layer to the open part (it was drawn for the full width, narrower sheets simply cut it).
        p.Blit(layer, 0, 0);

        // The two wooden rolls, the right one travelling with the edge of the sheet.
        VRoll(p, left, top - 4, 9, height + 8);
        VRoll(p, left + 6 + sheet - 3, top - 4, 9, height + 8);
        Haze(p, left + 6 + (sheet / 2.0), top + height + 6, sheet / 2.0 + 6, 2.2, 0x0F0C16);

        if (sheet > 50)
        {
            // The icon pressed onto the stamp: drops from above, big, and lands with a ripple.
            var land = Math.Clamp((t - ItemTimeline.Pop) / 0.2, 0, 1);
            if (t >= ItemTimeline.Pop && t < ItemTimeline.Drop + 0.12)
            {
                var y = top + 19 - ((1 - Ease3(land)) * 20);
                var scale = 1 + ((1 - Ease3(land)) * 0.9);
                var lift = Math.Clamp((t - ItemTimeline.Drop) / 0.12, 0, 1);
                PutIcon(p, ItemIcon(s), left + 25, y - (lift * 5), scale * (1 - lift * 0.5), scale * (1 - lift * 0.5), (t - ItemTimeline.Shown + 0.05) / 0.35);
                if (land >= 1 && t < ItemTimeline.Shown + 0.2)
                {
                    var r = (t - ItemTimeline.Pop - 0.2) / 0.2 * 10;
                    for (var a = 0; a < 36; a++)
                    {
                        var ang = a / 36.0 * Math.PI * 2;
                        p.Set(left + 25 + (int)(Math.Cos(ang) * (12 + r)), top + 19 + (int)(Math.Sin(ang) * (12 + r) * 0.8), 0x7A5A28, (int)(200 * (1 - (r / 10))));
                    }
                }
            }
        }

        // Wax seal on the right edge, a ribbon hanging from it.
        if (sheet > 50) Wax(p, left + 6 + sheet - 10, top + height - 7, 6, s.Potion ? 0xB04858u : 0x7A4CC0u);
        Sparks(p, left + 25, top + 19, t - ItemTimeline.Pop - 0.15, 5, 14, 0xFFDC7A);
        return ItemFade(p, t);
    }

    // ---------------------------------------------------------------- E: the Poké Ball that opens

    private static Pix BallHalf(bool upper)
    {
        var half = new Pix(24, 12);
        for (var y = 0; y < 12; y++)
        {
            for (var x = 0; x < 24; x++)
            {
                var yy = upper ? y - 11.5 : y + 0.5;
                var d = Math.Sqrt(Math.Pow(x - 11.5, 2) + Math.Pow(yy, 2));
                if (d > 12) continue;
                uint c;
                if (d > 10.9) c = 0x14101C;
                else if (upper) c = Mix(0xF05048, 0xA82024, (x / 24.0) * 0.5 + ((11 - y) / 11.0) * 0.1);
                else c = Mix(0xFFFFFF, 0xC8C4D8, (x / 24.0) * 0.6 + (y / 12.0) * 0.2);
                half.Set(x, y, c);
            }
        }

        if (upper)
        {
            half.Set(5, 5, 0xFFFFFF);
            half.Set(6, 4, 0xFFFFFF);
            half.Rect(4, 6, 3, 1, 0xFF9088);
        }

        return half;
    }

    private static Pix ItemBall(ItemSample s, double t)
    {
        var p = new Pix(IW, IH);
        const int cx = 28;
        const int cy = 48;
        var opened = Smooth((t - ItemTimeline.Pop) / 0.2) * (1 - Smooth((t - ItemTimeline.Inside + 0.05) / 0.12));
        var fall = Fall(t);

        // Floor rings spreading out of the ball.
        Haze(p, cx, cy + 12, 17, 3.2, 0x0F0C16);
        for (var k = 0; k < 3; k++)
        {
            var since = (t - ItemTimeline.Pop - (k * 0.18)) / 0.7;
            if (since is <= 0 or >= 1) continue;
            var rx = 8 + (since * 26);
            for (var a = 0; a < 90; a++)
            {
                var ang = a / 90.0 * Math.PI * 2;
                p.Set((int)(cx + (Math.Cos(ang) * rx)), (int)(cy + 12 + (Math.Sin(ang) * rx * 0.2)), s.Potion ? 0xE070FFu : 0xFF6A9Cu, (int)(210 * (1 - since)));
            }
        }

        // A star of light thrown out of the opening.
        if (opened > 0.05)
        {
            for (var r = 0; r < 10; r++)
            {
                var ang = (r / 10.0 * Math.PI * 2) + (t * 0.8);
                for (var d = 6; d < 6 + (int)(opened * 30); d++)
                {
                    if (ItemBayer[d & 3, r & 3] >= (1 - (d / 40.0)) * 11) continue;
                    p.Set((int)(cx + (Math.Cos(ang) * d)), (int)(cy - 3 + (Math.Sin(ang) * d * 0.9)), 0xFFE9A0, 220);
                }
            }
        }

        // The ball: the lower half stays, the upper half lifts and tilts.
        var lower = BallHalf(false);
        p.Blit(lower, cx - 12, cy);
        var lid = Turned(BallHalf(true).Scaled(1), -opened * 16);
        p.Blit(lid, cx - (lid.W / 2) + (int)(opened * 1), cy - lid.H + 3 - (int)(opened * 7));
        p.Rect(cx - 12, cy - 1, 24, 3, 0x14101C);
        p.Rect(cx - 11, cy, 22, 1, 0x3A3250);
        p.Rect(cx - 3, cy - 3, 7, 7, 0x14101C);
        p.Rect(cx - 2, cy - 2, 5, 5, opened > 0.1 ? 0xFFE9A0u : 0xEFEFF8u);
        p.Set(cx - 1, cy - 1, 0xFFFFFF);

        // The item spins on its own axis above the ball.
        if (Out(t))
        {
            var rise = Rise(t);
            var y = t < ItemTimeline.Shown ? cy - 3 - (rise * 28)
                : t < ItemTimeline.Drop ? 17 + Math.Round(Math.Sin((t - ItemTimeline.Shown) * 4.5) * 1.5)
                : 17 + (fall * fall * 26);
            var scale = t < ItemTimeline.Shown ? 0.4 + (0.6 * rise) : 1 - (fall * 0.7);
            var turn = Math.Max(0.18, Math.Abs(Math.Cos((t - ItemTimeline.Pop) * 5)));
            if (t >= ItemTimeline.Shown + 0.35 && t < ItemTimeline.Drop) turn = 1;
            Glow(p, cx, y + 1, 13, 13, 0xFFE9A0, 100);
            PutIcon(p, ItemIcon(s), cx, y, scale * turn, scale, (t - ItemTimeline.Shown + 0.05) / 0.35);
        }

        // «Click»: a star on the button when it closes.
        var click = t - ItemTimeline.Inside;
        if (click is >= 0 and < 0.3) Star(p, cx, cy, click < 0.12 ? 3 : 1, 0xFFE9A0);
        Sparks(p, cx, cy - 2, t - ItemTimeline.Pop, 6, 18, 0xFFDC7A);

        DarkTag(p, 62, 22, s, 0xFF6A9C, 0xFFB8D0, (int)PlateSlide(t));
        return ItemFade(p, t);
    }

    // ---------------------------------------------------------------- F: the inventory slots

    private static void Slot(Pix p, int x, int y, bool lit, double pulse)
    {
        p.Notched(x, y, 26, 26, lit ? Mix(0xE0A83A, 0xFFEAA0, pulse) : 0x0B0910, 2);
        p.Notched(x + 1, y + 1, 24, 24, lit ? 0x6A4A18u : 0x2A2244u, 2);
        p.Notched(x + 2, y + 2, 22, 22, 0x120C1C, 2);
        p.VGrad(x + 3, y + 3, 20, 20, lit ? 0x2A2038u : 0x1C1530u, 0x0E0A16);
        p.HLine(x + 3, y + 3, 20, 0x3A2E5C);
        // Four dither dots in the corners, like the game's item boxes.
        foreach (var (dx, dy) in new[] { (4, 4), (21, 4), (4, 21), (21, 21) }) p.Set(x + dx, y + dy, lit ? 0xE0A83Au : 0x3A2E5Cu);
    }

    private static Pix ItemSlots(ItemSample s, double t)
    {
        var p = new Pix(IW, IH);
        var fillers = new[] { new ItemSample("", "", true), new ItemSample("", "", false), new ItemSample("", "", true) };
        // The row makes room: three slots slide apart and a fourth opens at the right.
        var make = Smooth((t - 0.15) / 0.3);
        var startX = 14 - (int)((1 - make) * 14 * -1);
        Haze(p, 14 + (2 * 28) + 13 + 14, 59, 60, 3, 0x0F0C16);
        var lit = t >= ItemTimeline.Pop;
        var pulse = (Math.Sin(t * 16) + 1) / 2;
        for (var i = 0; i < 4; i++)
        {
            var x = startX + (i * 28) - (int)((1 - make) * 14);
            if (i == 3 && make < 0.05) continue;
            var isNew = i == 3;
            var slideIn = isNew ? (int)((1 - make) * 6) : 0;
            Slot(p, x, 22 + slideIn, isNew && lit, pulse);
            if (!isNew)
            {
                var icon = ItemIcon(fillers[i]);
                PutIcon(p, icon, x + 13, 35, 1, 1);
                p.Rect(x + 3, y: 3 + 22, 20, 20, 0x0B0910, 110);
            }
        }

        // The new item drops in from above with a bounce and settles; a +1 floats up.
        var nx = startX + (3 * 28) - 0;
        if (t >= ItemTimeline.Pop)
        {
            var land = Math.Clamp((t - ItemTimeline.Pop) / 0.3, 0, 1);
            var y = 35 - ((1 - Ease3(land)) * 24) - (land > 0.7 ? Math.Sin((land - 0.7) / 0.3 * Math.PI) * 3 : 0);
            var squash = land is > 0.9 and < 1 ? 1.15 : 1;
            PutIcon(p, ItemIcon(s), nx + 13, y, squash, 1 / squash, (t - ItemTimeline.Shown + 0.05) / 0.35);
            // The count badge on the corner.
            if (s.Amount == "NUEVO")
            {
                // A gold star pinned to the corner: it is new.
                Star(p, nx + 22, 22 + 4, 2, 0xFFDC7A);
            }
            else
            {
                Chip(p, nx + 26 - SmallWidth(s.Amount) - 5, 22 + 18, s.Amount, 0x2A1A08, 0xFFDC7A, 0xE0A83A);
            }
        }

        // +1 floating up, gold, with a shadow.
        var plus = (t - ItemTimeline.Pop - 0.25) / 0.7;
        if (plus is > 0 and < 1) p.Text("+1", nx + 8, 18 - (int)(plus * 12), 0xFFDC7A, 1, 0x3A2008);

        Sparks(p, nx + 13, 35, t - ItemTimeline.Pop - 0.2, 6, 15, 0xFFDC7A);

        // The shine that sweeps the slot once the item has settled, and the row sliding back at the end.
        var sweep = (t - ItemTimeline.Shown) / 0.5;
        if (sweep is > 0 and < 1)
        {
            for (var yy = 26; yy < 44; yy++) for (var k = 0; k < 2; k++) { var sx = nx + 4 + (int)(sweep * 20) + k - ((yy - 26) / 4); if (sx >= nx + 3 && sx < nx + 23) p.Set(sx, yy, 0xFFFFFF, 70); }
        }

        // Name under the row on a plate of the app's own look.
        var tagW = SmallWidth(s.Name) + 12;
        p.Notched(14, 51, tagW + 82, 10, 0x0B0910, 1);
        p.Notched(15, 52, tagW + 80, 8, 0x1C1530, 1);
        p.Rect(15, 52, 2, 8, 0xE0A83A);
        Text2(p, s.Name, 20, 54, 0xF4F0FF, 0x100C1C);
        p.Small("A LA MOCHILA", 20 + SmallWidth(s.Name) + 6, 54, 0x9A90B8);
        p.Small(s.Amount, 20 + SmallWidth(s.Name) + 6 + SmallWidth("A LA MOCHILA") + 4, 54, 0xFFDC7A);
        return ItemFade(p, t);
    }

    // ---------------------------------------------------------------- the pictures

    private static Pix ItemFloor(int width, int height)
    {
        var band = new Pix(width, height);
        band.Rect(0, 0, width, height, 0x0A0A12);
        for (var y = 0; y < height; y += 4) band.HLine(0, y, width, 0x0E0E1A);
        return band;
    }

    [Fact]
    public void Every_item_look_draws_with_pixels_in_every_moment()
    {
        foreach (var (_, _, look) in ItemLooks)
        {
            foreach (var item in ItemSamples)
            {
                foreach (var moment in ItemMoments)
                {
                    var frame = look(item, moment);
                    Assert.Equal(IW, frame.W);
                    Assert.Contains(frame.B.Where((_, i) => i % 4 == 3), alpha => alpha == 255);
                }
            }
        }
    }

    [Fact]
    public void Writes_the_item_pictures_when_asked()
    {
        var folder = Dir("PERMALOCKE_PIXEL_DIR");
        if (folder is null) return;
        Directory.CreateDirectory(folder);

        const int zoom = 3;
        var cell = IW * zoom;
        var rowH = IH * zoom;

        // One strip per look: the four moments in a row, for the TM and for the potion.
        foreach (var (name, _, look) in ItemLooks)
        {
            var strip = ItemFloor((cell * 4) + 30, (rowH * 2) + 24);
            for (var row = 0; row < 2; row++)
            {
                for (var i = 0; i < 4; i++)
                {
                    var frame = look(ItemSamples[row], ItemMoments[i]).Scaled(zoom);
                    strip.Blit(frame, 6 + (i * (cell + 6)), 8 + (row * (rowH + 8)));
                }
            }

            PngFile.Write(strip.B, strip.W, strip.H, 1, Path.Combine(folder, $"objeto-{name}.png"));
        }

        // All of them side by side at the moment the item rests, next to the animation as it is now.
        var compare = ItemFloor(cell + 150, (rowH * (ItemLooks.Length + 1)) + 8);
        var line = 0;
        foreach (var (name, title, look) in ItemLooks.Prepend(("0", "ACTUAL", Today)))
        {
            var y = 4 + (line * rowH);
            compare.Text(name, 8, y + 6, 0xFFFFFF, scale: 5);
            compare.Text(title.Length > 12 ? title[..12] : title, 8, y + 56, 0xB0A8D0, scale: 1);
            compare.Blit(look(ItemSamples[0], 1.1).Scaled(zoom), 150, y);
            compare.Rect(0, y + rowH - 1, compare.W, 1, 0x1E1C30);
            line++;
        }

        PngFile.Write(compare.B, compare.W, compare.H, 1, Path.Combine(folder, "objeto-comparar.png"));
    }

    /// <summary>The animation as the app draws it today, in the same canvas size.</summary>
    private static Pix Today(ItemSample item, double t)
    {
        var icon = ItemIcon(item);
        var bgra = new byte[icon.W * icon.H * 4];
        for (var i = 0; i < icon.W * icon.H; i++)
        {
            bgra[(i * 4)] = icon.B[(i * 4)];
            bgra[(i * 4) + 1] = icon.B[(i * 4) + 1];
            bgra[(i * 4) + 2] = icon.B[(i * 4) + 2];
            bgra[(i * 4) + 3] = icon.B[(i * 4) + 3];
        }

        var scene = new ItemScene(1);
        scene.Render(new ItemScene.Item(bgra, icon.W, icon.H, item.Name.Replace("Ó", "O"), item.Amount == "NUEVO" ? 1 : 3), t);
        var frame = new Pix(IW, IH);
        for (var y = 0; y < Math.Min(IH, scene.Height); y++)
        {
            for (var x = 0; x < Math.Min(IW, scene.Width); x++)
            {
                var i = ((y * scene.Width) + x) * 4;
                if (scene.Pixels[i + 3] > 0) frame.Set(x, y, Rgb(scene.Pixels[i + 2], scene.Pixels[i + 1], scene.Pixels[i]));
            }
        }

        return frame;
    }
}
