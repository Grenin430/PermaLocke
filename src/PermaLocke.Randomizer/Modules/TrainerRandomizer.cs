using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Trainers">Trainers whose party changed.</param>
/// <param name="Pokemon">Individual trainer Pokémon replaced.</param>
/// <param name="MovesCleared">Entries whose explicit moveset was handed back to the game.</param>
/// <param name="LevelsRaised">Pokémon whose level the role moved.</param>
public sealed record TrainerResult(int Trainers, int Pokemon, int MovesCleared, int LevelsRaised = 0,
    int FullyEvolved = 0);

/// <summary>
/// Replaces the species of every trainer Pokémon in <c>trpoke</c> (<c>a/1/0/7</c>), and raises
/// their levels by whatever the <b>role</b> asks for.
/// <para>
/// The randomization of species still never touches levels: the competition's caps are read off
/// the Kahuna parties, and moving a level as a side effect of shuffling species would silently
/// move the cap that governs ten players. Raising them is a separate, declared decision that
/// comes from the role and applies to every trainer alike.
/// </para>
/// </summary>
public sealed class TrainerRandomizer(RomWorkspace workspace, RandomizerOptions options)
{
    /// <summary>
    /// Builds one pool per trainer class that has a floor, checking the class names first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name is verified against the cartridge before anything is written, which is the §52
    /// rule: a class id typed from memory that lands on somebody else would quietly make the wrong
    /// battle harder and never fail. A mismatch stops the randomization.
    /// </para>
    /// <para>
    /// The pools are built once and shared, not per trainer, because filtering the species list is
    /// the expensive part and there are only a handful of classes.
    /// </para>
    /// </remarks>
    public static Dictionary<int, SpeciesPool> FloorPools(RomWorkspace workspace,
        RandomizerOptions options, SpeciesPool pool)
    {
        var pools = new Dictionary<int, SpeciesPool>();

        if (options.TrainerMinimums.Count == 0)
        {
            return pools;
        }

        var names = workspace.Config.GetText(TextName.TrainerClasses);

        foreach (var rule in options.TrainerMinimums)
        {
            var actual = rule.Class >= 0 && rule.Class < names.Length ? names[rule.Class] : null;

            if (!string.Equals(actual, rule.Name, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"La clase {rule.Class} se llama «{actual ?? "(no existe)"}» y la configuración "
                    + $"dice «{rule.Name}» ({rule.Note}). No se toca ningún entrenador: subir el "
                    + "suelo del combate equivocado no falla, solo sale mal al jugarlo.");
            }

            pools[rule.Class] = pool.Where(
                s => pool.BaseStatTotal(s) >= rule.MinimumBaseStatTotal,
                $"un total base de {rule.MinimumBaseStatTotal} o más");
        }

        return pools;
    }

    /// <summary>The class of every trainer, so a floor can be aimed at one.</summary>
    /// <remarks>
    /// Read from the trainer table and not from the party file, which does not carry it. Both come
    /// from the same staged mod so the indices line up — crossing a vanilla table with a generated
    /// party file is the §47 mistake.
    /// </remarks>
    private int[] TrainerClasses(LayeredFsMod mod)
    {
        using var trainers = new GarcPatcher(mod.Stage(GameFiles.TrainerData));
        var classes = new int[trainers.FileCount];

        for (var i = 0; i < trainers.FileCount; i++)
        {
            var entry = trainers.Read(i);

            classes[i] = entry.Length >= 0x14
                ? BitConverter.ToUInt16(entry, ExtraPokemonRandomizer.ClassOffset)
                : -1;
        }

        return classes;
    }

    public async Task<TrainerResult> ApplyAsync(IRandomSource random, SpeciesPool pool,
        LayeredFsMod mod, CancellationToken ct = default)
    {
        var floors = FloorPools(workspace, options, pool);

        // La forma regional, de su propia fuente: la especie de cada hueco sale igual que antes (§138).
        var forms = random.Derive("forms");
        var classes = floors.Count > 0 ? TrainerClasses(mod) : [];

        var path = mod.Stage(GameFiles.TrainerPokemon);
        var untouchable = options.ProtectedSpecies.ToHashSet();

        // La tabla de evoluciones se lee del MOD, no del cartucho, porque si las lineas evolutivas
        // se han randomizado la final de cada especie es otra. Se abre una sola vez.
        var evolutions = options.FullyEvolvedFromLevel > 0
            ? EvolutionTable.Read(mod.Stage(GameFiles.Evolution))
            : null;

        var evolved = 0;

        var trainers = 0;
        var replaced = 0;
        var movesCleared = 0;
        var levelsRaised = 0;

        using (var patcher = new GarcPatcher(path))
        {
            for (var trainer = 0; trainer < patcher.FileCount; trainer++)
            {
                ct.ThrowIfCancellationRequested();

                var party = patcher.Read(trainer);
                var count = TrainerPokemonTable.Count(party);

                // El suelo lo pone la CLASE, asi que un combate de la liga se sortea de otro saco.
                var here = trainer < classes.Length && floors.TryGetValue(classes[trainer], out var floor)
                    ? floor
                    : pool;
                if (count == 0)
                {
                    continue; // the cartridge holds one six byte subfile with no party at all
                }

                var changed = false;
                for (var slot = 0; slot < count; slot++)
                {
                    // El nivel del CARTUCHO, guardado antes de que el rol lo suba: es lo que dice
                    // en que momento de la historia aparece este entrenador.
                    var storyLevel = TrainerPokemonTable.GetLevel(party, slot);

                    // El nivel lo sube el ROL, no la randomización, y se sube SIEMPRE: también en
                    // los Pokémon protegidos, porque un Cosmog al nivel del cartucho en un juego
                    // donde todo lo demás va un 20% por encima sería un regalo, no una protección.
                    if (options.EnemyLevelPercent > 0)
                    {
                        var raised = Raise(TrainerPokemonTable.GetLevel(party, slot),
                            options.EnemyLevelPercent);

                        if (raised != TrainerPokemonTable.GetLevel(party, slot))
                        {
                            TrainerPokemonTable.SetLevel(party, slot, raised);
                            levelsRaised++;
                            changed = true;
                        }
                    }

                    var original = TrainerPokemonTable.GetSpecies(party, slot);
                    if (original == 0 || untouchable.Contains(original))
                    {
                        continue;
                    }

                    var species = here.Pick(random, original);

                    // De la sexta prueba en adelante, todos evolucionados del todo.
                    if (evolutions is not null && storyLevel >= options.FullyEvolvedFromLevel)
                    {
                        var last = evolutions.FinalOf(species);

                        if (last != species)
                        {
                            species = last;
                            evolved++;
                        }
                    }

                    TrainerPokemonTable.SetSpecies(party, slot, species, here.Forms.Pick(forms, species));
                    replaced++;
                    changed = true;

                    // A moveset chosen for the old species means nothing on the new one.
                    if (options.TrainerMovesFromLearnset && TrainerPokemonTable.HasExplicitMoves(party, slot))
                    {
                        TrainerPokemonTable.ClearMoves(party, slot);
                        movesCleared++;
                    }
                }

                if (!changed)
                {
                    continue;
                }

                patcher.Write(trainer, party);
                trainers++;
            }
        }

        await VerifyAsync(path, ct);
        return new TrainerResult(trainers, replaced, movesCleared, levelsRaised, evolved);
    }

    /// <summary>
    /// A cartridge level raised by a percentage, rounded to the nearest level and capped at 100.
    /// </summary>
    /// <remarks>
    /// To the nearest, halves up — <c>MidpointRounding.AwayFromZero</c> only decides the halves, it
    /// is not a ceiling. Rounding rather than truncating matters at the bottom of the game, where a
    /// level 4 raised 20% is 4.8. And it is not a ceiling on purpose: 12 × 1.2 = 14.4 gives 14,
    /// which is the first trial's cap in <c>Data/levelcaps.json</c>.
    /// </remarks>
    public static int Raise(int level, int percent) =>
        Math.Clamp((int)Math.Round(level * (1 + (percent / 100.0)), MidpointRounding.AwayFromZero), 1, 100);

    /// <summary>
    /// Reads the result back with the pk3DS reader and checks the two things that would ruin a
    /// run: a banned species in a party, or a party that changed size.
    /// </summary>
    private async Task VerifyAsync(string path, CancellationToken ct)
    {
        var vanilla = new GARC.LazyGARC(await File.ReadAllBytesAsync(
            workspace.PathOf(GameFiles.TrainerPokemon), ct));
        var patched = new GARC.LazyGARC(await File.ReadAllBytesAsync(path, ct));

        if (patched.FileCount != vanilla.FileCount)
        {
            throw new InvalidDataException(
                $"trpoke se quedó con {patched.FileCount} entrenadores en vez de {vanilla.FileCount}.");
        }

        var banned = options.BannedSpecies.ToHashSet();
        var untouchable = options.ProtectedSpecies.ToHashSet();

        for (var trainer = 0; trainer < patched.FileCount; trainer++)
        {
            var party = patched[trainer];
            if (party.Length != vanilla[trainer].Length)
            {
                throw new InvalidDataException(
                    $"El equipo del entrenador {trainer} cambió de tamaño: {vanilla[trainer].Length} -> {party.Length}.");
            }

            for (var slot = 0; slot < TrainerPokemonTable.Count(party); slot++)
            {
                var species = TrainerPokemonTable.GetSpecies(party, slot);
                if (species != 0 && banned.Contains(species) && !untouchable.Contains(species))
                {
                    throw new InvalidDataException(
                        $"El randomizador dejó la especie prohibida {species} en el entrenador {trainer}, hueco {slot}.");
                }
            }
        }
    }
}
