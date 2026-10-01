using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Shells;

/// <summary>A start menu of tiles and a back bar on every screen (GAME BOY). See the XAML for the composition.</summary>
public partial class MenuShell : ShellBase
{
    public MenuShell()
    {
        InitializeComponent();
        PreviewKeyDown += OnShellKey;
    }

    protected override UIElement SectionHost => Section;

    private bool MenuOpen => MenuLayer.Visibility == Visibility.Visible;

    /// <summary>For the previews: shows and hides the start menu from outside.</summary>
    public void ShowMenu() => OpenMenu();

    public void HideMenu() => CloseMenu();

    private void OpenMenu()
    {
        MenuLayer.Visibility = Visibility.Visible;
        PageLayer.Visibility = Visibility.Collapsed;
        Dispatcher.BeginInvoke(() =>
        {
            if (Tiles.ItemContainerGenerator.ContainerFromItem(Tiles.SelectedItem) is ListBoxItem item) item.Focus();
            else Tiles.Focus();
        }, DispatcherPriority.Input);
    }

    private void CloseMenu()
    {
        MenuLayer.Visibility = Visibility.Collapsed;
        PageLayer.Visibility = Visibility.Visible;
    }

    private void OnMenu(object sender, RoutedEventArgs e) => OpenMenu();

    private void OnPlay(object sender, RoutedEventArgs e) => CloseMenu();

    private void OnPageChosen(object sender, SelectionChangedEventArgs e) => ChoosePage(e);

    private void OnShellKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsVisible && (MenuOpen || e.OriginalSource is not TextBox))
        {
            if (MenuOpen) CloseMenu();
            else OpenMenu();
            e.Handled = true;
        }
    }

    /// <summary>Opening a tile: the section opens even when it is the one already on screen.</summary>
    private void Pick(ListBoxItem item)
    {
        if (item.DataContext is SectionViewModel section && Main is { } main)
        {
            main.SelectedSection = section;
        }

        CloseMenu();
    }

    private void OnTileClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(Tiles, source) is ListBoxItem item)
        {
            Pick(item);
        }
    }

    private void OnTileKey(object sender, KeyEventArgs e)
    {
        if ((e.Key is Key.Enter or Key.Space) && Keyboard.FocusedElement is ListBoxItem item)
        {
            Pick(item);
            e.Handled = true;
        }
    }
}
