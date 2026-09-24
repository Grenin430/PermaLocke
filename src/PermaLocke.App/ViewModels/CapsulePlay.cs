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
public sealed record CapsulePlay(IReadOnlyList<int> Steps, BitmapSource? Sprite, bool Shiny, bool Legendary, int Seed,
    long StartedAt)
{
    /// <summary>Seconds since the coin dropped.</summary>
    public double Elapsed => Stopwatch.GetElapsedTime(StartedAt).TotalSeconds;

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
