using System.Windows;
using System.Windows.Controls;

namespace PermaLocke.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    /// <summary>NOVEDADES, beside the version (1.0.7).</summary>
    private void OnChangelog(object sender, RoutedEventArgs e) =>
        new ChangelogWindow { Owner = Window.GetWindow(this) }.ShowDialog();
}
