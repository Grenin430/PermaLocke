using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="LevelUp">Level-up learnset slots that held one and got another move.</param>
/// <param name="Egg">Egg moves replaced.</param>
/// <param name="Trainers">Trainer Pokémon whose moveset was handed back to the game.</param>
/// <param name="Statics">Static encounters whose moveset was handed back to the game.</param>
public sealed record BannedMoveResult(int LevelUp, int Egg, int Trainers, int Statics)
{
    public int Total => LevelUp + Egg + Trainers + Statics;
}

/// <summary>
/// Takes the banned moves out of everywhere a Pokémon can get a move from (§162).
/// </summary>
/// <remarks>
/// <para>
/// Asked for on 2026-09-22: the one-hit knockouts — Guillotine, Horn Drill, Fissure, Sheer Cold — that «absolutely nobody
/// can use», to learn or against the player. Measured on the installed world first: 65 in level-up learnsets and 16 in
/// egg moves; none in TMs, tutors, trainer movesets or statics. The base layer had 2 in trainer movesets, which the
/// trainer module already cleared when it changed the species. Wild Pokémon and every trainer without a moveset of its
/// own take their moves from the learnset, so the learnsets are most of it.
/// </para>
/// <para>
/// It runs last and changes <b>only the slots that held a banned move</b>, from a stream of its own, so the rest of a
/// world a player is in the middle of comes out byte for byte the same. Taking them out of the draw instead would have
/// shifted every learnset in the game to change 65 slots.
/// </para>
/// <para>
/// What goes in instead depends on where. A learnset slot gets a status move it did not have: a one-hit knockout has no
/// base power, so it sits in a slot that the reordering by power (§158) leaves where it is, and an attack there would
/// break that order. An egg move gets any other move. A trainer or static that names a banned move gets its four slots
/// emptied, so the game builds its moveset from its learnset, which by then is clean — a thing the game plainly
/// supports, since half the cartridge's trainers ship that way.
/// </para>
/// </remarks>
public sealed class BannedMoveScrubber(RomWorkspace workspace, RandomizerOptions options)
{
    /// <summary>Where the four moves of a static encounter start (pk3DS's <c>RelearnMoves</c>).</summary>
    public const int StaticMovesOffset = 0x0C;

    public async Task<BannedMoveResult> ApplyAsync(IRandomSource random, LayeredFsMod mod, CancellationToken ct = default)
    {
        var banned = options.BannedMoves.ToHashSet();

        if (banned.Count == 0)
        {
            return new BannedMoveResult(0, 0, 0, 0);
        }

        var moves = workspace.Config.Moves;
        var names = workspace.Config.GetText(TextName.MoveNames);
        var (physical, special) = MoveCatalog.DetectCategories([.. moves.Select(move => move.Category)], names);
        var teachable = MoveTable.Teachable([.. moves.Select(move => move.PP)], options.EffectiveMaxMove(moves.Length - 1))
            .Where(id => !banned.Contains(id))
            .ToList();
        var status = teachable
            .Where(id => moves[id].Power == 0 && moves[id].Category != physical && moves[id].Category != special)
            .ToList();

        // Una corriente por fichero, derivada una vez: cada especie sigue tirando donde lo dejó la anterior.
        var learnsetDraw = random.Derive("aprendizajes");
        var eggDraw = random.Derive("huevo");
        var none = new NoRandom();

        var levelUp = Patch(mod, GameFiles.Learnset,
            entry => ScrubLearnset(entry, banned, [0], none),
            entry => ScrubLearnset(entry, banned, status, learnsetDraw));
        var egg = Patch(mod, GameFiles.EggMove,
            entry => ScrubEggMoves(entry, banned, [0], none),
            entry => ScrubEggMoves(entry, banned, teachable, eggDraw));
        var trainers = await ScrubTrainersAsync(mod, banned, ct);
        var statics = PatchStatics(mod, banned);

        await VerifyAsync(mod, banned, ct);
        return new BannedMoveResult(levelUp, egg, trainers, statics);
    }

    /// <summary>
    /// Replaces the banned moves of one level-up learnset — (move, level) pairs up to <c>FFFF</c> — keeping the levels.
    /// </summary>
    /// <returns>How many slots were replaced.</returns>
    public static int ScrubLearnset(byte[] entry, IReadOnlySet<int> banned, IReadOnlyList<int> replacements,
        IRandomSource random)
    {
        var present = new List<int>();
        var slots = new List<int>();

        for (var at = 0; at + 4 <= entry.Length; at += 4)
        {
            var move = BitConverter.ToUInt16(entry, at);

            if (move == 0xFFFF)
            {
                break;
            }

            present.Add(move);
            if (banned.Contains(move)) slots.Add(at);
        }

        foreach (var at in slots)
        {
            var replacement = Pick(replacements, present, random);
            BitConverter.GetBytes((ushort)replacement).CopyTo(entry, at);
            present.Add(replacement);
        }

        return slots.Count;
    }

    /// <summary>Replaces the banned moves of one egg move list: form index, count, then the moves.</summary>
    public static int ScrubEggMoves(byte[] entry, IReadOnlySet<int> banned, IReadOnlyList<int> replacements,
        IRandomSource random)
    {
        if (entry.Length < 4)
        {
            return 0;
        }

        var count = BitConverter.ToUInt16(entry, 2);
        var present = new List<int>();
        var slots = new List<int>();

        for (var i = 0; i < count && 4 + (2 * i) + 2 <= entry.Length; i++)
        {
            var move = BitConverter.ToUInt16(entry, 4 + (2 * i));
            present.Add(move);
            if (banned.Contains(move)) slots.Add(4 + (2 * i));
        }

        foreach (var at in slots)
        {
            var replacement = Pick(replacements, present, random);
            BitConverter.GetBytes((ushort)replacement).CopyTo(entry, at);
            present.Add(replacement);
        }

        return slots.Count;
    }

    /// <summary>Empties the moveset of every Pokémon of one trainer that names a banned move.</summary>
    public static int ScrubTrainerParty(byte[] party, IReadOnlySet<int> banned)
    {
        var scrubbed = 0;

        for (var index = 0; (index + 1) * TrainerPokemonTable.EntrySize <= party.Length; index++)
        {
            if (TrainerPokemonTable.GetMoves(party, index).Any(banned.Contains))
            {
                TrainerPokemonTable.ClearMoves(party, index);
                scrubbed++;
            }
        }

        return scrubbed;
    }

    /// <summary>Empties the moveset of every static encounter that names a banned move.</summary>
    public static int ScrubStatics(byte[] payload, IReadOnlySet<int> banned)
    {
        var layout = StaticEncounterTable.Statics;
        var scrubbed = 0;

        for (var row = 0; row < StaticEncounterTable.Count(payload, layout); row++)
        {
            var at = (row * layout.Stride) + StaticMovesOffset;

            if (Enumerable.Range(0, 4).Any(slot => banned.Contains(BitConverter.ToUInt16(payload, at + (slot * 2)))))
            {
                Array.Clear(payload, at, 8);
                scrubbed++;
            }
        }

        return scrubbed;
    }

    /// <summary>A replacement the list does not already hold, from wherever the draw lands, walking on from there.</summary>
    private static int Pick(IReadOnlyList<int> replacements, List<int> present, IRandomSource random)
    {
        if (replacements.Count == 0)
        {
            throw new InvalidOperationException("No queda ningún movimiento con el que sustituir a los prohibidos.");
        }

        var start = random.Next(replacements.Count);

        for (var step = 0; step < replacements.Count; step++)
        {
            var move = replacements[(start + step) % replacements.Count];

            if (!present.Contains(move))
            {
                return move;
            }
        }

        return replacements[start];
    }

    /// <summary>
    /// Patches a GARC in place, subfile by subfile, and stages it only when something in it has to change: a file that
    /// holds none of the banned moves stays out of the mod.
    /// </summary>
    /// <param name="count">Counts what <paramref name="scrub"/> would change, on a copy, without drawing anything.</param>
    private int Patch(LayeredFsMod mod, string file, Func<byte[], int> count, Func<byte[], int> scrub)
    {
        // Se mira primero donde este ahora -lo ya generado o la capa base- sin tocar nada.
        var current = Staged(mod, file) ?? workspace.PathOf(file);
        if (!Holds(current, count))
        {
            return 0;
        }

        var path = mod.Stage(file);
        var changed = 0;

        using var patcher = new GarcPatcher(path);

        for (var index = 0; index < patcher.FileCount; index++)
        {
            var entry = patcher.Read(index);
            var here = scrub(entry);

            if (here > 0)
            {
                patcher.Write(index, entry);
                changed += here;
            }
        }

        return changed;
    }

    private int PatchStatics(LayeredFsMod mod, IReadOnlySet<int> banned)
    {
        var current = Staged(mod, GameFiles.EncounterStatic) ?? workspace.PathOf(GameFiles.EncounterStatic);
        int found;

        using (var probe = new GarcPatcher(current))
        {
            found = ScrubStatics(probe.Read(StaticEncounterTable.Statics.Subfile).ToArray(), banned);
        }

        if (found == 0)
        {
            return 0;
        }

        using var patcher = new GarcPatcher(mod.Stage(GameFiles.EncounterStatic));
        var payload = patcher.Read(StaticEncounterTable.Statics.Subfile);
        var scrubbed = ScrubStatics(payload, banned);
        patcher.Write(StaticEncounterTable.Statics.Subfile, payload);
        return scrubbed;
    }

    private async Task<int> ScrubTrainersAsync(LayeredFsMod mod, IReadOnlySet<int> banned, CancellationToken ct)
    {
        var current = Staged(mod, GameFiles.TrainerPokemon) ?? workspace.PathOf(GameFiles.TrainerPokemon);
        var parties = new GARC.LazyGARC(await File.ReadAllBytesAsync(current, ct));
        var scrubbed = 0;

        for (var trainer = 0; trainer < parties.FileCount; trainer++)
        {
            var party = parties[trainer];
            var here = ScrubTrainerParty(party, banned);

            if (here > 0)
            {
                parties[trainer] = party;
                scrubbed += here;
            }
        }

        if (scrubbed > 0)
        {
            mod.Stage(GameFiles.TrainerPokemon);
            await mod.WriteAsync(GameFiles.TrainerPokemon, await Task.Run(parties.Save, ct), ct);
        }

        return scrubbed;
    }

    /// <summary>The file as this generation left it, when it has been staged; null when it is still the base layer's.</summary>
    private static string? Staged(LayeredFsMod mod, string file) =>
        mod.StagedFiles.Contains(file)
            ? Path.Combine(mod.RomFsDirectory, file.Replace('/', Path.DirectorySeparatorChar))
            : null;

    /// <summary>Whether any subfile of a GARC holds a banned move where a move goes, as the counting scrub reads it.</summary>
    private static bool Holds(string path, Func<byte[], int> count)
    {
        using var patcher = new GarcPatcher(path);

        for (var index = 0; index < patcher.FileCount; index++)
        {
            if (count(patcher.Read(index)) > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads everything back and refuses a world where a banned move survived anywhere it looks.</summary>
    private async Task VerifyAsync(LayeredFsMod mod, IReadOnlySet<int> banned, CancellationToken ct)
    {
        var none = new NoRandom();

        foreach (var (file, scrub) in new (string, Func<byte[], int>)[]
                 {
                     (GameFiles.Learnset, entry => ScrubLearnset(entry, banned, [0], none)),
                     (GameFiles.EggMove, entry => ScrubEggMoves(entry, banned, [0], none))
                 })
        {
            using var patcher = new GarcPatcher(Staged(mod, file) ?? workspace.PathOf(file));

            for (var index = 0; index < patcher.FileCount; index++)
            {
                if (scrub(patcher.Read(index)) > 0)
                {
                    throw new InvalidDataException($"Queda un movimiento prohibido en {file}, subfichero {index}.");
                }
            }
        }

        var parties = new GARC.LazyGARC(await File.ReadAllBytesAsync(
            Staged(mod, GameFiles.TrainerPokemon) ?? workspace.PathOf(GameFiles.TrainerPokemon), ct));

        for (var trainer = 0; trainer < parties.FileCount; trainer++)
        {
            if (ScrubTrainerParty(parties[trainer], banned) > 0)
            {
                throw new InvalidDataException($"El entrenador {trainer} sigue llevando un movimiento prohibido.");
            }
        }

        using var statics = new GarcPatcher(Staged(mod, GameFiles.EncounterStatic) ?? workspace.PathOf(GameFiles.EncounterStatic));
        if (ScrubStatics(statics.Read(StaticEncounterTable.Statics.Subfile), banned) > 0)
        {
            throw new InvalidDataException("Un estático sigue llevando un movimiento prohibido.");
        }
    }

    /// <summary>For the checks, which only count: nothing they do comes from a draw.</summary>
    private sealed class NoRandom : IRandomSource
    {
        public ulong Seed => 0;
        public int Next(int maxExclusive) => 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0;
        public bool Chance(double probability) => false;
        public IRandomSource Derive(string salt) => this;
    }
}
