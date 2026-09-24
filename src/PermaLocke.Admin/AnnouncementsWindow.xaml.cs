using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The tournament's announcements. Everything it does lives in <see cref="AnnouncementsViewModel"/>.</summary>
public partial class AnnouncementsWindow : Window
{
    public AnnouncementsWindow(AnnouncementsViewModel model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.RefreshCommand.Execute(null);
    }
}
