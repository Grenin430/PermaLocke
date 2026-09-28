using System.Windows.Controls;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin.Pages;

/// <summary>A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in <see cref="ReportsViewModel"/>.</summary>
public partial class ReportsPage : UserControl
{
    public ReportsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as ReportsViewModel)?.RefreshCommand.Execute(null);
    }
}
