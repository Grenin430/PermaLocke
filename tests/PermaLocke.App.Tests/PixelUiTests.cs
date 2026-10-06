using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
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
        const string text = "ÁÉÍÓÚÜÑ áéíóúüñ ¿¡ 2ª 1º §175 «Nidoran♀» → ← × … · % + - / \\ < >( ) [ ] : ; , . ' \"";
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

    [Fact]
    public void Every_look_defines_every_colour_the_styles_read()
    {
        // Un diseño al que le falte un color caería al del diseño de siempre, que no falla: saldría un trozo violeta en medio
        // de otro tema sin que nadie se enterase.
        var original = PixelTheme.All[0];
        Assert.Equal("clasico", original.Key);

        foreach (var theme in PixelTheme.All)
        {
            Assert.All(original.Colours.Keys, key => Assert.True(theme.Colours.ContainsKey(key), $"{theme.Key} no define {key}"));
            Assert.False(string.IsNullOrWhiteSpace(theme.Name));
            Assert.False(string.IsNullOrWhiteSpace(theme.Tagline));
            Assert.False(string.IsNullOrWhiteSpace(theme.About));
        }

        Assert.Equal(PixelTheme.All.Count, PixelTheme.All.Select(theme => theme.Key).Distinct().Count());
    }

    [Fact]
    public void The_five_looks_differ_in_how_the_window_is_composed_and_not_only_in_colour()
    {
        // Lo que se pidió: que se reconozcan por la estructura. Cada diseño tiene su propio marco.
        Assert.Equal(PixelTheme.All.Count, PixelTheme.All.Select(theme => theme.Shell).Distinct().Count());
        Assert.Equal(Enum.GetValues<ShellKind>().Length, PixelTheme.All.Select(theme => theme.Shell).Distinct().Count());
    }

    [Fact]
    public void The_game_boy_look_leaves_every_colour_in_its_four_greens_and_does_it_only_once()
    {
        var gameBoy = PixelTheme.Find("gameboy");
        Assert.NotNull(gameBoy.Shades);

        foreach (var colour in new[] { Colors.Red, Colors.Black, Colors.White, Color.FromRgb(0x40, 0x7A, 0xC0) })
        {
            var mapped = gameBoy.Map(colour);
            Assert.Contains(mapped, gameBoy.Shades!);
            Assert.Equal(mapped, gameBoy.Map(mapped));
        }

        // Los demás diseños no tocan ningún color.
        Assert.Equal(Colors.Red, PixelTheme.Find("esmeralda").Map(Colors.Red));
    }

    [Fact]
    public void Text_colours_of_every_look_stay_readable_on_the_floors_they_are_drawn_on()
    {
        // WCAG 2: 4.5 para el texto corriente, 3 para lo grande o lo que es estado. El clasico es el original y no se mide.
        var tooLow = new List<string>();
        foreach (var theme in PixelTheme.All.Where(theme => theme.Key != "clasico"))
        {
            foreach (var floor in new[] { "PxFace", "PxWell", "PxSidebar" })
            {
                foreach (var (ink, minimum) in new[]
                         {
                             ("PxText", 4.5), ("PxTextDim", 4.5), ("PxAccentInk", 3.0), ("PxGood", 3.0), ("PxBad", 3.0),
                             ("PxWarn", 3.0), ("PxGold", 3.0)
                         })
                {
                    // La barra lateral solo lleva el texto corriente y el del acento en los diseños donde existe a la vista.
                    if (floor == "PxSidebar" && ink != "PxText") continue;

                    var ratio = Contrast(theme[ink], theme[floor]);
                    if (ratio < minimum) tooLow.Add($"{theme.Key}: {ink} sobre {floor} = {ratio:0.0} (< {minimum})");
                }
            }

            var onAccent = Contrast(theme["PxOnAccent"], theme["PxAccent"]);
            if (onAccent < 4.5) tooLow.Add($"{theme.Key}: PxOnAccent sobre PxAccent = {onAccent:0.0}");
        }

        Assert.True(tooLow.Count == 0, string.Join(Environment.NewLine, tooLow));
    }

    private static double Contrast(Color a, Color b)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

        var (high, low) = (Luminance(a), Luminance(b));
        if (high < low) (high, low) = (low, high);
        return (high + 0.05) / (low + 0.05);
    }

    [Fact]
    public void An_unknown_look_is_the_original()
    {
        Assert.Equal("clasico", PixelTheme.Find("no-existe").Key);
        Assert.Equal("clasico", PixelTheme.Find(null).Key);
    }

    [Fact]
    public void The_look_chosen_is_the_one_the_app_starts_with_and_a_broken_file_is_the_original()
    {
        var folder = Path.Combine(Path.GetTempPath(), "permalocke-tema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            // Sin fichero: el diseño de siempre.
            Assert.Equal("clasico", PermaLocke.App.Services.ThemeStore.Start(folder, null));

            File.WriteAllText(Path.Combine(folder, "tema.json"), "{ \"tema\": \"rotom\" }");
            Assert.Equal("rotom", PermaLocke.App.Services.ThemeStore.Start(folder, null));

            // La línea de comandos manda sobre lo guardado, y no lo cambia.
            Assert.Equal("gameboy", PermaLocke.App.Services.ThemeStore.Start(folder, "gameboy"));
            Assert.Equal("rotom", PermaLocke.App.Services.ThemeStore.Start(folder, null));

            // Un fichero roto, o un diseño que ya no existe, no impiden arrancar.
            File.WriteAllText(Path.Combine(folder, "tema.json"), "esto no es json");
            Assert.Equal("clasico", PermaLocke.App.Services.ThemeStore.Start(folder, null));
            File.WriteAllText(Path.Combine(folder, "tema.json"), "{ \"tema\": \"inventado\" }");
            Assert.Equal("clasico", PermaLocke.App.Services.ThemeStore.Start(folder, null));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
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
