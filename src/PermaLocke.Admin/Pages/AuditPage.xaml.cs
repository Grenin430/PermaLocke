using System.Windows.Controls;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin.Pages;

/// <summary>A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in <see cref="AuditViewModel"/>.</summary>
public partial class AuditPage : UserControl
{
    public AuditPage()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as AuditViewModel)?.RefreshCommand.Execute(null);
    }
}
