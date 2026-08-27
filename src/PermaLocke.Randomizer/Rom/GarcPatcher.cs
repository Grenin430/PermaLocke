using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Rom;

/// <summary>
/// Overwrites subfiles inside a GARC without repacking the container.
/// Only valid when the replacement is exactly as long as the original, which is the case for
/// every fixed-size game table. Repacking with pk3DS's PackGARC rewrites headers and padding,
/// and for encdata that produced a file Ultra Moon would not read.
/// </summary>
public sealed class GarcPatcher : IDisposable
{
    private readonly FileStream _stream;
    private readonly GARC.GARCFile _garc;

    public GarcPatcher(string path)
    {
        _garc = GARC.UnpackGARC(path);
        _stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
    }

    public int FileCount => _garc.fato.EntryCount;

    private (long Offset, int Length) Locate(int index)
    {
        var sub = _garc.fatb.Entries[index].SubEntries[0];
        if (!sub.Exists)
            throw new ArgumentException($"subfile {index} does not exist");
        return (_garc.DataOffset + sub.Start, sub.End - sub.Start);
    }

    public byte[] Read(int index)
    {
        var (offset, length) = Locate(index);
        var buffer = new byte[length];
        _stream.Position = offset;
        _stream.ReadExactly(buffer, 0, length);
        return buffer;
    }

    public void Write(int index, byte[] data)
    {
        var (offset, length) = Locate(index);
        if (data.Length != length)
            throw new ArgumentException($"subfile {index}: replacement is {data.Length} bytes, slot holds {length}");
        _stream.Position = offset;
        _stream.Write(data, 0, data.Length);
    }

    /// <summary>
    /// Reads one subfile without ever opening the container for writing.
    /// </summary>
    /// <remarks>
    /// For looking at a mod that is already installed, which is a folder the emulator owns. The
    /// normal constructor takes a read/write handle because patching needs one, and asking for
    /// write access to something you only intend to read is how a reader ends up truncating a
    /// file it was never meant to touch.
    /// </remarks>
    public static byte[] ReadOnly(string path, int index)
    {
        var garc = GARC.UnpackGARC(path);
        var sub = garc.fatb.Entries[index].SubEntries[0];

        if (!sub.Exists)
        {
            throw new ArgumentException($"subfile {index} does not exist");
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[sub.End - sub.Start];
        stream.Position = garc.DataOffset + sub.Start;
        stream.ReadExactly(buffer, 0, buffer.Length);
        return buffer;
    }

    public void Dispose() => _stream.Dispose();
}
