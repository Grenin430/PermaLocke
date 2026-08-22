using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Replaced">Entries whose species changed.</param>
/// <param name="Protected">Entries left alone because the story depends on them.</param>
/// <param name="Starters">The three species the player will get to choose from.</param>
/// <param name="LevelsRaised">Entries whose level the role moved.</param>
public sealed record StaticEncounterResult(
    int Replaced, int Protected, IReadOnlyList<string> Starters, int LevelsRaised = 0);

/// <summary>
/// Rewrites the starters, the eleven fossils, gifts, static encounters, totems and the species
/// received in in-game trades. All of it lives in one 20 KB GARC.
/// </summary>
public sealed class StaticEncounterRandomizer(RomWorkspace workspace, RandomizerOptions options)
{
    public async Task<StaticEncounterResult> ApplyAsync(IRandomSource random, SpeciesPool pool,
        LayeredFsMod mod, CancellationToken ct = default)
    {
        var path = mod.Stage(GameFiles.EncounterStatic);
        var names = workspace.Config.GetText(TextName.SpeciesNames);
        var untouchable = options.ProtectedSpecies.ToHashSet();

        var replaced = 0;
        var kept = 0;
        var raised = 0;
        string[] starters;

        using (var patcher = new GarcPatcher(path))
        {
            var gifts = patcher.Read(StaticEncounterTable.Gifts.Subfile);

            RandomizeStarters(gifts, random, pool, untouchable, ref replaced, ref kept);
            starters =
            [
                .. Enumerable.Range(0, StaticEncounterTable.StarterCount)
                    .Select(i => names[StaticEncounterTable.GetSpecies(gifts, StaticEncounterTable.Gifts, i)]),
            ];

            Randomize(gifts, StaticEncounterTable.Gifts, random, pool, untouchable,
                StaticEncounterTable.StarterCount, ref replaced, ref kept);
            patcher.Write(StaticEncounterTable.Gifts.Subfile, gifts);

            foreach (var layout in (EncounterEntryLayout[])[StaticEncounterTable.Statics, StaticEncounterTable.Trades])
            {
                ct.ThrowIfCancellationRequested();
                var payload = patcher.Read(layout.Subfile);
                Randomize(payload, layout, random, pool, untouchable, 0, ref replaced, ref kept);
                raised += Raise(payload, layout);
                patcher.Write(layout.Subfile, payload);
            }
        }

        await VerifyAsync(path, untouchable, ct);
        return new StaticEncounterResult(replaced, kept, starters, raised);
    }

    /// <summary>
    /// The three starters are rolled together, so the player is not offered the same species
    /// three times.
    /// </summary>
    private void RandomizeStarters(byte[] gifts, IRandomSource random, SpeciesPool pool,
        HashSet<int> untouchable, ref int replaced, ref int kept)
    {
        var chosen = new HashSet<int>();
        for (var i = 0; i < StaticEncounterTable.StarterCount; i++)
        {
            var original = StaticEncounterTable.GetSpecies(gifts, StaticEncounterTable.Gifts, i);
            if (original == 0 || untouchable.Contains(original))
            {
                kept++;
                continue;
            }

            var pick = pool.Pick(random, original);
            if (options.DistinctStarters)
            {
                // Bounded: accept a repeat rather than spin forever on a tiny pool.
                for (var attempt = 0; attempt < 64 && !chosen.Add(pick); attempt++)
                {
                    pick = pool.Pick(random, original);
                }
            }

            StaticEncounterTable.SetSpecies(gifts, StaticEncounterTable.Gifts, i, pick);
            replaced++;
        }
    }

    private static void Randomize(byte[] payload, EncounterEntryLayout layout, IRandomSource random,
        SpeciesPool pool, HashSet<int> untouchable, int from, ref int replaced, ref int kept)
    {
        for (var i = from; i < StaticEncounterTable.Count(payload, layout); i++)
        {
            var original = StaticEncounterTable.GetSpecies(payload, layout, i);
            if (original == 0)
            {
                continue; // an unused slot stays unused
            }

            if (untouchable.Contains(original))
            {
                kept++;
                continue;
            }

            StaticEncounterTable.SetSpecies(payload, layout, i, pool.Pick(random, original));
            replaced++;
        }
    }

    /// <summary>
    /// Raises the levels of a table by whatever the role asks for, and says how many moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes the trials keep up with the rest of the game. The Totem Pokémon are not
    /// trainers — they live here, in the statics — so the level raise that reaches every trainer
    /// was passing straight over them, leaving the eight trial bosses at cartridge level while
    /// everything around them climbed 20%. The competition's own cap table is <em>built</em> from
    /// the raised levels, so a Totem left behind is a boss the player outlevels by design.
    /// </para>
    /// <para>
    /// Gifts and trades have no level to raise, and would be the wrong thing to raise anyway: a
    /// starter or a fossil is something the player receives, and making it stronger is a present,
    /// not a difficulty. The layout says which tables carry a level, and only those change.
    /// </para>
    /// </remarks>
    private int Raise(byte[] payload, EncounterEntryLayout layout)
    {
        if (options.EnemyLevelPercent <= 0 || layout.LevelOffset is null)
        {
            return 0;
        }

        var moved = 0;
        for (var i = 0; i < StaticEncounterTable.Count(payload, layout); i++)
        {
            var level = StaticEncounterTable.GetLevel(payload, layout, i);
            if (level <= 0)
            {
                continue;
            }

            var raised = TrainerRandomizer.Raise(level, options.EnemyLevelPercent);
            if (raised == level)
            {
                continue;
            }

            StaticEncounterTable.SetLevel(payload, layout, i, raised);
            moved++;
        }

        return moved;
    }

    /// <summary>
    /// Reads the patched GARC back with the pk3DS reader, not the patcher that wrote it, and
    /// refuses the result if a banned species got through.
    /// </summary>
    private async Task VerifyAsync(string path, HashSet<int> untouchable, CancellationToken ct)
    {
        var reloaded = new GARC.LazyGARC(await File.ReadAllBytesAsync(path, ct));
        var banned = options.BannedSpecies.ToHashSet();

        foreach (var layout in (EncounterEntryLayout[])
                 [StaticEncounterTable.Gifts, StaticEncounterTable.Statics, StaticEncounterTable.Trades])
        {
            var payload = reloaded[layout.Subfile];
            for (var i = 0; i < StaticEncounterTable.Count(payload, layout); i++)
            {
                var species = StaticEncounterTable.GetSpecies(payload, layout, i);
                if (species != 0 && banned.Contains(species) && !untouchable.Contains(species))
                {
                    throw new InvalidDataException(
                        $"El randomizador dejó la especie prohibida {species} en {layout.Name}, entrada {i}.");
                }
            }
        }
    }
}
