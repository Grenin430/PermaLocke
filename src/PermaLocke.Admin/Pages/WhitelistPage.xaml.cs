using System.Windows.Controls;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin.Pages;

/// <summary>A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in <see cref="WhitelistViewModel"/>.</summary>
public partial class WhitelistPage : UserControl
{
    public WhitelistPage()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as WhitelistViewModel)?.RefreshCommand.Execute(null);
    }
}
