using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Services;

/// <summary>One frame of a killcam: when it was taken, relative to the moment the bar reached zero.</summary>
public sealed record KillcamFrame(int Milliseconds, BitmapSource Image);

/// <summary>
/// The killcam file: the last seconds of a battle before a Pokémon died, as JPEG frames in one file.
/// </summary>
/// <remarks>
/// <para>
/// One file per fallen Pokémon, under <c>Saves/killcam/&lt;run&gt;/&lt;pokemon&gt;.killcam</c>. <c>Saves/</c> is
/// outside the repository, and it has to be: these are pictures of the game, and the game is Nintendo's.
/// </para>
/// <para>
/// Frames and not a video because nothing on Windows writes one without shipping an encoder, and JPEG
/// is in WPF already: about a hundred frames of 400×240 at 20 fps, a couple of megabytes. Each frame
/// keeps its time relative to the fall, so the replay can be played at real speed or slowed down, and a
/// frame the recorder missed does not shift everything after it.
/// </para>
/// <para>
/// Layout: <c>PLKC</c>, version, width, height, frame count; then per frame its time in milliseconds,
/// the JPEG length and the JPEG. Little-endian throughout.
/// </para>
/// </remarks>
public static class KillcamClip
{
    private const int Version = 1;
    private static readonly byte[] Magic = "PLKC"u8.ToArray();

    public static string PathFor(string savesFolder, Guid runId, Guid pokemonId) =>
        Path.Combine(savesFolder, "killcam", runId.ToString("N"), $"{pokemonId:N}.killcam");

    /// <summary>Writes the frames, BGRA of the given size, to a temporary file and moves it into place.</summary>
    public static void Write(string path, int width, int height, IReadOnlyList<(int Milliseconds, byte[] Bgra)> frames)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporary = path + ".tmp";

        using (var file = File.Create(temporary))
        using (var writer = new BinaryWriter(file, Encoding.UTF8))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(width);
            writer.Write(height);
            writer.Write(frames.Count);

            foreach (var (milliseconds, bgra) in frames)
            {
                var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
                var encoder = new JpegBitmapEncoder { QualityLevel = 82 };
                encoder.Frames.Add(BitmapFrame.Create(image));

                using var jpeg = new MemoryStream();
                encoder.Save(jpeg);

                writer.Write(milliseconds);
                writer.Write((int)jpeg.Length);
                writer.Write(jpeg.GetBuffer(), 0, (int)jpeg.Length);
            }
        }

        // Escrito entero y después movido: una killcam a medias no es una killcam.
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Reads a clip, or nothing when the file is missing or is not a killcam.</summary>
    public static IReadOnlyList<KillcamFrame> Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var file = File.OpenRead(path);
            using var reader = new BinaryReader(file, Encoding.UTF8);

            if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic) || reader.ReadInt32() != Version)
            {
                return [];
            }

            reader.ReadInt32();
            reader.ReadInt32();

            var count = reader.ReadInt32();
            var frames = new List<KillcamFrame>(count);

            for (var i = 0; i < count; i++)
            {
                var milliseconds = reader.ReadInt32();
                var jpeg = reader.ReadBytes(reader.ReadInt32());

                var decoder = new JpegBitmapDecoder(new MemoryStream(jpeg), BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                frame.Freeze();

                frames.Add(new KillcamFrame(milliseconds, frame));
            }

            return frames;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or NotSupportedException or FileFormatException)
        {
            return [];
        }
    }
}
