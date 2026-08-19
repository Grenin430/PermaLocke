using PermaLocke.Core.Domain;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Runs against the real ROM in the repository's ROM/ folder. Skipped, not failed, when the
/// ROM is absent: it is a 4 GB file that is deliberately not in source control.
/// </summary>
public sealed class RomInspectorTests
{
    private static string? FindRomFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ROM");
            if (File.Exists(Path.Combine(dir.FullName, "PermaLocke.slnx")) && Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    [Fact]
    public void The_local_rom_is_recognised_as_a_decrypted_ultra_moon_dump()
    {
        var folder = FindRomFolder();
        if (folder is null)
        {
            return;
        }

        var roms = RomInspector.ScanFolder(folder);
        if (roms.Count == 0)
        {
            // xUnit 2 has no runtime skip; a machine without the 4 GB ROM simply has
            // nothing to check here. The two tests below still cover the rejection paths.
            return;
        }

        var rom = roms[0];

        Assert.Equal(GameVersion.UltraMoon, rom.Game);
        Assert.True(rom.IsDecrypted, "La ROM debe estar desencriptada para Azahar y pk3DS.");
        Assert.Equal("00040000001B5100", rom.TitleId);
        Assert.StartsWith("CTR-P-A2B", rom.ProductCode);
        Assert.True(rom.IsSupported);
    }

    [Fact]
    public void A_file_that_is_not_a_cartridge_dump_is_rejected()
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(temp, new byte[0x5000]);
            Assert.Null(RomInspector.TryInspect(temp));
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void A_missing_file_is_rejected_without_throwing()
    {
        Assert.Null(RomInspector.TryInspect(Path.Combine(Path.GetTempPath(), "no-existe.3ds")));
    }
}
