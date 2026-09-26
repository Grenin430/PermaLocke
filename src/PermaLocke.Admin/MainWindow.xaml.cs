using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The admin's window. Everything it does lives in its view model.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is AdminViewModel model)
            {
                model.SheetRequested += (_, sheet) => new PlayerSheetWindow(sheet) { Owner = this }.Show();
            }
        };
    }

    private void OnRules(object sender, RoutedEventArgs e) =>
        new RulesWindow(((AdminViewModel)DataContext).Rules) { Owner = this }.Show();

    private void OnAudit(object sender, RoutedEventArgs e) =>
        new AuditWindow(((AdminViewModel)DataContext).Audit) { Owner = this }.Show();

    private void OnWhitelist(object sender, RoutedEventArgs e) =>
        new WhitelistWindow(((AdminViewModel)DataContext).Whitelist) { Owner = this }.Show();

    private void OnUsage(object sender, RoutedEventArgs e) =>
        new UsageWindow(((AdminViewModel)DataContext).Usage) { Owner = this }.Show();

    private void OnReports(object sender, RoutedEventArgs e) =>
        new ReportsWindow(((AdminViewModel)DataContext).Reports) { Owner = this }.Show();

    private void OnCleanup(object sender, RoutedEventArgs e) =>
        new CleanupWindow(((AdminViewModel)DataContext).Cleanup) { Owner = this }.Show();

    private void OnAnnouncements(object sender, RoutedEventArgs e) =>
        new AnnouncementsWindow(((AdminViewModel)DataContext).Announcements) { Owner = this }.Show();

    /// <summary>Marks every player, or none if they already were.</summary>
    private void OnChooseAll(object sender, RoutedEventArgs e)
    {
        if (PlayerList.SelectedItems.Count == PlayerList.Items.Count) PlayerList.UnselectAll();
        else PlayerList.SelectAll();
    }

    private void OnPlayersChosen(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        ((AdminViewModel)DataContext).Choose(PlayerList.SelectedItems.Cast<Services.PlayerLine>());
}
