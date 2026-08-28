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

                // Sin piedra no es una mega que se pueda tener: es el caso de Rayquaza, cuya mega
                // va por un movimiento. Se deja fuera para no inventar una forma inalcanzable.
                if (form > 0 && stone > 0 && !here.Contains(form))
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

    public async Task<MegaTrainerResult> ApplyAsync(IRandomSource random, LayeredFsMod mod,
        CancellationToken ct = default)
    {
        if (!options.MegaTrainers || options.ImportantTrainerClasses.Count == 0)
        {
            return new MegaTrainerResult(0, 0, 0);
        }

        var forms = ReadForms(workspace.PathOf(GameFiles.MegaEvolution));
        var banned = options.BannedSpecies.ToHashSet();

        // Los legendarios prohibidos siguen prohibidos: la lista dice «esto no se reparte nunca», y
        // un Mega Mewtwo de regalo en un combate de mitad de partida es exactamente lo que evita.
        var candidates = forms.Keys
            .Where(species => species <= options.MaxSpecies && !banned.Contains(species))
            .OrderBy(species => species)
            .ToArray();

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

        var battles = 0;
        var tooEarly = 0;

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

                TrainerPokemonTable.SetSpecies(party, slot, species, choices[random.Next(choices.Count)]);
                TrainerPokemonTable.ClearMoves(party, slot);

                parties[trainer] = party;
                battles++;
            }
        }

        await mod.WriteAsync(GameFiles.TrainerPokemon, await Task.Run(parties.Save, ct), ct);
        await VerifyAsync(mod, forms, ct);

        return new MegaTrainerResult(battles, tooEarly, candidates.Length);
    }

    /// <summary>
    /// Reads the written file back and checks that every form it wrote is one the cartridge has.
    /// </summary>
    /// <remarks>
    /// A form index the species does not own is the failure that would not announce itself: the
    /// game would draw something, or nothing, and no error would be raised anywhere.
    /// </remarks>
    private async Task VerifyAsync(LayeredFsMod mod,
        IReadOnlyDictionary<int, IReadOnlyList<int>> forms, CancellationToken ct)
    {
        var written = new GARC.LazyGARC(
            await File.ReadAllBytesAsync(Path.Combine(mod.RomFsDirectory,
                GameFiles.TrainerPokemon.Replace('/', Path.DirectorySeparatorChar)), ct));

        for (var trainer = 0; trainer < written.FileCount; trainer++)
        {
            var party = written[trainer];

            // El hueco tiene que caber ENTERO: con «< Length» el ultimo se leia a medias y el
            // campo de forma, que va en 0x12, caia fuera del array.
            for (var slot = 0; (slot + 1) * TrainerPokemonTable.EntrySize <= party.Length; slot++)
            {
                var form = TrainerPokemonTable.GetForm(party, slot);

                if (form == 0)
                {
                    continue;
                }

                var species = TrainerPokemonTable.GetSpecies(party, slot);

                if (forms.TryGetValue(species, out var valid) && valid.Contains(form))
                {
                    continue;
                }

                throw new InvalidDataException(
                    $"El entrenador {trainer}, hueco {slot + 1}, quedó con la especie {species} en "
                    + $"forma {form}, que no es una megaevolución del cartucho.");
            }
        }
    }
}
