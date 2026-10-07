using System.Text.Json;
using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The icons the LUDÓPATA wheel asks for, against the table of icons that have been measured.
/// </summary>
/// <remarks>
/// <para>
/// This is the test that stops a wedge going blank in silence. <see cref="ItemIconIndex.TryGet"/>
/// answers false for an item nobody has looked at, and the screen turns that into «no picture»
/// rather than into the wrong picture — which is right, and is also invisible. The only place the
/// mismatch can be caught is here, comparing the shipped file against the shipped table.
/// </para>
/// <para>
/// It reads <c>Data/roulette.json</c> raw instead of through the catalogue: this project cannot
/// see <c>PermaLocke.Data</c>, and pulling one field out of a file does not need it.
/// </para>
/// </remarks>
[Collection("ItemIconIndex")]
public sealed class RouletteWheelIconTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static List<int> WantedItems()
    {
        using var stream = File.OpenRead(Path.Combine(Root(), "Data", "roulette.json"));
        using var document = JsonDocument.Parse(stream);

        return [.. document.RootElement.GetProperty("caras").EnumerateArray()
            .Where(face => face.TryGetProperty("icono", out _))
            .Select(face => face.GetProperty("icono").GetInt32())];
    }

    [Fact]
    public void Every_item_the_wheel_draws_has_a_measured_icon()
    {
        var wanted = WantedItems();

        Assert.NotEmpty(wanted);

        foreach (var item in wanted.Distinct())
        {
            Assert.True(ItemIconIndex.TryGet(item, out _),
                $"El objeto {item} sale en la ruleta y su icono no está medido en ItemIconIndex.");
        }
    }

    /// <summary>
    /// The three measured for this wheel, with the neighbour that anchored each one.
    /// </summary>
    /// <remarks>
    /// Anchored by looking, as §45 requires. Icon 308 is a white curved fang — the Sharp Fang,
    /// item 327 — so the disc right after it is TM01. And 649, 650 and 651 are a silver cap, a
    /// gold cap and a dark blue bracelet, in that order, which is exactly items 795, 796 and 797.
    /// One round thing could be anything; three in that order could not.
    /// </remarks>
    [Theory]
    [InlineData(328, 309)] // MT01
    [InlineData(795, 649)] // Chapa Plateada
    [InlineData(796, 650)] // Chapa Dorada
    public void The_icons_measured_for_the_wheel_are_where_they_were_seen(int item, int expected)
    {
        Assert.True(ItemIconIndex.TryGet(item, out var icon));
        Assert.Equal(expected, icon);
    }

    /// <summary>
    /// The TM entry must not be read as the whole block: only the first is claimed.
    /// </summary>
    /// <remarks>
    /// The hundred TMs share twenty discs, so there is no id-to-icon mapping for the rest and
    /// inventing one would draw the wrong type's disc. The wheel only ever says «a TM».
    /// </remarks>
    [Fact]
    public void Only_the_first_machine_is_claimed()
    {
        Assert.True(ItemIconIndex.TryGet(328, out _));
        Assert.False(ItemIconIndex.TryGet(329, out _));
        Assert.False(ItemIconIndex.TryGet(427, out _));
    }
}
