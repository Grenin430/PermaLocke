using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink;

/// <summary>
/// Reads the game's own record counters out of the save file.
/// </summary>
/// <remarks>
/// <para>
/// Ultra Moon keeps around two hundred counters — the ones the trainer card shows — and among them
/// are exactly the things a competition wants to reward: Z-moves used, battles fled from, shinies
/// met, trainers fought. Counting those again from outside would mean watching memory for events
/// the game already writes down, and getting a second number that can only be worse.
/// </para>
/// <para>
/// The price is the same as the box viewer's: the file holds what the player last saved. Reading
/// needs no permission and breaks nothing, but a counter will not move until they save.
/// </para>
/// </remarks>
public sealed class SaveRecordReader(PlayerSave save, ILogger<SaveRecordReader> logger) : IGameRecords
{
    /// <summary>
    /// Records PermaLocke asks for. A short list on purpose: reading two hundred numbers nobody
    /// looks at would just be noise in the log.
    /// </summary>
    public static readonly int[] Wanted =
    [
        2,    // Momento en que se completó la historia: distinto de cero = campeón
        3,    // Combates totales
        4,    // Combates contra salvajes
        5,    // Combates contra entrenadores
        6,    // Pokémon capturados
        21,   // Pokémon derrotados
        34,   // Wonder trades
        41,   // Movimientos Z usados
        42,   // Poké Balls usadas
        46,   // Huidas de combate
        127,  // Pokémon variocolor encontrados
    ];

    public Task<GameRecordSnapshot> ReadAsync(CancellationToken ct = default) => Task.Run(Read, ct);

    private GameRecordSnapshot Read()
    {
        var now = DateTimeOffset.Now;
        var path = save.Find();

        if (path is null)
        {
            return GameRecordSnapshot.Unavailable(
                "No se encuentra la partida de Ultra Luna. ¿Has jugado y guardado alguna vez con este emulador?",
                now);
        }

        var notice = save.IsGameLoaded()
            ? "El juego está abierto: estos números son los de la última vez que guardaste."
            : null;

        try
        {
            return ReadFrom(path, notice, now);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron leer los récords de {Path}", path);
            return GameRecordSnapshot.Unavailable(
                "No se han podido leer los contadores de la partida. El detalle está en la carpeta Logs.", now);
        }
    }

    /// <summary>Reads one specific save file, so the whole read can be exercised on a copy.</summary>
    public GameRecordSnapshot ReadFrom(string path, string? notice = null, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.Now;

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return GameRecordSnapshot.Unavailable(
                $"El fichero de partida no se ha podido leer como Ultra Luna: {path}", now);
        }

        var values = new Dictionary<int, int>(Wanted.Length);

        foreach (var record in Wanted)
        {
            values[record] = game.Records.GetRecord(record);
        }

        // Todo lo que hay en la mochila, sin mirar la cantidad: un objeto clave puede estar con
        // cantidad cero y seguir estando. Lo que se busca aquí son los premios que el juego da y
        // no quita, como los cristales Z de las pruebas.
        var items = game.Inventory.Pouches
            .SelectMany(pouch => pouch.Items)
            .Where(item => item.Index != 0)
            .Select(item => item.Index)
            .ToHashSet();

        logger.LogInformation(
            "Récords leídos: {Caught} capturas, {Balls} balls, {Fled} huidas, {Z} movimientos Z, {Items} objetos",
            values.GetValueOrDefault(6), values.GetValueOrDefault(42),
            values.GetValueOrDefault(46), values.GetValueOrDefault(41), items.Count);

        return new GameRecordSnapshot(true, null, notice, values, now, items);
    }
}
