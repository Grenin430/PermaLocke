using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// One pull, as the capsule machine plays it: the balls it climbs through, who comes out, and when the coin dropped.
/// </summary>
/// <remarks>
/// Everything in it was decided and written before it exists. The start is a moment rather than a flag so that the
/// machine can be drawn from it at any time — a gacha screen opened again mid-pull finds the ball where it should be.
/// </remarks>
/// <param name="Steps">Tier indexes the ball shows, from the cheapest the banner holds to the one that came out.</param>
/// <param name="StartedAt">A <see cref="Stopwatch"/> timestamp.</param>
/// <param name="Speed">
/// How fast the machine runs up to the open: 1, or <see cref="Views.CapsuleTimeline.ExpressSpeed"/> for a pull in a row
/// (§189). Chosen by the streak, never by what came out.
/// </param>
/// <param name="Streak">How many pulls in a row this one is, for the room to show.</param>
public sealed record CapsulePlay(IReadOnlyList<int> Steps, BitmapSource? Sprite, bool Shiny, bool Legendary, int Seed,
    long StartedAt, double Speed = 1, int Streak = 1)
{
    /// <summary>Real seconds SALTAR has moved this pull forward.</summary>
    private double _skipped;

    /// <summary>
    /// Where the pull is, in its own seconds since the coin dropped: <see cref="Views.CapsuleTimeline"/> moments, sped
    /// up before the open for a pull in a row and moved forward by SALTAR.
    /// </summary>
    public double Elapsed => Views.CapsuleTimeline.Warp(Stopwatch.GetElapsedTime(StartedAt).TotalSeconds + _skipped, Speed);

    /// <summary>
    /// SALTAR: on to the ball about to open, or, once it is opening, to the Pokémon out. The pull was decided before the
    /// coin dropped, so skipping only moves the clock everyone draws from.
    /// </summary>
    /// <returns>False when there was nothing left to skip.</returns>
    public bool Skip()
    {
        var now = Elapsed;
        var target = Views.CapsuleTimeline.SkipTarget(now);
        if (target <= now)
        {
            return false;
        }

        _skipped += Views.CapsuleTimeline.RealFor(target, Speed) - Views.CapsuleTimeline.RealFor(now, Speed);
        return true;
    }

    /// <summary>
    /// The balls a pull climbs through: it drops as the cheapest ball on the banner and climbs at most twice.
    /// </summary>
    /// <remarks>
    /// The same tease the old reel had — start at the bottom and step up — told with Poké Balls. Starting from the
    /// banner's own cheapest tier and not from Tier 1 matters: a BUENO machine has no Poké Balls in its dome, so a
    /// Poké Ball dropping out of it would be a ball that is not there.
    /// </remarks>
    public static IReadOnlyList<int> StepsFor(int lowest, int final)
    {
        lowest = Math.Min(lowest, final);

        return (final - lowest) switch
        {
            0 => [final],
            1 => [lowest, final],
            _ => [lowest, (lowest + final) / 2, final],
        };
    }
}
