using PermaLocke.Randomizer;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Which megas may actually be handed out.
/// </summary>
/// <remarks>
/// This exists because of a ghost. The mega table is <b>not indexed by species alone</b>: measured
/// on the expansion mod it has 1330 entries for 1026 species, the rows above the species count
/// being alternate forms, and eight of its keys name nothing at all. Handing one of those out
/// wrote «species 1315, form 4» into a boss — a Pokémon that does not exist, in a file that saved
/// without complaining.
/// </remarks>
public sealed class MegaCandidateTests
{
    private static readonly Dictionary<int, IReadOnlyList<int>> Forms = new()
    {
        [6] = new[] { 1, 2 },       // Charizard X e Y
        [150] = new[] { 1, 2 },     // Mewtwo, prohibido
        [445] = new[] { 1 },        // Garchomp
        [1315] = new[] { 4 }        // fila de forma alternativa: no es una especie
    };

    [Fact]
    public void A_row_above_the_species_count_is_not_a_species()
    {
        var options = new RandomizerOptions { BannedSpecies = [] };

        Assert.DoesNotContain(1315, MegaTrainerRandomizer.Candidates(Forms, options, 1025));
    }

    [Fact]
    public void Banned_legendaries_stay_banned()
    {
        var options = new RandomizerOptions { BannedSpecies = [150] };
        var candidates = MegaTrainerRandomizer.Candidates(Forms, options, 1025);

        Assert.DoesNotContain(150, candidates);
        Assert.Contains(6, candidates);
        Assert.Contains(445, candidates);
    }

    /// <summary>A lower ceiling in the file wins over the game's own.</summary>
    [Fact]
    public void The_configured_ceiling_is_respected()
    {
        var options = new RandomizerOptions { MaxSpecies = 151, BannedSpecies = [] };
        var candidates = MegaTrainerRandomizer.Candidates(Forms, options, 1025);

        Assert.Contains(6, candidates);
        Assert.Contains(150, candidates);
        Assert.DoesNotContain(445, candidates);
    }

    /// <summary>The list comes out ordered, so a seed picks the same mega every time.</summary>
    [Fact]
    public void The_order_is_fixed()
    {
        var candidates = MegaTrainerRandomizer.Candidates(Forms, new RandomizerOptions
        {
            BannedSpecies = []
        }, 1025);

        Assert.Equal(candidates.Order(), candidates);
    }
}
