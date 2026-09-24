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
    /// <para>
    /// The bytes come back <b>as stored</b>. <c>LazyGARC</c> quietly decompresses an LZ11 subfile
    /// (first byte 0x11) and this does not, so on a compressed container such as the encounter data
    /// <c>a/0/8/3</c> every subfile reads as noise and nothing throws. Measured on 2026-09-21: an audit
    /// of the wild tables read through here found zero encounter slots. Every caller today reads
    /// uncompressed containers (species, statics, text, moves, learnsets); a new one on another
    /// container has to check first, or use <c>LazyGARC</c>.
    /// </para>
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

    /// <summary>How many subfiles a container has, without opening it for writing.</summary>
    public static int CountReadOnly(string path) => GARC.UnpackGARC(path).fato.EntryCount;

    /// <summary>
    /// Every subfile, read once and without write access. A subfile that does not exist comes back empty.
    /// </summary>
    /// <remarks>
    /// <see cref="ReadOnly"/> parses the whole header for each subfile it is asked for, which is fine for one and
    /// quadratic for the 1300 learnsets of the expansion. Same caveat as <see cref="ReadOnly"/>: a compressed
    /// subfile comes back compressed.
    /// </remarks>
    public static byte[][] ReadAllReadOnly(string path)
    {
        var garc = GARC.UnpackGARC(path);
        var files = new byte[garc.fato.EntryCount][];

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        for (var index = 0; index < files.Length; index++)
        {
            var sub = garc.fatb.Entries[index].SubEntries[0];

            if (!sub.Exists)
            {
                files[index] = [];
                continue;
            }

            files[index] = new byte[sub.End - sub.Start];
            stream.Position = garc.DataOffset + sub.Start;
            stream.ReadExactly(files[index], 0, files[index].Length);
        }

        return files;
    }

    public void Dispose() => _stream.Dispose();
}
