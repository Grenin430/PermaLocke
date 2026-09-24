using System.Windows.Controls;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// The LUDÓPATA wheel: the wheel of fortune in the arcade room, and the card of what came out once it has landed.
/// </summary>
/// <remarks>
/// Presentation only. By the time the wheel turns, the spin has been written into the save and recorded. The wheel
/// (<see cref="RouletteMachine"/>) draws itself from the view model's <c>CurrentPlay</c>; since §175 there is no
/// animation here to start or wait for.
/// </remarks>
public partial class RouletteView : UserControl
{
    public RouletteView()
    {
        InitializeComponent();
        Machine.RenderFailed += ex => (DataContext as RouletteViewModel)?.AnimationFailed(ex);
    }
}
