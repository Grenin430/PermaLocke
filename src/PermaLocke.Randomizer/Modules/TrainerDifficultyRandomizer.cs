using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="SmarterTrainers">Trainers whose AI byte gained a bit.</param>
/// <param name="Pokemon">Trainer Pokémon looked at.</param>
/// <param name="IvsRaised">Pokémon with at least one IV that went up.</param>
/// <param name="EvsDealt">Pokémon whose EVs were dealt again for their species.</param>
/// <param name="HoldingBefore">Pokémon that already held something: Z crystals, Leftovers...</param>
/// <param name="ItemsGiven">Items this module handed out.</param>
public sealed record TrainerDifficultyResult(int SmarterTrainers, int Pokemon, int IvsRaised, int EvsDealt,
    int HoldingBefore, int ItemsGiven);

/// <summary>
/// Makes the trainers harder without touching who they are: their AI byte, their IVs, their EV
/// spreads and how many of their Pokémon hold an item.
/// </summary>
/// <remarks>
/// <para>
/// Runs after every module that decides a trainer's Pokémon — species, the role's extra one and the
/// megas — because an EV spread only means something for the species and form that ends up in the
/// slot, and after the Pokémon data module because the base stats it reads have to be the ones the
/// game will use.
/// </para>
/// <para>
/// Every change is a fixed-size field patched in place, so neither GARC is repacked. The check
/// afterwards is the other half: species, form, level and moves must come out byte for byte as they
/// went in, EV totals unchanged, IVs never lower (§122).
/// </para>
/// </remarks>
public sealed class TrainerDifficultyRandomizer(RomWorkspace workspace, TrainerDifficultyOptions options)
{
    /// <summary>The AI byte of a 0x14 byte trainer entry. pk3DS's <c>TrainerData7.AI</c>.</summary>
    public const int AiOffset = 0x0C;

    public const int MaxEvPerStat = 252;

    // Stat positions in a trainer entry's EV bytes, measured (see TrainerPokemonTable.SetEvs).
    private const int Hp = 0, Attack = 1, Defense = 2, SpecialAttack = 3, SpecialDefense = 4, Speed = 5;

    /// <summary>The AI byte with the configured bits added and every bit it had kept.</summary>
    public static int WithAiFlags(int ai, int flags) => (ai | flags) & 0xFF;

    /// <summary>
    /// An IV raised by a percentage, rounded away from zero like the role's levels, and capped at 31.
    /// </summary>
    /// <remarks>
    /// A percentage of zero is zero: the first trainers of the game, whose IVs the cartridge left at
    /// 0, stay there. That is what «raise them 10-20% of what they have» means and it is written down
    /// so nobody mistakes it for a bug.
    /// </remarks>
    public static int RaiseIv(int iv, int percent) =>
        Math.Clamp((int)Math.Round(iv * (1 + (percent / 100.0)), MidpointRounding.AwayFromZero), 0, 31);

    /// <summary>
    /// Deals <paramref name="total"/> EVs over the six stats for a species with these base stats.
    /// </summary>
    /// <param name="baseStats">
    /// Six values in the same order as the EV bytes: HP, Attack, Defense, Sp. Atk, Sp. Def, Speed.
    /// </param>
    /// <remarks>
    /// The plain rule a player would use, so it can be explained in one sentence: 252 into the
    /// better attacking stat, then 252 into Speed if the species is fast or into HP if it is not,
    /// and whatever is left into the next stat in line. Nothing clever, and never worse than a spread
    /// chosen for a different Pokémon.
    /// </remarks>
    public static byte[] SpreadEvs(int total, IReadOnlyList<int> baseStats, int fastSpeed)
    {
        var physical = baseStats[Attack] >= baseStats[SpecialAttack];
        var attack = physical ? Attack : SpecialAttack;
        var otherAttack = physical ? SpecialAttack : Attack;
        var bulkier = baseStats[Defense] >= baseStats[SpecialDefense] ? Defense : SpecialDefense;
        var otherBulk = bulkier == Defense ? SpecialDefense : Defense;

        int[] order = baseStats[Speed] >= fastSpeed
            ? [attack, Speed, Hp, bulkier, otherBulk, otherAttack]
            : [attack, Hp, bulkier, otherBulk, Speed, otherAttack];

        var evs = new byte[6];
        var left = Math.Max(0, total);

        foreach (var stat in order)
        {
            var given = Math.Min(MaxEvPerStat, left);
            evs[stat] = (byte)given;
            left -= given;
        }

        return evs;
    }

    /// <summary>How many more items it takes for <paramref name="percent"/>% of the Pokémon to hold one.</summary>
    public static int ItemsToGive(int pokemon, int holding, int percent) =>
        Math.Max(0, (int)Math.Ceiling(pokemon * percent / 100.0) - holding);

    /// <summary>
    /// The base stats of a species and form, in EV order, from the concatenated personal table.
    /// </summary>
    /// <remarks>
    /// A form reads its own row — a Mega Charizard X is not built like a Charizard — found the way
    /// pk3DS's <c>PersonalInfo.FormeIndex</c> finds it. Null when the species is not in the table,
    /// so an entry nobody can read keeps the spread it has rather than one dealt from garbage.
    /// </remarks>
    public static int[]? BaseStatsOf(byte[] packed, int species, int form)
    {
        if (PersonalEntry7.RowOf(packed, species, form) is not { } row)
        {
            return null;
        }

        var start = row * PersonalEntry7.Size;
        return [.. PersonalEntry7.StatOrder.Select(stat => PersonalEntry7.GetStat(packed, start, stat))];
    }

    /// <summary>
    /// Stops before anything is written if a configured item is not what its name says.
    /// </summary>
    public static void CheckItemNames(string[] itemNames, TrainerDifficultyOptions options)
    {
        foreach (var item in options.HeldItemsAny.Concat(options.HeldItemsPhysical).Concat(options.HeldItemsSpecial))
        {
            var actual = item.Id > 0 && item.Id < itemNames.Length ? itemNames[item.Id] : null;

            if (!string.Equals(actual, item.Name, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"El objeto {item.Id} se llama «{actual ?? "(no existe)"}» y la configuración dice "
                    + $"«{item.Name}». No se toca ningún entrenador: repartir el objeto equipado "
                    + "equivocado no falla, solo sale mal al jugarlo.");
            }
        }
    }

    public async Task<TrainerDifficultyResult> ApplyAsync(IRandomSource random, LayeredFsMod mod,
        CancellationToken ct = default)
    {
        CheckItemNames(workspace.Config.GetText(TextName.ItemNames), options);

        var personalPath = mod.Stage(GameFiles.Personal);
        var packed = GarcPatcher.ReadOnly(personalPath, GarcPatcher.CountReadOnly(personalPath) - 1);

        var dataPath = mod.Stage(GameFiles.TrainerData);
        var partyPath = mod.Stage(GameFiles.TrainerPokemon);

        var dataBefore = new GARC.LazyGARC(await File.ReadAllBytesAsync(dataPath, ct));
        var partiesBefore = new GARC.LazyGARC(await File.ReadAllBytesAsync(partyPath, ct));

        var smarter = 0;

        using (var trainers = new GarcPatcher(dataPath))
        {
            for (var trainer = 0; trainer < trainers.FileCount; trainer++)
            {
                var entry = trainers.Read(trainer);

                if (entry.Length < 0x14)
                {
                    continue;
                }

                var ai = WithAiFlags(entry[AiOffset], options.AiFlagsAdded);

                if (ai != entry[AiOffset])
                {
                    entry[AiOffset] = (byte)ai;
                    trainers.Write(trainer, entry);
                    smarter++;
                }
            }
        }

        // Una corriente por palanca, derivada de la del modulo: cambiar el porcentaje de objetos no
        // tiene que mover que IVs le tocan a cada Pokemon (§27).
        var ivRandom = random.Derive("ivs");
        var itemRandom = random.Derive("items");

        var parties = new List<byte[]>();
        var eligible = new List<(int Trainer, int Slot)>();
        var pokemon = 0;
        var ivsRaised = 0;
        var evsDealt = 0;
        var holding = 0;
        var given = 0;

        // El fichero se cierra ANTES de releerlo: con el escritor abierto la comprobacion no puede
        // ni abrirlo, que es como fallo la primera generacion de verdad.
        using (var patcher = new GarcPatcher(partyPath))
        {
            for (var trainer = 0; trainer < patcher.FileCount; trainer++)
            {
                ct.ThrowIfCancellationRequested();
                var party = patcher.Read(trainer);
                parties.Add(party);

                for (var slot = 0; slot < TrainerPokemonTable.Count(party); slot++)
                {
                    if (TrainerPokemonTable.GetSpecies(party, slot) == 0)
                    {
                        continue;
                    }

                    pokemon++;

                    if (options.IvRaiseMaxPercent > 0)
                    {
                        var percent = ivRandom.Next(options.IvRaiseMinPercent, options.IvRaiseMaxPercent + 1);
                        var raised = false;

                        for (var stat = 0; stat < 6; stat++)
                        {
                            var iv = TrainerPokemonTable.GetIv(party, slot, stat);
                            var next = RaiseIv(iv, percent);

                            if (next != iv)
                            {
                                TrainerPokemonTable.SetIv(party, slot, stat, next);
                                raised = true;
                            }
                        }

                        if (raised) ivsRaised++;
                    }

                    var total = TrainerPokemonTable.GetEvs(party, slot).ToArray().Sum(b => b);

                    if (options.RecalculateEvs && total > 0
                        && BaseStatsOf(packed, TrainerPokemonTable.GetSpecies(party, slot),
                            TrainerPokemonTable.GetForm(party, slot)) is { } stats)
                    {
                        TrainerPokemonTable.SetEvs(party, slot, SpreadEvs(total, stats, options.EvFastSpeed));
                        evsDealt++;
                    }

                    if (TrainerPokemonTable.GetItem(party, slot) != 0)
                    {
                        holding++;
                    }
                    else
                    {
                        eligible.Add((trainer, slot));
                    }
                }
            }

            var toGive = Math.Min(eligible.Count, ItemsToGive(pokemon, holding, options.HeldItemPercent));

            // Barajado parcial: los primeros toGive de la lista quedan elegidos al azar y sin repetir.
            for (var i = 0; i < toGive; i++)
            {
                var j = i + itemRandom.Next(eligible.Count - i);
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);

                var (trainer, slot) = eligible[i];
                var party = parties[trainer];
                var stats = BaseStatsOf(packed, TrainerPokemonTable.GetSpecies(party, slot),
                    TrainerPokemonTable.GetForm(party, slot));
                var physical = stats is null || stats[Attack] >= stats[SpecialAttack];
                var choices = options.HeldItemsAny
                    .Concat(physical ? options.HeldItemsPhysical : options.HeldItemsSpecial)
                    .ToArray();

                if (choices.Length == 0)
                {
                    break;
                }

                TrainerPokemonTable.SetItem(party, slot, choices[itemRandom.Next(choices.Length)].Id);
                given++;
            }

            for (var trainer = 0; trainer < parties.Count; trainer++)
            {
                if (!parties[trainer].AsSpan().SequenceEqual(partiesBefore[trainer]))
                {
                    patcher.Write(trainer, parties[trainer]);
                }
            }

        }

        await VerifyAsync(dataPath, partyPath, dataBefore, partiesBefore, holding + given, ct);
        return new TrainerDifficultyResult(smarter, pokemon, ivsRaised, evsDealt, holding, given);
    }

    /// <summary>
    /// Reads both files back and checks that only the four levers moved.
    /// </summary>
    private async Task VerifyAsync(string dataPath, string partyPath, GARC.LazyGARC dataBefore,
        GARC.LazyGARC partiesBefore, int expectedHolding, CancellationToken ct)
    {
        var data = new GARC.LazyGARC(await File.ReadAllBytesAsync(dataPath, ct));

        for (var trainer = 0; trainer < data.FileCount; trainer++)
        {
            var entry = data[trainer];
            var before = dataBefore[trainer];

            if (entry.Length >= 0x14 && (entry[AiOffset] & options.AiFlagsAdded) != options.AiFlagsAdded)
            {
                throw new InvalidDataException($"El entrenador {trainer} se quedó sin la IA pedida.");
            }

            for (var at = 0; at < entry.Length; at++)
            {
                if (at != AiOffset && entry[at] != before[at])
                {
                    throw new InvalidDataException(
                        $"El entrenador {trainer} cambió el byte 0x{at:X2}, que este módulo no toca.");
                }
            }
        }

        var parties = new GARC.LazyGARC(await File.ReadAllBytesAsync(partyPath, ct));
        var holding = 0;

        for (var trainer = 0; trainer < parties.FileCount; trainer++)
        {
            var party = parties[trainer];
            var before = partiesBefore[trainer];

            if (party.Length != before.Length)
            {
                throw new InvalidDataException($"El equipo del entrenador {trainer} cambió de tamaño.");
            }

            for (var slot = 0; slot < TrainerPokemonTable.Count(party); slot++)
            {
                var at = slot * TrainerPokemonTable.EntrySize;

                // Especie, forma, nivel, movimientos, sexo, habilidad y naturaleza: iguales byte a byte.
                if (party[at] != before[at] || party[at + 1] != before[at + 1]
                    || !party.AsSpan(at + 0x0C, 0x08).SequenceEqual(before.AsSpan(at + 0x0C, 0x08))
                    || !party.AsSpan(at + 0x16, 0x0A).SequenceEqual(before.AsSpan(at + 0x16, 0x0A)))
                {
                    throw new InvalidDataException(
                        $"El entrenador {trainer}, hueco {slot + 1}, cambió algo más que IV, EV u objeto.");
                }

                if (TrainerPokemonTable.GetEvs(party, slot).ToArray().Sum(b => b)
                    != TrainerPokemonTable.GetEvs(before, slot).ToArray().Sum(b => b))
                {
                    throw new InvalidDataException(
                        $"El entrenador {trainer}, hueco {slot + 1}, cambió cuántos EV lleva.");
                }

                if (TrainerPokemonTable.GetEvs(party, slot).ToArray().Any(ev => ev > MaxEvPerStat))
                {
                    throw new InvalidDataException(
                        $"El entrenador {trainer}, hueco {slot + 1}, pasa de {MaxEvPerStat} EV en una estadística.");
                }

                for (var stat = 0; stat < 6; stat++)
                {
                    if (TrainerPokemonTable.GetIv(party, slot, stat) < TrainerPokemonTable.GetIv(before, slot, stat))
                    {
                        throw new InvalidDataException(
                            $"El entrenador {trainer}, hueco {slot + 1}, bajó un IV.");
                    }
                }

                // Las dos banderas de encima de los IV, intactas.
                if ((BitConverter.ToUInt32(party, at + 0x08) >> 30) != (BitConverter.ToUInt32(before, at + 0x08) >> 30))
                {
                    throw new InvalidDataException(
                        $"El entrenador {trainer}, hueco {slot + 1}, cambió las banderas de los IV.");
                }

                var item = TrainerPokemonTable.GetItem(before, slot);

                if (item != 0 && TrainerPokemonTable.GetItem(party, slot) != item)
                {
                    throw new InvalidDataException(
                        $"El entrenador {trainer}, hueco {slot + 1}, perdió el objeto que ya llevaba.");
                }

                if (TrainerPokemonTable.GetSpecies(party, slot) != 0 && TrainerPokemonTable.GetItem(party, slot) != 0)
                {
                    holding++;
                }
            }
        }

        if (holding != expectedHolding)
        {
            throw new InvalidDataException(
                $"Tenían que quedar {expectedHolding} Pokémon con objeto y hay {holding}.");
        }
    }
}
