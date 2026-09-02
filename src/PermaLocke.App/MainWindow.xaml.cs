using System.Windows;
using PermaLocke.App.Services;

namespace PermaLocke.App;

/// <summary>
/// The shell window. Holds no logic beyond making its own frame match the theme and fit the screen.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DarkFrame.Apply(this);
    }

    /// <summary>
    /// Sets the window to the chosen size, shrinking it only if this screen cannot take it.
    /// </summary>
    /// <remarks>
    /// The shrinking is not a loophole in «fixed», it is what stops a chosen size from being a bug
    /// on somebody else's machine. 860 plus a title bar plus a taskbar is taller than a 1366×768
    /// laptop, so without this the window would open with its own bottom edge off the screen —
    /// buttons included. A size that does not fit is not a size.
    /// <para>
    /// It reads the <b>working area</b> rather than the screen, because the taskbar is not screen
    /// the window can use.
    /// </para>
    /// </remarks>
    public void Resize(WindowSize size)
    {
        var area = SystemParameters.WorkArea;

        // El minimo y el maximo se sueltan antes de medir: si no, el tamaño anterior impide crecer.
        MinWidth = MinHeight = 0;
        MaxWidth = MaxHeight = double.PositiveInfinity;

        Width = Math.Min(size.Width, area.Width);
        Height = Math.Min(size.Height, area.Height);

        // Y se fija ahí: sin margen de redimensión, la única medida posible es la que se acaba de
        // calcular, de modo que nadie ve una ventana a medio estirar.
        MinWidth = MaxWidth = Width;
        MinHeight = MaxHeight = Height;

        Left = area.Left + ((area.Width - Width) / 2);
        Top = area.Top + ((area.Height - Height) / 2);
    }
}
