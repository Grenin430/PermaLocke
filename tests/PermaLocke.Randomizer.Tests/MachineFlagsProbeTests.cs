using pk3DS.Core.CTR;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The TM compatibility bits, anchored against the real table.
/// </summary>
/// <remarks>
/// Skips itself when the world is not installed. What it pins is that the offset and the length
/// written down are the ones the cartridge actually uses — measured from Mew, who learns nearly
/// every TM, and Ditto, who learns none.
/// </remarks>
public sealed class MachineFlagsProbeTests
{
    private const int Ditto = 132;

    private const int Mew = 151;

    private static byte[]? Packed()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory?.FullName ?? ".", "Expansion", "romfs", "a", "0", "1", "7");

        if (!File.Exists(path))
        {
            return null;
        }

        var garc = new GARC.LazyGARC(File.ReadAllBytes(path));
        return garc[garc.FileCount - 1];
    }

    [Fact]
    public void Mew_learns_a_hundred_and_Ditto_learns_none()
    {
        if (Packed() is not { } packed)
        {
            return;
        }

        Assert.Equal(MachineFlags.Count, MachineFlags.Read(packed, Mew).Count(on => on));
        Assert.Equal(0, MachineFlags.Read(packed, Ditto).Count(on => on));
    }

    /// <summary>
    /// The four bits past the hundredth are zero in every species, which is what fixes the length.
    /// </summary>
    [Fact]
    public void Nothing_lives_in_the_spare_bits()
    {
        if (Packed() is not { } packed)
        {
            return;
        }

        var rows = packed.Length / PersonalEntry7.Size;

        for (var species = 0; species < rows; species++)
        {
            var last = packed[(species * PersonalEntry7.Size) + MachineFlags.Offset + MachineFlags.Length - 1];

            Assert.Equal(0, last >> 4);
        }
    }

    /// <summary>Written and read back, bit for bit.</summary>
    [Fact]
    public void What_is_written_comes_back()
    {
        if (Packed() is not { } packed)
        {
            return;
        }

        var before = packed[(Mew * PersonalEntry7.Size) + MachineFlags.Offset + MachineFlags.Length - 1];
        var flags = MachineFlags.Read(packed, Mew);
        flags[0] = false;
        flags[99] = false;

        MachineFlags.Write(packed, Mew, flags);
        var back = MachineFlags.Read(packed, Mew);

        Assert.Equal(flags, back);
        Assert.Equal(MachineFlags.Count - 2, back.Count(on => on));

        // Y los cuatro bits de arriba, intactos.
        Assert.Equal(before >> 4,
            packed[(Mew * PersonalEntry7.Size) + MachineFlags.Offset + MachineFlags.Length - 1] >> 4);
    }
}
