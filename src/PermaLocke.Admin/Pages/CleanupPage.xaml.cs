using System.Windows.Controls;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin.Pages;

/// <summary>A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in <see cref="CleanupViewModel"/>.</summary>
public partial class CleanupPage : UserControl
{
    public CleanupPage()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as CleanupViewModel)?.RefreshCommand.Execute(null);
    }
}
