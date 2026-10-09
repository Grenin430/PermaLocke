namespace PermaLocke.App.Views;

/// <summary>
/// How long each part of an item's animation lasts, in seconds (2026-10-09): the dither coming in, the anticipation, the
/// resonance (only the Mega Stones have one), the climax, the wrap-up, and the dither going out. Each category has its own,
/// in <see cref="ItemTimeline.For"/>, and that is the one place to tune them.
/// </summary>
public readonly record struct ItemPhases(double In, double Anticipation, double Resonance, double Climax, double Wrap, double Out)
{
    public double ResonanceAt => In + Anticipation;

    public double ClimaxAt => ResonanceAt + Resonance;

    public double WrapAt => ClimaxAt + Climax;

    public double OutAt => WrapAt + Wrap;

    public double Length => OutAt + Out;

    /// <summary>How much of the drawing is on at a moment: 0 to 1, in through the dither and out through it.</summary>
    public double Alpha(double t) => t < In ? Math.Clamp(t / In, 0, 1) : t > OutAt ? Math.Clamp(1 - ((t - OutAt) / Out), 0, 1) : 1;
}

public static class ItemTimeline
{
    /// <summary>
    /// The phases of a category at a power (<see cref="ItemCategory"/> says what a power is). Between 1.4 and 3.2 seconds, the
    /// Mega Stones apart with 4.5: the frequent kinds are short so that they never tire, the rare ones take their time.
    /// </summary>
    public static ItemPhases For(ItemCategory category, int power = 0)
    {
        var strength = Math.Clamp(power, 0, 3);

        return category switch
        {
            ItemCategory.Misc => new(0.20, 0.20, 0, 0.40, 0.40, 0.25),
            ItemCategory.Berry => new(0.25, 0.30, 0, 0.50, 0.35, 0.30),
            ItemCategory.Healing => new(0.25, 0.40, 0, 0.60 + (0.10 * strength), 0.50, 0.30),
            ItemCategory.Boost => new(0.25, 0.30, 0, 0.70 + (0.05 * strength), 0.40, 0.30),
            ItemCategory.Evolution => new(0.30, 0.60, 0, 0.55, 0.55, 0.35),
            ItemCategory.Battle => new(0.15, 0.20, 0, 0.45, 0.45, 0.30),
            ItemCategory.PokeBall => new(0.25, 0.30, 0, 0.60, 0.50, 0.30),
            ItemCategory.Machine => new(0.30, 0.40, 0, 0.70, 0.50, 0.30),
            ItemCategory.Key => new(0.40, 0.80, 0, 0.90, 0.70, 0.40),
            ItemCategory.ZCrystal => new(0.40, 0.70, 0, 0.80, 0.70, 0.40),
            ItemCategory.MegaStone => new(0.40, 0.70, 1.00, 0.70, 1.25, 0.45),
            _ => new(0.20, 0.20, 0, 0.40, 0.40, 0.25)
        };
    }

    // The classic scene (ClassicStyle): the bag with the icon jumping out of it, still the one every category plays until it
    // has its own. They go on being stretched to the length of the category.
    public const double In = 0.3;
    public const double Pop = 0.35;
    public const double Shown = 0.65;
    public const double Drop = 1.55;
    public const double Inside = 1.85;
    public const double Out = 2.7;
    public const double Fade = 0.35;
    public const double Length = Out + Fade;
}

/// <summary>
/// An item just picked up, bought or given, going into the bag (2026-09-28, asked by the organiser): the cousin of the
/// card that flies into the album (§190), small and quiet, in the dark band beside the emulator's bottom screen.
/// </summary>
/// <remarks>
/// <para>
/// What it does depends on the kind of item: each category has its <see cref="ItemStyle"/>, drawn on the common trunk
/// (<see cref="ItemCanvas"/>) with the bag, the plate and the dither in and out. Beside the bag a plate like the game's own
/// item tags says what it was and how many.
/// </para>
/// <para>
/// Every cell is a pixel of the game, so it matches the emulator next to it. Pure: a moment and an item make a frame,
/// premultiplied BGRA, transparent around the drawing.
/// </para>
/// </remarks>
public sealed class ItemScene
{
    /// <summary>The width of every scene, in pixels of the game: what the band beside the emulator's bottom screen gives.</summary>
    public const int SceneWidth = 150;

    /// <summary>The height of the common scene. A style with a climax may ask for more (<see cref="HeightFor"/>).</summary>
    public const int SceneHeight = 64;

    private readonly ItemCanvas _canvas;

    /// <param name="pixel">Monitor pixels per pixel of the game.</param>
    /// <param name="height">The height the style of what is going to be played declares: <see cref="HeightFor"/>.</param>
    public ItemScene(double pixel, int height = SceneHeight)
    {
        _canvas = new ItemCanvas(pixel, height);
    }

    public double Pixel => _canvas.Pixel;

    /// <summary>The height in pixels of the game.</summary>
    public int GameHeight => _canvas.GameHeight;

    /// <summary>The frame in monitor pixels.</summary>
    public int Width => _canvas.Width;

    public int Height => _canvas.Height;

    /// <summary>The frame, premultiplied BGRA.</summary>
    public byte[] Pixels => _canvas.Pixels;

    /// <summary>What the last frame asked to draw outside of the scene: zero for a style that stays inside its height.</summary>
    public int Overdraw => _canvas.Overdraw;

    /// <summary>
    /// What the scene shows: the icon as BGRA, its name and how many. The rest says how to show it: the kind of item and its
    /// power (<see cref="ItemCatalog"/>), the colour of its icon, the seed that makes this one different from the last, and a
    /// second line for the plate (the type of the move of a TM). <c>Kind</c> is the type (the game's number, 0 to 17) the item
    /// is of, when it has one that could be read: the move of a TM, the Pokémon of a Mega Stone, or -1. <c>Count</c> is how many
    /// items had been picked up before this one, which the styles that alternate use so as never to repeat themselves.
    /// </summary>
    public sealed record Item(byte[] Icon, int IconWidth, int IconHeight, string Name, int Amount,
        ItemCategory Category = ItemCategory.Misc, int Power = 0, uint Tint = 0, int Seed = 0, string? Detail = null,
        int Kind = -1, int Count = 0);

    /// <summary>The height of the scene this item needs, in pixels of the game: its style says.</summary>
    public static int HeightFor(Item item) => ItemStyles.For(item.Category).Height;

    /// <summary>How long this item's animation takes, in seconds: what the window waits for.</summary>
    public double LengthFor(Item item) => ItemStyles.For(item.Category).Phases(item).Length;

    /// <summary>The seed of one item picked up: its id and how many items came before it, mixed so that neighbours differ.</summary>
    public static int SeedFor(int itemId, int counter) => unchecked((itemId * 7919) ^ (counter * 104729) ^ (counter << 16));

    /// <summary>
    /// How long it takes to go out when the player carries on (§243): the dither of the exit, at a pace of its own. Time, not
    /// frames, so that it is the same at 30 and at 60 per second.
    /// </summary>
    public const double ExitSeconds = 0.18;

    /// <param name="fade">How much of the scene is left, 1 to 0: what the window lowers when the player goes on and the
    /// animation has to leave at once. The scene goes on playing under it.</param>
    public void Render(Item item, double t, double fade = 1)
    {
        _canvas.Clear();

        var style = ItemStyles.For(item.Category);
        var phases = style.Phases(item);
        if (t < 0 || t >= phases.Length || fade <= 0) return;

        _canvas.Alpha = phases.Alpha(t) * Math.Min(1, fade);
        style.Draw(_canvas, item, phases, t);
    }

    /// <summary>A wrapped parcel, for an item whose icon the cartridge does not have.</summary>
    public static (byte[] Pixels, int Width, int Height) Parcel()
    {
        string[] art =
        [
            "....oo..oo....",
            "...oyyooyyo...",
            "....oyyyyo....",
            "oooooooooooooo",
            "obbbbbyybbbbbo",
            "obbbbbyybbbbbo",
            "oooooooooooooo",
            ".oBBBByyBBBBo.",
            ".oBBBByyBBBBo.",
            ".oBBBByyBBBBo.",
            ".oBBBByyBBBBo.",
            ".oBBBByyBBBBo.",
            ".oooooooooooo.",
        ];
        var colours = new Dictionary<char, uint>
        {
            ['o'] = ItemCanvas.Outline, ['y'] = ItemCanvas.Gold, ['b'] = ItemCanvas.Bgra(0xB0, 0x7B, 0xF0), ['B'] = ItemCanvas.Bgra(0x7A, 0x4C, 0xC0)
        };

        var width = art[0].Length;
        var pixels = new byte[width * art.Length * 4];
        for (var y = 0; y < art.Length; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!colours.TryGetValue(art[y][x], out var c)) continue;
                BitConverter.GetBytes(c).CopyTo(pixels, ((y * width) + x) * 4);
            }
        }

        return (pixels, width, art.Length);
    }
}
