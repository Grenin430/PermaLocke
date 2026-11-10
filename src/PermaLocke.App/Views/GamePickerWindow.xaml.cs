using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.Views;

/// <summary>
/// The launcher's first window (2026-11-10): which game to play, asked every time PermaLocke opens. A playable game closes it with that game
/// chosen; one in development says why it cannot be played yet and leaves the choice open.
/// </summary>
public partial class GamePickerWindow : Window
{
    /// <summary>One card: the game and what the card shows of it.</summary>
    public sealed record Card(GameInfo Game, ImageSource? Cover)
    {
        public string Name => Game.Name.ToUpperInvariant();

        public string Platform => Game.Platform == GamePlatform.N3ds ? "3DS" : "DS";

        public Color BadgeColour => Game.Platform == GamePlatform.N3ds ? Color.FromRgb(0xC8, 0x2A, 0x3A) : Color.FromRgb(0x5A, 0x5E, 0x6A);

        public string State => Game.Playable ? "LISTO PARA JUGAR" : "EN DESARROLLO";

        public Color StateColour => Game.Playable ? Color.FromRgb(0x7C, 0xD8, 0x7A) : Color.FromRgb(0xF0, 0xB8, 0x48);

        public Visibility Shade => Game.Playable ? Visibility.Collapsed : Visibility.Visible;
    }

    public GamePickerWindow()
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        Games.ItemsSource = GameCatalog.All.Select(game => new Card(game, CoverOf(game.Id))).ToList();
    }

    /// <summary>The game chosen, once the window has closed with one.</summary>
    public GameInfo? Chosen { get; private set; }

    private static ImageSource? CoverOf(GameVersion id)
    {
        var file = id switch
        {
            GameVersion.UltraMoon => "ultraluna.png",
            GameVersion.HeartGold => "heartgold.png",
            _ => null
        };

        if (file is null) return null;
        try
        {
            return new BitmapImage(new Uri($"pack://application:,,,/Assets/Games/{file}"));
        }
        catch (Exception)
        {
            // Una portada que no carga no impide elegir el juego: la tarjeta sigue con su nombre.
            return null;
        }
    }

    private void OnGame(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Card card) return;
        if (!card.Game.Playable)
        {
            PixelDialog.Show(card.Game.Name, card.Game.Note, owner: this);
            return;
        }

        Chosen = card.Game;
        DialogResult = true;
    }

    private void OnQuit(object sender, RoutedEventArgs e) => DialogResult = false;
}
