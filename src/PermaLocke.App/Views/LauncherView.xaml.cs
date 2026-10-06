using System.Windows;
using System.Windows.Controls;

namespace PermaLocke.App.Views;

public partial class LauncherView : UserControl
{
    public LauncherView() => InitializeComponent();

    /// <summary>A card of NOVEDADES opens that version as its own post, like an event on Steam. Presentation only.</summary>
    private void OnUpdate(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string version })
        {
            new ChangelogWindow(version) { Owner = Window.GetWindow(this) }.Show();
        }
    }

    private void OnAllUpdates(object sender, RoutedEventArgs e) =>
        new ChangelogWindow { Owner = Window.GetWindow(this) }.Show();
}
