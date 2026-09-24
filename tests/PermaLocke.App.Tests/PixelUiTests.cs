using System.IO;
using System.Text.RegularExpressions;
using PermaLocke.App.Views.Pixel;

namespace PermaLocke.App.Tests;

/// <summary>
/// The hand-drawn pieces of the pixel style (§176): a glyph or an icon typed one row too short draws crooked without
/// failing anywhere, so their shape is checked here.
/// </summary>
public sealed class PixelUiTests
{
    [Fact]
    public void Every_glyph_is_a_rectangle_of_cells_that_fits_in_the_line()
    {
        foreach (var (ch, rows) in PixelFont.All)
        {
            // Siete filas, o hasta dos más por debajo de la línea: la g y la y bajan dos, la coma y el guion bajo una.
            Assert.True(rows.Length >= PixelFont.CapRows && rows.Length <= PixelFont.CapRows + PixelFont.Below,
                $"«{ch}» tiene {rows.Length} filas");
            Assert.All(rows, row => Assert.Equal(rows[0].Length, row.Length));
            Assert.All(rows, row => Assert.Matches("^[.#]+$", row));
            Assert.Contains(rows, row => row.Contains('#'));
        }
    }

    [Fact]
    public void Spanish_text_needs_no_box_for_a_missing_letter()
    {
        // Lo que HOME y el historial escriben de verdad: ordinales, apartados, flechas y los dos símbolos de sexo.
        const string text = "ÁÉÍÓÚÜÑ áéíóúüñ ¿¡ 2ª 1º §175 «Nidoran♀» → ← × … · % + - / < > ( ) [ ] : ; , . ' \"";
        foreach (var ch in text.Where(ch => ch != ' '))
        {
            var drawn = new List<(int X, int Y)>();
            PixelFont.Draw(ch.ToString(), 0, 0, (x, y) => drawn.Add((x, y)));

            // La caja de «falta» es un rectángulo hueco de 4 x 7: ningún glifo de verdad es exactamente eso.
            var box = drawn.Count == 18 && drawn.All(p => p.X is >= 0 and <= 3) && drawn.Min(p => p.Y) == PixelFont.Above;
            Assert.False(box, $"«{ch}» sale como caja");
        }
    }

    [Fact]
    public void Wrap_never_returns_a_line_wider_than_asked()
    {
        const string text = "Rol cambiado de «normal» a «ludopata». A petición del jugador, para probar la ruleta (§175).";
        foreach (var width in new[] { 20, 40, 80, 160 })
        {
            Assert.All(PixelFont.Wrap(text, width), line => Assert.True(PixelFont.Measure(line) <= width, line));
        }
    }

    [Fact]
    public void Every_icon_is_twelve_by_twelve_and_uses_only_known_colours()
    {
        foreach (var (key, rows) in PixelIcons.All)
        {
            Assert.True(rows.Length == PixelIcons.Size, $"{key} tiene {rows.Length} filas");
            foreach (var row in rows)
            {
                Assert.True(row.Length == PixelIcons.Size, $"{key}: «{row}»");
                Assert.All(row.Where(letter => letter != '.'),
                    letter => Assert.True(PixelIcons.TryColour(letter, out _), $"{key} usa «{letter}», que no es un color"));
            }
        }
    }

    [Fact]
    public void Every_section_has_its_own_pixel_icon()
    {
        var viewModels = Path.Combine(RepoRoot(), "src", "PermaLocke.App", "ViewModels");
        var keys = Directory.GetFiles(viewModels, "*.cs")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "IconKey => \"(Icon[A-Za-z]+)\"").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);
        var drawn = PixelIcons.All.Select(pair => pair.Key).ToHashSet();

        // Sin dibujo se cae al punto, que no falla: saldría una sección con un punto por icono sin que nadie se enterase.
        Assert.All(keys, key => Assert.Contains(key, drawn));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PermaLocke.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encuentra la raíz del repositorio.");
    }
}
