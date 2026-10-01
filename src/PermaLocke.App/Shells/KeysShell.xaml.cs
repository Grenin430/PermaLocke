using System.Windows;
using System.Windows.Controls;

namespace PermaLocke.App.Shells;

/// <summary>A device: icon keys, one screen and soft keys (ROTOM DEX). See the XAML for the composition.</summary>
public partial class KeysShell : ShellBase
{
    public KeysShell()
    {
        InitializeComponent();
    }

    protected override UIElement SectionHost => Section;

    private void OnPageChosen(object sender, SelectionChangedEventArgs e) => ChoosePage(e);
}
