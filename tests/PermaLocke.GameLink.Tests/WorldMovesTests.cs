using PKHeX.Core;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// The installed world's learnsets and moves (§142): what the reminder offers and what the builder hands out.
/// </summary>
/// <remarks>Global state, like <see cref="WorldLimits"/>: every test puts it back as it found it, and runs alone.</remarks>
[Collection("WorldLimits")]
public sealed class WorldMovesTests
{
    /// <summary>
    /// A banned move (§162) is left out of every learnset the application reads, so neither the builder nor the
    /// reminder can hand one out even from a world generated before the ban.
    /// </summary>
    [Fact]
    public void A_banned_move_is_left_out_of_the_learnsets_and_of_what_a_pokemon_is_built_with()
    {
        try
        {
            Publish(new() { [26] = [new(33, 1), new(90, 10), new(45, 20)] });
            WorldMoves.Banned = new HashSet<int> { 90 };

            Assert.Equal([33, 45], WorldMoves.LevelUpOf(26, 0)!.Select(entry => entry.Move));
            Assert.DoesNotContain(90, WorldMoves.MovesAt(26, 0, 50)!);
        }
        finally
        {
            Clear();
        }
    }

    private static void Publish(Dictionary<int, IReadOnlyList<LevelUpMove>> rows, int count = 30,
        Dictionary<(int, int), int>? forms = null, IReadOnlyList<WorldMove>? moves = null)
    {
        var all = new IReadOnlyList<LevelUpMove>[count];

        for (var row = 0; row < count; row++)
        {
            all[row] = rows.TryGetValue(row, out var list) ? list : [];
        }

        WorldMoves.Learnsets = all;
        WorldMoves.FormRows = forms ?? [];
        WorldMoves.Moves = moves;
    }

    private static void Clear()
    {
        WorldMoves.Learnsets = null;
        WorldMoves.FormRows = new Dictionary<(int Species, int Form), int>();
        WorldMoves.Moves = null;
        WorldMoves.MoveNames = null;
        WorldMoves.Banned = new HashSet<int>();
    }

    /// <summary>A form with a row of its own reads that row; any other form reads its species'.</summary>
    [Fact]
    public void A_form_with_its_own_row_reads_that_row()
    {
        try
        {
            Publish(new() { [26] = [new(1, 1)], [28] = [new(2, 1)] }, forms: new() { [(26, 1)] = 28 });

            Assert.Equal(2, WorldMoves.LevelUpOf(26, 1)![0].Move);
            Assert.Equal(1, WorldMoves.LevelUpOf(26, 0)![0].Move);
            Assert.Equal(1, WorldMoves.LevelUpOf(26, 5)![0].Move);
        }
        finally
        {
            Clear();
        }
    }

    /// <summary>What the game gives a wild one: the last four it has learnt, each once.</summary>
    [Fact]
    public void The_moves_at_a_level_are_the_last_four_learnt()
    {
        try
        {
            Publish(new()
            {
                [25] = [new(10, 1), new(11, 1), new(12, 5), new(11, 8), new(13, 10), new(14, 15), new(15, 30)]
            });

            Assert.Equal([11, 12, 13, 14], WorldMoves.MovesAt(25, 0, 20)!);
            Assert.Equal([10, 11, 12, 0], WorldMoves.MovesAt(25, 0, 7)!);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public void Without_a_published_world_it_says_it_does_not_know()
    {
        Clear();

        Assert.Null(WorldMoves.LevelUpOf(25, 0));
        Assert.Null(WorldMoves.MovesAt(25, 0, 50));
        Assert.Null(WorldMoves.MoveOf(33));
    }

    /// <summary>
    /// With a world installed, what PermaLocke builds knows what its species learns THERE — randomized — and with
    /// the world's PP, not PKHeX's cartridge moveset. Before §142 a gacha Pokémon came with the cartridge's moves.
    /// </summary>
    [Fact]
    public void A_built_pokemon_knows_what_its_species_learns_in_the_installed_world()
    {
        try
        {
            var moves = new WorldMove[800];
            Array.Fill(moves, new WorldMove(0, 1, 40, 100, 35));
            moves[(int)Move.Surf] = new WorldMove(10, 2, 90, 100, 7);

            Publish(new() { [25] = [new((int)Move.Surf, 1), new((int)Move.Growl, 1), new((int)Move.Tackle, 1)] },
                moves: moves);

            var pikachu = PokemonBuilder.Build(
                new NewPokemon(25, 20, Nature: 0, AbilityId: 9, Ivs: [31, 31, 31, 31, 31, 31], IsShiny: false),
                new SAV7USUM { OT = "Grenin", Language = (int)LanguageID.Spanish });

            Assert.Equal([(ushort)Move.Surf, (ushort)Move.Growl, (ushort)Move.Tackle, 0],
                [pikachu.Move1, pikachu.Move2, pikachu.Move3, pikachu.Move4]);
            Assert.Equal(7, pikachu.Move1_PP);

            // Y los mismos como «para volver a aprender», que es lo que el recuerda-movimientos ofrece siempre.
            Assert.Equal(pikachu.Move1, pikachu.RelearnMove1);
            Assert.Equal(pikachu.Move3, pikachu.RelearnMove3);
        }
        finally
        {
            Clear();
        }
    }
}
