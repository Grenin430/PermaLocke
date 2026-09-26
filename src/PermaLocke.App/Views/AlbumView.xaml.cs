using System.Windows.Controls;
using System.Windows.Input;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// ÁLBUM (§186, §187). All the work is in the view model; here only the focus, so the arrows turn pages, where a card
/// leaves its pocket from, handed to the hand, and the drawing failures handed to the log.
/// </summary>
public partial class AlbumView : UserControl
{
    public AlbumView()
    {
        InitializeComponent();

        Loaded += (_, _) => Focus();
        Stage.MouseDown += (_, _) => Focus();
        Stage.RenderFailed += ex => (DataContext as AlbumViewModel)?.AnimationFailed(ex);

        // La carta sale de su funda: la mano sabe desde dónde para hacerla volar.
        Stage.CardOpening += (_, pocket) => Hand.FlyFrom(pocket, Stage);
        Hand.RenderFailed += ex => (DataContext as AlbumViewModel)?.AnimationFailed(ex);
    }
}
