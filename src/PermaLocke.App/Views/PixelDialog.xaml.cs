using System.Windows;
using System.Windows.Media;
using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>What a notice is: its title is drawn in the colour of its kind.</summary>
public enum PixelDialogKind
{
    Info,
    Warning,
    Error
}

/// <summary>
/// PermaLocke's own message box (2026-10-30), in pixels, in place of Windows'. <see cref="Show"/> tells something (one button) or asks
/// (<paramref name="ask"/>: two, and «no» is the default).
/// </summary>
public partial class PixelDialog : Window
{
    private PixelDialog(string title, string message, PixelDialogKind kind, bool ask)
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        Title = title;
        Frame.Title = title.ToUpperInvariant();
        Body.Text = message;

        if (ask)
        {
            Yes.Content = "SÍ";
            No.Visibility = Visibility.Visible;
            Yes.IsDefault = false;
            No.IsDefault = true;
        }

        if (kind != PixelDialogKind.Info && TryFindResource(kind == PixelDialogKind.Error ? "PxBad" : "PxWarn") is Color colour)
        {
            Body.Colour = colour;
        }
    }

    /// <summary>Shows the notice and waits; true when the player accepted (always, for a notice that only tells).</summary>
    /// <param name="onTop">Over everything, for what must be seen even with the game and the app behind (the end of a run).</param>
    public static bool Show(string title, string message, PixelDialogKind kind = PixelDialogKind.Info, bool ask = false,
        Window? owner = null, bool onTop = false)
    {
        try
        {
            var dialog = new PixelDialog(title, message, kind, ask) { Topmost = onTop };
            if (owner is { IsVisible: true }) dialog.Owner = owner;
            else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return dialog.ShowDialog() == true;
        }
        catch (Exception)
        {
            // Sin los recursos de píxel (un fallo al arrancar) el aviso tiene que salir igual.
            var shown = MessageBox.Show(message, title, ask ? MessageBoxButton.YesNo : MessageBoxButton.OK,
                kind == PixelDialogKind.Error ? MessageBoxImage.Error : MessageBoxImage.Information);
            return shown is MessageBoxResult.OK or MessageBoxResult.Yes;
        }
    }

    private void OnYes(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnNo(object sender, RoutedEventArgs e) => DialogResult = false;
}
