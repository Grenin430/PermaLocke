using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using PermaLocke.App.Services;
using PermaLocke.App.Shells;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App;

/// <summary>
/// The shell window. Holds no logic beyond making its own frame match the look in use and fit the screen: what is drawn
/// inside is a shell (<c>Shells/</c>) that the look asks for, and that shares this window's data.
/// </summary>
public partial class MainWindow : Window
{
    private ShellKind? _shownShell;

    public MainWindow()
    {
        InitializeComponent();
        DarkFrame.Apply(this);

        // El marco según el diseño en uso (2026-10-01): al arrancar y cada vez que se cambia desde la galería.
        PixelTheme.Changed += ApplyShell;
        Closed += (_, _) => PixelTheme.Changed -= ApplyShell;
        ApplyShell();
    }

    /// <summary>
    /// Builds the frame the look in use asks for, and throws the old one away. A new one each time, and not one kept
    /// hidden: a hidden frame is not asked to redraw when the colours change, and would come back in the old ones.
    /// </summary>
    private void ApplyShell()
    {
        var kind = PixelTheme.Current.Shell;
        if (_shownShell == kind) return;

        _shownShell = kind;
        ShellSlot.Content = kind switch
        {
            ShellKind.Tabs => new TabsShell(),
            ShellKind.Menu => new MenuShell(),
            ShellKind.Dock => new DockShell(),
            ShellKind.Keys => new KeysShell(),
            _ => new RailShell()
        };
    }

    /// <summary>
    /// The little unfold when the window comes back from the tab.
    /// </summary>
    /// <remarks>
    /// The content and not the window: <c>Window.Opacity</c> only does anything with
    /// <c>AllowsTransparency</c>, which this window does not have and does not need. Scaling from
    /// 0.97 and fading in over a sixth of a second is enough to read as «se despliega» without
    /// making you wait for it — the window is already usable while it plays. Not at all when the player asked
    /// Windows for no animations.
    /// </remarks>
    public void PlayUnfold()
    {
        if (ShellSupport.ReducedMotion)
        {
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        ShellSlot.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(170),
            EasingFunction = ease
        });

        foreach (var axis in new[] { System.Windows.Media.ScaleTransform.ScaleXProperty, System.Windows.Media.ScaleTransform.ScaleYProperty })
        {
            SlotScale.BeginAnimation(axis, new DoubleAnimation
            {
                From = 0.97,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(230),
                EasingFunction = ease
            });
        }
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
