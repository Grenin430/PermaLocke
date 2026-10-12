using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.GameLink.Field;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

/// <summary>What a payment did: whether it went through, the money left, and why not when it did not.</summary>
public sealed record MoneyResult(bool Success, int Balance, string? Problem = null);

/// <summary>
/// The player's money in Ultra Moon, the currency of the singleplayer app (2026-10-12): the gacha, the shop and everything that
/// cost points is paid with the Pokédollars of the game.
/// </summary>
/// <remarks>
/// <para>
/// Two places, never both. With the game closed the money is in the save file, block Misc (file 0x4400, the money at +4, a
/// 32-bit number up to 9.999.999 as PKHeX has it), and it is written there with a backup first and read back after, like a
/// gacha Pokémon. With the game loaded the file is worthless (the emulator writes its own copy on the next in-game save), so
/// the money is changed in the game's memory: the save data the game holds (<see cref="LiveSave.Base"/>), where the Misc block
/// sits at an offset of its own.
/// </para>
/// <para>
/// That offset is not guessed. The blocks in memory are packed and their spacing is not the file's (see <see cref="LiveSave"/>),
/// so the Misc block is found by its own bytes: the file's block, money left out, is looked for in a short window of the
/// live save data (2 KB from +0x3600, both spacings measured so far land inside it) and only a near-perfect match counts.
/// A few reads, never a sweep. Without a match nothing is written and the payment is refused, saying why.
/// </para>
/// <para>
/// UNVERIFIED in the game (2026-10-12): the file path is PKHeX's; the live path follows the bag's (same save data, same
/// heap), but whether the game reads its money from there has not been seen. <c>Probe --dinero</c> looks without writing.
/// </para>
/// </remarks>
public sealed class GameMoney(PlayerSave save, AzaharRpcClient client, LiveSave live, string backupFolder, ILogger<GameMoney> logger)
{
    public const int Max = 9_999_999;
    private const int MiscFileOffset = 0x4400;
    private const int MoneyInMisc = 4;
    private const uint SearchFrom = 0x3600;
    private const int SearchLength = 0x800;
    private const int Chunk = 0x200;

    private readonly object _gate = new();
    private uint? _base;
    private uint? _miscAt;

    /// <summary>The money now, or null when it cannot be read (no save, or the game loaded and its money not found).</summary>
    public int? Read()
    {
        lock (_gate)
        {
            if (save.IsGameLoaded())
            {
                return LiveAddress() is { } address && client.TryReadMemory(address, 4, out var bytes) ? (int)BitConverter.ToUInt32(bytes) : null;
            }

            return LoadFile() is { } file ? (int)file.Save.Money : null;
        }
    }

    /// <summary>Takes <paramref name="amount"/> from the money, in the file or in the game, and checks it really went.</summary>
    public MoneyResult Spend(int amount)
    {
        if (amount <= 0) return new MoneyResult(false, Read() ?? 0, "La cantidad a pagar debe ser mayor que cero.");

        lock (_gate)
        {
            try
            {
                return save.IsGameLoaded() ? SpendLive(amount) : SpendInFile(amount);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "No se pudo cobrar {Amount} del dinero de la partida", amount);
                return new MoneyResult(false, 0, "No se ha podido tocar la partida. Vuelve a intentarlo.");
            }
        }
    }

    private MoneyResult SpendInFile(int amount)
    {
        if (save.Find() is not { } path || LoadFile() is not { } file)
        {
            return new MoneyResult(false, 0, "No se encuentra tu partida de Ultra Luna.");
        }

        var game = file.Save;
        var before = (int)game.Money;
        if (before < amount) return new MoneyResult(false, before, $"No te llega el dinero: tienes {before} ₽ y hace falta {amount} ₽.");

        Backup(path);
        game.Money = (uint)(before - amount);
        File.WriteAllBytes(path, game.Write().ToArray());

        // Leído otra vez del disco: solo se da por pagado lo que de verdad está escrito.
        var after = LoadFile() is { } again ? (int)again.Save.Money : -1;
        if (after != before - amount)
        {
            logger.LogError("Pago de {Amount}: la partida tiene {After} y debería tener {Expected}", amount, after, before - amount);
            return new MoneyResult(false, Math.Max(0, after), "La partida no ha quedado como debía. Mira la copia en Saves.");
        }

        logger.LogInformation("Pagado {Amount} ₽ en la partida: {Before} -> {After}", amount, before, after);
        return new MoneyResult(true, after);
    }

    private MoneyResult SpendLive(int amount)
    {
        if (LiveAddress() is not { } address || !client.TryReadMemory(address, 4, out var bytes))
        {
            return new MoneyResult(false, 0, "No encuentro tu dinero en el juego. Guarda la partida y vuelve a intentarlo.");
        }

        var before = (int)BitConverter.ToUInt32(bytes);
        if (before is < 0 or > Max) return new MoneyResult(false, 0, "El dinero del juego no se ha leído bien. No se ha cobrado nada.");
        if (before < amount) return new MoneyResult(false, before, $"No te llega el dinero: tienes {before} ₽ y hace falta {amount} ₽.");

        client.WriteMemory(address, BitConverter.GetBytes((uint)(before - amount)));

        var after = client.TryReadMemory(address, 4, out var check) ? (int)BitConverter.ToUInt32(check) : -1;
        if (after != before - amount)
        {
            logger.LogError("Pago de {Amount} en el juego: tiene {After} y debería tener {Expected}", amount, after, before - amount);
            return new MoneyResult(false, Math.Max(0, after), "El juego no ha aceptado el cambio de dinero.");
        }

        logger.LogInformation("Pagado {Amount} ₽ en el juego: {Before} -> {After}", amount, before, after);
        return new MoneyResult(true, after);
    }

    /// <summary>Where the money is in the game's memory now, or null. The Misc block is found once per save-data address.</summary>
    private uint? LiveAddress()
    {
        if (live.Base() is not { } start) return null;
        if (_base == start && _miscAt is { } known) return start + known + MoneyInMisc;

        _base = start;
        _miscAt = FindMisc(start);
        return _miscAt is { } found ? start + found + MoneyInMisc : null;
    }

    /// <summary>
    /// The Misc block's offset from the save data's start in memory, found by matching the file's block (money left out) in a
    /// short window. Null without a near-perfect, unique match.
    /// </summary>
    private uint? FindMisc(uint start)
    {
        if (LoadFile() is not { } file) return null;
        var block = file.Save.AllBlocks.First(b => b.Offset == MiscFileOffset);
        var print = file.Save.Data.Slice(block.Offset, block.Length).ToArray();

        var window = new byte[SearchLength];
        for (var at = 0; at < SearchLength; at += Chunk)
        {
            if (!client.TryReadMemory(start + SearchFrom + (uint)at, Chunk, out var part)) return null;
            part.CopyTo(window, at);
        }

        var wanted = Enumerable.Range(0, print.Length).Where(i => print[i] != 0 && i is not (>= MoneyInMisc and < MoneyInMisc + 4)).ToArray();
        if (wanted.Length < 32) return null;

        var best = (Offset: -1, Score: 0.0, Second: 0.0);
        for (var pos = 0; pos + print.Length <= window.Length; pos += 4)
        {
            var hits = wanted.Count(i => window[pos + i] == print[i]);
            var score = hits / (double)wanted.Length;
            if (score > best.Score) best = (pos, score, best.Score);
            else if (score > best.Second) best = best with { Second = score };
        }

        if (best.Score < 0.9 || best.Second > 0.6)
        {
            logger.LogWarning("Dinero del juego: el bloque Misc no aparece claro en memoria (mejor {Best:P0}, segundo {Second:P0})", best.Score, best.Second);
            return null;
        }

        var offset = SearchFrom + (uint)best.Offset;
        logger.LogInformation("Dinero del juego: bloque Misc en la partida en memoria +0x{Offset:X5} ({Score:P0} igual que el fichero)", offset, best.Score);
        return offset;
    }

    private (SAV7USUM Save, string Path)? LoadFile()
    {
        if (save.Find() is not { } path || !File.Exists(path)) return null;
        var bytes = File.ReadAllBytes(path);
        return SaveUtil.TryGetSaveFile(bytes, out var parsed) && parsed is SAV7USUM usum ? (usum, path) : null;
    }

    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);
        var name = $"main-{DateTime.Now:yyyyMMdd-HHmmss-fff}-pago.sav";
        File.Copy(path, Path.Combine(backupFolder, name), overwrite: false);
    }
}
