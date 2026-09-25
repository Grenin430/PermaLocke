using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Battles">Important battles that got one.</param>
/// <param name="TooEarly">Important battles left alone for being below the level floor.</param>
/// <param name="Candidates">Species that could have been picked.</param>
public sealed record MegaTrainerResult(int Battles, int TooEarly, int Candidates);

/// <summary>
/// Gives the late important battles one Pokémon that is already mega evolved.
/// </summary>
/// <remarks>
/// <para>
/// A mega is a <b>form</b> of its species, not a species of its own, so a trainer entry with
/// species 6 and form 1 is a Mega Charizard X that walks into the battle already transformed. That
/// is why this takes one byte and no measurement of the game's AI: it never mega evolves, it
/// arrives mega. The player saw exactly this in another randomizer, where a boss came out as a
/// mega form the game rendered without complaint.
/// </para>
/// <para>
/// The alternative -- handing the trainer the stone and hoping the AI uses it -- depends on
/// behaviour nobody here has measured, and on whatever the trainer data says about it. This does
/// not. What it loses is the moment: there is no animation and no "the foe is mega evolving",
/// because nothing evolves.
/// </para>
/// <para>
/// It <b>replaces</b> one of the trainer's own Pokémon rather than adding one, which is what the
/// competition asks: a boss with five keeps five, and one of them is the mega. Adding would make it
/// a second helping of the role's extra Pokémon (§47).
/// </para>
/// </remarks>
public sealed class MegaTrainerRandomizer(RomWorkspace workspace, RandomizerOptions options)
{
    /// <summary>
    /// Which mega form each species has: <c>species -> forms</c>, from <c>a/0/1/5</c>.
    /// </summary>
    /// <remarks>
    /// Measured, not assumed. Each entry's first field is the <b>form number</b> and the third is
    /// the stone: Charizard has two entries, form 1 with Charizardita X and form 2 with
    /// Charizardita Y, and Mewtwo the same. Every other mega-capable species has a single entry
    /// with form 1.
    /// </remarks>
    public static IReadOnlyDictionary<int, IReadOnlyList<int>> ReadForms(string garcPath)
    {
        const int entrySize = 8;

        using var patcher = new GarcPatcher(garcPath);
        var forms = new Dictionary<int, IReadOnlyList<int>>();

        for (var species = 0; species < patcher.FileCount; species++)
        {
            var entry = patcher.Read(species);
            var here = new List<int>();

            for (var at = 0; at + entrySize <= entry.Length; at += entrySize)
            {
                var form = BitConverter.ToUInt16(entry, at);
                var stone = BitConverter.ToUInt16(entry, at + 4);

                // Rayquaza entra aunque no tenga piedra (su mega va por Ascenso Draco): a un jefe se le pone la forma
                // directamente, sin piedra, igual que a los demás. Pedido por el jugador el 2026-09-25.
                if (form > 0 && !here.Contains(form))
                {
                    here.Add(form);
                }
            }

            if (here.Count > 0)
            {
                forms[species] = here;
            }
        }

        return forms;
    }

    /// <summary>
    /// The mega species that may actually be handed out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every species with a mega, banned legendaries included: until 2026-09-25 they were left out, and the player
    /// never asked for that. One filter remains, the <b>species ceiling</b>, which is not paperwork: the mega table is not indexed by
    /// species alone. Measured on the expansion mod, it has <b>1330 entries for 1026 species</b>,
    /// because the rows above the species count are alternate forms. Eight of its keys name nothing
    /// at all. Written without this filter, a boss became «species 1315, form 4» — a Pokémon that
    /// does not exist, in a file that saved without complaint.
    /// </para>
    /// <para>
    /// It lives here, shared, because it was written twice: this module had it and the static
    /// override did not, and the one that did not is the one that produced the ghost.
    /// </para>
    /// </remarks>
    public static int[] Candidates(IReadOnlyDictionary<int, IReadOnlyList<int>> forms,
        RandomizerOptions options, int gameMaxSpecies)
    {
        // Los legendarios de bannedSpecies también (2026-09-25, a petición del jugador: «quiero que puedan salir de
        // megas todos»). bannedSpecies sigue valiendo para el resto del sorteo; aquí solo manda el techo de especies.
        return [.. forms.Keys
            .Where(species => species <= options.EffectiveMaxSpecies(gameMaxSpecies))
            .Order()];
    }

    public async Task<MegaTrainerResult> ApplyAsync(IRandomSource random, LayeredFsMod mod,
        CancellationToken ct = default)
    {
        if (!options.MegaTrainers || options.ImportantTrainerClasses.Count == 0)
        {
            return new MegaTrainerResult(0, 0, 0);
        }

        var forms = ReadForms(workspace.PathOf(GameFiles.MegaEvolution));
        var candidates = Candidates(forms, options, workspace.MaxSpecies);

        if (candidates.Length == 0)
        {
            return new MegaTrainerResult(0, 0, 0);
        }

        var dataPath = mod.Stage(GameFiles.TrainerData);
        var partyPath = mod.Stage(GameFiles.TrainerPokemon);

        var parties = new GARC.LazyGARC(await File.ReadAllBytesAsync(partyPath, ct));

        // El suelo se expresa en niveles del cartucho y se sube con la MISMA cuenta que subió los
        // equipos, así que el corte se mueve exactamente igual que ellos. Comparar un umbral
        // vanilla contra niveles ya subidos dejaría entrar combates de antes de la séptima prueba.
        var floor = TrainerRandomizer.Raise(options.MegaTrainerMinimumLevel, options.EnemyLevelPercent);

        var tooEarly = 0;

        // Lo que ESTE modulo escribio, para poder comprobar solo eso. El cartucho ya usa el campo
        // de forma para formas normales -el Lycanroc Noche de un entrenador es la forma 1- asi que
        // exigir que toda forma sea una mega abortaria la randomizacion en cuanto una sobreviviera,
        // por ejemplo con el modulo de entrenadores apagado.
        var written = new Dictionary<(int Trainer, int Slot), (int Species, int Form)>();

        using (var trainers = new GarcPatcher(dataPath))
        {
            for (var trainer = 0; trainer < trainers.FileCount; trainer++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = trainers.Read(trainer);

                if (entry.Length < 0x14)
                {
                    continue;
                }

                var trainerClass = BitConverter.ToUInt16(entry, ExtraPokemonRandomizer.ClassOffset);
                var count = entry[ExtraPokemonRandomizer.CountOffset];

                if (!options.ImportantTrainerClasses.Contains(trainerClass)
                    || count == 0 || trainer >= parties.FileCount)
                {
                    continue;
                }

                var party = parties[trainer];

                // La cuenta que manda es la de la tabla de entrenadores. Si el equipo no mide lo
                // que ella dice, algo se ha entendido mal y no se toca (§47).
                if (party.Length != count * TrainerPokemonTable.EntrySize)
                {
                    continue;
                }

                var highest = Enumerable.Range(0, count).Max(i => TrainerPokemonTable.GetLevel(party, i));

                if (highest < floor)
                {
                    tooEarly++;
                    continue;
                }

                var slot = random.Next(count);
                var species = candidates[random.Next(candidates.Length)];
                var choices = forms[species];

                var form = choices[random.Next(choices.Count)];

                TrainerPokemonTable.SetSpecies(party, slot, species, form);
                TrainerPokemonTable.ClearMoves(party, slot);

                parties[trainer] = party;
                written[(trainer, slot)] = (species, form);
                // el contador es el propio diccionario
            }
        }

        await mod.WriteAsync(GameFiles.TrainerPokemon, await Task.Run(parties.Save, ct), ct);
        await VerifyAsync(mod, written, ct);

        return new MegaTrainerResult(written.Count, tooEarly, candidates.Length);
    }

    /// <summary>
    /// Reads the written file back and checks that every mega this module wrote is really there.
    /// </summary>
    /// <remarks>
    /// <b>Only what it wrote.</b> The cartridge already uses the form field for ordinary alternate
    /// forms — a trainer's Lycanroc Midnight is form 1 — so demanding that every form in the file be
    /// a mega would abort the whole randomization the moment one survived, which is exactly what
    /// happens with the trainer module switched off. What has to be true is narrower and stronger:
    /// the slots this module touched hold the species and form it chose.
    /// </remarks>
    private static async Task VerifyAsync(LayeredFsMod mod,
        IReadOnlyDictionary<(int Trainer, int Slot), (int Species, int Form)> expected,
        CancellationToken ct)
    {
        var file = new GARC.LazyGARC(
            await File.ReadAllBytesAsync(Path.Combine(mod.RomFsDirectory,
                GameFiles.TrainerPokemon.Replace('/', Path.DirectorySeparatorChar)), ct));

        foreach (var (where, what) in expected)
        {
            var party = file[where.Trainer];

            if ((where.Slot + 1) * TrainerPokemonTable.EntrySize <= party.Length
                && TrainerPokemonTable.GetSpecies(party, where.Slot) == what.Species
                && TrainerPokemonTable.GetForm(party, where.Slot) == what.Form)
            {
                continue;
            }

            throw new InvalidDataException(
                $"El entrenador {where.Trainer}, hueco {where.Slot + 1}, debía quedar con la especie "
                + $"{what.Species} en forma {what.Form} y no es lo que hay en el fichero.");
        }
    }
}
