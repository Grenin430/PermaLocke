using System.Windows.Controls;
using PermaLocke.Admin.Services;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin.Pages;

public partial class PlayersPage : UserControl
{
    public PlayersPage()
    {
        InitializeComponent();
    }

    private void OnPlayerChosen(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is PlayerLine player)
        {
            (DataContext as AdminViewModel)?.OpenSheetCommand.Execute(player);
        }
    }
}
