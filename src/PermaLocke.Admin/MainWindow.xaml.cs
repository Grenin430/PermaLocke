using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The admin's window. Everything it does lives in its view model.</summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnAudit(object sender, RoutedEventArgs e) =>
        new AuditWindow(((AdminViewModel)DataContext).Audit) { Owner = this }.Show();
}
