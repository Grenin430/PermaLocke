using PermaLocke.GameLink.Clock;
using Xunit;

namespace PermaLocke.GameLink.Tests;

public sealed class AlolaClockTests
{
    /// <summary>The clock block of the player's own qt-config.ini, as it was on 2026-09-13.</summary>
    private static readonly string[] PlayersConfig =
    [
        "init_clock\\default=true",
        "init_clock=0",
        "init_time\\default=true",
        "init_time=946681277",
        "init_time_offset\\default=true",
        "init_time_offset=0",
        "init_ticks_type\\default=true",
        "init_ticks_type=0"
    ];

    [Fact]
    public void The_players_config_uses_the_computers_time()
    {
        var clock = AzaharClockSettings.Parse(PlayersConfig);

        Assert.True(clock.UsesSystemTime);
        Assert.Equal(0, clock.OffsetSeconds);
    }

    [Fact]
    public void The_default_flag_lines_are_not_read_as_the_setting()
    {
        // «init_clock\default=true» empieza igual que la clave; tomarla por el valor daría hora fija.
        var clock = AzaharClockSettings.Parse(["init_clock\\default=true"]);

        Assert.True(clock.UsesSystemTime);
    }

    [Fact]
    public void Ultra_moon_runs_twelve_hours_apart()
    {
        var now = new DateTime(2026, 9, 13, 21, 48, 0);

        var game = AlolaClock.GameTime(now, AzaharClockSettings.Default);

        Assert.Equal(new DateTime(2026, 9, 14, 9, 48, 0), game);
    }

    [Fact]
    public void A_clock_fixed_at_boot_cannot_be_known()
    {
        var clock = AzaharClockSettings.Parse(["init_clock=1"]);

        Assert.Null(AlolaClock.GameTime(new DateTime(2026, 9, 13, 21, 48, 0), clock));
    }

    [Fact]
    public void An_offset_moves_the_console_clock_in_seconds()
    {
        var clock = AzaharClockSettings.Parse(["init_clock=0", "init_time_offset=3600"]);

        Assert.Equal(new DateTime(2026, 9, 14, 10, 48, 0), AlolaClock.GameTime(new DateTime(2026, 9, 13, 21, 48, 0), clock));
    }

    [Fact]
    public void A_negative_offset_follows_azahars_own_arithmetic()
    {
        // −25 h: Azahar resta el día entero y SUMA la hora que sobra, así que el reloj se mueve −23 h.
        var clock = AzaharClockSettings.Parse(["init_clock=0", "init_time_offset=-90000"]);

        var game = AlolaClock.GameTime(new DateTime(2026, 9, 13, 21, 48, 0), clock, isMoon: false);

        Assert.Equal(new DateTime(2026, 9, 12, 22, 48, 0), game);
    }

    [Theory]
    [InlineData(5, 59, AlolaPeriod.Night)]
    [InlineData(6, 0, AlolaPeriod.Morning)]
    [InlineData(9, 59, AlolaPeriod.Morning)]
    [InlineData(10, 0, AlolaPeriod.Day)]
    [InlineData(16, 59, AlolaPeriod.Day)]
    [InlineData(17, 0, AlolaPeriod.Evening)]
    [InlineData(17, 59, AlolaPeriod.Evening)]
    [InlineData(18, 0, AlolaPeriod.Night)]
    [InlineData(0, 0, AlolaPeriod.Night)]
    public void The_day_is_cut_where_the_seventh_generation_cuts_it(int hour, int minute, AlolaPeriod expected)
    {
        Assert.Equal(expected, AlolaClock.PeriodOf(new TimeOnly(hour, minute)));
    }
}
