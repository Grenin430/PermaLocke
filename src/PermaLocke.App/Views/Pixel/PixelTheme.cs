using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PermaLocke.App.Views.Pixel;

/// <summary>How a <see cref="PixelPanel"/> draws its edge.</summary>
public enum PanelStyle
{
    /// <summary>The original: ink outline, a bevel lit on top, notched corners and a hard shadow.</summary>
    Bevel,

    /// <summary>The menus of the handhelds of 2002: round corners, a dark line and a light one inside it, no shadow.</summary>
    Rounded,

    /// <summary>The first Game Boy: a double ink line with the fill between, square corners, a one-cell shadow.</summary>
    Heavy,

    /// <summary>A neon tube round a dark glass: one bright line, brighter corners, a dithered glow just inside.</summary>
    Neon,

    /// <summary>A screen set in a device: a pale bezel two cells wide, lit on top, round the fill.</summary>
    Device
}

/// <summary>What <see cref="PixelBackdrop"/> paints under the screens.</summary>
public enum BackdropStyle
{
    Lattice,
    Stripes,
    LcdGrid,
    Stars,
    Grille
}

/// <summary>Where the navigation lives and how the frame of the window is composed (2026-10-01).</summary>
public enum ShellKind
{
    /// <summary>The original: a wide side rail with the names, the sky of Alola over the header.</summary>
    Rail,

    /// <summary>Pocket tabs along the top, a second row of tabs for a group, and a dialogue box along the bottom.</summary>
    Tabs,

    /// <summary>No permanent navigation: a start menu of tiles, and every screen has a back bar with where you are.</summary>
    Menu,

    /// <summary>A floating cockpit: points and actions as a HUD on top, a dock of icons along the bottom.</summary>
    Dock,

    /// <summary>A device: a red body with a column of icon keys, one big screen, and soft keys for the pages underneath.</summary>
    Keys
}

/// <summary>How a <see cref="PixelWindow"/> carries its title.</summary>
public enum WindowKind
{
    /// <summary>A full band across the top, in its own colour.</summary>
    Band,

    /// <summary>A small label chip at the top left, inside the box.</summary>
    Tag,

    /// <summary>The title centred between two rules, no band.</summary>
    Plain,

    /// <summary>A bar of accent, the title, and a hairline running out to the right.</summary>
    Hud
}

/// <summary>
/// A whole look for the pixel interface (2026-10-01, the organiser asked for several to choose from): the colours of
/// <c>Themes/PixelColours.xaml</c>, how panels draw their edge, what the floor under the screens is, how a window
/// carries its title, where the navigation lives, and for the Game Boy, the four greens everything is redrawn in.
/// </summary>
/// <remarks>
/// <para>
/// Applied live: the colours are replaced as one dictionary, every style reads them through <c>DynamicResource</c>, and
/// the custom controls that draw themselves in cells are asked to draw again. The window swaps its shell when the look
/// asks for another one, keeping the screen the player was on.
/// </para>
/// <para>
/// The scenes (gacha, cemetery, album, the trainer's room) keep their own colours: they are places, not interface. Only
/// the Game Boy redraws them, because its whole point is that nothing leaves the four greens.
/// </para>
/// </remarks>
public sealed record PixelTheme(
    string Key,
    string Name,
    string Tagline,
    string About,
    IReadOnlyDictionary<string, Color> Colours,
    PanelStyle Panels,
    BackdropStyle Backdrop,
    ShellKind Shell,
    WindowKind Windows,
    Color[]? Shades = null)
{
    // Arriba del todo: los inicializadores estáticos van en orden de texto y All los usa.
    private static readonly Color[] Dmg = [C(0xFF0F380F), C(0xFF306230), C(0xFF8BAC0F), C(0xFF9BBC0F)];
    private static readonly PixelTheme Original = Classic();

    /// <summary>The look in use. The original until <see cref="Apply"/> says otherwise.</summary>
    public static PixelTheme Current { get; private set; } = Original;

    /// <summary>The player asked Windows for no animations: what bobs, breathes or flashes stays still instead.</summary>
    public static bool ReducedMotion => !SystemParameters.ClientAreaAnimation;

    /// <summary>Raised after a look has been applied and the pixel controls have been asked to draw again.</summary>
    public static event Action? Changed;

    public Color this[string key] => Colours.TryGetValue(key, out var colour) ? colour : Original.Colours[key];

    /// <summary>Outlines: of panels, bars and buttons.</summary>
    public Color Ink => this["PxInk"];

    /// <summary>What an icon dims towards when its section is not the one on screen.</summary>
    public Color Dim => this["PxDim"];

    /// <summary>The second line of a <see cref="PanelStyle.Rounded"/> or <see cref="PanelStyle.Device"/> edge, and the
    /// corners of a <see cref="PanelStyle.Neon"/> one.</summary>
    public Color Rim => this["PxRim"];

    /// <summary>How many cells thick the edge of a window is: where its title band starts, and so how far down its title goes.</summary>
    public int Inset => Panels switch
    {
        PanelStyle.Rounded => 2,
        PanelStyle.Heavy => 3,
        PanelStyle.Device => 4,
        _ => 1
    };

    public Color RuleDark => this["PxRuleDark"];

    public Color RuleLight => this["PxRuleLight"];

    /// <summary>
    /// Every colour drawn in cells turned into one of the theme's shades, by brightness: the Game Boy's four greens. Null
    /// keeps colours as they are.
    /// </summary>
    /// <param name="scene">For the scenes, which are almost all dark: the brightness is lifted first so they keep detail.</param>
    public Color Map(Color colour, bool scene = false)
    {
        if (Shades is not { Length: > 0 } shades) return colour;

        // Un tono que ya es de la paleta se queda: así pasar dos veces por aquí no oscurece nada.
        foreach (var shade in shades)
        {
            if (shade.R == colour.R && shade.G == colour.G && shade.B == colour.B) return shade;
        }

        var luma = ((colour.R * 0.299) + (colour.G * 0.587) + (colour.B * 0.114)) / 255.0;
        // Con raíz: las escenas son casi todas tonos oscuros, y a tramos iguales se iban enteras al verde más negro.
        return shades[Math.Clamp((int)((scene ? Math.Sqrt(luma) : luma) * shades.Length), 0, shades.Length - 1)];
    }

    /// <summary>The same over a whole BGRA frame of a scene, in place; nothing for a theme without shades.</summary>
    public void MapPixels(byte[] bgra)
    {
        if (Shades is null) return;

        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            if (bgra[i + 3] == 0) continue;
            var mapped = Map(Color.FromRgb(bgra[i + 2], bgra[i + 1], bgra[i]), scene: true);
            bgra[i] = mapped.B;
            bgra[i + 1] = mapped.G;
            bgra[i + 2] = mapped.R;
        }
    }

    public static IReadOnlyList<PixelTheme> All { get; } = [Original, Emerald(), GameBoy(), UltraWormhole(), Rotom()];

    public static PixelTheme Find(string? key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Original;

    private static readonly (string Brush, string Colour)[] Brushes =
    [
        ("PxBaseBrush", "PxBase"), ("PxSidebarBrush", "PxSidebar"), ("PxInkBrush", "PxInk"),
        ("PxAccentDeepBrush", "PxAccentDeep"), ("PxAccentBrush", "PxAccent"), ("PxTextBrush", "PxText"),
        ("PxWellBrush", "PxWell"), ("PxAccentLightBrush", "PxAccentLight")
    ];

    /// <summary>
    /// Puts a look in place, now: its colours replace the dictionary of the pixel colours as one piece, so every
    /// <c>DynamicResource</c> in the application changes at once, and the controls that draw themselves are asked to
    /// draw again. Unknown keys keep the original.
    /// </summary>
    public static void Apply(string? key, ResourceDictionary application)
    {
        var theme = Find(key);
        Current = theme;

        var fresh = new ResourceDictionary();
        foreach (var (name, colour) in theme.Colours) fresh[name] = colour;
        foreach (var (brush, colour) in Brushes)
        {
            var made = new SolidColorBrush(theme[colour]);
            made.Freeze();
            fresh[brush] = made;
        }

        if (FindColours(application) is { } found)
        {
            found.Parent.MergedDictionaries[found.Index] = fresh;
        }
        else
        {
            foreach (var entry in fresh.Keys) application[entry] = fresh[entry];
        }

        // Cuando WPF ya ha repartido los recursos nuevos: lo que se pinta en celdas no los lee, se pinta otra vez.
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            foreach (Window window in Application.Current.Windows) Refresh(window);
            Changed?.Invoke();
        });
    }

    private static (ResourceDictionary Parent, int Index)? FindColours(ResourceDictionary dictionary)
    {
        for (var i = 0; i < dictionary.MergedDictionaries.Count; i++)
        {
            var merged = dictionary.MergedDictionaries[i];
            // Solo las claves propias: Contains también mira las fusionadas.
            if (merged.Keys.Cast<object>().Contains("PxInk")) return (dictionary, i);
            if (FindColours(merged) is { } deeper) return deeper;
        }

        return null;
    }

    private static void Refresh(DependencyObject visual)
    {
        if (visual is PixelSprite sprite) sprite.Rebake();
        if (visual is PixelWindow window) window.Restyle();
        if (visual is FrameworkElement element) element.InvalidateVisual();

        var count = visual is Visual ? VisualTreeHelper.GetChildrenCount(visual) : 0;
        for (var i = 0; i < count; i++) Refresh(VisualTreeHelper.GetChild(visual, i));
    }

    private static Color C(uint argb) =>
        Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    /// <summary>The violet night PermaLocke has worn since §176.</summary>
    private static PixelTheme Classic() => new("clasico", "CLÁSICO", "La noche violeta de siempre",
        "Barra lateral con los nombres a la vista, cielo de Alola sobre la cabecera y cada ventana con su banda de título. El diseño original, para volver cuando quieras.",
        new Dictionary<string, Color>
        {
            ["PxInk"] = C(0xFF09070E), ["PxShadow"] = C(0xFF05040A), ["PxSidebar"] = C(0xFF0E0B18), ["PxBase"] = C(0xFF120E20),
            ["PxFace"] = C(0xFF18132C), ["PxFaceHigh"] = C(0xFF211A3A), ["PxFaceLift"] = C(0xFF2C2349), ["PxWell"] = C(0xFF0C0916),
            ["PxWellFocus"] = C(0xFF120D22), ["PxAccent"] = C(0xFFB07BF0), ["PxAccentLight"] = C(0xFFD2ADFF),
            ["PxAccentDeep"] = C(0xFF3A2766), ["PxOnAccent"] = C(0xFF09070E), ["PxText"] = C(0xFFEEEAF8),
            ["PxTextDim"] = C(0xFF9A94B4), ["PxTextFaint"] = C(0xFF625C80), ["PxGood"] = C(0xFF7CF096), ["PxGoodDeep"] = C(0xFF1C4428),
            ["PxBad"] = C(0xFFFF6A55), ["PxBadDeep"] = C(0xFF4A1818), ["PxBadHover"] = C(0xFF6A2222), ["PxWarn"] = C(0xFFFFC04A),
            ["PxWarnDeep"] = C(0xFF4A3310), ["PxGold"] = C(0xFFFFD24A), ["PxGlass"] = C(0xC8120E20), ["PxDim"] = C(0xFF2A2246),
            ["PxRim"] = C(0xFF2E254C), ["PxRuleDark"] = C(0xFF05040A), ["PxRuleLight"] = C(0xFF2A2246), ["PxCursor"] = C(0xFFEEEAF8),
            ["PxRunning"] = C(0xFF2E6B45), ["PxRunningText"] = C(0xFFE4FFEC), ["PxRunningGlyph"] = C(0xFF8EF0AE),
            ["PxAccentInk"] = C(0xFFB07BF0), ["PxAccentInkLight"] = C(0xFFD2ADFF)
        }, PanelStyle.Bevel, BackdropStyle.Lattice, ShellKind.Rail, WindowKind.Band);

    /// <summary>
    /// ESMERALDA: the menus of the Game Boy Advance games — white boxes with round corners and a double edge, dark text
    /// over a teal floor of diagonal stripes, pocket tabs along the top like the bag, and the dialogue box along the bottom.
    /// </summary>
    private static PixelTheme Emerald() => new("esmeralda", "ESMERALDA", "Pestañas de mochila",
        "Claro y ordenado, como los menús de Game Boy Advance. La navegación son pestañas arriba, como los bolsillos de la mochila; las páginas de un grupo van en una segunda fila, y lo que el juego necesita se dice en el cuadro de diálogo de abajo.",
        new Dictionary<string, Color>
        {
            ["PxInk"] = C(0xFF29314A), ["PxShadow"] = C(0xFFD3D7CF), ["PxSidebar"] = C(0xFFF4F4EC), ["PxBase"] = C(0xFF8FD0C0),
            ["PxFace"] = C(0xFFFBFBF6), ["PxFaceHigh"] = C(0xFFB9DAF6), ["PxFaceLift"] = C(0xFFE3EBF2), ["PxWell"] = C(0xFFE3E7DF),
            ["PxWellFocus"] = C(0xFFEFF5FA), ["PxAccent"] = C(0xFFF2803E), ["PxAccentLight"] = C(0xFFF8A468),
            ["PxAccentDeep"] = C(0xFFC9E2F8), ["PxOnAccent"] = C(0xFF29314A), ["PxText"] = C(0xFF333B45),
            ["PxTextDim"] = C(0xFF455060), ["PxTextFaint"] = C(0xFF5C6877), ["PxGood"] = C(0xFF1F7A38), ["PxGoodDeep"] = C(0xFFD3EFD6),
            ["PxBad"] = C(0xFFD43A2A), ["PxBadDeep"] = C(0xFFF8DAD3), ["PxBadHover"] = C(0xFFF3BFB4), ["PxWarn"] = C(0xFFB5710F),
            ["PxWarnDeep"] = C(0xFFF6E7C6), ["PxGold"] = C(0xFF9A7208), ["PxGlass"] = C(0xE6FBFBF6), ["PxDim"] = C(0xFFCFCFC6),
            ["PxRim"] = C(0xFF8DB4DC), ["PxRuleDark"] = C(0xFF8DB4DC), ["PxRuleLight"] = C(0xFFFFFFFF), ["PxCursor"] = C(0xFFE04030),
            ["PxRunning"] = C(0xFFD3EFD6), ["PxRunningText"] = C(0xFF1F6B34), ["PxRunningGlyph"] = C(0xFF1F7A38),
            ["PxAccentInk"] = C(0xFFB44F0B), ["PxAccentInkLight"] = C(0xFFC25A12)
        }, PanelStyle.Rounded, BackdropStyle.Stripes, ShellKind.Tabs, WindowKind.Tag);

    /// <summary>
    /// GAME BOY: four greens and nothing else, the first screen most of us saw a Pokémon on. Every cell the interface
    /// draws — icons and sprites too — is redrawn in them by brightness. No permanent navigation: a start menu.
    /// </summary>
    private static PixelTheme GameBoy() => new("gameboy", "GAME BOY", "Menú de inicio",
        "Cuatro verdes y nada más, como la primera Game Boy: iconos, Pokémon y escenas incluidos. No hay barra: el menú de inicio es una cuadrícula y cada pantalla trae su botón ◀ MENÚ con el sitio donde estás, para llevar la atención solo a lo que haces.",
        new Dictionary<string, Color>
        {
            ["PxInk"] = Dmg[0], ["PxShadow"] = Dmg[2], ["PxSidebar"] = Dmg[3], ["PxBase"] = Dmg[2],
            ["PxFace"] = Dmg[3], ["PxFaceHigh"] = Dmg[2], ["PxFaceLift"] = Dmg[2], ["PxWell"] = Dmg[2],
            ["PxWellFocus"] = Dmg[3], ["PxAccent"] = Dmg[0], ["PxAccentLight"] = Dmg[0],
            ["PxAccentDeep"] = Dmg[2], ["PxOnAccent"] = Dmg[3], ["PxText"] = Dmg[0],
            ["PxTextDim"] = Dmg[0], ["PxTextFaint"] = Dmg[1], ["PxGood"] = Dmg[0], ["PxGoodDeep"] = Dmg[2],
            ["PxBad"] = Dmg[0], ["PxBadDeep"] = Dmg[2], ["PxBadHover"] = Dmg[1], ["PxWarn"] = Dmg[0],
            ["PxWarnDeep"] = Dmg[2], ["PxGold"] = Dmg[0], ["PxGlass"] = Dmg[3], ["PxDim"] = Dmg[2],
            ["PxRim"] = Dmg[1], ["PxRuleDark"] = Dmg[0], ["PxRuleLight"] = Dmg[3], ["PxCursor"] = Dmg[0],
            ["PxRunning"] = Dmg[0], ["PxRunningText"] = Dmg[3], ["PxRunningGlyph"] = Dmg[3],
            ["PxAccentInk"] = Dmg[0], ["PxAccentInkLight"] = Dmg[0]
        }, PanelStyle.Heavy, BackdropStyle.LcdGrid, ShellKind.Menu, WindowKind.Plain, Dmg);

    /// <summary>
    /// ULTRAUMBRAL: the inside of an Ultra Wormhole — black-blue glass, cyan tubes of light with magenta corners, and
    /// stars. The darkest of the five and the most electric.
    /// </summary>
    private static PixelTheme UltraWormhole() => new("ultraumbral", "ULTRAUMBRAL", "Cabina y muelle",
        "Un puesto de mando dentro de un Ultraumbral: tu estado (puntos, rol, regalos, jugar) flota arriba como un HUD, y la navegación es un muelle de iconos abajo donde solo la sección activa enseña su nombre. El contenido ocupa todo el espacio de en medio.",
        new Dictionary<string, Color>
        {
            ["PxInk"] = C(0xFF02030A), ["PxShadow"] = C(0xFF000000), ["PxSidebar"] = C(0xFF060A18), ["PxBase"] = C(0xFF040714),
            ["PxFace"] = C(0xFF0A1026), ["PxFaceHigh"] = C(0xFF101A3C), ["PxFaceLift"] = C(0xFF15224A), ["PxWell"] = C(0xFF03050E),
            ["PxWellFocus"] = C(0xFF0A1430), ["PxAccent"] = C(0xFF2EE6FF), ["PxAccentLight"] = C(0xFF9AF6FF),
            ["PxAccentDeep"] = C(0xFF0D3552), ["PxOnAccent"] = C(0xFF02030A), ["PxText"] = C(0xFFE6F4FF),
            ["PxTextDim"] = C(0xFF7FA2C8), ["PxTextFaint"] = C(0xFF5878A4), ["PxGood"] = C(0xFF3CFFA0), ["PxGoodDeep"] = C(0xFF0A3A2A),
            ["PxBad"] = C(0xFFFF4F7A), ["PxBadDeep"] = C(0xFF3E0E22), ["PxBadHover"] = C(0xFF5E1634), ["PxWarn"] = C(0xFFFFD23C),
            ["PxWarnDeep"] = C(0xFF3A2E08), ["PxGold"] = C(0xFFFFE45A), ["PxGlass"] = C(0xD0040714), ["PxDim"] = C(0xFF14204A),
            ["PxRim"] = C(0xFFFF4FD8), ["PxRuleDark"] = C(0xFF000000), ["PxRuleLight"] = C(0xFF1B6D86), ["PxCursor"] = C(0xFFFF4FD8),
            ["PxRunning"] = C(0xFF0A3A2A), ["PxRunningText"] = C(0xFF9AFFD0), ["PxRunningGlyph"] = C(0xFF3CFFA0),
            ["PxAccentInk"] = C(0xFF2EE6FF), ["PxAccentInkLight"] = C(0xFF9AF6FF)
        }, PanelStyle.Neon, BackdropStyle.Stars, ShellKind.Dock, WindowKind.Hud);

    /// <summary>
    /// ROTOM DEX: the Pokédex of Ultra Sun and Ultra Moon — a red body down the side, every window a dark teal screen in
    /// a pale bezel with a red title band, the charcoal of its speaker grille underneath, and electric blue for what is
    /// pressed.
    /// </summary>
    private static PixelTheme Rotom() => new("rotom", "ROTOM DEX", "La Pokédex",
        "Un aparato: cuerpo rojo con una columna de teclas de iconos a un lado, una pantalla grande con su banda de título y, debajo, las teclas de página del grupo. Lo que el juego necesita pasa como un rótulo luminoso por la parte baja de la pantalla.",
        new Dictionary<string, Color>
        {
            ["PxInk"] = C(0xFF1A0A0E), ["PxShadow"] = C(0xFF200608), ["PxSidebar"] = C(0xFFCF2036), ["PxBase"] = C(0xFF24272C),
            ["PxFace"] = C(0xFF15303A), ["PxFaceHigh"] = C(0xFFC21C31), ["PxFaceLift"] = C(0xFF214652), ["PxWell"] = C(0xFF0B1C22),
            ["PxWellFocus"] = C(0xFF12303A), ["PxAccent"] = C(0xFF3BB3FF), ["PxAccentLight"] = C(0xFF8FD6FF),
            ["PxAccentDeep"] = C(0xFF8E1426), ["PxOnAccent"] = C(0xFF1A0A0E), ["PxText"] = C(0xFFF4FBFB),
            ["PxTextDim"] = C(0xFFA6CCD3), ["PxTextFaint"] = C(0xFF7CA4AC), ["PxGood"] = C(0xFF6BF08A), ["PxGoodDeep"] = C(0xFF12402A),
            ["PxBad"] = C(0xFFFF6464), ["PxBadDeep"] = C(0xFF4A1218), ["PxBadHover"] = C(0xFF6A1A22), ["PxWarn"] = C(0xFFFFC83C),
            ["PxWarnDeep"] = C(0xFF4A3812), ["PxGold"] = C(0xFFFFD84A), ["PxGlass"] = C(0xD815303A), ["PxDim"] = C(0xFF7A1222),
            ["PxRim"] = C(0xFFDCE4E4), ["PxRuleDark"] = C(0xFF7A1020), ["PxRuleLight"] = C(0xFFF07080), ["PxCursor"] = C(0xFFFFD84A),
            ["PxRunning"] = C(0xFF12402A), ["PxRunningText"] = C(0xFFD4FFE0), ["PxRunningGlyph"] = C(0xFF6BF08A),
            ["PxAccentInk"] = C(0xFF3BB3FF), ["PxAccentInkLight"] = C(0xFF8FD6FF)
        }, PanelStyle.Device, BackdropStyle.Grille, ShellKind.Keys, WindowKind.Band);
}
