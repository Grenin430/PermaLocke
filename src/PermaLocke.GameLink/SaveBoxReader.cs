using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink;

/// <summary>
/// Reads the player's PC boxes out of the save file.
/// </summary>
/// <remarks>
/// <para>
/// The save, not memory, for the same reason the delivery writes there (<see cref="SaveBoxDelivery"/>):
/// it is a fixed format PKHeX has read for years and behaves the same on ten other machines,
/// whereas locating 960 box slots in RAM would be a fresh investigation.
/// </para>
/// <para>
/// The price is different here, and smaller. Reading never needs the game closed — nothing is
/// written — but what the file holds is <b>the last thing the player saved</b>. So when the game
/// is loaded the snapshot says so, instead of passing off a stale PC as the live one.
/// </para>
/// </remarks>
public sealed class SaveBoxReader(PlayerSave save, ILocationLookup locations, string language,
    ILogger<SaveBoxReader> logger) : IBoxReader
{
    private readonly GameStrings _strings = GameInfo.GetStrings(language);

    public Task<BoxSnapshot> ReadAsync(CancellationToken ct = default) => Task.Run(Read, ct);

    private BoxSnapshot Read()
    {
        var now = DateTimeOffset.Now;
        var path = save.Find();

        if (path is null)
        {
            return BoxSnapshot.Unavailable(
                "No se encuentra tu partida de Ultra Luna.",
                now);
        }

        var notice = save.IsGameLoaded()
            ? "Guarda y cierra el juego para poder usar el visor Pokémon."
            : null;

        try
        {
            return ReadFrom(path, notice, now);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron leer las cajas de {Path}", path);
            return BoxSnapshot.Unavailable(
                "No se ha podido leer la partida.", now);
        }
    }

    /// <summary>
    /// Reads one specific save file. Separate from finding it so the file handling can be
    /// exercised without an emulator anywhere near it.
    /// </summary>
    public BoxSnapshot ReadFrom(string path, string? notice = null, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.Now;

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return BoxSnapshot.Unavailable(
                "No se ha podido leer tu partida.", now);
        }

        var snapshot = ReadFrom(game, notice, now);

        logger.LogInformation("Cajas leídas de {Path}: {Count} Pokémon en {Boxes} cajas",
            path, snapshot.Total, snapshot.Boxes.Count);

        return snapshot;
    }

    /// <summary>
    /// Walks an already opened save. This is where all the reading lives, so it can be tested
    /// against a save built in memory: PKHeX does not recognise a blank save written back to
    /// disk, so a full round trip through a file would need the player's own partida.
    /// </summary>
    public BoxSnapshot ReadFrom(SAV7USUM game, string? notice = null, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.Now;
        var boxes = new List<BoxContents>(game.BoxCount + 1)
        {
            // El equipo va primero porque es lo que el jugador está usando ahora mismo. Es otro
            // almacén del save, con seis huecos en vez de treinta, así que se marca como tal:
            // quien escriba tiene que saber en cuál de los dos está metiendo la mano.
            ReadParty(game)
        };

        for (var box = 0; box < game.BoxCount; box++)
        {
            var occupants = new List<BoxedPokemon>();

            for (var slot = 0; slot < game.BoxSlotCount; slot++)
            {
                if (game.GetBoxSlotAtIndex(box, slot) is PK7 { Species: > 0 } pokemon)
                {
                    occupants.Add(Describe(pokemon, box, slot));
                }
            }

            boxes.Add(new BoxContents(box + 1, BoxName(game, box), occupants, game.BoxSlotCount));
        }

        return new BoxSnapshot(true, null, notice, boxes, game.BoxSlotCount, game.OT, now);
    }

    /// <summary>
    /// The six the player is carrying.
    /// </summary>
    /// <remarks>
    /// <c>PartyCount</c> is how many are in it, not how big it is, so the loop runs to six and
    /// skips the empty ones: reading only up to the count would hide a hole left by a Pokémon
    /// deposited from the middle of the party.
    /// </remarks>
    private BoxContents ReadParty(SAV7USUM game)
    {
        const int PartySlots = 6;
        var members = new List<BoxedPokemon>(PartySlots);

        for (var slot = 0; slot < PartySlots; slot++)
        {
            if (game.GetPartySlotAtIndex(slot) is PK7 { Species: > 0 } pokemon)
            {
                members.Add(Describe(pokemon, BoxedPokemon.PartyBox, slot, inParty: true));
            }
        }

        return new BoxContents(0, "Equipo", members, PartySlots, IsParty: true);
    }

    /// <summary>The name the player gave the box, or the number when they never renamed it.</summary>
    private static string BoxName(SAV7USUM game, int box)
    {
        var name = game.BoxLayout.GetBoxName(box);
        return string.IsNullOrWhiteSpace(name) ? $"Caja {box + 1}" : name;
    }

    private BoxedPokemon Describe(PK7 pokemon, int box, int slot, bool inParty = false)
    {
        // Un Pokémon guardado en caja no lleva sus estadísticas de combate: el juego se las
        // calcula al sacarlo. Esto hace lo mismo en memoria. No se escribe nada en la partida.
        //
        // Ojo con lo que vale ese cálculo: PKHeX saca la estadística de SU tabla de estadísticas
        // base, y esta competición se juega con la ROM randomizada y shuffleBaseStats activo, así
        // que esa tabla no es la del cartucho. Por eso sale marcado como calculado: es una
        // estimación, y la pantalla lo dice en vez de darlo por bueno.
        //
        // El equipo sí las lleva guardadas, y esas son las de verdad. Recalcularlas ahí sería
        // tirar lo que escribió el juego para poner un número peor.
        // Antes de recalcular nada: ResetPartyStats toca la cola, no el bloque firmado, pero lo que se
        // quiere saber es si la entrada TAL COMO ESTÁ EN LA PARTIDA cuadra con su firma (§97).
        var intact = pokemon.ChecksumValid;

        if (!inParty)
        {
            pokemon.ResetPartyStats();
        }

        return new BoxedPokemon(
            Box: box,
            Slot: slot,
            Species: pokemon.Species,
            Form: pokemon.Form,
            SpeciesName: Name(_strings.Species, pokemon.Species, $"#{pokemon.Species}"),
            Nickname: pokemon.IsNicknamed ? pokemon.Nickname : string.Empty,
            // Con la curva del mundo instalado: PKHeX acaba en la 807 y a las de gen 8-9 les pone
            // crecimiento Medio, así que un Ferrocuello de nivel 59 salía de nivel 64 (§134).
            Level: Data.GameLevels.Of(pokemon),
            IsShiny: pokemon.IsShiny,
            IsEgg: pokemon.IsEgg,
            GenderMark: GenderMarkFor(pokemon.Gender),
            NatureName: Name(_strings.Natures, (int)pokemon.Nature, "?"),
            AbilityName: Name(_strings.Ability, Data.PokemonAbility.Of(pokemon), "?"),
            HeldItemName: pokemon.HeldItem == 0 ? string.Empty : Name(_strings.Item, pokemon.HeldItem, "?"),
            BallName: Name(_strings.balllist, pokemon.Ball, "?"),
            TrainerName: pokemon.OriginalTrainerName,
            MetLocationName: locations.GetName(pokemon.MetLocation),
            MetLevel: pokemon.MetLevel,
            Moves: [.. pokemon.Moves.Where(move => move != 0).Select(move => Name(_strings.Move, move, "?"))],
            Stats: [pokemon.Stat_HPMax, pokemon.Stat_ATK, pokemon.Stat_DEF,
                pokemon.Stat_SPA, pokemon.Stat_SPD, pokemon.Stat_SPE],
            Ivs: [pokemon.IV_HP, pokemon.IV_ATK, pokemon.IV_DEF,
                pokemon.IV_SPA, pokemon.IV_SPD, pokemon.IV_SPE],
            Evs: [pokemon.EV_HP, pokemon.EV_ATK, pokemon.EV_DEF,
                pokemon.EV_SPA, pokemon.EV_SPD, pokemon.EV_SPE],
            Friendship: pokemon.OriginalTrainerFriendship,
            Pid: pokemon.PID,
            StatsAreComputed: !inParty,
            Ball: pokemon.Ball,
            Nature: (int)pokemon.Nature,
            IsIntact: intact,
            StatLevel: inParty ? pokemon.Stat_Level : 0,

            // Contexto de gen 9 y no el del PK7: la tabla de formas de gen 7 no tiene las de Galar ni
            // las de Hisui, y un Meowth de forma 2 se quedaría sin nombre (§140).
            FormName: pokemon.Form > 0
                ? ShowdownParsing.GetStringFromForm(pokemon.Form, _strings, pokemon.Species, EntityContext.Gen9)
                : string.Empty,

            // Los cuatro huecos tal cual, vacíos incluidos: el recuerda-movimientos escribe en un hueco
            // concreto y tiene que saber qué hay en cada uno (§142).
            MoveIds: [pokemon.Move1, pokemon.Move2, pokemon.Move3, pokemon.Move4],
            RelearnMoveIds: [pokemon.RelearnMove1, pokemon.RelearnMove2, pokemon.RelearnMove3, pokemon.RelearnMove4]);
    }

    /// <summary>The Mars and Venus signs, built from their code points rather than typed.</summary>
    /// <remarks>
    /// Typed, the symbol would have to survive the encoding of this file, the compiler reading it
    /// and the font drawing it. Two of those three are worth removing from the question.
    /// </remarks>
    private static string GenderMarkFor(int gender) => gender switch
    {
        0 => ((char)0x2642).ToString(),
        1 => ((char)0x2640).ToString(),
        _ => string.Empty,
    };

    /// <summary>An id out of range must not take the whole PC down with it.</summary>
    private static string Name(IReadOnlyList<string> table, int index, string fallback) =>
        index >= 0 && index < table.Count && !string.IsNullOrWhiteSpace(table[index])
            ? table[index]
            : fallback;
}
