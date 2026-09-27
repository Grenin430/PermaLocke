using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Battle;

namespace PermaLocke.App.Tests;

/// <summary>The cap panel takes the party's HP from the battle in progress (1.0.4.9).</summary>
public sealed class CapBadgeTests
{
    private static LivePartyMember Member(int slot, int species, int hp, int max) =>
        new(slot, species, "", "", 20, hp, max, false, (uint)slot + 1, 0, "", "");

    [Fact]
    public void In_battle_the_hp_comes_from_the_battle_and_an_unclear_match_keeps_the_party()
    {
        IReadOnlyList<LivePartyMember> party = [Member(0, 25, 50, 50), Member(1, 19, 40, 40), Member(2, 19, 40, 40)];
        var table = new BattleTable(0, [
            new BattleBlock(0, 0, 25, 50, 12, 0),
            new BattleBlock(0, 0, 19, 40, 5, 1),
            new BattleBlock(0, 0, 19, 40, 9, 2),
            new BattleBlock(0, 0, 25, 50, 1, 12)]);

        var shown = CapBadge.WithBattleHp(party, [table]);

        Assert.Equal(12, shown[0].CurrentHp);
        // Dos Rattata con los mismos PS máximos: no se sabe cuál es cuál, se queda lo del equipo.
        Assert.Equal(40, shown[1].CurrentHp);
        Assert.Same(party, CapBadge.WithBattleHp(party, []));
    }

    [Fact]
    public void A_level_up_mid_battle_keeps_the_same_block()
    {
        IReadOnlyList<LivePartyMember> party = [Member(0, 25, 50, 50)];
        var bound = new Dictionary<uint, int>();
        CapBadge.WithBattleHp(party, [new BattleTable(0, [new BattleBlock(0, 0, 25, 50, 20, 0)])], bound);

        // Sube de nivel: el combate ya tiene 54 de máximo, el equipo aún no.
        var shown = CapBadge.WithBattleHp(party, [new BattleTable(0, [new BattleBlock(0, 0, 25, 54, 24, 0)])], bound);

        Assert.Equal(24, shown[0].CurrentHp);
        Assert.Equal(54, shown[0].MaxHp);
    }
}
