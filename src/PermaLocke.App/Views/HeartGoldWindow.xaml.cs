using System.Windows;
using PermaLocke.App.Services;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>HeartGold's window (2026-11-10): randomize and play, nothing else.</summary>
public partial class HeartGoldWindow : Window
{
    private readonly HeartGoldViewModel _model;

    public HeartGoldWindow(HeartGoldViewModel model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();
        DarkFrame.Apply(this);
        Activated += (_, _) => _model.Refresh();
    }

    private void OnChooseSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Ajustes del Randomizer",
            Filter = "Ajustes del Randomizer (*.rnqs)|*.rnqs"
        };

        if (dialog.ShowDialog(this) == true) _model.UseSettings(dialog.FileName);
    }
}
