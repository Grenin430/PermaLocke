using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The crash reports on the server. Everything it does lives in <see cref="ReportsViewModel"/>.</summary>
public partial class ReportsWindow : Window
{
    public ReportsWindow(ReportsViewModel model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.RefreshCommand.Execute(null);
    }
}
