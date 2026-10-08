using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The random mode of the field items (§163): every spot drawn, with a cap on repeats, like the reference's world.
/// </summary>
/// <remarks>
/// Asked for on 2026-09-22 after comparing the two: PermaLocke shuffled what the cartridge already placed, so every run
/// had the same items, only in other places; the reference draws from the whole catalogue, and never more than twice
/// the same thing.
/// </remarks>
public sealed class FieldItemRandomTests
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

    /// <summary>The spots of the cartridge's world: 493 ordinary ones and 45 gold ones.</summary>
    private const int OrdinarySpots = 493, GoldSpots = 45;

    [Fact]
    public void A_machine_lying_in_two_zones_becomes_one_machine_in_both()
    {
        // MT93 sits in both halves of Poni Canyon in the cartridge: after the shuffle it is one TM there, never two places.
        var original = new[] { 330, 333, 618 };
        var map = FieldItemRandomizer.MachineMap(original, [618, 330, 333]);

        Assert.Equal(618, map[330]);
        Assert.Equal(original.Length, map.Values.Distinct().Count());
    }

    [Fact]
    public void No_item_comes_out_more_times_than_the_cap_and_every_one_is_from_the_pool()
    {
        var pool = Enumerable.Range(100, 534).ToList();

        var drawn = FieldItemRandomizer.DrawCapped(OrdinarySpots, pool, 2, new SeededRandomSource(20260922));

        Assert.Equal(OrdinarySpots, drawn.Count);
        Assert.All(drawn, item => Assert.Contains(item, pool));
        Assert.True(drawn.GroupBy(item => item).Max(group => group.Count()) <= 2);
    }

    /// <summary>
    /// A cap and not an even spread: the reference placed 335 distinct items in its 493 spots, leaving 199 of 534 out.
    /// An even spread would use all 493 before repeating any.
    /// </summary>
    [Fact]
    public void It_repeats_before_using_everything_like_the_reference()
    {
        var pool = Enumerable.Range(100, 534).ToList();

        var distinct = Enumerable.Range(0, 50)
            .Select(seed => FieldItemRandomizer.DrawCapped(OrdinarySpots, pool, 2, new SeededRandomSource((ulong)seed))
                .Distinct().Count())
            .Average();

        Assert.InRange(distinct, 320, 350);
    }

    [Fact]
    public void The_same_seed_places_the_same_items()
    {
        var pool = Enumerable.Range(1, 100).ToList();

        Assert.Equal(
            FieldItemRandomizer.DrawCapped(GoldSpots, pool, 2, new SeededRandomSource(7)),
            FieldItemRandomizer.DrawCapped(GoldSpots, pool, 2, new SeededRandomSource(7)));
    }

    /// <summary>With more spots than the cap allows, the count starts again rather than leaving a spot empty.</summary>
    [Fact]
    public void More_spots_than_the_cap_allows_start_the_count_again()
    {
        var drawn = FieldItemRandomizer.DrawCapped(10, [5, 6], 2, new SeededRandomSource(1));

        Assert.Equal(10, drawn.Count);
        Assert.All(drawn, item => Assert.Contains(item, new[] { 5, 6 }));

        // Cada tanda de cuatro vacía el saco entero antes de empezar otra.
        foreach (var round in drawn.Chunk(4).Where(chunk => chunk.Length == 4))
        {
            Assert.Equal(2, round.Count(item => item == 5));
        }
    }

    [Fact]
    public void An_empty_pool_is_refused_rather_than_leaving_spots_empty() =>
        Assert.Throws<InvalidDataException>(() =>
            FieldItemRandomizer.DrawCapped(3, [], 2, new SeededRandomSource(1)));

    /// <summary>The pocket is bits 7-10 of the u16 at 0x08 of the item's entry.</summary>
    [Fact]
    public void The_pocket_is_read_from_the_item_entry()
    {
        static byte[] Entry(int pocket, int otherBits) // otherBits: los que no son del bolsillo
        {
            var entry = new byte[0x24];
            BitConverter.GetBytes((ushort)((pocket << 7) | otherBits)).CopyTo(entry, 8);
            return entry;
        }

        var pockets = FieldItemRandomizer.ReadPockets([Entry(0, 0x7F), Entry(4, 0), Entry(3, 0x801), new byte[4]]);

        Assert.Equal([0, 4, 3, -1], pockets);
    }

    /// <summary>
    /// General, medicine and berries only: never a key item, a TM, a Z-Crystal or a Roto power, never an unused
    /// «(?)» entry, and never a banned one.
    /// </summary>
    [Fact]
    public void The_pool_is_general_medicine_and_berries_minus_the_banned()
    {
        int[] pockets = [0, 0, 1, 3, 4, 2, 5, 7, 6, 0];
        string[] names = ["", "Master Ball", "Poción", "Baya Zreza", "Amuleto Iris", "MT01", "Normastal Z", "Cristal", "Roto", "(?)"];

        var pool = FieldItemRandomizer.RandomPool(pockets, names, new HashSet<int> { 1 });

        Assert.Equal([2, 3], pool);
    }

    /// <summary>The competition's configuration asks for the reference's shape, and never a Master Ball.</summary>
    [Fact]
    public void The_competition_draws_at_random_at_most_twice_and_never_a_master_ball()
    {
        var options = RandomizerOptionsLoader.Load(Path.Combine(Root(), "Data", "randomizer.json"));

        Assert.True(options.FieldItems);
        Assert.Equal(FieldItemsMode.Random, options.FieldItemsMode);
        Assert.Equal(1, options.FieldItemsMaxRepeats);
        Assert.Contains(1, options.FieldItemsBanned);
    }

    /// <summary>A configuration that does not say keeps what the randomizer always did: shuffle what is there.</summary>
    [Fact]
    public void Without_a_mode_the_old_shuffle_stays() =>
        Assert.Equal(FieldItemsMode.Shuffle, new RandomizerOptions().FieldItemsMode);
}
