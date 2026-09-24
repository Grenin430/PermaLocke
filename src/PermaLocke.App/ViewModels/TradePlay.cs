using System.Diagnostics;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// One wonder trade, as the cabin plays it: who goes and in which ball, who comes, what the screen will say, and when it
/// was confirmed.
/// </summary>
/// <remarks>
/// Everything in it was decided and written into the save before it exists. The start is a moment rather than a flag,
/// like the capsule machine's (§171), so the cabin can be drawn from it at any time and the view model never waits on
/// an animation.
/// </remarks>
/// <param name="GivenBall">The ball the given Pokémon lives in, as the save numbers it (4 is the Poké Ball).</param>
/// <param name="GivenBallIcon">The cartridge's icon of that ball, for the balls the scene does not draw itself.</param>
/// <param name="StartedAt">A <see cref="Stopwatch"/> timestamp.</param>
public sealed record TradePlay(
    BitmapSource? GivenSprite,
    int GivenBall,
    BitmapSource? GivenBallIcon,
    BitmapSource? ReceivedSprite,
    int Generation,
    IReadOnlyList<TradeType> Types,
    int GivenTotal,
    int Total,
    int Difference,
    bool Shiny,
    bool Legendary,
    int Seed,
    long StartedAt)
{
    /// <summary>Seconds since the trade was confirmed.</summary>
    public double Elapsed => Stopwatch.GetElapsedTime(StartedAt).TotalSeconds;
}
