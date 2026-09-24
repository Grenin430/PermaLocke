using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Cutting the category sheet of <c>a/0/6/6</c> into its three icons (§144), on a sheet built here.
/// </summary>
public sealed class MoveCategoryIconTests
{
    private static readonly (byte R, byte G, byte B) Grey = (150, 146, 140);
    private static readonly (byte R, byte G, byte B) Orange = (240, 80, 16);
    private static readonly (byte R, byte G, byte B) Blue = (40, 100, 210);

    /// <summary>A 64×64 sheet with 41×18 bands at rows 1, 21 and 41, as the cartridge has them.</summary>
    private static byte[] Sheet(params (byte R, byte G, byte B)[] bands) => Sheet(18, bands);

    private static byte[] Sheet(int height, params (byte R, byte G, byte B)[] bands)
    {
        var pixels = new byte[64 * 64 * 4];

        for (var band = 0; band < bands.Length; band++)
        {
            for (var y = 1 + (band * 20); y < 1 + (band * 20) + (band == 2 ? height : 18); y++)
            {
                for (var x = 5; x <= 45; x++)
                {
                    var at = ((y * 64) + x) * 4;
                    (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) =
                        (bands[band].R, bands[band].G, bands[band].B, 255);
                }
            }
        }

        return pixels;
    }

    /// <summary>Which is which comes from the colour, whatever the order on the sheet.</summary>
    [Fact]
    public void Each_icon_is_named_by_its_colour_not_by_its_place()
    {
        var icons = MoveCategoryIconReader.Split(64, 64, Sheet(Blue, Grey, Orange));

        Assert.NotNull(icons);
        Assert.Equal(3, icons.Count);
        Assert.All(icons.Values, icon => Assert.Equal((41, 18), (icon.Width, icon.Height)));

        // El primer píxel de cada recorte es del color de su banda.
        Assert.Equal(Blue.B, icons[MoveCategoryIcon.Special].Pixels[2]);
        Assert.Equal(Orange.R, icons[MoveCategoryIcon.Physical].Pixels[0]);
        Assert.Equal(Grey.G, icons[MoveCategoryIcon.Status].Pixels[1]);
    }

    /// <summary>Two blues are not a category sheet: refused, not guessed.</summary>
    [Fact]
    public void A_sheet_without_one_of_each_is_refused()
    {
        Assert.Null(MoveCategoryIconReader.Split(64, 64, Sheet(Blue, Blue, Orange)));
        Assert.Null(MoveCategoryIconReader.Split(64, 64, Sheet(Grey, Orange)));
    }

    [Fact]
    public void Bands_of_different_sizes_are_refused()
    {
        Assert.Null(MoveCategoryIconReader.Split(64, 64, Sheet(12, Grey, Orange, Blue)));
    }
}
