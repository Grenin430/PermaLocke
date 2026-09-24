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
    /// <summary>The last line written, so twenty seconds of the same numbers are written once.</summary>
    private string? _lastRead;

    /// <summary>
    /// Records PermaLocke asks for. A short list on purpose: reading two hundred numbers nobody
    /// looks at would just be noise in the log.
    /// </summary>
    public static readonly int[] Wanted =
    [
        2,    // Momento en que se completó la historia: distinto de cero = campeón
        72,   // Dominsignias recogidas («Stickers Collected»)
        100,  // Veces que se ha defendido el título de campeón
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
                "No se encuentra tu partida de Ultra Luna.",
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
                "No se han podido leer los contadores de la partida.", now);
        }
    }

    /// <summary>Reads one specific save file, so the whole read can be exercised on a copy.</summary>
    public GameRecordSnapshot ReadFrom(string path, string? notice = null, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.Now;

        if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
        {
            return GameRecordSnapshot.Unavailable(
                "No se ha podido leer tu partida.", now);
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

        // Los mil contadores de evento enteros. No cuesta nada leerlos y evita tener que mantener
        // aquí una lista de cuáles interesan: eso ya lo dice Data/achievements.json.
        var work = game.Blocks.EventWork;
        var works = new Dictionary<int, int>(work.EventWorkCount);

        for (var counter = 0; counter < work.EventWorkCount; counter++)
        {
            works[counter] = work.GetWork(counter);
        }

        // Se lee cada veinte segundos y casi siempre dice lo mismo: se apunta cuando cambia algo (§167).
        var line = $"Récords leídos: {values.GetValueOrDefault(6)} capturas, {values.GetValueOrDefault(42)} balls, "
                   + $"{values.GetValueOrDefault(46)} huidas, {values.GetValueOrDefault(41)} movimientos Z, "
                   + $"{items.Count} objetos";

        if (line != _lastRead)
        {
            _lastRead = line;
            logger.LogInformation("{Records}", line);
        }

        return new GameRecordSnapshot(true, null, notice, values, now, items, works);
    }
}
