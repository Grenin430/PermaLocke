using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Battle;
using PKHeX.Core;

namespace PermaLocke.GameLink.Tests;

public sealed class BattleIdentityTests
{
    private static readonly BattleFaint Faint = new(0, 831, true);
    private static LivePartyMember Member(int slot, uint pid, int species = 831) =>
        new(slot, species, "Pokemon", "Pokemon", 5, 20, 20, false, pid, 0, "", "");
    private static PK7 Identity(uint pid)
    {
        var pk = new PK7 { Species = 831, PID = pid };
        pk.RefreshChecksum();
        return pk;
    }

    [Fact]
    public void A_battle_position_that_differs_from_the_party_is_resolved_by_identity()
    {
        var wooloo = Member(1, 123);
        Assert.Equal(wooloo, BattlePokemon.MatchPlayer(Faint, [Member(0, 456, 349), wooloo], [Identity(123)]));
    }

    [Fact]
    public void Two_of_the_same_species_are_distinguished_by_pid_not_slot()
    {
        var fallen = Member(1, 123);
        Assert.Equal(fallen, BattlePokemon.MatchPlayer(Faint, [Member(0, 456), fallen], [Identity(123), Identity(123)]));
    }

    [Fact]
    public void Disagreeing_identities_never_choose_a_death()
    {
        Assert.Null(BattlePokemon.MatchPlayer(Faint, [Member(0, 123), Member(1, 456)], [Identity(123), Identity(456)]));
    }

    [Fact]
    public void An_unknown_pid_does_not_fall_back_to_a_different_pokemon_in_the_same_slot()
    {
        Assert.Null(BattlePokemon.MatchPlayer(Faint, [Member(0, 123)], [Identity(456)]));
    }

    [Fact]
    public void Unreadable_pk7_preserves_the_unambiguous_original_slot_fallback()
    {
        var member = Member(0, 123);
        Assert.Equal(member, BattlePokemon.MatchPlayer(Faint, [member], [null]));
    }

    [Fact]
    public void An_unreadable_identity_does_not_choose_between_duplicates()
    {
        Assert.Null(BattlePokemon.MatchPlayer(Faint, [Member(0, 123), Member(1, 456)], [null]));
    }

    [Fact]
    public void A_broken_checksum_cannot_resolve_a_mismatched_slot()
    {
        var broken = Identity(123);
        broken.PID++;
        Assert.False(broken.ChecksumValid);
        Assert.Null(BattlePokemon.MatchPlayer(Faint, [Member(1, 124)], [broken]));
    }
}
