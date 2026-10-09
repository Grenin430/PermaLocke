using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

/// <summary>The item going into the bag (2026-09-28): drawn in game pixels that need not be whole monitor pixels.</summary>
public sealed class ItemSceneTests
{
    /// <summary>
    /// The bag's body is solid art. With the game's pixel at 2.4 monitor pixels, blocks of two left a transparent line
    /// every few columns and rows through it (2026-10-09, «salen unas rectas negras»).
    /// </summary>
    [Theory]
    [InlineData(1.4)]
    [InlineData(2.4)]
    [InlineData(1.7)]
    [InlineData(3)]
    public void The_bag_has_no_holes_whatever_the_size_of_the_game_pixel(double pixel)
    {
        var scene = new ItemScene(pixel);
        var (icon, width, height) = ItemScene.Parcel();
        scene.Render(new ItemScene.Item(icon, width, height, "MT85 - VUELO", 1), 0.5);

        // Bag body: art rows 9-17, columns 3-20, with the bag's bottom at row 58 and 20 rows of art.
        int Edge(int game) => (int)Math.Round(game * pixel);
        for (var y = Edge(38 + 9); y < Edge(38 + 18); y++)
        {
            for (var x = Edge(6 + 3); x < Edge(6 + 21); x++)
            {
                Assert.Equal(0xFF, scene.Pixels[(((y * scene.Width) + x) * 4) + 3]);
            }
        }
    }
}
