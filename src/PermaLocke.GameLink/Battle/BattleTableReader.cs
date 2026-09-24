using Microsoft.Extensions.Logging;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Battle;

/// <summary>
/// Reads the battle tables from the running game without ever searching memory in a loop.
/// </summary>
/// <remarks>
/// <para>
/// Written after the research that found these tables <b>froze the emulator</b> (§114): it searched
/// every four seconds across 30 MB that do not exist, the emulator logged an error per missing page
/// and stops emulating while it searches. Everything here is shaped by that:
/// </para>
/// <list type="bullet">
/// <item>A search is <b>one call over one megabyte</b> — where both tables sat — at most every
/// <see cref="SearchEvery"/>, and only while no battle is known. Measured: 48 searches over 68 MB took
/// 0.7 s, so one over a megabyte is a fraction of a frame.</item>
/// <item>Nothing wider, ever. A search of the whole linear heap once a minute was here and was taken
/// out after a burst of large searches froze the game (§114 ter): a battle whose tables sit elsewhere
/// has its deaths seen when it ends, as before.</item>
/// <item>During a battle nothing is searched: the known blocks are read, a few dozen bytes each, and
/// the first one that stops looking like a battle block drops the lot.</item>
/// </list>
/// </remarks>
public sealed class BattleTableReader(AzaharRpcClient client, ILogger<BattleTableReader> logger)
{
    /// <summary>The megabyte where both tables sat in every battle measured.</summary>
    private const uint Window = 0x30000000, WindowSize = 0x00100000;

    public static readonly TimeSpan SearchEvery = TimeSpan.FromSeconds(3);

    /// <summary>The fork returns at most this many hits per call; past it, the search is paged.</summary>
    private const int SearchCap = 255;

    private IReadOnlyList<(uint Origin, int[] Ids)> _known = [];
    private DateTimeOffset _lastSearch = DateTimeOffset.MinValue;
    private int _failedReadings;

    /// <summary>
    /// Lets the next <see cref="Read"/> search at once instead of waiting its turn.
    /// </summary>
    /// <remarks>
    /// For when the game has just said a wild battle started (its counter went up, §117): the ball rule needs
    /// the species within a second, and waiting up to <see cref="SearchEvery"/> could let a duplicate be caught.
    /// It is still one search of one megabyte, triggered by a battle, never a loop.
    /// </remarks>
    public void SearchSoon() => _lastSearch = DateTimeOffset.MinValue;

    /// <summary>The whole Pokémon behind a block, or null when it cannot be read or is not that Pokémon.</summary>
    /// <remarks>One read of 296 bytes from the block's own pointer. See <see cref="BattlePokemon"/>.</remarks>
    public PKHeX.Core.PK7? ReadPokemon(BattleBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        return client.TryReadMemory(block.Pointer, BattlePokemon.ReadLength, out var bytes)
            ? BattlePokemon.Parse(bytes, block.Species)
            : null;
    }

    /// <summary>
    /// The battle tables as they are now. Empty when there is no battle, or when it is not time to
    /// look for one yet.
    /// </summary>
    public IReadOnlyList<BattleTable> Read(DateTimeOffset now)
    {
        if (_known.Count > 0)
        {
            if (ReadKnown() is { } tables)
            {
                _failedReadings = 0;
                return tables;
            }

            logger.LogInformation("Las tablas del combate han dejado de validar; se vuelve a comprobar el combate");
            _known = [];
            _failedReadings = 0;
        }

        if (now - _lastSearch < SearchEvery)
        {
            return [];
        }

        _lastSearch = now;

        // Solo el megabyte donde han estado las tablas en todos los combates medidos. Había una búsqueda
        // de los 64 MB para cuando ahí no hubiera nada, y se quitó: una ráfaga de búsquedas grandes
        // CONGELÓ el juego (§114 ter), y una sola cada minuto durante toda una sesión es un riesgo que
        // no compensa. Si un combate pone las tablas en otro sitio, su muerte se ve al terminar.
        var found = Locate(Window, WindowSize);

        var battle = BattleFaintTracker.InProgress(found);

        if (battle.Count > 0)
        {
            _known = [.. battle.Select(table => (table.Origin, table.Blocks.Select(block => block.BattleId).ToArray()))];

            logger.LogInformation("Combate localizado: {Tables} tablas en {Origins}, {Blocks} Pokémon",
                battle.Count, string.Join(", ", battle.Select(table => $"0x{table.Origin:X8}")),
                battle[0].Blocks.Count);
        }

        return battle;
    }

    private IReadOnlyList<BattleTable> Locate(uint start, uint size)
    {
        var blocks = new List<BattleBlock>();
        var end = start + size;
        var from = start;

        for (var page = 0; page < 8 && from < end; page++)
        {
            var hits = client.SearchMemory(from, end - from, BattleLayout.SearchPattern, BattleLayout.SearchMask, stride: 4);

            foreach (var hit in hits)
            {
                if (client.TryReadMemory(hit, BattleLayout.ReadLength, out var bytes)
                    && BattleLayout.Parse(hit, bytes) is { } block)
                {
                    blocks.Add(block);
                }
            }

            if (hits.Count < SearchCap)
            {
                break;
            }

            from = hits[^1] + 4;
        }

        return BattleTable.Group(blocks);
    }

    /// <summary>Re-reads the known blocks, or null as soon as one is no longer a battle block.</summary>
    private IReadOnlyList<BattleTable>? ReadKnown()
    {
        var tables = new List<BattleTable>(_known.Count);

        foreach (var (origin, ids) in _known)
        {
            var blocks = new List<BattleBlock>(ids.Length);
            var probe = new BattleTable(origin, []);

            foreach (var id in ids)
            {
                var header = probe.HeaderOf(id);

                if (!client.TryReadMemory(header, BattleLayout.ReadLength, out var bytes))
                {
                    // A missing reply is not a battle ending. The monitor skips failed polls, preserving
                    // the faint tracker, encounter and recording until another reading can validate them.
                    // Repeated failures do release the address so relocation remains possible.
                    if (++_failedReadings < 3)
                        throw new AzaharRpcException($"Lectura de combate interrumpida en 0x{header:X8}; se conserva el seguimiento para reintentar.");
                    return null;
                }

                if (BattleLayout.Parse(header, bytes) is not { } block
                    || block.BattleId != id)
                {
                    return null;
                }

                blocks.Add(block);
            }

            tables.Add(new BattleTable(origin, blocks));
        }

        return tables;
    }
}
