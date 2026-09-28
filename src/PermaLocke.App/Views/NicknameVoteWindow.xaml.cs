using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PermaLocke.App.Services;
using PermaLocke.App.ViewModels;

namespace PermaLocke.App.Views;

/// <summary>
/// Presentation only: places the window on the left of the emulator, half way down (of the screen without it), moves the bar
/// smoothly and gives the keyboard back to the game when it closes.
/// </summary>
public partial class NicknameVoteWindow : Window
{
    private readonly DispatcherTimer _bar = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly IntPtr _game = GameWindow.Handle();

    /// <summary>Up in the top left corner instead of half way down: the catcher's question, so it never covers a vote.</summary>
    public bool AtTop { get; init; }

    public NicknameVoteWindow(NicknameVoteViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        _bar.Tick += (_, _) => viewModel.Tick(DateTimeOffset.Now);
        viewModel.Tick(DateTimeOffset.Now);

        SizeChanged += (_, _) => Place();
        Loaded += (_, _) =>
        {
            Place();
            _bar.Start();

            // Para escribir el mote hace falta el teclado; para lo demás basta el ratón y el juego sigue con el suyo.
            if (viewModel.IsPropose)
            {
                Activate();
                Entry.Focus();
            }
        };

        Closed += (_, _) =>
        {
            _bar.Stop();
            if (viewModel.IsPropose && _game != IntPtr.Zero) SetForegroundWindow(_game);
        };
    }

    private void Place()
    {
        var dpi = VisualTreeHelper.GetDpi(this);

        // A la izquierda y a media altura (lo pidió el organizador): ahí no hay nada más de PermaLocke.
        if (_game != IntPtr.Zero && GameWindow.ClientBox(_game) is { } box)
        {
            Left = (box.Left / dpi.DpiScaleX) + 8;
            Top = AtTop ? (box.Top / dpi.DpiScaleY) + 8 : ((box.Top + (box.Height / 2.0)) / dpi.DpiScaleY) - (ActualHeight / 2);
            return;
        }

        var area = SystemParameters.WorkArea;
        Left = area.Left + 16;
        Top = AtTop ? area.Top + 16 : area.Top + (area.Height / 2) - (ActualHeight / 2);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
}
