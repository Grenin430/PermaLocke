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
                "No se encuentra la partida de Ultra Luna. ¿Has jugado y guardado alguna vez con este emulador?",
                now);
        }

        var notice = save.IsGameLoaded()
            ? "El juego está abierto: esto es lo último que guardaste, no lo que tienes ahora mismo en pantalla."
            : null;

        try
        {
            return ReadFrom(path, notice, now);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron leer las cajas de {Path}", path);
            return BoxSnapshot.Unavailable(
                "No se ha podido leer la partida. El detalle está en la carpeta Logs.", now);
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
                $"El fichero de partida no se ha podido leer como Ultra Luna: {path}", now);
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
        var boxes = new List<BoxContents>(game.BoxCount);

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

            boxes.Add(new BoxContents(box + 1, BoxName(game, box), occupants));
        }

        return new BoxSnapshot(true, null, notice, boxes, game.BoxSlotCount, game.OT, now);
    }

    /// <summary>The name the player gave the box, or the number when they never renamed it.</summary>
    private static string BoxName(SAV7USUM game, int box)
    {
        var name = game.BoxLayout.GetBoxName(box);
        return string.IsNullOrWhiteSpace(name) ? $"Caja {box + 1}" : name;
    }

    private BoxedPokemon Describe(PK7 pokemon, int box, int slot)
    {
        // Un Pokémon guardado en caja no lleva sus estadísticas de combate: el juego se las
        // calcula al sacarlo. Esto hace lo mismo en memoria. No se escribe nada en la partida.
        pokemon.ResetPartyStats();

        return new BoxedPokemon(
            Box: box,
            Slot: slot,
            Species: pokemon.Species,
            Form: pokemon.Form,
            SpeciesName: Name(_strings.Species, pokemon.Species, $"#{pokemon.Species}"),
            Nickname: pokemon.IsNicknamed ? pokemon.Nickname : string.Empty,
            Level: pokemon.CurrentLevel,
            IsShiny: pokemon.IsShiny,
            IsEgg: pokemon.IsEgg,
            GenderMark: GenderMarkFor(pokemon.Gender),
            NatureName: Name(_strings.Natures, (int)pokemon.Nature, "?"),
            AbilityName: Name(_strings.Ability, pokemon.Ability, "?"),
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
            Pid: pokemon.PID);
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
