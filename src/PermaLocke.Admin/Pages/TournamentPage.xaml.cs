using System.Windows;
using System.Windows.Controls;
using PermaLocke.Admin.ViewModels;

namespace PermaLocke.Admin.Pages;

public partial class TournamentPage : UserControl
{
    public TournamentPage()
    {
        InitializeComponent();
    }

    // TIENDA (2026-09-28): la regla oficial shop.json, «abierta» y «abreEnPrueba».
    private async void OnShopClose(object sender, RoutedEventArgs e) => await SetShopAsync(false, 0, "¿Cerrar la TIENDA a todos?");

    private async void OnShopOpen(object sender, RoutedEventArgs e) => await SetShopAsync(true, 0, "¿Abrir la TIENDA a todos?");

    private async void OnShopAtTrial(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(Trial.Text, out var trial) && trial is >= 1 and <= 12)
        {
            await SetShopAsync(true, trial, $"¿Abrir la TIENDA al superar la prueba {trial}?");
        }
        else
        {
            MessageBox.Show("Pon un número de prueba del 1 al 12.", "Tienda");
        }
    }

    private async Task SetShopAsync(bool open, int trial, string question)
    {
        if (MessageBox.Show(question, "Tienda", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        MessageBox.Show(await ((AdminViewModel)DataContext).Rules.SetShopAsync(open, trial), "Tienda");
    }
}
