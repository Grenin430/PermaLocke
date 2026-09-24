using System.Diagnostics;
using System.Windows.Media.Imaging;
using PermaLocke.App.Views;

namespace PermaLocke.App.ViewModels;

/// <summary>One of the six wedges of a spin, with its picture out of the cartridge.</summary>
public sealed record RoulettePlayWedge(string Label, string Figure, bool Good, BitmapSource? Icon, string FaceId);

/// <summary>
/// One spin as the wheel plays it: the six faces, which one won, how it brakes, where the wheel was, and when it was
/// confirmed.
/// </summary>
/// <remarks>
/// Everything in it was decided, written into the save and recorded before it exists. The start is a moment rather
/// than a flag, like the capsule machine's (§171) and the trade cabin's (§174), so the wheel can be drawn from any
/// moment and the view model never waits on an animation.
/// </remarks>
/// <param name="StartAngle">Where the last spin left the wheel.</param>
/// <param name="OwedBefore">Spins owed before this one, for the chip that goes into the slot.</param>
/// <param name="StartedAt">A <see cref="Stopwatch"/> timestamp.</param>
public sealed record RoulettePlay(
    IReadOnlyList<RoulettePlayWedge> Wedges,
    int WinningIndex,
    WheelEnding Ending,
    double StartAngle,
    int Seed,
    int OwedBefore,
    long StartedAt)
{
    /// <summary>Seconds since the spin was confirmed.</summary>
    public double Elapsed => Stopwatch.GetElapsedTime(StartedAt).TotalSeconds;
}
