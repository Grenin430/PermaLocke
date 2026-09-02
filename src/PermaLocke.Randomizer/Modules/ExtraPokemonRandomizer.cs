using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="Battles">Important battles that got at least one more Pokémon.</param>
/// <param name="Added">Pokémon added in total.</param>
/// <param name="NoRoom">Battles left alone because the party was already full.</param>
public sealed record ExtraPokemonResult(int Battles, int Added, int NoRoom);

/// <summary>
/// Gives the important battles the extra Pokémon the role asks for.
/// </summary>
/// <remarks>
/// <para>
/// This is the one module that <b>cannot</b> patch bytes in place, which is why it took its own
/// pass. A party lives in <c>trpoke</c> as a run of 0x20 byte entries and nothing else, so a
/// seventh entry makes the subfile longer and the whole GARC has to be repacked. Everywhere else
/// the rule of §19 holds; here it cannot, so the safety comes from verifying afterwards instead:
/// every party is read back and its length checked against the count the trainer table declares.
/// </para>
/// <para>
/// The new Pokémon is a <b>copy of the last one in the party</b> with its species swapped. That is
/// deliberate: the 0x20 entry has fields nobody here has identified, and copying a neighbour
/// guarantees every one of them is something this trainer's format already contained. Inventing an
/// entry from zeroes would look right and could mean anything.
/// </para>
/// <para>
/// Six is the hard ceiling: the count lives in a single byte the game reads as a party size, and a
/// seventh member is not a thing the battle engine has. A boss already at six is left alone and
/// counted, never truncated.
/// </para>
/// </remarks>
public sealed class ExtraPokemonRandomizer(RomWorkspace workspace, RandomizerOptions options)
{
    /// <summary>Most Pokémon a trainer can carry.</summary>
    public const int MaxParty = 6;

    /// <summary>Where the party size lives inside a 0x14 byte trainer entry.</summary>
    public const int CountOffset = 0x03;

    /// <summary>Where the trainer class lives, as a 16 bit value.</summary>
    public const int ClassOffset = 0x00;

    public async Task<ExtraPokemonResult> ApplyAsync(IRandomSource random, SpeciesPool pool,
        LayeredFsMod mod, CancellationToken ct = default)
    {
        if (options.ExtraTrainerPokemon <= 0 || options.ImportantTrainerClasses.Count == 0)
        {
            return new ExtraPokemonResult(0, 0, 0);
        }

        var dataPath = mod.Stage(GameFiles.TrainerData);
        var partyPath = mod.Stage(GameFiles.TrainerPokemon);

        var parties = new GARC.LazyGARC(await File.ReadAllBytesAsync(partyPath, ct));
        var untouchable = options.ProtectedSpecies.ToHashSet();

        // Los mismos suelos por clase que usa el randomizador de entrenadores, construidos con la
        // misma funcion: dos definiciones de «el Alto Mando va desde 500» acabarian discrepando.
        var floors = TrainerRandomizer.FloorPools(workspace, options, pool);

        int battles = 0, added = 0, noRoom = 0;
        var counts = new Dictionary<int, int>();

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

                var trainerClass = BitConverter.ToUInt16(entry, ClassOffset);
                if (!options.ImportantTrainerClasses.Contains(trainerClass))
                {
                    continue;
                }

                var count = entry[CountOffset];
                if (count == 0 || trainer >= parties.FileCount)
                {
                    continue; // sin equipo no hay a qué añadir
                }

                if (count >= MaxParty)
                {
                    noRoom++;
                    continue;
                }

                var room = MaxParty - count;
                var extra = Math.Min(options.ExtraTrainerPokemon, room);
                var party = parties[trainer];

                // La cuenta que manda es la de la tabla de entrenadores. Si el equipo no mide lo
                // que dice, algo se ha entendido mal y es mejor no tocarlo.
                if (party.Length != count * TrainerPokemonTable.EntrySize)
                {
                    continue;
                }

                var grown = new byte[party.Length + (extra * TrainerPokemonTable.EntrySize)];
                party.CopyTo(grown, 0);

                for (var n = 0; n < extra; n++)
                {
                    var slot = count + n;
                    var source = slot - 1; // el último que ya existía, o el recién añadido

                    Array.Copy(grown, source * TrainerPokemonTable.EntrySize,
                        grown, slot * TrainerPokemonTable.EntrySize, TrainerPokemonTable.EntrySize);

                    var original = TrainerPokemonTable.GetSpecies(grown, source);
                    if (original != 0 && !untouchable.Contains(original))
                    {
                        // El suelo de la clase manda tambien aqui. Sin esto, el septimo Pokemon
                        // del rol entraba en la liga sacado del saco entero: se veia un Volbeat de
                        // 430 al lado de cinco de 500 para arriba.
                        var here = floors.TryGetValue(trainerClass, out var floor) ? floor : pool;
                        TrainerPokemonTable.SetSpecies(grown, slot, here.Pick(random, original));
                    }

                    // Los movimientos del copiado no son de esta especie, y el nivel se hereda a
                    // propósito: el añadido no puede ser un regalo más débil que el resto.
                    if (TrainerPokemonTable.HasExplicitMoves(grown, slot))
                    {
                        TrainerPokemonTable.ClearMoves(grown, slot);
                    }

                    added++;
                }

                parties[trainer] = grown;
                entry[CountOffset] = (byte)(count + extra);
                trainers.Write(trainer, entry);
                counts[trainer] = count + extra;
                battles++;
            }
        }

        // El GARC de equipos se reescribe entero: es el único sitio donde crece un subfichero.
        await File.WriteAllBytesAsync(partyPath, parties.Save(), ct);

        await VerifyAsync(dataPath, partyPath, counts, ct);
        return new ExtraPokemonResult(battles, added, noRoom);
    }

    /// <summary>
    /// Reads both files back and refuses a result whose parties do not match what the trainer
    /// table says they are.
    /// </summary>
    /// <remarks>
    /// Repacking a GARC is exactly the operation that has burned this project before, so the check
    /// is not a formality: every trainer's party has to be its declared count times the entry size,
    /// the file has to still hold the same number of trainers, and the ones that were grown have to
    /// have grown by what was asked.
    /// </remarks>
    private async Task VerifyAsync(string dataPath, string partyPath,
        IReadOnlyDictionary<int, int> expected, CancellationToken ct)
    {
        var vanilla = new GARC.LazyGARC(await File.ReadAllBytesAsync(
            workspace.PathOf(GameFiles.TrainerPokemon), ct));
        var parties = new GARC.LazyGARC(await File.ReadAllBytesAsync(partyPath, ct));
        var trainers = new GARC.LazyGARC(await File.ReadAllBytesAsync(dataPath, ct));

        if (parties.FileCount != vanilla.FileCount)
        {
            throw new InvalidDataException(
                $"trpoke se quedó con {parties.FileCount} equipos en vez de {vanilla.FileCount}.");
        }

        for (var trainer = 0; trainer < trainers.FileCount; trainer++)
        {
            var entry = trainers[trainer];
            if (entry.Length < 0x14 || trainer >= parties.FileCount)
            {
                continue;
            }

            var count = entry[CountOffset];
            var party = parties[trainer];

            if (count > MaxParty)
            {
                throw new InvalidDataException(
                    $"El entrenador {trainer} se quedó con {count} Pokémon, y el máximo es {MaxParty}.");
            }

            if (party.Length != count * TrainerPokemonTable.EntrySize && party.Length >= 0x20)
            {
                throw new InvalidDataException(
                    $"El entrenador {trainer} dice llevar {count} Pokémon pero su equipo mide "
                    + $"{party.Length} bytes, que no son {count} entradas de {TrainerPokemonTable.EntrySize}.");
            }

            if (expected.TryGetValue(trainer, out var wanted) && count != wanted)
            {
                throw new InvalidDataException(
                    $"Al entrenador {trainer} se le pidieron {wanted} Pokémon y quedó con {count}.");
            }
        }
    }
}
