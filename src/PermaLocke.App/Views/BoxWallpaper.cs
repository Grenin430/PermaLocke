using System.Windows;
using System.Windows.Media;

namespace PermaLocke.App.Views;

/// <summary>
/// The wallpaper behind a box of the PC: a flat ground with a small motif repeated in staggered rows, drawn cell by
/// cell like the rest of PermaLocke's pixel art.
/// </summary>
/// <remarks>
/// <para>
/// The game gives every box a wallpaper, and a grid of thirty holes on a plain panel read as a spreadsheet. These are
/// drawn here and not taken from the cartridge: the motifs are a handful of cells each, written as text below, in four
/// muted colours per theme so the icons in the holes stay the brightest thing on screen.
/// </para>
/// <para>
/// No gradients. A theme with two grounds (the beach) meets at a dithered seam and nowhere else.
/// </para>
/// </remarks>
public sealed class BoxWallpaper : FrameworkElement
{
    /// <summary>The theme of the party, which is not a box.</summary>
    public const int PartyTheme = -1;

    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme), typeof(int), typeof(BoxWallpaper),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Where a floor shadow sits, as a fraction of the height. Zero for none.</summary>
    public static readonly DependencyProperty FloorAtProperty = DependencyProperty.Register(
        nameof(FloorAt), typeof(double), typeof(BoxWallpaper),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public BoxWallpaper()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public int Theme
    {
        get => (int)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public double FloorAt
    {
        get => (double)GetValue(FloorAtProperty);
        set => SetValue(FloorAtProperty, value);
    }

    /// <summary>How many box themes there are; boxes past the last one start again from the first.</summary>
    public static int Count => Themes.Length;

    /// <summary>The theme a box gets, by its number as the game counts them from one. <see cref="Theme"/> takes that number.</summary>
    public static int ForBox(int number) => ((Math.Max(1, number) - 1) % Themes.Length + Themes.Length) % Themes.Length;

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var cell = ToastPixels.Cell(this);
        var columns = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX / cell);
        var rows = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY / cell);

        if (columns < 6 || rows < 6)
        {
            return;
        }

        var theme = Theme == PartyTheme ? Party : Themes[ForBox(Theme)];
        var canvas = new ToastPixels.Canvas(columns, rows);

        Ground(canvas, theme);

        for (var j = 0; ; j++)
        {
            var y = theme.OffsetY + (j * theme.PitchY);

            if (y >= rows)
            {
                break;
            }

            var shift = j % 2 == 1 ? theme.PitchX / 2 : 0;

            for (var i = -1; ; i++)
            {
                var x = theme.OffsetX + shift + (i * theme.PitchX);

                if (x >= columns)
                {
                    break;
                }

                var motif = theme.Motifs[Math.Abs((i * 7) + (j * 13)) % theme.Motifs.Length];

                if (y + motif.Length <= theme.MotifFloor(rows))
                {
                    canvas.Sprite(motif, x, y, theme.Key);
                }
            }
        }

        if (FloorAt > 0)
        {
            Floor(canvas, columns, rows, theme.Ground);
        }

        Frame(canvas, columns, rows, theme.Ground);

        ToastPixels.Draw(drawingContext, this, canvas);
    }

    private static void Ground(ToastPixels.Canvas canvas, Wallpaper theme)
    {
        var seam = theme.SecondGround is null ? canvas.Rows : (int)(canvas.Rows * theme.SeamAt);

        for (var y = 0; y < canvas.Rows; y++)
        {
            for (var x = 0; x < canvas.Columns; x++)
            {
                var colour = theme.Ground;

                if (theme.SecondGround is { } second)
                {
                    // Dos filas de trama a la altura de la costura, y en ningún otro sitio.
                    var below = y > seam || (y == seam && (x % 2 == 0)) || (y == seam - 1 && (x % 4 == 1));
                    colour = below ? second : theme.Ground;
                }

                canvas.Put(x, y, colour);
            }
        }
    }

    /// <summary>A stepped oval of shade for something to stand on.</summary>
    private void Floor(ToastPixels.Canvas canvas, int columns, int rows, Color ground)
    {
        var centre = columns / 2;
        var y = (int)(rows * FloorAt);
        var half = Math.Min(columns / 4, 14);
        int[] widths = [half - 4, half - 1, half, half, half - 1, half - 4];

        for (var row = 0; row < widths.Length; row++)
        {
            for (var x = centre - widths[row]; x < centre + widths[row]; x++)
            {
                canvas.Put(x, y + row - 2, ToastPixels.Mix(ground, Colors.Black, 0.45));
            }
        }
    }

    /// <summary>A one-cell ink outline with notched corners and a dark lip along the top, so it sits sunk in.</summary>
    private static void Frame(ToastPixels.Canvas canvas, int columns, int rows, Color ground)
    {
        for (var x = 0; x < columns; x++)
        {
            canvas.Put(x, 0, ToastPixels.Ink);
            canvas.Put(x, rows - 1, ToastPixels.Ink);
            canvas.Put(x, 1, ToastPixels.Mix(ground, Colors.Black, 0.5));
        }

        for (var y = 0; y < rows; y++)
        {
            canvas.Put(0, y, ToastPixels.Ink);
            canvas.Put(columns - 1, y, ToastPixels.Ink);
            canvas.Put(1, y, ToastPixels.Mix(ground, Colors.Black, 0.3));
        }

        foreach (var (x, y) in new[] { (0, 0), (columns - 1, 0), (0, rows - 1), (columns - 1, rows - 1) })
        {
            canvas.Put(x, y, Colors.Transparent, 0);
        }
    }

    // ================================================================== LOS FONDOS

    private sealed record Wallpaper(
        string Name,
        Color Ground,
        Dictionary<char, Color> Key,
        string[][] Motifs,
        int PitchX,
        int PitchY,
        int OffsetX = 2,
        int OffsetY = 2,
        Color? SecondGround = null,
        double SeamAt = 1)
    {
        /// <summary>Motifs stay above the seam of a two-ground theme, where their own ground is.</summary>
        public int MotifFloor(int rows) => SecondGround is null ? int.MaxValue : (int)(rows * SeamAt) - 1;
    }

    private static Color C(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static Dictionary<char, Color> Keys(uint a, uint b, uint c = 0x000000) =>
        new() { ['a'] = C(a), ['b'] = C(b), ['c'] = C(c) };

    private static readonly Wallpaper Party = new("EQUIPO", C(0x211A2C), Keys(0x4E2432, 0x352E44, 0x120E1A),
    [
        [
            "..aaa..",
            ".aaaaa.",
            "aaaaaaa",
            "cccbccc",
            "bbbbbbb",
            ".bbbbb.",
            "..bbb.."
        ]
    ], 12, 11, 3, 2);

    private static readonly Wallpaper[] Themes =
    [
        new("BOSQUE", C(0x1C3325), Keys(0x2E5A3A, 0x244A30, 0x3A2E22),
        [
            [
                "..a..",
                ".aab.",
                "aaabb",
                ".abb.",
                "..c.."
            ],
            [
                ".a.",
                "aab",
                ".c."
            ]
        ], 10, 9),

        new("CIUDAD", C(0x2B2733), Keys(0x3A3544, 0x221F29, 0x332F3C),
        [
            [
                "aaaaaaab",
                "cccccccb",
                "cccccccb",
                "bbbbbbbb"
            ]
        ], 8, 4, 0, 0),

        new("DESIERTO", C(0x4F3D24), Keys(0x5E4A2C, 0x3C5230, 0x45351F),
        [
            [
                "....aaaa....",
                "..aa....aa..",
                "aa........aa"
            ],
            [
                "....aaaa....",
                "..aa....aa..",
                "aa........aa",
                ".....b......",
                "....bbb.....",
                ".....b......"
            ]
        ], 12, 9, 0, 3),

        new("SABANA", C(0x444020), Keys(0x57522A, 0x3A3619, 0x615B2E),
        [
            [
                "a.a.a",
                ".aaa.",
                "..b.."
            ],
            [
                ".c.",
                "cac"
            ]
        ], 9, 7),

        new("ROCAS", C(0x342D29), Keys(0x463D37, 0x261F1C, 0x3D3530),
        [
            [
                ".aa.",
                "acca",
                ".cc."
            ],
            [
                "b...",
                ".b..",
                ".bb.",
                "...b"
            ]
        ], 11, 9),

        new("VOLCÁN", C(0x331B1C), Keys(0x6E2A1D, 0x9A421F, 0x271415),
        [
            [
                "a....",
                ".a...",
                ".aa..",
                "...a.",
                "...ab"
            ],
            [
                ".cc.",
                "cccc"
            ]
        ], 12, 10),

        new("NIEVE", C(0x27323F), Keys(0x4A5D78, 0x36445A, 0x3E4E66),
        [
            [
                "..a..",
                "a.a.a",
                ".aaa.",
                "a.a.a",
                "..a.."
            ],
            [
                "b"
            ]
        ], 13, 11),

        new("CUEVA", C(0x201C2A), Keys(0x463B66, 0x5D5090, 0x2C2738),
        [
            [
                "..a..",
                ".aab.",
                ".aab.",
                "aaabb"
            ],
            [
                "cc.",
                "ccc"
            ]
        ], 13, 10),

        new("PLAYA", C(0x1D3C4A), Keys(0x2B586A, 0x24495A, 0x000000),
        [
            [
                ".aa...",
                "a..a.."
            ]
        ], 8, 5, 1, 2, C(0x55462A), 0.72),

        new("FONDO MARINO", C(0x142438), Keys(0x2A4668, 0x1C3A34, 0x223A58),
        [
            [
                ".aa.",
                "a..a",
                "a..a",
                ".aa."
            ],
            [
                "c"
            ],
            [
                ".b",
                "b.",
                ".b",
                "b."
            ]
        ], 10, 9),

        new("CIELO", C(0x223A5C), Keys(0x31507A, 0x2A466C, 0x000000),
        [
            [
                "...aa.....",
                ".aaaaaa...",
                "aaaaaaaaaa",
                ".bbbbbbbb."
            ]
        ], 16, 9),

        new("NOCHE", C(0x13122A), Keys(0x4E4C84, 0x7C78B4, 0x2A2850),
        [
            [
                "a"
            ],
            [
                ".b.",
                "bab",
                ".b."
            ],
            [
                "c"
            ]
        ], 7, 6)
    ];
}
