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
/// <param name="StarterCandidates">How many species the starters could have been drawn from.</param>
public sealed record StaticEncounterResult(
    int Replaced, int Protected, IReadOnlyList<string> Starters, int LevelsRaised = 0,
    int StarterCandidates = 0);

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

        var starterPool = StarterPool(pool);

        using (var patcher = new GarcPatcher(path))
        {
            var gifts = patcher.Read(StaticEncounterTable.Gifts.Subfile);

            RandomizeStarters(gifts, random, starterPool, untouchable, ref replaced, ref kept);
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

                var claimed = layout == StaticEncounterTable.Statics
                    ? ApplyOverrides(payload, layout, random, pool, ref replaced)
                    : [];

                Randomize(payload, layout, random, pool, untouchable, 0, ref replaced, ref kept,
                    claimed);
                raised += Raise(payload, layout);
                patcher.Write(layout.Subfile, payload);
            }
        }

        await VerifyAsync(path, untouchable, ct);
        return new StaticEncounterResult(replaced, kept, starters, raised, starterPool.Count);
    }

    /// <summary>
    /// Applies the per-encounter rules, and returns the entries they claimed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It runs <b>before</b> the ordinary draw and hands back what it touched, so nothing gets
    /// rolled twice: an entry that was made a mega and then re-rolled would end up an ordinary
    /// species with a form index left over from somebody else, which is the sort of thing that
    /// draws a Pokémon with no model.
    /// </para>
    /// <para>
    /// An override that matches nothing <b>throws</b>. It is aimed at a species and a form the
    /// cartridge is supposed to have; if it is not there, either the table moved or somebody typed
    /// it wrong, and both of those are worth stopping for. Writing a world where the rule silently
    /// did nothing is how you find out six hours into a run.
    /// </para>
    /// </remarks>
    /// <summary>What each override rule matched, for the report.</summary>
    public List<string> Touched { get; } = [];

    private HashSet<int> ApplyOverrides(byte[] payload, EncounterEntryLayout layout,

        IRandomSource random, SpeciesPool pool, ref int replaced)
    {
        var claimed = new HashSet<int>();

        if (options.StaticOverrides.Count == 0)
        {
            return claimed;
        }

        var megaForms = MegaTrainerRandomizer.ReadForms(workspace.PathOf(GameFiles.MegaEvolution));
        var megas = MegaTrainerRandomizer.Candidates(megaForms, options, workspace.MaxSpecies);
        var count = StaticEncounterTable.Count(payload, layout);

        foreach (var rule in options.StaticOverrides)
        {
            var hits = Enumerable.Range(0, count)
                .Where(i => StaticEncounterTable.GetSpecies(payload, layout, i) == rule.Species
                            && StaticEncounterTable.GetForm(payload, layout, i) == rule.Form
                            && (rule.Level is not { } wanted
                                || StaticEncounterTable.GetLevel(payload, layout, i) == wanted))
                .ToArray();

            if (hits.Length == 0)
            {
                throw new InvalidDataException(
                    $"No hay ningún estático con la especie {rule.Species} la forma {rule.Form} y el nivel {rule.Level} "
                    + $"({rule.Note}). O la tabla ha cambiado o el número está mal, y en los dos "
                    + "casos escribir un mundo donde la regla no hizo nada es peor que parar.");
            }

            // Cuantas entradas toca cada regla. Una especie puede salir varias veces en la tabla
            // -- los ultraentes aparecen en la historia y otra vez en el ultraespacio -- y la regla
            // las coge TODAS. Decirlo es la diferencia entre saber lo que se ha hecho y suponerlo.
            Touched.Add(rule.Note + ": " + hits.Length + " ("
                + string.Join(", ", hits.Select(i => "Nv."
                    + StaticEncounterTable.GetLevel(payload, layout, i))) + ")");


            foreach (var index in hits)
            {
                if (rule.Rule == StaticOverrideRule.Mega)
                {
                    if (megas.Length == 0)
                    {
                        throw new InvalidDataException(
                            "No queda ninguna mega elegible: todas están en bannedSpecies.");
                    }

                    var species = megas[random.Next(megas.Length)];
                    var forms = megaForms[species];
                    var form = forms[random.Next(forms.Count)];

                    StaticEncounterTable.SetSpecies(payload, layout, index, species, form);
                }
                else
                {
                    // Forma 0 y nada mas, que es lo que hace que nunca salga una mega por aqui.
                    var strong = pool.Where(s => pool.BaseStatTotal(s) >= rule.MinimumBaseStatTotal,
                        $"un total base de {rule.MinimumBaseStatTotal} o mas");

                    StaticEncounterTable.SetSpecies(payload, layout, index,
                        strong.Pick(random, rule.Species));
                }

                claimed.Add(index);
                replaced++;
            }
        }

        return claimed;
    }

    /// <summary>
    /// The pool the starters are drawn from: species with two evolutions ahead of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the cartridge's own evolution table rather than from a list of species, so it
    /// stays right for every family without anybody maintaining it, and so a species added or
    /// changed upstream cannot silently drop out of the answer.
    /// </para>
    /// <para>
    /// The table is read <b>as the module sees it</b>: the starters are chosen before the data
    /// module touches the evolution lines, so with those randomized the guarantee is about the
    /// cartridge's families. The report says which, instead of implying more than it checked.
    /// </para>
    /// </remarks>
    private SpeciesPool StarterPool(SpeciesPool pool)
    {
        if (!options.StartersWithTwoEvolutions)
        {
            return pool;
        }

        var evolutions = EvolutionTable.Read(workspace.PathOf(GameFiles.Evolution));

        return pool.Where(evolutions.HasTwoEvolutionsAhead,
            "ser la primera etapa de una linea de tres");
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
        SpeciesPool pool, HashSet<int> untouchable, int from, ref int replaced, ref int kept,
        HashSet<int>? claimed = null)
    {
        for (var i = from; i < StaticEncounterTable.Count(payload, layout); i++)
        {
            // Lo que ya se llevo una regla propia no se vuelve a sortear: quedaria una especie
            // corriente con el indice de forma de otra, que es como se dibuja un Pokemon sin modelo.
            if (claimed is not null && claimed.Contains(i))
            {
                continue;
            }

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
