using System.Windows;
using PermaLocke.App.Services;

namespace PermaLocke.App;

/// <summary>
/// The shell window. Holds no logic beyond making its own frame match the theme and fit the screen.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// The size the window is fixed at, declared here and not in the XAML so the fitting below
    /// has something to compare against.
    /// </summary>
    /// <remarks>
    /// It is the widest thing the application has to show plus room to breathe: the map's largest
    /// island is 1024 across and its side panel another 290. Changing the window's size is these
    /// two numbers and nothing else.
    /// </remarks>
    private const double FixedWidth = 1180;

    private const double FixedHeight = 760;

    public MainWindow()
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        FitToScreen();
    }

    /// <summary>
    /// Sets the window to its fixed size, shrinking it only if this screen cannot take it.
    /// </summary>
    /// <remarks>
    /// The shrinking is not a loophole in «fixed», it is what stops a fixed size from being a bug
    /// on somebody else's machine. 760 plus a title bar plus a taskbar is taller than a 1366×768
    /// laptop, so on one of those the window opened with its own bottom edge off the screen —
    /// buttons included. A size that does not fit is not a size.
    /// <para>
    /// It reads the <b>working area</b> rather than the screen, because the taskbar is not screen
    /// the window can use.
    /// </para>
    /// </remarks>
    private void FitToScreen()
    {
        var area = SystemParameters.WorkArea;

        Width = Math.Min(FixedWidth, area.Width);
        Height = Math.Min(FixedHeight, area.Height);

        // Y se fija ahí: sin margen de redimensión, la única medida posible es la que se acaba de
        // calcular, de modo que nadie ve una ventana a medio estirar.
        MinWidth = MaxWidth = Width;
        MinHeight = MaxHeight = Height;
    }
}
