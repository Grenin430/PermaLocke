using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The tournament's whitelist. Everything it does lives in <see cref="WhitelistViewModel"/>.</summary>
public partial class WhitelistWindow : Window
{
    public WhitelistWindow(WhitelistViewModel model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.RefreshCommand.Execute(null);
    }
}
