using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PermaLocke.App.ViewModels;

/// <summary>One cell of the spinning reel.</summary>
/// <remarks>
/// Every cell but the winner is decoration: an icon of the cartridge picked at random. They fly
/// past in full colour, and the reel says so in writing — they are not candidates, because the
/// roll was already decided before the wheel started turning.
/// </remarks>
public sealed partial class ReelCellViewModel(BitmapSource? sprite, bool isWinner) : ObservableObject
{
    public BitmapSource? Sprite { get; } = sprite;

    /// <summary>True for the single cell the reel stops on.</summary>
    public bool IsWinner { get; } = isWinner;

    /// <summary>Species with no known icon still get a cell; it shows a question mark.</summary>
    public bool HasSprite => Sprite is not null;

    /// <summary>Set when the reel has stopped: the winner grows and lights up.</summary>
    [ObservableProperty]
    private bool _isRevealed;

    /// <summary>Set on every other cell at the same moment, so the winner is the one that reads.</summary>
    [ObservableProperty]
    private bool _isDimmed;
}

/// <summary>What the view needs to run one spin. Sizes and easing are the view's business.</summary>
/// <param name="WinnerIndex">Cell the reel has to stop on.</param>
/// <param name="Duration">How long the whole spin lasts, already scaled by tier.</param>
/// <param name="Stopped">
/// Called when the wheel has actually come to rest. The reveal hangs off this and not off a
/// timer: building 118 cells takes long enough that the animation starts noticeably later than
/// the request, and a timed reveal fired while the reel was still moving — leaving the winner
/// half a cell outside its own frame.
/// </param>
public sealed record SpinRequest(int WinnerIndex, TimeSpan Duration, Action Stopped);
