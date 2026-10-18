using System.Windows.Controls;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin.Pages;

/// <summary>A page of Admin (2026-09-28: the old window, now inside the main one). Everything it does lives in <see cref="AnnouncementsViewModel"/>.</summary>
public partial class AnnouncementsPage : UserControl
{
    public AnnouncementsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as AnnouncementsViewModel)?.RefreshCommand.Execute(null);
    }
}
