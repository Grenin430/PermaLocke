namespace PermaLocke.App.Views;

/// <summary>
/// The colours of the sea in one mood (2026-10-10): the water as a ramp from the surface to the abyss (every step has its own hue: the
/// shadows are not just darker, they lean to blue, violet or red), the light that comes down through it, the sediment of the floor, and the
/// colour the writing glows in. Plus how the mood moves: how fast, how rough.
/// </summary>
public sealed class CapSeaPalette
{
    /// <summary>Eight steps from just under the surface to the abyss.</summary>
    public required uint[] Water { get; init; }

    public required uint Shaft { get; init; }

    public required uint Snow { get; init; }

    /// <summary>The writing: names, levels, the number on the buoy. Light on every mood, so it reads over any water.</summary>
    public required uint Glow { get; init; }

    /// <summary>The writing's shadow: dark with the hue of the water, never black.</summary>
    public required uint Ink { get; init; }

    /// <summary>The sediment, from the lit top to the dark bottom: four steps.</summary>
    public required uint[] Floor { get; init; }

    /// <summary>The strata, one colour each, cycled; the cleared trials.</summary>
    public required uint[] Strata { get; init; }

    /// <summary>How fast the sea moves, 1 when calm: the clock of everything that drifts.</summary>
    public required double Tempo { get; init; }

    /// <summary>How rough the surface is and how hard the current pulls the snow.</summary>
    public required double Agitation { get; init; }

    /// <summary>How strong the light shafts are, 0 to 1.</summary>
    public required double ShaftStrength { get; init; }

    public static CapSeaPalette Of(CapMood mood) => mood switch
    {
        CapMood.Battle => Battle,
        CapMood.Danger => Danger,
        CapMood.Mourning => Mourning,
        CapMood.Cap => Capped,
        _ => Calm
    };

    // Calm: a warm teal that falls to indigo.
    private static readonly CapSeaPalette Calm = new()
    {
        Water = [0x2FA3B5, 0x1E86A2, 0x176A93, 0x105084, 0x0B3A72, 0x082A5C, 0x061D48, 0x041233],
        Shaft = 0xA8F0FF, Snow = 0xBFE8F0, Glow = 0xE8FFF8, Ink = 0x020A12,
        Floor = [0x5E8C8A, 0x3F6670, 0x2A4659, 0x1B2D45],
        Strata = [0x7A6A58, 0x58607A, 0x6A5470, 0x4C7068, 0x85704A, 0x4A5470],
        Tempo = 1.0, Agitation = 1.0, ShaftStrength = 0.75
    };

    // Combat: the water turns cold and hard, the current runs.
    private static readonly CapSeaPalette Battle = new()
    {
        Water = [0x5C9AC8, 0x3A78AE, 0x265A92, 0x1A4278, 0x112F62, 0x0B224D, 0x07173B, 0x040F2A],
        Shaft = 0xD8ECFF, Snow = 0xDCEBFF, Glow = 0xF0F8FF, Ink = 0x030A18,
        Floor = [0x5A7A9A, 0x3D5878, 0x2A3F5C, 0x1B2842],
        Strata = [0x6A7288, 0x4E5C82, 0x625A82, 0x486A7A, 0x7A6E58, 0x465278],
        Tempo = 1.7, Agitation = 1.9, ShaftStrength = 0.55
    };

    // Danger: a red tide, the abyss goes to wine and violet.
    private static readonly CapSeaPalette Danger = new()
    {
        Water = [0x9A4048, 0x7A2E48, 0x5C2250, 0x441A52, 0x30134A, 0x220C3A, 0x16082C, 0x0C0420],
        Shaft = 0xFF9078, Snow = 0xF0B8A8, Glow = 0xFFE4D0, Ink = 0x14040E,
        Floor = [0x7A4A5A, 0x58364E, 0x3C2442, 0x261632],
        Strata = [0x7A4A4A, 0x62405A, 0x70505A, 0x5A3A48, 0x80584A, 0x523A5A],
        Tempo = 2.3, Agitation = 2.6, ShaftStrength = 0.5
    };

    // Mourning: grey and violet, still; what falls is ash.
    private static readonly CapSeaPalette Mourning = new()
    {
        Water = [0x727C92, 0x58637E, 0x434E6C, 0x333D5A, 0x252E48, 0x1B223A, 0x121730, 0x0A0D22],
        Shaft = 0xB8C0D8, Snow = 0xC8CCD8, Glow = 0xDCE0F0, Ink = 0x080A18,
        Floor = [0x5A607A, 0x434862, 0x2E3350, 0x1E2238],
        Strata = [0x5E6074, 0x4E5470, 0x5E5878, 0x465068, 0x6A6460, 0x424A68],
        Tempo = 0.45, Agitation = 0.35, ShaftStrength = 0.3
    };

    // Cap: the sea floods with gold light from the surface.
    private static readonly CapSeaPalette Capped = new()
    {
        Water = [0x78E8D8, 0x4CC8C8, 0x32A4BE, 0x2280A6, 0x185C8C, 0x104274, 0x0A2E5E, 0x06204A],
        Shaft = 0xFFF2A8, Snow = 0xFFF4D0, Glow = 0xFFFBE0, Ink = 0x020A10,
        Floor = [0x9AA070, 0x6E8068, 0x4A5E5E, 0x2E4050],
        Strata = [0x9A8A5A, 0x6A8078, 0x8A7A62, 0x5E8A78, 0xA89058, 0x587A8A],
        Tempo = 1.15, Agitation = 0.8, ShaftStrength = 1.0
    };

    /// <summary>The sky at an hour of Alola, 0 to 24: the top colour and the colour at the horizon, between the keyframes of the day.</summary>
    public static (uint Top, uint Horizon) Sky(double hour)
    {
        // Night, dawn, morning, noon, afternoon, dusk, night: each a top and a horizon.
        (double At, uint Top, uint Horizon)[] keys =
        [
            (0, 0x050B24, 0x16294C), (5, 0x0A1430, 0x2A3A66), (6.5, 0x3A3A78, 0xF0905A), (8.5, 0x4A82D0, 0xB8E0F4),
            (12, 0x3A8AE0, 0xB8ECFA), (16.5, 0x4A82D0, 0xF0D8A0), (18.5, 0x4A3A82, 0xF0704A), (20.5, 0x10183C, 0x3A3A6A), (24, 0x050B24, 0x16294C)
        ];

        hour = ((hour % 24) + 24) % 24;
        for (var i = 1; i < keys.Length; i++)
        {
            if (hour > keys[i].At) continue;
            var k = (hour - keys[i - 1].At) / (keys[i].At - keys[i - 1].At);
            return (CapSheet.Mix(keys[i - 1].Top, keys[i].Top, k), CapSheet.Mix(keys[i - 1].Horizon, keys[i].Horizon, k));
        }

        return (keys[0].Top, keys[0].Horizon);
    }

    /// <summary>How dark the sky is: 1 at night with stars, 0 in daylight.</summary>
    public static double Night(double hour)
    {
        hour = ((hour % 24) + 24) % 24;
        if (hour is >= 7.5 and <= 18) return 0;
        if (hour is >= 20.5 or <= 5) return 1;
        return hour < 7.5 ? 1 - ((hour - 5) / 2.5) : (hour - 18) / 2.5;
    }

    /// <summary>The ordered dither: 4×4 Bayer thresholds, 0 to 15.</summary>
    public static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>A colour out of a ramp at a position 0 to 1, dithered: neighbours take one of the two steps it is between, in the pattern of the cell.</summary>
    public static uint Dithered(uint[] ramp, double position, int x, int y, double bias = 0)
    {
        var scaled = Math.Clamp(position, 0, 1) * (ramp.Length - 1);
        var step = Math.Min(ramp.Length - 2, (int)scaled);
        var fraction = scaled - step + bias;
        var threshold = (Bayer[((y & 3) * 4) + (x & 3)] + 0.5) / 16.0;
        return fraction > threshold ? ramp[step + 1] : ramp[step];
    }
}
