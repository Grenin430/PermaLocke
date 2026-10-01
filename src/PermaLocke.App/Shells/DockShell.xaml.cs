using System.Windows;
using System.Windows.Controls;

namespace PermaLocke.App.Shells;

/// <summary>A HUD on top and a dock of icons along the bottom (ULTRAUMBRAL). See the XAML for the composition.</summary>
public partial class DockShell : ShellBase
{
    public DockShell()
    {
        InitializeComponent();
    }

    protected override UIElement SectionHost => Section;

    private void OnPageChosen(object sender, SelectionChangedEventArgs e) => ChoosePage(e);
}
