using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PermaLocke.App.Services;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Shells;

/// <summary>One look in the gallery: whether it is the one in use now.</summary>
public sealed record ThemeCard(PixelTheme Theme, bool IsCurrent);

/// <summary>
/// The gallery of looks (2026-10-01): pick one and the whole window changes at once, keeping the screen and the data.
/// Open next to the window so both are seen while choosing; nothing here decides for the player.
/// </summary>
public partial class DesignGalleryWindow : Window
{
    private static DesignGalleryWindow? _open;

    public DesignGalleryWindow()
    {
        InitializeComponent();
        DarkFrame.Apply(this);

        // En una pantalla baja la ventana no se sale: lo que no cabe se desplaza.
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 16);
        Rebuild(PixelTheme.Current.Key);

        PixelTheme.Changed += OnThemeChanged;
        Closed += (_, _) =>
        {
            PixelTheme.Changed -= OnThemeChanged;
            _open = null;
        };
    }

    /// <summary>Opens the gallery beside the owner when the screen has room, over it otherwise; one at a time.</summary>
    public static void Open(Window? owner)
    {
        if (_open is { } existing)
        {
            existing.Activate();
            return;
        }

        var gallery = new DesignGalleryWindow { Owner = owner };
        _open = gallery;

        var area = SystemParameters.WorkArea;
        if (owner is not null && area.Right - (owner.Left + owner.Width) >= gallery.Width + 12)
        {
            gallery.WindowStartupLocation = WindowStartupLocation.Manual;
            gallery.Left = owner.Left + owner.Width + 12;
            gallery.Top = Math.Max(area.Top, Math.Min(owner.Top, area.Bottom - gallery.Height));
        }
        else
        {
            gallery.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        gallery.Show();
    }

    /// <summary>The window changed look: the cards are drawn again, and the one pointed at stays pointed at.</summary>
    private void OnThemeChanged() => Rebuild((List.SelectedItem as ThemeCard)?.Theme.Key ?? PixelTheme.Current.Key);

    private void Rebuild(string pointed)
    {
        var cards = PixelTheme.All.Select(theme =>
            new ThemeCard(theme, string.Equals(theme.Key, PixelTheme.Current.Key, StringComparison.Ordinal))).ToList();

        List.ItemsSource = cards;
        List.SelectedItem = cards.FirstOrDefault(card => card.Theme.Key == pointed) ?? cards[0];
    }

    private void Use()
    {
        if (List.SelectedItem is ThemeCard { IsCurrent: false } card) ThemeStore.Choose(card.Theme.Key);
    }

    private void OnUse(object sender, RoutedEventArgs e) => Use();

    private void OnListKey(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter)
        {
            Use();
            e.Handled = true;
        }
    }

    private void OnListDouble(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(List, source) is not null) Use();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
