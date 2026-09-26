using System.Windows;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin;

/// <summary>A player's sheet. Everything it does lives in <see cref="PlayerSheetViewModel"/>.</summary>
public partial class PlayerSheetWindow : Window
{
    public PlayerSheetWindow(PlayerSheetViewModel model)
    {
        DataContext = model;
        InitializeComponent();
        Loaded += (_, _) => model.LoadCommand.Execute(null);
    }
}
