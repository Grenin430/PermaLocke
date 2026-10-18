using System.Windows;
using System.Windows.Input;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The admin's window: the menu and the page. Everything it does lives in its view models.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Ctrl+F goes to the player search from anywhere; Esc closes the open player sheet.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            GlobalSearch.Focus();
            GlobalSearch.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && DataContext is AdminViewModel { Sheet: not null } admin && admin.SelectedSection.Page is PlayersSection
                 && e.OriginalSource is not System.Windows.Controls.TextBox)
        {
            admin.CloseSheetCommand.Execute(null);
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <summary>Searching is for players: the search box opens JUGADORES, where the list shows what it finds.</summary>
    private void OnSearchFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is AdminViewModel admin && admin.SelectedSection.Page is not PlayersSection) admin.GoCommand.Execute("JUGADORES");
    }
}
