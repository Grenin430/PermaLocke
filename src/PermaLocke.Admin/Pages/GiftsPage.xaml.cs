using System.Windows;
using System.Windows.Controls;
using PermaLocke.Admin.Services;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin.Pages;

public partial class GiftsPage : UserControl
{
    public GiftsPage()
    {
        InitializeComponent();
    }

    /// <summary>Marks every player, or none if they already were.</summary>
    private void OnChooseAll(object sender, RoutedEventArgs e)
    {
        if (PlayerList.SelectedItems.Count == PlayerList.Items.Count) PlayerList.UnselectAll();
        else PlayerList.SelectAll();
    }

    private void OnPlayersChosen(object sender, SelectionChangedEventArgs e) =>
        (DataContext as AdminViewModel)?.Choose(PlayerList.SelectedItems.Cast<PlayerLine>());
}
