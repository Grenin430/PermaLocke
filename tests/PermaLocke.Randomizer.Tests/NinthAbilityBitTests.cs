using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Abilities of nine bits (§132): the gen 8-9 expansion keeps the ninth bit of every slot in the entry's last byte,
/// and a randomizer that writes only the byte hands out abilities that do not exist.
/// </summary>
public sealed class NinthAbilityBitTests
{
    private const int Protosynthesis = 281;

    /// <summary>The cartridge's own ceiling, which is what the expansion's worlds randomize to.</summary>
    private const int CartridgeMax = 233;

    private static readonly IReadOnlyList<int> CartridgePool = [.. Enumerable.Range(1, CartridgeMax)];

    /// <summary>Great Tusk as the expansion stores it: 25 in each slot, and the three ninth bits on.</summary>
    private static byte[] GreatTusk()
    {
        var entry = new byte[PersonalEntry7.Size];

        foreach (var offset in PersonalEntry7.AbilityOffsets)
        {
            entry[offset] = 25;
        }

        entry[PersonalEntry7.AbilityHighBitsOffset] = 0b111;
        return entry;
    }

    [Fact]
    public void The_ninth_bit_is_read_with_the_byte()
    {
        var entry = GreatTusk();

        for (var slot = 0; slot < 3; slot++)
        {
            Assert.Equal(Protosynthesis, PersonalEntry7.GetAbility(entry, 0, slot));
        }
    }

    /// <summary>Kingambit: only its second slot, Supreme Overlord, needs the bit.</summary>
    [Fact]
    public void Each_slot_has_its_own_bit()
    {
        var entry = new byte[PersonalEntry7.Size];
        entry[0x18] = 128;
        entry[0x19] = 37;
        entry[0x1A] = 46;
        entry[PersonalEntry7.AbilityHighBitsOffset] = 0b010;

        Assert.Equal(128, PersonalEntry7.GetAbility(entry, 0, 0));
        Assert.Equal(293, PersonalEntry7.GetAbility(entry, 0, 1));
        Assert.Equal(46, PersonalEntry7.GetAbility(entry, 0, 2));
    }

    [Fact]
    public void Writing_a_small_ability_clears_the_bit_and_only_its_own()
    {
        var entry = GreatTusk();

        PersonalEntry7.SetAbility(entry, 0, 1, 50);

        Assert.Equal(50, PersonalEntry7.GetAbility(entry, 0, 1));
        Assert.Equal(Protosynthesis, PersonalEntry7.GetAbility(entry, 0, 0));
        Assert.Equal(Protosynthesis, PersonalEntry7.GetAbility(entry, 0, 2));
        Assert.Equal(0b101, entry[PersonalEntry7.AbilityHighBitsOffset]);
    }

    [Fact]
    public void Writing_a_large_ability_sets_the_bit()
    {
        var entry = new byte[PersonalEntry7.Size];

        PersonalEntry7.SetAbility(entry, 0, 2, 288);

        Assert.Equal(288, PersonalEntry7.GetAbility(entry, 0, 2));
        Assert.Equal(0b100, entry[PersonalEntry7.AbilityHighBitsOffset]);
    }

    [Fact]
    public void Nothing_past_nine_bits_is_written()
    {
        var entry = new byte[PersonalEntry7.Size];

        Assert.Throws<ArgumentOutOfRangeException>(() => PersonalEntry7.SetAbility(entry, 0, 0, 512));
        Assert.Throws<ArgumentOutOfRangeException>(() => PersonalEntry7.SetAbility(entry, 0, 0, -1));
    }

    /// <summary>The bug itself: after randomizing to the cartridge's abilities, no bit may be left behind.</summary>
    [Fact]
    public void Randomizing_to_the_cartridge_leaves_no_ninth_bit_behind()
    {
        var entry = GreatTusk();

        PokemonDataRandomizer.RandomizeAbilities(entry, 0, new SeededRandomSource(7), new SeededRandomSource(8),
            CartridgePool);

        for (var slot = 0; slot < 3; slot++)
        {
            Assert.InRange(PersonalEntry7.GetAbility(entry, 0, slot), 1, CartridgeMax);
        }

        Assert.Equal(0, entry[PersonalEntry7.AbilityHighBitsOffset]);
    }

    /// <summary>
    /// The fix must not change the draws: a world regenerated with the same seed keeps every ability it had, and
    /// only loses the bits. The same entry with and without them gets the same three abilities.
    /// </summary>
    [Fact]
    public void The_bits_do_not_change_which_abilities_come_out()
    {
        var flagged = GreatTusk();
        var plain = GreatTusk();
        plain[PersonalEntry7.AbilityHighBitsOffset] = 0;

        PokemonDataRandomizer.RandomizeAbilities(flagged, 0, new SeededRandomSource(7), new SeededRandomSource(8), CartridgePool);
        PokemonDataRandomizer.RandomizeAbilities(plain, 0, new SeededRandomSource(7), new SeededRandomSource(8), CartridgePool);

        Assert.Equal(plain, flagged);
    }

    /// <summary>
    /// Ability 256 has a byte of zero, and the one-byte reader took its slot for empty and drew nothing for it. It
    /// is randomized now, from its own stream, so the slots after it get exactly the draws they always got.
    /// </summary>
    [Fact]
    public void A_slot_holding_256_is_randomized_without_moving_the_others()
    {
        // Antes: el primer hueco «vacío» (256 leído como 0) y los otros dos de verdad.
        var before = new byte[PersonalEntry7.Size];
        before[0x19] = 30;
        before[0x1A] = 40;

        var now = (byte[])before.Clone();
        now[PersonalEntry7.AbilityHighBitsOffset] = 0b001;

        PokemonDataRandomizer.RandomizeAbilities(before, 0, new SeededRandomSource(7), new SeededRandomSource(8), CartridgePool);
        PokemonDataRandomizer.RandomizeAbilities(now, 0, new SeededRandomSource(7), new SeededRandomSource(8), CartridgePool);

        Assert.Equal(0, PersonalEntry7.GetAbility(before, 0, 0));
        Assert.InRange(PersonalEntry7.GetAbility(now, 0, 0), 1, CartridgeMax);

        Assert.Equal(PersonalEntry7.GetAbility(before, 0, 1), PersonalEntry7.GetAbility(now, 0, 1));
        Assert.Equal(PersonalEntry7.GetAbility(before, 0, 2), PersonalEntry7.GetAbility(now, 0, 2));
        Assert.Equal(0, now[PersonalEntry7.AbilityHighBitsOffset]);
    }
}
