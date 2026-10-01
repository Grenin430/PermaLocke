using System.Windows;
using System.Windows.Controls;
using PermaLocke.App.Shells;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        // El nombre del diseño en uso, en la cabecera de su panel: se pone al cargar y cada vez que cambia.
        void Name() => DesignName.Text = PixelTheme.Current.Name;
        Loaded += (_, _) =>
        {
            PixelTheme.Changed -= Name;
            PixelTheme.Changed += Name;
            Name();
        };
        Unloaded += (_, _) => PixelTheme.Changed -= Name;
    }

    /// <summary>NOVEDADES, beside the version (1.0.7).</summary>
    private void OnChangelog(object sender, RoutedEventArgs e) =>
        new ChangelogWindow { Owner = Window.GetWindow(this) }.ShowDialog();

    /// <summary>The gallery of looks (2026-10-01).</summary>
    private void OnDesign(object sender, RoutedEventArgs e) => DesignGalleryWindow.Open(Window.GetWindow(this));
}
