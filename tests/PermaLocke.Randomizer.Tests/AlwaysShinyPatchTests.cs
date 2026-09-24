using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Randomizer.Tests;

/// <summary>The test switch that makes everything shiny: where it goes and the IPS file that puts it there.</summary>
public sealed class AlwaysShinyPatchTests
{
    /// <summary>
    /// The 48 bytes around the site in the player's installed code.bin, starting at 0x2205B0. The branch just before
    /// it (AND #1) is a different check that the pattern must not take.
    /// </summary>
    private static readonly byte[] Measured = Convert.FromHexString(
        "013021E2032092E11800000A0020E0E3" +
        "002022E0023021E2032092E11C00000A" +
        "0020E0E3020020E0031021E2010090E1");

    [Fact]
    public void The_condition_byte_is_the_one_after_the_pattern()
    {
        var at = AlwaysShinyPatch.Find(Measured);

        Assert.Equal(0x1F, at);
        Assert.Equal(AlwaysShinyPatch.Original, Measured[at!.Value]);
    }

    [Fact]
    public void Two_matches_are_not_a_site()
    {
        var twice = Measured.Concat(Measured).ToArray();

        Assert.Null(AlwaysShinyPatch.Find(twice));
    }

    [Fact]
    public void No_match_is_not_a_site()
    {
        Assert.Null(AlwaysShinyPatch.Find(new byte[64]));
    }

    [Fact]
    public void The_ips_file_writes_one_byte_at_the_offset()
    {
        var ips = AlwaysShinyPatch.Ips(0x2205CF);

        Assert.Equal("PATCH"u8.ToArray(), ips[..5]);
        Assert.Equal(new byte[] { 0x22, 0x05, 0xCF, 0x00, 0x01, 0xEA }, ips[5..11]);
        Assert.Equal("EOF"u8.ToArray(), ips[11..]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0x1000000)]
    [InlineData(0x454F46)]
    public void Offsets_an_ips_file_cannot_hold_are_refused(int offset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AlwaysShinyPatch.Ips(offset));
    }
}
