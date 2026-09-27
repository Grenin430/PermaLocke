using System.IO;
using System.Text.Json;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Tests;

/// <summary>The shared killcam (1.0.5.5): a local clip goes through the video and comes back a clip, timed the same.</summary>
public sealed class KillcamVideoTests
{
    [Fact]
    public void A_killcam_survives_the_video_round_trip()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"permalocke-kv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            const int W = KillcamRecorder.Width, H = KillcamRecorder.Height;
            var frames = new List<(int, byte[])>();
            for (var i = 0; i < 40; i++)
            {
                var bgra = new byte[W * H * 4];
                for (var p = 0; p < W * H; p++)
                {
                    // Un bloque que se mueve sobre un fondo: algo que el vídeo tiene que seguir.
                    var x = p % W;
                    var inside = x >= i * 5 && x < (i * 5) + 60;
                    bgra[(p * 4) + 0] = (byte)(inside ? 30 : 200);
                    bgra[(p * 4) + 1] = (byte)(inside ? 30 : 120);
                    bgra[(p * 4) + 2] = (byte)(inside ? 220 : 40);
                    bgra[(p * 4) + 3] = 255;
                }

                frames.Add(((i * 50) - 1500, bgra));
            }

            var original = Path.Combine(folder, "a.killcam");
            KillcamClip.Write(original, W, H, frames);

            var video = KillcamVideo.Encode(original);
            Assert.NotNull(video);
            Assert.True(video!.Length < new FileInfo(original).Length);

            var back = Path.Combine(folder, "b.killcam");
            Assert.True(KillcamVideo.Decode(video, back));

            var clip = KillcamClip.Read(back);
            Assert.InRange(clip.Count, 38, 40);
            Assert.Equal(-1500, clip[0].Milliseconds);
            Assert.Equal(W, clip[0].Image.PixelWidth);
            Assert.False(KillcamVideo.Decode([1, 2, 3], back));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_grave_goes_to_the_server_and_back_whole()
    {
        var grave = new Grave(Guid.NewGuid(), "Pepe", "Rattata", 19, true, PokemonOrigin.Capture, EncounterType.Wild,
            DateTimeOffset.Now.AddDays(-2), DateTimeOffset.Now, "Tauros", ["Tauros"], "En el combate, en el momento", true, 50,
            null, 1);

        var back = JsonSerializer.Deserialize<Grave>(JsonSerializer.Serialize(grave),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(grave with { BattleAgainst = back.BattleAgainst }, back);
        Assert.Equal(["Tauros"], back.BattleAgainst);
    }
}
