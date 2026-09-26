using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The server cleanup. Everything it does lives in <see cref="CleanupViewModel"/>.</summary>
public partial class CleanupWindow : Window
{
    public CleanupWindow(CleanupViewModel model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.RefreshCommand.Execute(null);
    }
}
