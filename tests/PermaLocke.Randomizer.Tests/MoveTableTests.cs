using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Guards the one thing standing between a randomized learnset and a Z-move: a Pokémon offered
/// an attack with one PP and several hundred power, which the game hands over like any other.
/// </summary>
public sealed class MoveTableTests
{
    /// <summary>Move id to PP, as the cartridge stores it. Index 0 is the placeholder.</summary>
    private static int[] Pp(params int[] values) => values;

    [Fact]
    public void A_move_with_one_pp_is_never_teachable()
    {
        var teachable = MoveTable.Teachable(Pp(0, 35, 1, 15, 1, 20), maxMove: 5);

        Assert.Equal([1, 3, 5], teachable);
    }

    /// <summary>The placeholder at id 0 is not a move and must not be handed out.</summary>
    [Fact]
    public void The_zero_entry_is_left_out()
    {
        var teachable = MoveTable.Teachable(Pp(40, 35), maxMove: 1);

        Assert.Equal([1], teachable);
    }

    /// <summary>
    /// The table can be shorter than the highest id the game claims; asking past its end must
    /// leave the move out rather than throw in the middle of a randomization.
    /// </summary>
    [Fact]
    public void Ids_past_the_end_of_the_table_are_left_out()
    {
        var teachable = MoveTable.Teachable(Pp(0, 35, 20), maxMove: 900);

        Assert.Equal([1, 2], teachable);
    }

    /// <summary>
    /// An unreadable table gives nothing, which is what makes the randomizer refuse out loud
    /// instead of falling back to handing out every move.
    /// </summary>
    [Fact]
    public void An_empty_table_teaches_nothing()
    {
        Assert.Empty(MoveTable.Teachable([], maxMove: 728));
        Assert.Empty(MoveTable.Teachable(Pp(0, 40), maxMove: 0));
    }

    /// <summary>
    /// The shape the real cartridge has: Ultra Moon knows 728 moves and 55 of them carry one PP —
    /// the eighteen type Z-moves with their two variants each, the exclusive ones, Struggle and
    /// Sketch. Measured off the ROM, and kept here so a change to the rule has to face the number.
    /// </summary>
    [Fact]
    public void The_ultra_moon_shape_leaves_out_fifty_five_moves()
    {
        var pp = new int[729];
        Array.Fill(pp, 15);
        pp[0] = 0;

        int[] forbidden =
        [
            165, 166, // Forcejeo y Esquema
            .. Enumerable.Range(622, 37), // los dieciocho de tipo, dos variantes cada uno, y Pikavoltio
            .. Enumerable.Range(695, 9), // los exclusivos de Sol y Luna
            719, // Gigarrayo Fulminante
            .. Enumerable.Range(723, 6), // los que añade Ultra
        ];

        foreach (var id in forbidden)
        {
            pp[id] = 1;
        }

        var teachable = MoveTable.Teachable(pp, maxMove: 728);

        Assert.Equal(55, forbidden.Distinct().Count());
        Assert.Equal(728 - 55, teachable.Count);
        Assert.DoesNotContain(646, teachable); // Gigavoltio Destructor
        Assert.DoesNotContain(658, teachable); // Pikavoltio Letal
        Assert.DoesNotContain(728, teachable); // Estruendo Implacable
        Assert.Contains(1, teachable); // Destructor
    }
}
