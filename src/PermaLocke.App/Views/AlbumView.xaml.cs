using System.Windows.Controls;
using System.Windows.Input;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// ÁLBUM (§186). All the work is in the view model; here only the focus, so the arrows turn pages, and the drawing
/// failures handed to the log.
/// </summary>
public partial class AlbumView : UserControl
{
    public AlbumView()
    {
        InitializeComponent();

        Loaded += (_, _) => Focus();
        Stage.MouseDown += (_, _) => Focus();
        Stage.RenderFailed += ex => (DataContext as AlbumViewModel)?.AnimationFailed(ex);
        Hand.RenderFailed += ex => (DataContext as AlbumViewModel)?.AnimationFailed(ex);
    }
}
