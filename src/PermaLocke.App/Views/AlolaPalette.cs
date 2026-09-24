using System.Windows.Media;

namespace PermaLocke.App.Views;

/// <summary>
/// The colours of the Alola sky by hour, shared by the sidebar's window and the header's banner so the two
/// never disagree about what time it is.
/// </summary>
/// <remarks>
/// Picked by hand, a few per part of the day, and interpolated by the fractional hour so the sky never jumps.
/// They are drawing, not the game's own sky.
/// </remarks>
internal static class AlolaPalette
{
    /// <summary>Top, middle and bottom of the sky by hour.</summary>
    private static readonly (double Hour, Color Top, Color Middle, Color Low)[] Skies =
    [
        (0.0, Rgb(0x06, 0x05, 0x12), Rgb(0x0C, 0x0A, 0x1F), Rgb(0x19, 0x14, 0x32)),
        (4.6, Rgb(0x06, 0x05, 0x12), Rgb(0x0C, 0x0A, 0x1F), Rgb(0x19, 0x14, 0x32)),
        (5.8, Rgb(0x10, 0x0E, 0x2C), Rgb(0x3A, 0x25, 0x52), Rgb(0x8A, 0x4A, 0x62)),
        (6.8, Rgb(0x22, 0x36, 0x68), Rgb(0x6E, 0x68, 0x98), Rgb(0xE6, 0x9E, 0x78)),
        (8.5, Rgb(0x2A, 0x58, 0x94), Rgb(0x4E, 0x8A, 0xC2), Rgb(0x9C, 0xCB, 0xE0)),
        (12.0, Rgb(0x29, 0x60, 0xA5), Rgb(0x4A, 0x94, 0xCF), Rgb(0x98, 0xD2, 0xE8)),
        (16.0, Rgb(0x2A, 0x5B, 0x9B), Rgb(0x54, 0x8C, 0xC3), Rgb(0xB2, 0xD0, 0xDC)),
        (17.2, Rgb(0x38, 0x3D, 0x78), Rgb(0xAE, 0x5E, 0x78), Rgb(0xF0, 0xA4, 0x56)),
        (18.1, Rgb(0x19, 0x17, 0x3E), Rgb(0x48, 0x2E, 0x5E), Rgb(0x9E, 0x4E, 0x68)),
        (19.2, Rgb(0x09, 0x08, 0x17), Rgb(0x11, 0x0F, 0x2E), Rgb(0x21, 0x19, 0x3C)),
        (24.0, Rgb(0x06, 0x05, 0x12), Rgb(0x0C, 0x0A, 0x1F), Rgb(0x19, 0x14, 0x32))
    ];

    public static readonly Color SunHigh = Rgb(0xFF, 0xF3, 0xC4);
    public static readonly Color SunLow = Rgb(0xFF, 0xA6, 0x48);
    public static readonly Color MoonLight = Rgb(0xE8, 0xE4, 0xF4);
    public static readonly Color MoonShade = Rgb(0xA8, 0xA2, 0xC0);
    public static readonly Color Star = Rgb(0xD8, 0xD4, 0xEE);
    public static readonly Color Ink = Rgb(0x05, 0x04, 0x08);

    public static readonly int[,] Bayer =
    {
        { 0, 8, 2, 10 },
        { 12, 4, 14, 6 },
        { 3, 11, 1, 9 },
        { 15, 7, 13, 5 }
    };

    public static (Color Top, Color Middle, Color Low) SkyAt(double hour)
    {
        hour = ((hour % 24) + 24) % 24;

        for (var i = 1; i < Skies.Length; i++)
        {
            if (hour <= Skies[i].Hour)
            {
                var a = Skies[i - 1];
                var b = Skies[i];
                var t = (hour - a.Hour) / (b.Hour - a.Hour);
                return (Lerp(a.Top, b.Top, t), Lerp(a.Middle, b.Middle, t), Lerp(a.Low, b.Low, t));
            }
        }

        return (Skies[0].Top, Skies[0].Middle, Skies[0].Low);
    }

    /// <summary>Where the sun or the moon is on its arc: 0 rising on the left, 1 setting on the right.</summary>
    public static (bool IsDay, double Along) Body(double hour)
    {
        hour = ((hour % 24) + 24) % 24;
        var isDay = hour is >= 6 and < 18;
        return (isDay, isDay ? (hour - 6) / 12 : ((hour + 6) % 24) / 12);
    }

    /// <summary>How much of the night is in the sky: stars come out after dusk and go before dawn.</summary>
    public static double NightAmount(double hour)
    {
        hour = ((hour % 24) + 24) % 24;

        return hour switch
        {
            >= 19.5 or <= 4.5 => 1,
            > 4.5 and < 6 => (6 - hour) / 1.5,
            > 18 and < 19.5 => (hour - 18) / 1.5,
            _ => 0
        };
    }

    public static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + ((b.A - a.A) * t)),
        (byte)Math.Round(a.R + ((b.R - a.R) * t)),
        (byte)Math.Round(a.G + ((b.G - a.G) * t)),
        (byte)Math.Round(a.B + ((b.B - a.B) * t)));

    public static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
