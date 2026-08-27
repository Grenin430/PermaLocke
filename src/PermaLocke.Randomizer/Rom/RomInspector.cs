using System.Text;
using PermaLocke.Core.Domain;

namespace PermaLocke.Randomizer.Rom;

/// <param name="Game">Null when the cartridge is not a game PermaLocke supports.</param>
/// <param name="IsDecrypted">Azahar and pk3DS both need a decrypted dump.</param>
public sealed record RomInfo(
    string Path,
    string FileName,
    long SizeBytes,
    string TitleId,
    string ProductCode,
    bool IsDecrypted,
    GameVersion? Game)
{
    public bool IsSupported => Game is not null && IsDecrypted;
}

/// <summary>
/// Reads the NCSD/NCCH headers of a 3DS cartridge dump. Opens the file read-only and never
/// writes to it: the vanilla ROM is untouchable.
/// </summary>
/// <remarks>
/// Header layout per the 3DS file format documentation: the NCSD header sits at 0x100 and the
/// first partition's NCCH header at 0x4000.
/// </remarks>
public static class RomInspector
{
    private const long NcsdHeaderOffset = 0x100;
    private const long NcchHeaderOffset = 0x4000;

    /// <summary>Bit 2 of the last NCCH flag byte means "no encryption".</summary>
    private const byte NoCryptoFlag = 0x04;

    public static RomInfo? TryInspect(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < NcchHeaderOffset + 0x200)
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            var ncsd = ReadAt(stream, NcsdHeaderOffset, 0x20);
            if (Encoding.ASCII.GetString(ncsd, 0, 4) != "NCSD")
            {
                return null;
            }

            var titleId = BitConverter.ToUInt64(ncsd, 8).ToString("X16");

            var ncch = ReadAt(stream, NcchHeaderOffset, 0x200);
            if (Encoding.ASCII.GetString(ncch, 0x100, 4) != "NCCH")
            {
                return null;
            }

            var productCode = Encoding.ASCII.GetString(ncch, 0x150, 16).TrimEnd('\0');
            var isDecrypted = (ncch[0x18F] & NoCryptoFlag) != 0;

            return new RomInfo(path, file.Name, file.Length, titleId, productCode, isDecrypted,
                IdentifyGame(productCode));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Returns every recognisable cartridge dump found in a folder.</summary>
    public static IReadOnlyList<RomInfo> ScanFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return
        [
            .. Directory.EnumerateFiles(folder, "*.3ds", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(folder, "*.cci", SearchOption.TopDirectoryOnly))
                .Select(TryInspect)
                .OfType<RomInfo>()
        ];
    }

    /// <summary>
    /// Only Ultra Moon is recognised, and only from a product code verified against a real dump
    /// (CTR-P-A2BA, title id 00040000001B5100). Anything else returns null rather than a guess.
    /// </summary>
    private static GameVersion? IdentifyGame(string productCode) =>
        productCode.StartsWith("CTR-P-A2B", StringComparison.OrdinalIgnoreCase)
            ? GameVersion.UltraMoon
            : null;

    private static byte[] ReadAt(Stream stream, long offset, int count)
    {
        var buffer = new byte[count];
        stream.Position = offset;
        stream.ReadExactly(buffer, 0, count);
        return buffer;
    }
}
