using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PermaLocke.App.Views;

public partial class CemeteryView : UserControl
{
    public CemeteryView()
    {
        InitializeComponent();

        // Solo presentación: al abrir la sala, el foco va al reproductor grande, para que Espacio, las
        // flechas y Esc funcionen sin tener que pinchar antes.
        Theater.IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                Dispatcher.BeginInvoke(() => TheaterPlayer.Focus(), DispatcherPriority.Input);
            }
        };
    }
}
