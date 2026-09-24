using PermaLocke.Core.Services;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The one-hit knockouts are out of everything a Pokémon can get a move from (§162).
/// </summary>
/// <remarks>
/// Asked for on 2026-09-22. Measured on the installed world first: 65 in level-up learnsets and 16 in egg moves, none in
/// TMs, tutors, trainer movesets or statics — the trainer and static cases are guards for worlds that keep a species.
/// </remarks>
public sealed class BannedMoveTests
{
    private const int Fissure = 90, SheerCold = 329, Tackle = 33, Growl = 45, Leer = 43, Harden = 106;
    private static readonly HashSet<int> Banned = [12, 32, Fissure, SheerCold];

    private static byte[] Learnset(params (int Move, int Level)[] pairs)
    {
        var bytes = new byte[(pairs.Length + 1) * 4];
        for (var i = 0; i < pairs.Length; i++)
        {
            BitConverter.GetBytes((ushort)pairs[i].Move).CopyTo(bytes, i * 4);
            BitConverter.GetBytes((ushort)pairs[i].Level).CopyTo(bytes, (i * 4) + 2);
        }
        BitConverter.GetBytes(uint.MaxValue).CopyTo(bytes, pairs.Length * 4);
        return bytes;
    }

    private static List<(int Move, int Level)> Read(byte[] learnset)
    {
        var pairs = new List<(int, int)>();
        for (var at = 0; BitConverter.ToUInt16(learnset, at) != 0xFFFF; at += 4)
            pairs.Add((BitConverter.ToUInt16(learnset, at), BitConverter.ToUInt16(learnset, at + 2)));
        return pairs;
    }

    [Fact]
    public void Only_the_banned_slots_of_a_learnset_change_and_their_levels_stay()
    {
        var entry = Learnset((Tackle, 1), (Fissure, 12), (Growl, 20), (SheerCold, 40));

        var replaced = BannedMoveScrubber.ScrubLearnset(entry, Banned, [Leer, Harden, Growl], new SeededRandomSource(7));

        Assert.Equal(2, replaced);
        var after = Read(entry);
        Assert.Equal([1, 12, 20, 40], after.Select(pair => pair.Level));
        Assert.Equal(Tackle, after[0].Move);
        Assert.Equal(Growl, after[2].Move);
        Assert.DoesNotContain(after, pair => Banned.Contains(pair.Move));

        // Nada repetido: Growl ya lo tenía, así que los dos huecos se llenan con Leer y Harden.
        Assert.Equal(after.Count, after.Select(pair => pair.Move).Distinct().Count());
    }

    [Fact]
    public void A_learnset_without_banned_moves_is_left_byte_for_byte()
    {
        var entry = Learnset((Tackle, 1), (Growl, 5));
        var before = entry.ToArray();

        Assert.Equal(0, BannedMoveScrubber.ScrubLearnset(entry, Banned, [Leer], new SeededRandomSource(1)));
        Assert.Equal(before, entry);
    }

    [Fact]
    public void Egg_moves_lose_the_banned_ones()
    {
        var entry = new byte[4 + 3 * 2];
        BitConverter.GetBytes((ushort)0).CopyTo(entry, 0);
        BitConverter.GetBytes((ushort)3).CopyTo(entry, 2);
        BitConverter.GetBytes((ushort)Growl).CopyTo(entry, 4);
        BitConverter.GetBytes((ushort)Fissure).CopyTo(entry, 6);
        BitConverter.GetBytes((ushort)Tackle).CopyTo(entry, 8);

        Assert.Equal(1, BannedMoveScrubber.ScrubEggMoves(entry, Banned, [Leer], new SeededRandomSource(3)));
        Assert.Equal(Leer, BitConverter.ToUInt16(entry, 6));
        Assert.Equal(3, BitConverter.ToUInt16(entry, 2));   // el tamaño no cambia
    }

    /// <summary>A trainer Pokémon naming a banned move gets its moveset back from its learnset, which is clean.</summary>
    [Fact]
    public void A_trainer_pokemon_with_a_banned_move_leaves_its_moves_to_the_game()
    {
        var party = new byte[TrainerPokemonTable.EntrySize * 2];
        int[] first = [Tackle, SheerCold, Growl, 0], second = [Tackle, Growl, 0, 0];
        for (var slot = 0; slot < 4; slot++)
        {
            BitConverter.GetBytes((ushort)first[slot]).CopyTo(party, 0x18 + slot * 2);
            BitConverter.GetBytes((ushort)second[slot]).CopyTo(party, TrainerPokemonTable.EntrySize + 0x18 + slot * 2);
        }

        Assert.Equal(1, BannedMoveScrubber.ScrubTrainerParty(party, Banned));
        Assert.False(TrainerPokemonTable.HasExplicitMoves(party, 0));
        Assert.Equal(second, TrainerPokemonTable.GetMoves(party, 1));
    }

    [Fact]
    public void A_static_with_a_banned_move_leaves_its_moves_to_the_game()
    {
        var statics = new byte[0x38 * 2];
        BitConverter.GetBytes((ushort)Fissure).CopyTo(statics, BannedMoveScrubber.StaticMovesOffset + 2);
        BitConverter.GetBytes((ushort)Tackle).CopyTo(statics, 0x38 + BannedMoveScrubber.StaticMovesOffset);

        Assert.Equal(1, BannedMoveScrubber.ScrubStatics(statics, Banned));
        Assert.All(Enumerable.Range(0, 4), slot =>
            Assert.Equal(0, BitConverter.ToUInt16(statics, BannedMoveScrubber.StaticMovesOffset + slot * 2)));
        Assert.Equal(Tackle, BitConverter.ToUInt16(statics, 0x38 + BannedMoveScrubber.StaticMovesOffset));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>The four the player asked for, by the ids checked against the cartridge's own move names.</summary>
    [Fact]
    public void The_shipped_configuration_bans_the_four_one_hit_knockouts()
    {
        var options = RandomizerOptionsLoader.Load(Path.Combine(Root(), "Data", "randomizer.json"));

        Assert.Equal([12, 32, 90, 329], options.BannedMoves.Order());
    }
}
