using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Field;

/// <summary>
/// Berry piles give once per run (2026-09-28, players' list item 1): a pile picked once is put back as picked whenever
/// the game regrows it.
/// </summary>
/// <remarks>
/// <para>
/// The piles live in save block 22 (BerrySpot): 64 entries of 4 bytes, a state byte and three bytes of the day's roll.
/// In the organiser's save every entry had state 2 with its roll, so 2 is taken as «grown»; anything else as «picked».
/// <b>Not measured by picking a pile yet</b>: <c>Probe --campo</c> before and after picking one says whether it holds.
/// </para>
/// <para>
/// The game regrows the piles by its own clock, so nothing written into the save file can stop it. What works is the
/// live copy: found once by the save's rolls (never a sweep in a loop), then read every few seconds. A pile seen picked
/// is remembered on disk with its picked bytes; if it later reads grown, those bytes go back, and the next save keeps
/// them.
/// </para>
/// </remarks>
public sealed class BerryPileKeeper(AzaharRpcClient client, SavedGameCache saved, string stateFolder,
    ILogger<BerryPileKeeper> logger)
{
    public const int Block = 22, Piles = 64, EntrySize = 4, Grown = 2;

    private static readonly TimeSpan SearchEvery = TimeSpan.FromMinutes(1);

    private uint? _address;
    private DateTimeOffset _lastSearch = DateTimeOffset.MinValue;
    private Dictionary<int, string>? _picked;
    private Guid _run;

    /// <summary>One look: remembers newly picked piles and puts back any remembered one the game regrew.</summary>
    public void Keep(Guid run)
    {
        if (_run != run)
        {
            _run = run;
            _picked = null;
        }

        _picked ??= LoadPicked();

        if ((_address ??= Locate()) is not { } address) return;

        if (!client.TryReadMemory(address, Piles * EntrySize, out var live))
        {
            _address = null;
            return;
        }

        var changed = false;
        for (var pile = 0; pile < Piles; pile++)
        {
            var entry = live.AsSpan(pile * EntrySize, EntrySize);
            var remembered = _picked.TryGetValue(pile, out var hex);

            if (entry[0] != Grown && !remembered)
            {
                _picked[pile] = Convert.ToHexString(entry);
                changed = true;
                logger.LogInformation("Montón de bayas {Pile} cogido: ya no vuelve a dar en esta run", pile);
            }
            else if (entry[0] == Grown && remembered)
            {
                client.WriteMemory(address + (uint)(pile * EntrySize), Convert.FromHexString(hex!));
                logger.LogInformation("Montón de bayas {Pile} había vuelto a crecer: se deja cogido", pile);
            }
        }

        if (changed) SavePicked();
    }

    /// <summary>Whether a block of memory is this save's piles: most rolls equal (a few may have been picked or regrown since).</summary>
    public static bool MatchesSave(ReadOnlySpan<byte> live, ReadOnlySpan<byte> save)
    {
        if (live.Length < Piles * EntrySize || save.Length < Piles * EntrySize) return false;

        var same = 0;
        for (var pile = 0; pile < Piles; pile++)
        {
            if (live.Slice((pile * EntrySize) + 1, 3).SequenceEqual(save.Slice((pile * EntrySize) + 1, 3))) same++;
        }

        return same >= Piles * 3 / 4;
    }

    private uint? Locate()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastSearch < SearchEvery || saved.Load() is not { } game) return null;
        _lastSearch = now;

        var block = game.Save.AllBlocks[Block];
        var save = game.Save.Data.Slice(block.Offset, Piles * EntrySize).ToArray();

        // Los 16 primeros montones, sin su byte de estado: puede haber cambiado desde que se guardó.
        var pattern = save.AsSpan(0, AzaharRpcClient.MaxSearchPattern).ToArray();
        var mask = Enumerable.Repeat((byte)0xFF, pattern.Length).ToArray();
        for (var at = 0; at < mask.Length; at += EntrySize) mask[at] = 0;

        foreach (var hit in client.SearchMemory(0x30000000, 0x04000000, pattern, mask))
        {
            if (client.TryReadMemory(hit, Piles * EntrySize, out var bytes) && MatchesSave(bytes, save))
            {
                logger.LogInformation("Montones de bayas localizados en 0x{Address:X8}", hit);
                return hit;
            }
        }

        logger.LogWarning("No se han encontrado los montones de bayas en memoria");
        return null;
    }

    private string StateFile => Path.Combine(stateFolder, $"montones-cogidos-{_run:N}.json");

    private Dictionary<int, string> LoadPicked()
    {
        try
        {
            return File.Exists(StateFile)
                ? JsonSerializer.Deserialize<Dictionary<int, string>>(File.ReadAllText(StateFile)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "No se pudo leer {File}", StateFile);
            return [];
        }
    }

    private void SavePicked()
    {
        try
        {
            Directory.CreateDirectory(stateFolder);
            File.WriteAllText(StateFile, JsonSerializer.Serialize(_picked));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "No se pudo guardar {File}", StateFile);
        }
    }
}
