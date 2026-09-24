using Microsoft.Extensions.Logging.Abstractions;
using PKHeX.Core;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink.Data;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// What the wheel writes into the player's game.
/// </summary>
/// <remarks>
/// Against a save built here and kept in memory, the same limit the EV trainer and the name repair
/// have: PKHeX does not recognise a blank save written back to disk. The file round trip was
/// proved separately on a copy of the real partida — Poción 8 → 13 and an MT02 that was not there —
/// and then in the real game, twice.
/// </remarks>
public sealed class SaveRouletteWorldTests
{
    private static SaveRouletteWorld World() => new(
        new PlayerSave(new AzaharInstallation(NullLogger<AzaharInstallation>.Instance),
            new Rpc.AzaharRpcClient(), AppContext.BaseDirectory),
        Path.Combine(Path.GetTempPath(), "permalocke-tests"),
        new PkhexSpeciesLookup(),
        new PkhexItemLookup(),
        new PkhexAbilityLookup(),
        NullLogger<SaveRouletteWorld>.Instance);

    private static SAV7USUM Save(int party = 3)
    {
        var game = new SAV7USUM { OT = "Grenin", TID16 = 111, SID16 = 222 };

        for (var slot = 0; slot < party; slot++)
        {
            var pokemon = PokemonBuilder.Build(
                new NewPokemon(25 + slot, 20, 0, 0, [10, 11, 12, 13, 14, 15], false), game);

            pokemon.PID = (uint)(0x1000 + slot);
            pokemon.RefreshChecksum();
            game.SetPartySlotAtIndex(pokemon, slot, PokemonBuilder.InPlace);
        }

        return game;
    }

    private static RoulettePokemon Target(SAV7USUM game, int slot)
    {
        var pokemon = (PK7)game.GetPartySlotAtIndex(slot);
        return new RoulettePokemon(slot, pokemon.PID, pokemon.Species, pokemon.Nickname, pokemon.CurrentLevel);
    }

    [Fact]
    public void It_sees_the_party_and_the_bag()
    {
        var seen = World().Look(Save());

        Assert.Equal(3, seen.Party.Count);
        Assert.Equal(0x1000u, seen.Party[0].Pid);

        // Las cien MT del cartucho, que no son un rango: las da el propio bolsillo.
        Assert.Equal(100, seen.TmsAll.Count);
        Assert.Contains(328, seen.TmsAll);
        Assert.Contains(694, seen.TmsAll);
        Assert.Empty(seen.TmsHeld);
    }

    [Fact]
    public void An_ability_face_writes_the_ability()
    {
        var game = Save();
        var action = new RouletteAction(RouletteEffect.HabilidadBuena,
            [Target(game, 0), Target(game, 2)], [22, 26], [], 0);

        var lines = World().ApplyTo(game, action);

        Assert.Equal(2, lines.Count);
        Assert.Equal(22, ((PK7)game.GetPartySlotAtIndex(0)).Ability);
        Assert.Equal(26, ((PK7)game.GetPartySlotAtIndex(2)).Ability);
        Assert.True(((PK7)game.GetPartySlotAtIndex(0)).ChecksumValid);

        // Al que no le tocaba no se le toca.
        Assert.NotEqual(22, ((PK7)game.GetPartySlotAtIndex(1)).Ability);
    }

    /// <summary>
    /// A face that hands an old ability to a Pokémon carrying one of the mod's gives exactly that
    /// ability, not that ability plus 256.
    /// </summary>
    /// <remarks>
    /// §134: the ninth bit lives in 0x15. Writing only the byte at 0x14 — what the face did — left
    /// it behind, so Imán (22) on a Palafin with Cambio Heroico (278) would have come out as 278.
    /// </remarks>
    [Fact]
    public void An_ability_face_clears_the_ninth_bit_of_a_new_ability()
    {
        var game = Save();
        var palafin = (PK7)game.GetPartySlotAtIndex(0);
        PokemonAbility.Set(palafin, 278);
        palafin.RefreshChecksum();
        game.SetPartySlotAtIndex(palafin, 0, PokemonBuilder.InPlace);

        var action = new RouletteAction(RouletteEffect.HabilidadMala, [Target(game, 0)], [22], [], 0);
        World().ApplyTo(game, action);

        Assert.Equal(22, PokemonAbility.Of((PK7)game.GetPartySlotAtIndex(0)));
    }

    [Fact]
    public void Perfect_ivs_are_all_six()
    {
        var game = Save();
        var lines = World().ApplyTo(game,
            new RouletteAction(RouletteEffect.IvPerfectos, [Target(game, 1)], [], [], 0));

        var pokemon = (PK7)game.GetPartySlotAtIndex(1);

        Assert.Single(lines);
        Assert.Equal([31, 31, 31, 31, 31, 31],
            new[] { pokemon.IV_HP, pokemon.IV_ATK, pokemon.IV_DEF, pokemon.IV_SPA, pokemon.IV_SPD, pokemon.IV_SPE });
    }

    [Fact]
    public void Zero_ivs_are_all_six_too()
    {
        var game = Save();
        World().ApplyTo(game, new RouletteAction(RouletteEffect.IvCero, [Target(game, 0)], [], [], 0));

        var pokemon = (PK7)game.GetPartySlotAtIndex(0);

        Assert.Equal(0, pokemon.IV_HP + pokemon.IV_ATK + pokemon.IV_DEF
                        + pokemon.IV_SPA + pokemon.IV_SPD + pokemon.IV_SPE);
    }

    /// <summary>
    /// Death is the mark the rest of PermaLocke uses, and the line names who it was.
    /// </summary>
    /// <remarks>
    /// The naming is the §59 lesson: a destructive write that does not record what it destroyed
    /// leaves a Shedinja nobody can identify afterwards.
    /// </remarks>
    [Fact]
    public void Death_leaves_the_mark_and_says_who_it_was()
    {
        var game = Save();
        var victim = (PK7)game.GetPartySlotAtIndex(1);
        var was = victim.Nickname;

        var lines = World().ApplyTo(game,
            new RouletteAction(RouletteEffect.Muerte, [Target(game, 1)], [], [], 0));

        var after = (PK7)game.GetPartySlotAtIndex(1);

        // La marca ya no es un Shedinja: es quedarse sin PS, siendo él. La rueda mata igual, pero
        // lo que deja es el Pokémon debilitado y no otro Pokémon distinto.
        Assert.Equal(0, after.Stat_HPCurrent);
        Assert.True(PermaLocke.GameLink.Data.DeathMark.IsMarked(after));
        Assert.Equal(victim.Species, after.Species);
        Assert.Equal(was, after.Nickname);
        Assert.True(after.ChecksumValid);
        Assert.Contains(was, Assert.Single(lines));
    }

    /// <summary>
    /// A slot that no longer holds who the wheel picked is skipped, not overwritten.
    /// </summary>
    /// <remarks>
    /// The wheel decides against a reading of the party; if the player kept playing in between,
    /// the slot can hold somebody else. Writing to it anyway would kill the wrong Pokémon.
    /// </remarks>
    [Fact]
    public void A_slot_that_changed_hands_is_left_alone()
    {
        var game = Save();
        var stale = Target(game, 0) with { Pid = 0xDEADBEEF };

        var lines = World().ApplyTo(game,
            new RouletteAction(RouletteEffect.Muerte, [stale], [], [], 0));

        Assert.Equal(25, ((PK7)game.GetPartySlotAtIndex(0)).Species);
        Assert.Contains("no se le ha tocado", Assert.Single(lines));
    }

    [Fact]
    public void Items_are_added_to_the_right_pouch()
    {
        var game = Save();

        var lines = World().ApplyTo(game, new RouletteAction(RouletteEffect.DarCurativos, [], [],
            [new RouletteItemChange(17, 3), new RouletteItemChange(28, 3)], 0));

        var medicine = game.Inventory.Pouches.First(p => p.Type == InventoryType.Medicine);

        Assert.Equal(2, lines.Count);
        Assert.Equal(3, medicine.Items.First(i => i.Index == 17).Count);
        Assert.Equal(3, medicine.Items.First(i => i.Index == 28).Count);
    }

    [Fact]
    public void A_tm_lands_in_the_tm_pouch()
    {
        var game = Save();

        World().ApplyTo(game, new RouletteAction(RouletteEffect.DarMt, [], [],
            [new RouletteItemChange(381, 1)], 0));

        var tms = game.Inventory.Pouches.First(p => p.Type == InventoryType.TMHMs);

        Assert.Equal(1, tms.Items.First(i => i.Index == 381).Count);
    }

    /// <summary>Taking what is not there does nothing, and says so.</summary>
    [Fact]
    public void Taking_an_item_that_is_not_carried_changes_nothing()
    {
        var game = Save();

        var line = Assert.Single(World().ApplyTo(game, new RouletteAction(RouletteEffect.QuitarMt, [], [],
            [new RouletteItemChange(381, -1)], 0)));

        Assert.Contains("no llevabas ninguno", line);
        Assert.DoesNotContain(game.Inventory.Pouches.First(p => p.Type == InventoryType.TMHMs).Items,
            i => i.Index == 381 && i.Count > 0);
    }

    /// <summary>A bag cannot go below zero, however much the wheel asks for.</summary>
    [Fact]
    public void Taking_more_than_there_is_empties_it_and_stops()
    {
        var game = Save();
        World().ApplyTo(game, new RouletteAction(RouletteEffect.DarCurativos, [], [],
            [new RouletteItemChange(17, 2)], 0));

        World().ApplyTo(game, new RouletteAction(RouletteEffect.QuitarCurativos, [], [],
            [new RouletteItemChange(17, -9)], 0));

        var medicine = game.Inventory.Pouches.First(p => p.Type == InventoryType.Medicine);

        Assert.DoesNotContain(medicine.Items, i => i.Index == 17 && i.Count > 0);
    }

    [Fact]
    public void Nothing_to_do_writes_nothing()
    {
        var game = Save();

        Assert.Empty(World().ApplyTo(game, RouletteAction.Nothing(RouletteEffect.IvPerfectos)));
    }
}
