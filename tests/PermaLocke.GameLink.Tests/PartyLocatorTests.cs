using PKHeX.Core;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Guards the filter that decides what counts as a real Pokémon in memory.
/// </summary>
/// <remarks>
/// Written after a live failure: the locator latched onto random heap bytes that decrypted
/// into something with a plausible species and level but an unreadable trainer name, and
/// reported it as the player's party. The checksum test is what rules that out.
/// </remarks>
public sealed class PartyLocatorTests
{
    private static PK7 Sample()
    {
        var pokemon = new PK7
        {
            Species = 722,
            CurrentLevel = 6,
            Stat_HPMax = 24,
            Stat_HPCurrent = 24,
            OriginalTrainerName = "Grenin430"
        };

        pokemon.RefreshChecksum();
        return pokemon;
    }

    [Fact]
    public void A_real_Pokemon_passes()
    {
        Assert.True(PartyLocator.IsPlausible(Sample()));
    }

    [Fact]
    public void A_Pokemon_whose_checksum_does_not_match_is_rejected()
    {
        var pokemon = Sample();

        // Corrupts a field inside the checksummed block without refreshing it, which is what
        // random memory looks like: structurally readable, internally inconsistent.
        // Battle stats such as HP live outside that block and would not invalidate anything,
        // which is precisely why the filter also bounds them separately.
        pokemon.Species = 25;

        Assert.False(PartyLocator.IsPlausible(pokemon));
    }

    [Fact]
    public void Random_bytes_are_rejected()
    {
        var noise = new byte[new PK7().SIZE_PARTY];
        Random.Shared.NextBytes(noise);

        Assert.False(PartyLocator.IsPlausible(new PK7(noise)));
    }

    [Fact]
    public void Impossible_stats_are_rejected_even_with_a_valid_checksum()
    {
        var pokemon = Sample();
        pokemon.Stat_HPMax = 20103;
        pokemon.Stat_HPCurrent = 34246;
        pokemon.RefreshChecksum();

        Assert.False(PartyLocator.IsPlausible(pokemon));
    }

    [Fact]
    public void A_blank_trainer_name_is_rejected()
    {
        var pokemon = Sample();
        pokemon.OriginalTrainerName = string.Empty;
        pokemon.RefreshChecksum();

        Assert.False(PartyLocator.IsPlausible(pokemon));
    }
}
