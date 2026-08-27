using System.Text;

namespace PermaLocke.Randomizer.Rom;

/// <summary>One file inside the cartridge's RomFS, addressed absolutely within the .3ds.</summary>
public sealed record RomFsEntry(string Path, long Offset, long Size);

/// <summary>
/// Reads the RomFS directory of a decrypted 3DS cartridge dump without extracting anything.
/// Opens the file read-only; the vanilla ROM is never modified.
/// </summary>
public sealed class RomFsReader
{
    private const long NcsdHeaderOffset = 0x100;
    private const int MediaUnit = 0x200;

    private readonly string _path;
    private readonly Dictionary<string, RomFsEntry> _files = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, RomFsEntry> Files => _files;
    public long PartitionOffset { get; private set; }
    public long RomFsOffset { get; private set; }
    public long RomFsSize { get; private set; }

    public RomFsReader(string path)
    {
        _path = path;
        using var s = File.OpenRead(path);

        var ncsd = Read(s, NcsdHeaderOffset, 0x40);
        if (Encoding.ASCII.GetString(ncsd, 0, 4) != "NCSD")
            throw new InvalidDataException("Not an NCSD cartridge dump.");

        // Partition table lives at 0x120: eight pairs of (offset, length) in media units.
        PartitionOffset = (long)BitConverter.ToUInt32(ncsd, 0x120 - 0x100) * MediaUnit;

        var ncch = Read(s, PartitionOffset, 0x200);
        if (Encoding.ASCII.GetString(ncch, 0x100, 4) != "NCCH")
            throw new InvalidDataException("Partition 0 is not an NCCH.");

        RomFsOffset = PartitionOffset + (long)BitConverter.ToUInt32(ncch, 0x1B0) * MediaUnit;
        RomFsSize = (long)BitConverter.ToUInt32(ncch, 0x1B4) * MediaUnit;

        Index(s);
    }

    private void Index(FileStream s)
    {
        var ivfc = Read(s, RomFsOffset, 0x5C);
        if (Encoding.ASCII.GetString(ivfc, 0, 4) != "IVFC")
            throw new InvalidDataException("RomFS does not start with an IVFC header.");

        var masterHashSize = BitConverter.ToUInt32(ivfc, 0x08);
        var level0HashOffset = BitConverter.ToUInt64(ivfc, 0x0C);
        var level2BlockSize = 1UL << BitConverter.ToInt32(ivfc, 0x0C + (2 * 0x18) + 0x10);

        var bodyOffset = RomFsOffset + (long)Align(level0HashOffset + masterHashSize, level2BlockSize);

        // Level 3 header: four sections (dir hash, dir table, file hash, file table) then the data.
        var info = Read(s, bodyOffset, 0x28);
        var dirTableOffset = bodyOffset + BitConverter.ToUInt32(info, 0x0C);
        var dirTableSize = BitConverter.ToUInt32(info, 0x10);
        var fileTableOffset = bodyOffset + BitConverter.ToUInt32(info, 0x1C);
        var fileTableSize = BitConverter.ToUInt32(info, 0x20);
        var dataOffset = bodyOffset + BitConverter.ToUInt32(info, 0x24);

        var dirs = Read(s, dirTableOffset, (int)dirTableSize);
        var files = Read(s, fileTableOffset, (int)fileTableSize);

        WalkDirectory(dirs, files, 0, "", dataOffset);
    }

    private void WalkDirectory(byte[] dirs, byte[] files, uint dirOffset, string parentPath, long dataOffset)
    {
        while (dirOffset != 0xFFFFFFFF)
        {
            var o = (int)dirOffset;
            var siblingOffset = BitConverter.ToUInt32(dirs, o + 0x04);
            var childOffset = BitConverter.ToUInt32(dirs, o + 0x08);
            var fileOffset = BitConverter.ToUInt32(dirs, o + 0x0C);
            var nameLength = BitConverter.ToUInt32(dirs, o + 0x14);

            var name = nameLength is 0 or 0xFFFFFFFF
                ? ""
                : Encoding.Unicode.GetString(dirs, o + 0x18, (int)nameLength);
            var path = name.Length == 0 ? parentPath : Combine(parentPath, name);

            if (fileOffset != 0xFFFFFFFF)
                WalkFiles(files, fileOffset, path, dataOffset);

            if (childOffset != 0xFFFFFFFF)
                WalkDirectory(dirs, files, childOffset, path, dataOffset);

            dirOffset = siblingOffset;
        }
    }

    private void WalkFiles(byte[] files, uint fileOffset, string dirPath, long dataOffset)
    {
        while (fileOffset != 0xFFFFFFFF)
        {
            var o = (int)fileOffset;
            var siblingOffset = BitConverter.ToUInt32(files, o + 0x04);
            var offset = BitConverter.ToUInt64(files, o + 0x08);
            var size = BitConverter.ToUInt64(files, o + 0x10);
            var nameLength = BitConverter.ToUInt32(files, o + 0x1C);

            var name = nameLength is 0 or 0xFFFFFFFF
                ? ""
                : Encoding.Unicode.GetString(files, o + 0x20, (int)nameLength);
            var path = Combine(dirPath, name);
            _files[path] = new RomFsEntry(path, dataOffset + (long)offset, (long)size);

            fileOffset = siblingOffset;
        }
    }

    /// <summary>Copies one RomFS file out to disk. Returns false when the ROM has no such file.</summary>
    public bool ExtractTo(string romFsPath, string destination)
    {
        if (!_files.TryGetValue(romFsPath, out var entry))
            return false;

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var src = File.OpenRead(_path);
        using var dst = File.Create(destination);
        src.Position = entry.Offset;
        var remaining = entry.Size;
        var buffer = new byte[0x10000];
        while (remaining > 0)
        {
            var chunk = (int)Math.Min(buffer.Length, remaining);
            src.ReadExactly(buffer, 0, chunk);
            dst.Write(buffer, 0, chunk);
            remaining -= chunk;
        }
        return true;
    }

    private static string Combine(string a, string b) => a.Length == 0 ? b : a + "/" + b;

    private static ulong Align(ulong value, ulong alignment) =>
        value % alignment == 0 ? value : value + alignment - (value % alignment);

    private static byte[] Read(FileStream s, long offset, int count)
    {
        var buffer = new byte[count];
        s.Position = offset;
        s.ReadExactly(buffer, 0, count);
        return buffer;
    }
}
