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

        // Lo que el jugador RECIBE (iniciales, regalos, fosiles, tratos) es de su tipo en un rol MONOTYPE (§223); los
        // estaticos que se pelean, con sus dominantes y legendarios, siguen saliendo del pool de siempre.
        var given = options.MonoType is { } type ? pool.OfType(type) : pool;
        var starterPool = StarterPool(given);

        // La forma regional, de su propia fuente: la especie de cada fila sale igual que antes (§138).
        var forms = random.Derive("forms");

        using (var patcher = new GarcPatcher(path))
        {
            var gifts = patcher.Read(StaticEncounterTable.Gifts.Subfile);

            RandomizeStarters(gifts, random, forms, starterPool, untouchable, ref replaced, ref kept);
            starters =
            [
                .. Enumerable.Range(0, StaticEncounterTable.StarterCount)
                    .Select(i => names[StaticEncounterTable.GetSpecies(gifts, StaticEncounterTable.Gifts, i)]),
            ];

            Randomize(gifts, StaticEncounterTable.Gifts, random, forms, given, untouchable,
                StaticEncounterTable.StarterCount, ref replaced, ref kept);
            patcher.Write(StaticEncounterTable.Gifts.Subfile, gifts);

            foreach (var layout in (EncounterEntryLayout[])[StaticEncounterTable.Statics, StaticEncounterTable.Trades])
            {
                ct.ThrowIfCancellationRequested();
                var payload = patcher.Read(layout.Subfile);

                var isStatics = layout == StaticEncounterTable.Statics;

                // Las independientes se BUSCAN antes de que nada escriba -despues del sorteo la fila
                // del Necrozma ya es otra especie- y se APLICAN despues, sin tocar la corriente.
                var independent = isStatics ? LocateIndependent(payload, layout) : [];

                var claimed = isStatics
                    ? ApplyOverrides(payload, layout, random, pool, ref replaced)
                    : [];

                // De los estaticos, los corrientes (tipo 0: el Pokemon de la captura de Hau, Sudowoodo, Pinsir...) tambien son
                // del tipo en un MONOTYPE: el primero que se ve tras las Poke Balls es uno de ellos. Dominantes, legendarios y
                // ultraentes (tipos 1-3) siguen del pool de siempre.
                Randomize(payload, layout, random, forms, isStatics ? pool : given, untouchable, 0, ref replaced, ref kept,
                    claimed, isStatics && options.MonoType is not null ? given : null);

                ApplyIndependent(payload, layout, independent, random, pool);

                if (isStatics)
                {
                    Evolved += EvolveTotems(payload, layout, mod);
                }

                raised += Raise(payload, layout);
                patcher.Write(layout.Subfile, payload);
            }
        }

        await VerifyAsync(path, untouchable, ct);
        return new StaticEncounterResult(replaced, kept, starters, raised, starterPool.Count);
    }

    /// <summary>
    /// Entries a Mega rule wrote (2026-09-27). Legendaries may be megas since 2026-09-25 (Mega Darkrai comes with the mod),
    /// so <see cref="VerifyAsync"/> lets their banned species through there; seed 111 put Mega Darkrai on the Nihilego and
    /// the check stopped the whole randomization.
    /// </summary>
    private readonly HashSet<(int Subfile, int Index)> _megaEntries = [];

    /// <summary>What each override rule matched, for the report.</summary>
    public List<string> Touched { get; } = [];

    /// <summary>
    /// Applies the rules that run <b>before</b> the ordinary draw, and returns the entries they claimed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It hands back what it touched so nothing gets rolled twice: an entry that was made a mega and
    /// then re-rolled would end up an ordinary species with a form index left over from somebody
    /// else, which is the sort of thing that draws a Pokémon with no model.
    /// </para>
    /// <para>
    /// These rules share the ordinary draw's stream, and that is exactly why a rule added here moves
    /// the whole table: see <see cref="StaticOverride.IndependentDraw"/>. The ones marked independent
    /// are skipped here and applied by <see cref="ApplyIndependent"/> once the draw is done.
    /// </para>
    /// </remarks>
    private HashSet<int> ApplyOverrides(byte[] payload, EncounterEntryLayout layout,
        IRandomSource random, SpeciesPool pool, ref int replaced)
    {
        var claimed = new HashSet<int>();

        foreach (var rule in options.StaticOverrides.Where(rule => !rule.IndependentDraw))
        {
            foreach (var index in Hits(payload, layout, rule))
            {
                Pick(payload, layout, index, rule, random, pool);
                claimed.Add(index);
                replaced++;
            }
        }

        return claimed;
    }

    /// <summary>
    /// Finds, on the cartridge's own table, the entries each independent rule is aimed at.
    /// </summary>
    /// <remarks>
    /// It has to run <b>before</b> anything writes: the rule is found by the species and form the
    /// cartridge has there, and once the ordinary draw has been through, the Necrozma at row 159 is a
    /// Swampert and the rule would match nothing.
    /// </remarks>
    private List<(StaticOverride Rule, int[] Hits)> LocateIndependent(byte[] payload,
        EncounterEntryLayout layout) =>
        [.. options.StaticOverrides
            .Where(rule => rule.IndependentDraw)
            .Select(rule => (rule, Hits(payload, layout, rule)))];

    /// <summary>
    /// Applies the independent rules on top of the finished draw, each entry from a stream of its own.
    /// </summary>
    /// <remarks>
    /// The ordinary draw has already rolled these entries like any other — spending exactly the
    /// tiradas it spent before the rule existed — and this only overwrites the result. The stream is
    /// derived from the row, not taken from the shared one, and deriving does not advance the parent.
    /// So the rest of the world comes out byte for byte the same, which is the point.
    /// </remarks>
    private void ApplyIndependent(byte[] payload, EncounterEntryLayout layout,
        List<(StaticOverride Rule, int[] Hits)> located, IRandomSource random, SpeciesPool pool)
    {
        foreach (var (rule, hits) in located)
        {
            foreach (var index in hits)
            {
                Pick(payload, layout, index, rule, random.Derive($"static-override-{index}"), pool);
            }
        }
    }

    /// <summary>The entries a rule names, by the species, form and level the cartridge has there.</summary>
    /// <remarks>
    /// An override that matches nothing <b>throws</b>. It is aimed at a species and a form the
    /// cartridge is supposed to have; if it is not there, either the table moved or somebody typed
    /// it wrong, and both of those are worth stopping for. Writing a world where the rule silently
    /// did nothing is how you find out six hours into a run.
    /// </remarks>
    private int[] Hits(byte[] payload, EncounterEntryLayout layout, StaticOverride rule)
    {
        var count = StaticEncounterTable.Count(payload, layout);

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

        return hits;
    }

    private (int[] Species, IReadOnlyDictionary<int, IReadOnlyList<int>> Forms)? _megas;

    /// <summary>The megas this world declares, read once and only if some rule wants one.</summary>
    private (int[] Species, IReadOnlyDictionary<int, IReadOnlyList<int>> Forms) Megas()
    {
        if (_megas is { } known)
        {
            return known;
        }

        var forms = MegaTrainerRandomizer.ReadForms(workspace.PathOf(GameFiles.MegaEvolution));
        var species = MegaTrainerRandomizer.Candidates(forms, options, workspace.MaxSpecies);

        _megas = (species, forms);
        return _megas.Value;
    }

    /// <summary>What a rule puts in one entry.</summary>
    private void Pick(byte[] payload, EncounterEntryLayout layout, int index, StaticOverride rule,
        IRandomSource random, SpeciesPool pool)
    {
        if (rule.Rule == StaticOverrideRule.Mega)
        {
            var (megas, megaForms) = Megas();

            if (megas.Length == 0)
            {
                throw new InvalidDataException(
                    "No queda ninguna mega elegible: todas están en bannedSpecies.");
            }

            var species = megas[random.Next(megas.Length)];
            var forms = megaForms[species];
            var form = forms[random.Next(forms.Count)];

            StaticEncounterTable.SetSpecies(payload, layout, index, species, form);
            _megaEntries.Add((layout.Subfile, index));
            return;
        }

        // Forma 0 y nada mas, que es lo que hace que nunca salga una mega por aqui.
        var strong = pool.Where(s => pool.BaseStatTotal(s) >= rule.MinimumBaseStatTotal,
            $"un total base de {rule.MinimumBaseStatTotal} o mas");

        StaticEncounterTable.SetSpecies(payload, layout, index, strong.Pick(random, rule.Species));
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
    private void RandomizeStarters(byte[] gifts, IRandomSource random, IRandomSource forms, SpeciesPool pool,
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

            StaticEncounterTable.SetSpecies(gifts, StaticEncounterTable.Gifts, i, pick, pool.Forms.Pick(forms, pick));
            replaced++;
        }
    }

    private static void Randomize(byte[] payload, EncounterEntryLayout layout, IRandomSource random, IRandomSource forms,
        SpeciesPool pool, HashSet<int> untouchable, int from, ref int replaced, ref int kept,
        HashSet<int>? claimed = null, SpeciesPool? ordinary = null)
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

            var source = ordinary is not null && payload[(i * layout.Stride) + StaticEncounterTable.KindOffset] == 0
                ? ordinary
                : pool;
            var species = source.Pick(random, original);
            StaticEncounterTable.SetSpecies(payload, layout, i, species, source.Forms.Pick(forms, species));
            replaced++;
        }
    }


    /// <summary>How many Totems and allies <see cref="EvolveTotems"/> moved to their final evolution.</summary>
    public int Evolved { get; private set; }

    /// <summary>
    /// From the sixth trial on, a Totem and the allies it calls are final evolutions, like every trainer (2026-09-24, at
    /// the player's request).
    /// </summary>
    /// <remarks>
    /// The same cut as the trainers, <see cref="RandomizerOptions.FullyEvolvedFromLevel"/>, against the <b>cartridge</b>
    /// level, so it runs before <see cref="Raise"/>. Only the species changes, to the one the mod's own evolution table
    /// ends in; the form goes to 0 and takes nothing from the random streams, so the rest of the world comes out the same
    /// with the same seed. Before this, the Totems after the sixth trial were final only by luck.
    /// </remarks>
    private int EvolveTotems(byte[] payload, EncounterEntryLayout layout, LayeredFsMod mod)
    {
        if (options.FullyEvolvedFromLevel <= 0 || layout.LevelOffset is null)
        {
            return 0;
        }

        var evolutions = EvolutionTable.Read(mod.Stage(GameFiles.Evolution));
        var count = StaticEncounterTable.Count(payload, layout);
        var moved = 0;

        for (var i = 0; i < count; i++)
        {
            var level = StaticEncounterTable.GetLevel(payload, layout, i);

            if (!StaticEncounterTable.IsTotem(payload, layout, i) || level < options.FullyEvolvedFromLevel)
            {
                continue;
            }

            moved += Evolve(i, "Dominante");

            // Los que llama, que van en las filas de justo detrás: seguidas, de tipo corriente (0) y con un nivel entre
            // diez por debajo del Dominante y el suyo. Medido en el mundo generado: detrás de cada Dominante van de 4 a 8
            // y luego vienen estáticos sueltos de otro nivel (un Calyrex a 28 detrás de un Dominante a 60), que se quedan.
            for (var ally = i + 1; ally < count; ally++)
            {
                var allyLevel = StaticEncounterTable.GetLevel(payload, layout, ally);

                if (StaticEncounterTable.IsTotem(payload, layout, ally)
                    || payload[(ally * layout.Stride) + StaticEncounterTable.KindOffset] != 0
                    || allyLevel > level || allyLevel < level - 10)
                {
                    break;
                }

                if (allyLevel >= options.FullyEvolvedFromLevel)
                {
                    moved += Evolve(ally, "Aliado de Dominante");
                }
            }
        }

        return moved;

        int Evolve(int index, string what)
        {
            var species = StaticEncounterTable.GetSpecies(payload, layout, index);
            var last = evolutions.FinalOf(species);

            if (species <= 0 || last == species)
            {
                return 0;
            }

            StaticEncounterTable.SetSpecies(payload, layout, index, last, 0);
            Touched.Add($"{what} a su evolución final: {species} -> {last}");
            return 1;
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
                if (species != 0 && banned.Contains(species) && !untouchable.Contains(species)
                    && !_megaEntries.Contains((layout.Subfile, i)))
                {
                    throw new InvalidDataException(
                        $"El randomizador dejó la especie prohibida {species} en {layout.Name}, entrada {i}.");
                }
            }
        }
    }
}
