using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>What the save's play time and the emulator's exit code say about a reload trick (2026-09-26).</summary>
public sealed class IntegrityServiceTests
{
    [Fact]
    public void Play_time_that_grew_or_shrank_outside_PermaLocke_is_named()
    {
        var seen = TimeSpan.FromHours(3);

        Assert.Null(IntegrityService.ComparePlaytime(seen, seen + TimeSpan.FromSeconds(30)));
        Assert.Equal((IntegrityKinds.PlayedOutside, TimeSpan.FromMinutes(20)),
            IntegrityService.ComparePlaytime(seen, seen + TimeSpan.FromMinutes(20)));
        Assert.Equal((IntegrityKinds.Rewound, TimeSpan.FromMinutes(45)),
            IntegrityService.ComparePlaytime(seen, seen - TimeSpan.FromMinutes(45)));
    }

    [Fact]
    public void A_crash_is_never_a_close()
    {
        Assert.NotNull(IntegrityService.HowClosed(0, askedByPermaLocke: false));
        Assert.NotNull(IntegrityService.HowClosed(1, askedByPermaLocke: false));
        Assert.NotNull(IntegrityService.HowClosed(0xC0000005, askedByPermaLocke: true));
        Assert.Null(IntegrityService.HowClosed(0xC0000005, askedByPermaLocke: false));
    }
}
