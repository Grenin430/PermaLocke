using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>What the tournament takes on the server. Everything it does lives in <see cref="UsageViewModel"/>.</summary>
public partial class UsageWindow : Window
{
    public UsageWindow(UsageViewModel model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.RefreshCommand.Execute(null);
    }
}
