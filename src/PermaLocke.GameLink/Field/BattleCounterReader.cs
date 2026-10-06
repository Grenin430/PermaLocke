using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Rpc;
using PKHeX.Core;

namespace PermaLocke.GameLink.Field;

/// <summary>
/// The trainer card counters, read live: how many wild battles, captures, escapes and shinies.
/// </summary>
/// <remarks>
/// <para>
/// The game keeps its records in memory in the same layout as the save — the first hundred as 32-bit numbers,
/// the next hundred as 16-bit — and updates them there as things happen. Measured on 2026-09-14 (§117): record
/// 4, wild battles, went up <b>when each battle started</b>, before the battle tables appeared, and for a battle
/// the player fled from as much as for one that ended in a capture. That is what makes it the signal that a
/// wild battle has begun: the battle tables say a battle exists, not whether it is wild — a trainer's Pokémon
/// sat in the same slot 12 as a wild one.
/// </para>
/// <para>
/// Found by what it holds, never by an address alone. The first place tried is the one measured, a fixed
/// distance from the bag block; it is believed only if the counters that cannot move between saves — times
/// saved, the moment the story was completed — equal the save's and the rest are at or a little above it.
/// Otherwise one search for the save's own records, with the step counter masked out.
/// </para>
/// </remarks>
public sealed class BattleCounterReader(AzaharRpcClient client, BagService bag, SavedGameCache saved,
    ILogger<BattleCounterReader> logger, TimeProvider? timeProvider = null, LiveSave? live = null) : IBattleCounters
{
    /// <summary>From the bag block to the records, measured: bag at 0x33011934, records at 0x33079A48.</summary>
    public const uint DistanceFromBag = 0x68114;

    public const int WildBattlesRecord = 4;
    public const int CaughtRecord = 6;
    public const int FledRecord = 46;
    public const int ShinyRecord = 127;

    /// <summary>Enough bytes to reach record 127, the first 16-bit one past the hundred 32-bit ones.</summary>
    private const int ReadLength = (100 * 4) + (28 * 2);

    private static readonly TimeSpan SearchEvery = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private uint? _address;

    /// <summary>Whether the shiny counter checked out against the save when the block was located.</summary>
    private bool _shinyTrusted;
    private DateTimeOffset _lastSearch = DateTimeOffset.MinValue;
    private BattleCounters? _last;
    private int _readFailures;
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private DateTimeOffset _lastDirectCheck = DateTimeOffset.MinValue;
    private static readonly TimeSpan DirectCheckEvery = TimeSpan.FromSeconds(5);

    public string? Problem { get; private set; } = "Preparando la detección de encuentros.";

    /// <summary>Where a record sits inside the block.</summary>
    public static int OffsetOf(int record) => record < 100 ? record * 4 : (100 * 4) + ((record - 100) * 2);

    public BattleCounters? Read()
    {
        lock (_gate)
        {
            // A search may have found a save copy before the live bag was available. Such a copy
            // remains readable forever but never counts new battles. Prefer the validated live
            // reference when it appears, even if the old address still looks plausible.
            var now = _time.GetUtcNow();
            if (_address is not null && now - _lastDirectCheck >= DirectCheckEvery
                && saved.Load() is { } game && (FromGame(game.Save) ?? FromBag(now, game.Save)) is { } current)
                _address = current;

            _address ??= Locate();

            if (_address is not { } address) return null;
            if (!client.TryReadMemory(address, ReadLength, out var block))
            {
                Problem = "No se pueden leer los encuentros del juego. Se está recuperando la lectura.";
                if (++_readFailures >= 3)
                {
                    logger.LogWarning("Contadores ilegibles en 0x{Address:X8}; se descarta esa dirección", address);
                    _address = null;
                    _last = null;
                }
                return null;
            }
            _readFailures = 0;

            var counters = Parse(block);

            if (!_shinyTrusted)
            {
                counters = counters with { ShinyEncountered = -1 };
            }

            // Un contador que baja, o que salta cientos de golpe, no es el bloque: se vuelve a buscar.
            if (_last is { } before && !Plausible(before, counters))
            {
                logger.LogWarning("Los contadores del juego ya no cuadran en 0x{Address:X8}; se vuelven a localizar", address);
                _address = null;
                Problem = "Los encuentros todavía no se pueden verificar. Se está recuperando la lectura.";
                _last = null;
                return null;
            }

            Problem = null;
            _last = counters;
            return counters;
        }
    }

    public static BattleCounters Parse(ReadOnlySpan<byte> block) => new(
        BinaryPrimitives.ReadInt32LittleEndian(block[OffsetOf(WildBattlesRecord)..]),
        BinaryPrimitives.ReadInt32LittleEndian(block[OffsetOf(CaughtRecord)..]),
        BinaryPrimitives.ReadInt32LittleEndian(block[OffsetOf(FledRecord)..]),
        BinaryPrimitives.ReadUInt16LittleEndian(block[OffsetOf(ShinyRecord)..]));

    /// <summary>Counters only go up, and not by much between two readings a fraction of a second apart.</summary>
    public static bool Plausible(BattleCounters before, BattleCounters now) =>
        Step(before.WildBattles, now.WildBattles) && Step(before.Caught, now.Caught)
        && Step(before.Fled, now.Fled) && Step(before.ShinyEncountered, now.ShinyEncountered);

    private static bool Step(int before, int now) => now >= before && now - before <= 50;

    /// <summary>
    /// Whether a block of memory is this player's records: the counters that only move when saving are equal,
    /// and the ones that move while playing are at or not far above the save.
    /// </summary>
    public static bool MatchesSave(ReadOnlySpan<byte> block, Func<int, int> savedRecord)
    {
        if (block.Length < ReadLength)
        {
            return false;
        }

        // Veces guardado y momento en que se completó la historia: no se mueven sin guardar.
        if (At(block, 1) != savedRecord(1) || At(block, 2) != savedRecord(2))
        {
            return false;
        }

        // Sin el 127: es el primero de 16 bits y su posición está calculada, no medida. Si estuviera mal no
        // debe llevarse por delante los demás, así que se comprueba aparte (ShinyMatchesSave).
        foreach (var record in new[] { 3, WildBattlesRecord, 5, CaughtRecord, FledRecord })
        {
            var difference = At(block, record) - savedRecord(record);

            if (difference < 0 || difference > 5000)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the shiny counter read at its calculated place agrees with the save, so it can be trusted.
    /// </summary>
    public static bool ShinyMatchesSave(ReadOnlySpan<byte> block, Func<int, int> savedRecord)
    {
        if (block.Length < ReadLength)
        {
            return false;
        }

        var difference = At(block, ShinyRecord) - savedRecord(ShinyRecord);
        return difference is >= 0 and <= 50;
    }

    private static int At(ReadOnlySpan<byte> block, int record) => record < 100
        ? BinaryPrimitives.ReadInt32LittleEndian(block[OffsetOf(record)..])
        : BinaryPrimitives.ReadUInt16LittleEndian(block[OffsetOf(record)..]);

    private uint? Locate()
    {
        var now = _time.GetUtcNow();
        if (saved.Load() is not { } game)
        {
            Problem = "Guarda la partida desde el menú del juego para activar la detección de encuentros.";
            return null;
        }
        var save = game.Save;
        Problem = "Localizando los encuentros del juego. Guarda fuera de combate y espera unos segundos.";

        // A validated bag may become available after an unsuccessful search. Retry this cheap
        // reference independently, instead of keeping it behind the one-minute full-search cooldown.
        if (FromGame(save) is { } own) return own;
        if (FromBag(now, save) is { } direct) return direct;

        if (now - _lastSearch < SearchEvery) return null;
        _lastSearch = now;
        if (client.GetProcess() == uint.MaxValue)
        {
            Problem = "El juego se está reconectando; los encuentros todavía no se pueden leer.";
            return null;
        }
        // Los diez primeros récords de la partida, con los pasos sin comprobar: se mueven al andar.
        var pattern = new byte[40];
        var mask = Enumerable.Repeat((byte)0xFF, 40).ToArray();

        for (var record = 0; record < 10; record++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(pattern.AsSpan(record * 4), save.GetRecord(record));
        }

        foreach (var volatileRecord in new[] { 0, 3, WildBattlesRecord, 5, CaughtRecord, 7, 9 })
        {
            mask.AsSpan(volatileRecord * 4, 4).Clear();
        }

        foreach (var hit in client.SearchMemory(0x30000000, 0x04000000, pattern, mask))
        {
            if (client.TryReadMemory(hit, ReadLength, out var bytes) && MatchesSave(bytes, save.GetRecord))
            {
                Trust(bytes, save.GetRecord);
                logger.LogInformation("Contadores del juego localizados en 0x{Address:X8}", hit);
                return hit;
            }
        }

        logger.LogWarning("No se han encontrado los contadores del juego en memoria");
        return null;
    }

    /// <summary>
    /// The records where the game's own save data keeps them (<see cref="LiveSave"/>, 2026-10-06): no bag and no search needed.
    /// Believed only as any other candidate, against the save.
    /// </summary>
    private uint? FromGame(SAV7USUM save)
    {
        if (live?.AddressOf(LiveSave.RecordBlock) is not { } candidate || candidate == _address
            || !client.TryReadMemory(candidate, ReadLength, out var bytes) || !MatchesSave(bytes, save.GetRecord))
            return null;

        Trust(bytes, save.GetRecord);
        logger.LogInformation("Contadores del juego en 0x{Address:X8}, donde los guarda el propio juego", candidate);
        return candidate;
    }

    private uint? FromBag(DateTimeOffset now, SAV7USUM save)
    {
        if (now - _lastDirectCheck < DirectCheckEvery) return null;
        _lastDirectCheck = now;
        if (bag.Block is not { } block
            || !client.TryReadMemory(block.PointerTableAddress, block.Layout.PointerTableBytes, out var pointers)
            || !block.Layout.TryMatchPointerTable(block.PointerTableAddress, pointers, out var bagAddress)
            || bagAddress != block.BaseAddress)
            return null;

        var candidate = bagAddress + DistanceFromBag;
        if (candidate == _address || !client.TryReadMemory(candidate, ReadLength, out var bytes)
            || !MatchesSave(bytes, save.GetRecord))
            return null;

        Trust(bytes, save.GetRecord);
        logger.LogInformation("Contadores del juego en 0x{Address:X8}, a la distancia medida de la mochila", candidate);
        return candidate;
    }

    private void Trust(ReadOnlySpan<byte> block, Func<int, int> savedRecord)
    {
        _shinyTrusted = ShinyMatchesSave(block, savedRecord);

        if (!_shinyTrusted)
        {
            logger.LogWarning(
                "El contador de variocolor no casa con la partida en su sitio calculado: no se usará para la cláusula shiny");
        }
    }
}
