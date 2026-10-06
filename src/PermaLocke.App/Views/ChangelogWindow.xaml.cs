using System.Windows;
using System.Windows.Media;
using PermaLocke.App.Services;

namespace PermaLocke.App.Views;

/// <summary>
/// NOVEDADES (1.0.7): every version's notes from <c>Novedades.txt</c>, or one of them opened from its card on JUGAR like a
/// Steam event (1.0.9). Presentation only: <see cref="ReleaseNotes"/> reads the file.
/// </summary>
public partial class ChangelogWindow : Window
{
    public sealed record Line(string Text, int Scale, Thickness Margin, Color Colour);

    /// <param name="version">Only this version, as its own post; null for all of them.</param>
    public ChangelogWindow(string? version = null)
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        Color Res(string key) => (Color)FindResource(key);

        var notes = ReleaseNotes.Embedded().Where(n => version is null || n.Version == version).ToList();
        var single = version is not null && notes.Count == 1;
        Heading.Text = single ? notes[0].Kind : "NOVEDADES";

        Lines.ItemsSource = notes.SelectMany(note =>
            (single
                ? new[]
                {
                    new Line(note.Title, 4, new Thickness(0, 0, 0, 8), Res("PxText")),
                    new Line($"VERSIÓN {note.Version} · {note.DateText}".TrimEnd(' ', '·'), 2, new Thickness(0, 0, 0, 14), Res("PxTextDim"))
                }
                : [new Line($"{note.Kind} {note.Version}  {note.DateText}".TrimEnd(), 3, new Thickness(0, 6, 0, 10), Res("PxAccentLight"))])
            .Concat(note.Sections.SelectMany(section =>
                (section.Heading.Length > 0 ? [new Line(section.Heading, 2, new Thickness(0, 14, 0, 6), Res("PxGold"))] : Array.Empty<Line>())
                .Concat(section.Lines.Select(l => new Line(l, 2, new Thickness(0, 0, 0, 6), Res("PxText")))))))
            .ToList();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
