using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace PermaLocke.App.Views;

/// <summary>
/// NOVEDADES (1.0.7): shows <c>Novedades.txt</c>, embedded in the exe so an update brings its own. A line that is a
/// version is a title, a line in capitals a heading, anything else a paragraph. Presentation only.
/// </summary>
public partial class ChangelogWindow : Window
{
    public sealed record Line(string Text, int Scale, Thickness Margin, Color Colour);

    public ChangelogWindow()
    {
        InitializeComponent();
        PermaLocke.App.Services.DarkFrame.Apply(this);

        using var stream = typeof(ChangelogWindow).Assembly.GetManifestResourceStream("PermaLocke.App.Novedades.txt");
        var text = stream is null ? "" : new StreamReader(stream).ReadToEnd();
        Color Res(string key) => (Color)FindResource(key);

        Lines.ItemsSource = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Select(l =>
            Regex.IsMatch(l, @"^\d+(\.\d+)+$") ? new Line($"VERSIÓN {l}", 3, new Thickness(0, 6, 0, 10), Res("PxAccentLight"))
            : l == l.ToUpperInvariant() ? new Line(l, 2, new Thickness(0, 14, 0, 6), Res("PxGold"))
            : new Line(l, 2, new Thickness(0, 0, 0, 6), Res("PxText"))).ToList();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
