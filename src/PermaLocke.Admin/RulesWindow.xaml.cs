using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>The official rules. Everything it does lives in <see cref="RulesViewModel"/>.</summary>
public partial class RulesWindow : Window
{
    public RulesWindow(RulesViewModel model)
    {
        DataContext = model;
        InitializeComponent();
    }
}
