using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.Services;

/// <summary>
/// A killcam as a small H.264 video (1.0.5.5), for sharing it through the tournament server: about 250 KB instead of the
/// 1.7 MB of the local file, and it looks the same.
/// </summary>
/// <remarks>
/// <para>
/// Measured on the real killcams before choosing: JPEG at lower quality stayed near 450 KB, and storing only what changes
/// between frames grew, because the battle camera never stops moving. A video codec is what fits, and Windows has one
/// (Media Foundation, <see cref="Mf"/>), so nothing is shipped.
/// </para>
/// <para>
/// Layout: <c>PLKV</c>, version, the time of the first frame relative to the fall in milliseconds, then the MP4. The
/// video's own sample times carry the rest. Decoding writes a local <see cref="KillcamClip"/>, so the player that shows
/// this player's killcams shows the friends' ones unchanged.
/// </para>
/// </remarks>
public static class KillcamVideo
{
    private const int Version = 1;
    private const int Bitrate = 250_000;
    private static readonly byte[] Magic = "PLKV"u8.ToArray();
    private static readonly Lazy<bool> Started = new(() => { Mf.MFStartup(Mf.Version, 0); return true; });

    /// <summary>The local killcam as a shareable video, or null when it cannot be read or encoded.</summary>
    public static byte[]? Encode(string killcamPath)
    {
        var frames = KillcamClip.Read(killcamPath);
        if (frames.Count == 0) return null;

        _ = Started.Value;
        var width = frames[0].Image.PixelWidth;
        var height = frames[0].Image.PixelHeight;
        var first = frames[0].Milliseconds;
        var mp4 = Path.Combine(Path.GetTempPath(), $"permalocke-{Guid.NewGuid():N}.mp4");

        try
        {
            Mf.MFCreateSinkWriterFromURL(mp4, IntPtr.Zero, null, out var writer);

            try
            {
                var output = Mf.VideoType(Mf.H264, width, height, KillcamRecorder.FramesPerSecond);
                output.SetUINT32(Mf.AvgBitrate, Bitrate);
                writer.AddStream(output, out var stream);

                var input = Mf.VideoType(Mf.Rgb32, width, height, KillcamRecorder.FramesPerSecond);
                input.SetUINT32(Mf.DefaultStride, width * 4);
                writer.SetInputMediaType(stream, input, null);
                writer.BeginWriting();

                var pixels = new byte[width * height * 4];
                foreach (var frame in frames)
                {
                    new FormatConvertedBitmap(frame.Image, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, width * 4, 0);

                    Mf.MFCreateMemoryBuffer(pixels.Length, out var buffer);
                    buffer.Lock(out var target, out _, out _);
                    Marshal.Copy(pixels, 0, target, pixels.Length);
                    buffer.Unlock();
                    buffer.SetCurrentLength(pixels.Length);

                    Mf.MFCreateSample(out var sample);
                    sample.AddBuffer(buffer);
                    sample.SetSampleTime((frame.Milliseconds - first) * 10_000L);
                    sample.SetSampleDuration(10_000_000L / KillcamRecorder.FramesPerSecond);
                    writer.WriteSample(stream, sample);
                }

                writer.Finalize_();
            }
            finally
            {
                Marshal.ReleaseComObject(writer);
            }

            using var packed = new MemoryStream();
            using (var header = new BinaryWriter(packed, Encoding.UTF8, leaveOpen: true))
            {
                header.Write(Magic);
                header.Write(Version);
                header.Write(first);
            }

            packed.Write(File.ReadAllBytes(mp4));
            return packed.ToArray();
        }
        catch (Exception ex) when (ex is COMException or DllNotFoundException or EntryPointNotFoundException or IOException)
        {
            return null;
        }
        finally
        {
            File.Delete(mp4);
        }
    }

    /// <summary>Turns a shared video back into a local killcam at <paramref name="killcamPath"/>. False when it is not one.</summary>
    public static bool Decode(byte[] video, string killcamPath)
    {
        if (video.Length < 12 || !video.AsSpan(0, 4).SequenceEqual(Magic) || BitConverter.ToInt32(video, 4) != Version)
        {
            return false;
        }

        _ = Started.Value;
        var first = BitConverter.ToInt32(video, 8);
        var mp4 = Path.Combine(Path.GetTempPath(), $"permalocke-{Guid.NewGuid():N}.mp4");

        try
        {
            File.WriteAllBytes(mp4, video[12..]);

            Mf.MFCreateAttributes(out var attributes, 1);
            attributes.SetUINT32(Mf.EnableVideoProcessing, 1);
            Mf.MFCreateSourceReaderFromURL(mp4, attributes, out var reader);

            try
            {
                var frames = new List<(int, byte[])>();
                const int Width = KillcamRecorder.Width, Height = KillcamRecorder.Height;
                reader.SetCurrentMediaType(Mf.FirstVideoStream, IntPtr.Zero, Mf.VideoType(Mf.Rgb32, Width, Height, KillcamRecorder.FramesPerSecond));

                while (true)
                {
                    reader.ReadSample(Mf.FirstVideoStream, 0, out _, out var flags, out var time, out var sample);
                    if ((flags & Mf.EndOfStream) != 0) break;
                    if (sample is null) continue;

                    sample.ConvertToContiguousBuffer(out var buffer);
                    buffer.Lock(out var source, out _, out var length);
                    var pixels = new byte[Width * Height * 4];
                    Marshal.Copy(source, pixels, 0, Math.Min(length, pixels.Length));
                    buffer.Unlock();

                    // RGB32 llega sin alfa: se pone opaco, que el reproductor pinta BGRA.
                    for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
                    frames.Add(((int)(time / 10_000) + first, pixels));
                }

                if (frames.Count == 0) return false;
                KillcamClip.Write(killcamPath, Width, Height, frames);
                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(reader);
            }
        }
        catch (Exception ex) when (ex is COMException or DllNotFoundException or EntryPointNotFoundException or IOException)
        {
            return false;
        }
        finally
        {
            File.Delete(mp4);
        }
    }
}
