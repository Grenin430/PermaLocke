namespace PermaLocke.GameLink.Clock;

/// <summary>The four parts of the day the seventh generation tells apart.</summary>
public enum AlolaPeriod
{
    Morning,
    Day,
    Evening,
    Night
}

/// <summary>
/// How Azahar sets the console's clock, as its <c>qt-config.ini</c> says.
/// </summary>
/// <param name="UsesSystemTime">
/// <c>init_clock=0</c>: the console's clock is the computer's local time. The other mode starts it at a fixed
/// moment when the game boots, and from outside there is no telling how long ago that was.
/// </param>
/// <param name="OffsetSeconds"><c>init_time_offset</c>, added on top of the computer's time.</param>
public sealed record AzaharClockSettings(bool UsesSystemTime, long OffsetSeconds)
{
    /// <summary>Azahar's own defaults, which is what an emulator with no config file uses.</summary>
    public static AzaharClockSettings Default { get; } = new(UsesSystemTime: true, OffsetSeconds: 0);

    /// <summary>Reads the two settings from the lines of <c>qt-config.ini</c>; what is missing keeps its default.</summary>
    public static AzaharClockSettings Parse(IEnumerable<string> lines)
    {
        var settings = Default;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.StartsWith("init_clock=", StringComparison.Ordinal))
            {
                settings = settings with { UsesSystemTime = line["init_clock=".Length..].Trim() == "0" };
            }
            else if (line.StartsWith("init_time_offset=", StringComparison.Ordinal)
                     && long.TryParse(line["init_time_offset=".Length..].Trim(), out var offset))
            {
                settings = settings with { OffsetSeconds = offset };
            }
        }

        return settings;
    }

    /// <summary>The file's settings, or the defaults when there is no file to read.</summary>
    public static AzaharClockSettings Read(string configPath)
    {
        try
        {
            return File.Exists(configPath) ? Parse(File.ReadAllLines(configPath)) : Default;
        }
        catch (IOException)
        {
            // Azahar escribe su configuración al cerrar; si justo coincide, se vuelve a mirar en la siguiente vuelta.
            return Default;
        }
    }
}

/// <summary>
/// What time it is in the player's Alola.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is read from the game's memory — after two freezes caused by searching it (§114 ter), nothing is
/// asked of it that can be worked out from outside. Two facts do it:
/// </para>
/// <list type="bullet">
/// <item>With <c>init_clock</c> on system time, Azahar starts the console's clock at the computer's
/// <b>local</b> time. Read in its source (<c>core/hle/kernel/shared_page.cpp</c>): it takes the UTC clock,
/// adds an hour when daylight saving is on, and subtracts an epoch built with <c>mktime</c>, which is local.
/// Then it adds <c>init_time_offset</c>, in seconds.</item>
/// <item>Ultra Moon, like Moon, runs twelve hours apart from the console's clock. Every game PermaLocke
/// attaches to is Ultra Moon (title id <c>00040000001B5100</c>), and the player's save says so
/// (<c>SAV7USUM.Version = UM</c>).</item>
/// </list>
/// <para>
/// The console's clock then moves with emulated time, so pausing the emulator leaves the game a little
/// behind the computer. With the clock fixed at boot the answer is null, not a guess: there is no telling from
/// outside how long ago the game started.
/// </para>
/// </remarks>
public static class AlolaClock
{
    /// <summary>How far Ultra Moon's day is from the console's.</summary>
    public static readonly TimeSpan MoonShift = TimeSpan.FromHours(12);

    /// <summary>The time in the game, or null when it cannot be known.</summary>
    public static DateTime? GameTime(DateTime localNow, AzaharClockSettings clock, bool isMoon = true)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (!clock.UsesSystemTime)
        {
            return null;
        }

        var console = localNow + AzaharOffset(clock.OffsetSeconds);
        return isMoon ? console + MoonShift : console;
    }

    /// <summary>
    /// The offset exactly as Azahar applies it: whole days with their sign, and the rest of the seconds always
    /// added. A negative offset that is not a whole number of days therefore moves the clock less than it says —
    /// that is the emulator's arithmetic, copied so that the sky agrees with the game and not with the setting.
    /// </summary>
    private static TimeSpan AzaharOffset(long offset)
    {
        var days = offset / 86400 * 86400;
        var rest = Math.Abs(offset) - Math.Abs(days);
        return TimeSpan.FromSeconds(days + rest);
    }

    /// <summary>
    /// The part of the day, by the seventh generation's bands: morning 6:00–9:59, day 10:00–16:59, evening
    /// 17:00–17:59 and night 18:00–5:59.
    /// </summary>
    public static AlolaPeriod PeriodOf(TimeOnly time) => time.Hour switch
    {
        >= 6 and < 10 => AlolaPeriod.Morning,
        >= 10 and < 17 => AlolaPeriod.Day,
        17 => AlolaPeriod.Evening,
        _ => AlolaPeriod.Night
    };
}
