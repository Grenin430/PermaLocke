using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;

namespace PermaLocke.App.Tests;

/// <summary>The cap panel as a trophy plaque (§239): its size follows what it has to say, and it draws every case.</summary>
public sealed class CapPlaqueTests
{
    /// <summary>A stand-in for a cartridge icon: 40 by 30, transparent around a coloured blob.</summary>
    private static BitmapSource Icon(byte tint)
    {
        var pixels = new byte[40 * 30 * 4];
        for (var y = 0; y < 30; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                var i = ((y * 40) + x) * 4;
                if (Math.Pow((x - 20) / 12.0, 2) + Math.Pow((y - 16) / 11.0, 2) > 1) continue;
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = ((byte)(tint / 2), (byte)(255 - tint), tint, 255);
            }
        }

        var bitmap = BitmapSource.Create(40, 30, 96, 96, PixelFormats.Bgra32, null, pixels, 40 * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static readonly CapPlaqueMember[] Party =
    [
        new(Icon(200), "Chispas", 22, 48, 52, false),
        new(Icon(60), "Charizard", 21, 20, 55, false),
        new(Icon(120), "Gengar", 14, 0, 40, false),
        new(Icon(30), "Lapras", 24, 9, 90, false),
        new(Icon(240), "Heracross", 18, 60, 60, false),
        new(Icon(90), "Togepi", 1, 10, 10, true)
    ];

    [Fact]
    public void The_plaque_grows_with_the_party_and_with_the_reading_and_draws_every_case()
    {
        var full = new CapPlaqueData("PRUEBA 3 DE 12", 24, 26, 22, Party);
        var bare = full with { Highest = null, Party = [] };
        var league = full with { Trial = "CAMPEONATO DE LA LIGA", Cap = 100, Next = null };

        Assert.Equal(CapPlaque.Rows(bare) + 30 + (6 * 21), CapPlaque.Rows(full));

        foreach (var data in new[] { full, bare, league })
        {
            var sheet = CapPlaque.Paint(data);
            Assert.Equal(CapPlaque.Columns, sheet.W);
            Assert.Equal(CapPlaque.Rows(data), sheet.H);
            Assert.Contains(sheet.B.Where((_, i) => i % 4 == 3), alpha => alpha == 255);

            // La punta del escudo: la última fila solo tiene su centro.
            var last = (sheet.H - 1) * sheet.W * 4;
            Assert.Equal(0, sheet.B[last + 3]);
        }

        if (Environment.GetEnvironmentVariable("PERMALOCKE_SNAP_DIR") is not { Length: > 0 } dir) return;
        foreach (var (data, name) in new[] { (full, "CapPlaque"), (league, "CapPlaque-liga") })
        {
            var sheet = CapPlaque.Paint(data);
            const int scale = 3;
            var big = new byte[sheet.W * scale * sheet.H * scale * 4];
            for (var y = 0; y < sheet.H * scale; y++)
            {
                for (var x = 0; x < sheet.W * scale; x++)
                {
                    Buffer.BlockCopy(sheet.B, (((y / scale) * sheet.W) + (x / scale)) * 4, big, ((y * sheet.W * scale) + x) * 4, 4);
                }
            }

            var bitmap = BitmapSource.Create(sheet.W * scale, sheet.H * scale, 96, 96, PixelFormats.Bgra32, null, big, sheet.W * scale * 4);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(dir, name + ".png"));
            png.Save(file);
        }
    }
}
