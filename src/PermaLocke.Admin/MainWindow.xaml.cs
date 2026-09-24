using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The admin's window. Everything it does lives in its view model.</summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnAudit(object sender, RoutedEventArgs e) =>
        new AuditWindow(((AdminViewModel)DataContext).Audit) { Owner = this }.Show();

    private void OnWhitelist(object sender, RoutedEventArgs e) =>
        new WhitelistWindow(((AdminViewModel)DataContext).Whitelist) { Owner = this }.Show();

    private void OnPlayersChosen(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        ((AdminViewModel)DataContext).Choose(PlayerList.SelectedItems.Cast<Services.PlayerLine>());
}
