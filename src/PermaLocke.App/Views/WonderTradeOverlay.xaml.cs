using System.Windows.Controls;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// The wonder trade as it plays: the trade cabin in the arcade room, and the card of what arrived once it is out.
/// </summary>
/// <remarks>
/// <para>
/// Presentation only. It decides nothing: by the time the first frame runs, the trade has been drawn, written into
/// the save and recorded in the log. The cabin (<see cref="TradeMachine"/>) draws itself from the view model's
/// <c>CurrentPlay</c>, and the card comes up when the view model says the Pokémon is out.
/// </para>
/// <para>
/// It was a link cable between two pedestals (§121) with the three reveals in WPF boxes on top; since §174 it is a
/// cabin whose own screen says them.
/// </para>
/// </remarks>
public partial class WonderTradeOverlay : UserControl
{
    public WonderTradeOverlay()
    {
        InitializeComponent();
        Machine.RenderFailed += ex => (DataContext as WonderTradeViewModel)?.AnimationFailed(ex);
    }
}
