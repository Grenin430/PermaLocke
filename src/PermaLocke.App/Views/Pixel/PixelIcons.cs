using System.Windows.Media;

namespace PermaLocke.App.Views.Pixel;

/// <summary>
/// The sections' icons as pixel art: twelve by twelve cells, in colour, drawn by hand.
/// </summary>
/// <remarks>
/// <para>
/// Keyed by the same names as the vector icons (<c>IconHome</c>, <c>IconGacha</c>…), so a section keeps saying which
/// icon it wants in one place and both styles understand it (§176).
/// </para>
/// <para>
/// In colour on purpose, like the icons of a game's menu: a Poké Ball for the gacha, a trophy for the achievements,
/// a gravestone for the cemetery. The vector icons were one flat colour because at eighteen pixels a line blurs; a
/// pixel drawing has no line to blur.
/// </para>
/// </remarks>
public static class PixelIcons
{
    public const int Size = 12;

    /// <summary>The colours every icon draws from, by the letter the drawings use.</summary>
    private static readonly Dictionary<char, Color> Key = new()
    {
        ['o'] = Color.FromRgb(0x09, 0x07, 0x0E),
        ['w'] = Color.FromRgb(0xF4, 0xF0, 0xFC),
        ['W'] = Color.FromRgb(0xB8, 0xB2, 0xC8),
        ['k'] = Color.FromRgb(0x5C, 0x56, 0x7A),
        ['K'] = Color.FromRgb(0x36, 0x31, 0x4A),
        ['r'] = Color.FromRgb(0xE8, 0x4A, 0x3C),
        ['R'] = Color.FromRgb(0x9C, 0x2A, 0x28),
        ['g'] = Color.FromRgb(0xFF, 0xD2, 0x4A),
        ['G'] = Color.FromRgb(0xB0, 0x7A, 0x1C),
        ['y'] = Color.FromRgb(0xFF, 0xF0, 0xB0),
        ['b'] = Color.FromRgb(0x5E, 0x9C, 0xF0),
        ['B'] = Color.FromRgb(0x2E, 0x5C, 0xB0),
        ['e'] = Color.FromRgb(0x5C, 0xCC, 0x74),
        ['E'] = Color.FromRgb(0x2E, 0x7A, 0x40),
        ['v'] = Color.FromRgb(0xC0, 0x8C, 0xFF),
        ['V'] = Color.FromRgb(0x72, 0x44, 0xB8),
        ['n'] = Color.FromRgb(0xB0, 0x70, 0x38),
        ['N'] = Color.FromRgb(0x6A, 0x3C, 0x18),
        ['c'] = Color.FromRgb(0x9C, 0xF0, 0xF8),
        ['p'] = Color.FromRgb(0xF4, 0x8C, 0xC8),
    };

    private static readonly Dictionary<string, string[]> Drawings = new()
    {
        // Una consola portátil con el triángulo de jugar en la pantalla.
        ["IconPlay"] =
        [
            ".oooooooooo.",
            ".okkkkkkkko.",
            ".okwwwwwwko.",
            ".okwvwwwwko.",
            ".okwvvwwwko.",
            ".okwvvvwwko.",
            ".okwvvwwwko.",
            ".okwvwwwwko.",
            ".okwwwwwwko.",
            ".okkkkkkkko.",
            ".okrkkkkgko.",
            ".oooooooooo.",
        ],

        // Una casa: tejado rojo, puerta y ventana.
        ["IconHome"] =
        [
            ".....oo.....",
            "....orro....",
            "...orrrro...",
            "..orrrrrro..",
            ".orrrrrrRRo.",
            "oooooooooooo",
            ".owwwwwwwWo.",
            ".ownnwwbbWo.",
            ".ownnwwbbWo.",
            ".ownnwwwwWo.",
            ".ownNwwwwWo.",
            ".oooooooooo.",
        ],

        // Un dado enseñando un cinco.
        ["IconRandomizer"] =
        [
            ".oooooooooo.",
            ".owwwwwwwWo.",
            ".owvvwwvvWo.",
            ".owvvwwvvWo.",
            ".owwwwwwwWo.",
            ".owwwvvwwWo.",
            ".owwwvvwwWo.",
            ".owwwwwwwWo.",
            ".owvvwwvvWo.",
            ".owvvwwvvWo.",
            ".oWWWWWWWWo.",
            ".oooooooooo.",
        ],

        // Una Poké Ball.
        ["IconGacha"] =
        [
            "....oooo....",
            "..oorrrroo..",
            ".orrrrrrrro.",
            ".orwrrrrrRo.",
            "orwrrrrrrRRo",
            "ooooowwooooo",
            "owwwowwowwWo",
            "owwwwoowwwWo",
            ".owwwwwwwWo.",
            ".owwwwwwWWo.",
            "..oowwwWoo..",
            "....oooo....",
        ],

        // Una tienda con su toldo a rayas.
        ["IconShop"] =
        [
            "............",
            "oooooooooooo",
            "orwrwrwrwrwo",
            "orwrwrwrwrwo",
            "oooooooooooo",
            ".owwwwwwwWo.",
            ".obbwwwnnWo.",
            ".obbwwwnnWo.",
            ".owwwwwnnWo.",
            ".owwwwwnNWo.",
            ".oooooooooo.",
            "............",
        ],

        // Una copa.
        ["IconTrophy"] =
        [
            "..oooooooo..",
            "oogyggggGGoo",
            "ogoyggggGoGo",
            "ogoyggggGoGo",
            ".oogygggGoo.",
            "...oyggGo...",
            "....oggo....",
            ".....oo.....",
            "....oGGo....",
            "...ogggGo...",
            "..oooooooo..",
            "............",
        ],

        // Un mapa con su río, sus bosques y una marca.
        ["IconGrid"] =
        [
            "............",
            ".oooooooooo.",
            ".oyyeeyyyyo.",
            ".oyeeeyyryo.",
            ".oyyeyyrrro.",
            ".obyyyyyryo.",
            ".obbyyyyyyo.",
            ".oybbyyeeyo.",
            ".oyybbyeeyo.",
            ".oyyybbyyyo.",
            ".oooooooooo.",
            "............",
        ],

        // Una pesa.
        ["IconDumbbell"] =
        [
            "............",
            "............",
            ".oo......oo.",
            "oWko....oWko",
            "oWkooooooWko",
            "oWkWWWWWWWko",
            "oWkkkkkkkkko",
            "oWkooooooWko",
            "oWko....oWko",
            ".oo......oo.",
            "............",
            "............",
        ],

        // Un disco de MT.
        ["IconRefresh"] =
        [
            "...oooooo...",
            "..obbbbbbo..",
            ".obcbbbbbbo.",
            "obcbbbbbbbbo",
            "obbbbooobbbo",
            "obbbowwwobbo",
            "obbbowwwobbo",
            "obbbbooobbbo",
            "obbbbbbbbBbo",
            ".obbbbbbBBo.",
            "..obbbbBBo..",
            "...oooooo...",
        ],

        // Una tablilla con su hoja.
        ["IconDocument"] =
        [
            "....oooo....",
            ".oooWWWWooo.",
            ".onnoWWonno.",
            ".onwwwwwwno.",
            ".onwkkkkwno.",
            ".onwwwwwwno.",
            ".onwkkkkwno.",
            ".onwwwwwwno.",
            ".onwkkwwwno.",
            ".onwwwwwwno.",
            ".onnnnnnnno.",
            ".oooooooooo.",
        ],

        // Tres barras de colores.
        ["IconChart"] =
        [
            "............",
            "........ooo.",
            "........oeo.",
            "....ooo.oeo.",
            "....obo.oeo.",
            "....obo.oeo.",
            "ooo.obo.oeo.",
            "oro.obo.oeo.",
            "oro.obo.oeo.",
            "oro.obo.oEo.",
            "oooooooooooo",
            "............",
        ],

        // Una lápida sobre la hierba.
        ["IconGrave"] =
        [
            "....oooo....",
            "..ooWWWWoo..",
            ".oWWWWWWkWo.",
            ".oWWWkWWWko.",
            ".oWWkkkWWko.",
            ".oWWWkWWWko.",
            ".oWWWkWWWko.",
            ".oWWWWWWWko.",
            ".oWWWWWWkko.",
            "oooooooooooo",
            "oeeEeeeEeeeo",
            "oooooooooooo",
        ],

        // Dos espadas cruzadas.
        ["IconSwords"] =
        [
            "oo........oo",
            "oWo......oWo",
            ".oWo....oWo.",
            "..oWo..oWo..",
            "...oWooWo...",
            "....oWWo....",
            "...oWooWo...",
            "..ononnono..",
            ".ono....ono.",
            "ono......ono",
            "oo........oo",
            "............",
        ],

        // Una ruleta de cuatro cuñas, verde y roja, con su buje dorado.
        ["IconWheel"] =
        [
            "....oooo....",
            "..oorreeoo..",
            ".orrrreeeeo.",
            ".orrrreeeeo.",
            "orrrrrgeeeeo",
            "orrrrgGeeeeo",
            "oeeeeGgrrrro",
            "oeeeeegrrrro",
            ".oeeeerrrro.",
            ".oeeeerrrro.",
            "..ooeerroo..",
            "....oooo....",
        ],

        // Una caja de herramientas.
        ["IconTools"] =
        [
            "............",
            "....oooo....",
            "....o..o....",
            ".oooooooooo.",
            ".orrrrrrrro.",
            ".oRRRRRRRRo.",
            ".orrrggrrro.",
            ".orrrggrrro.",
            ".orrrrrrrro.",
            ".oRRRRRRRRo.",
            ".oooooooooo.",
            "............",
        ],

        // Una casilla marcada.
        ["IconCheck"] =
        [
            "............",
            ".oooooooooo.",
            ".oKKKKKKKKo.",
            ".oKKKKKKeKo.",
            ".oKKKKKeeKo.",
            ".oKeKKeeKKo.",
            ".oKeeeeeKKo.",
            ".oKKeeeKKKo.",
            ".oKKKeKKKKo.",
            ".oKKKKKKKKo.",
            ".oooooooooo.",
            "............",
        ],

        // Dos jugadores.
        ["IconPeople"] =
        [
            "............",
            "..ooo..ooo..",
            ".ovvvo.obbbo",
            ".ovvvo.obbbo",
            "..ooo...ooo.",
            ".ooooo.oooo.",
            "ovvvvvobbbbo",
            "ovvvvvobbbbo",
            "ovvvvvobbbbo",
            "oVVVVVoBBBBo",
            "oooooooooooo",
            "............",
        ],

        // Un regalo.
        ["IconGift"] =
        [
            "..oo....oo..",
            ".ogGo..oGgo.",
            "..ooGooGoo..",
            "oooooooooooo",
            "orrrrggrrrro",
            "oRRRRggRRRRo",
            "oooooooooooo",
            ".orrrggrrro.",
            ".orrrggrrro.",
            ".orrrggrrro.",
            ".oRRRggRRRo.",
            ".oooooooooo.",
        ],

        // El rombo de los puntos.
        ["IconPoints"] =
        [
            ".....oo.....",
            "....ovvo....",
            "...ovwvvo...",
            "..ovwvvvvo..",
            ".ovwvvvvvVo.",
            "ovvvvvvvvVVo",
            "ovvvvvvvVVVo",
            ".ovvvvvvVVo.",
            "..ovvvvVVo..",
            "...ovvVVo...",
            "....oVVo....",
            ".....oo.....",
        ],

        // La estrella de los variocolor, dorada y con brillo, como la del resumen del juego.
        ["IconStar"] =
        [
            ".....oo.....",
            "....oyyo....",
            "....oygo....",
            "ooooyygGoooo",
            "oyyyyggggGGo",
            ".oyggggggGo.",
            "..ogggggGo..",
            "..oggggGGo..",
            ".oggGooGGGo.",
            ".ogGo..oGGo.",
            "oGGo....oGGo",
            "ooo......ooo",
        ],

        // Una marca blanca: hecho, para ir encima de un color.
        ["IconTick"] =
        [
            "............",
            "............",
            ".........oo.",
            "........owwo",
            ".......owwo.",
            ".oo...owwo..",
            "owwo.owwo...",
            ".owwowwo....",
            "..owwwo.....",
            "...owo......",
            "....o.......",
            "............",
        ],

        // Una flecha que sale hacia arriba a la derecha: se escapó.
        ["IconAway"] =
        [
            "............",
            "....ooooooo.",
            "....owwwwwo.",
            ".....oowwwo.",
            "....owwwwwo.",
            "...owwwoowo.",
            "..owwwo.owo.",
            ".owwwo..oo..",
            "owwwo.......",
            "owwo........",
            ".oo.........",
            "............",
        ],

        // Una cruz roja: caído.
        ["IconCross"] =
        [
            "............",
            ".oo......oo.",
            "orro....orro",
            "orrro..orrRo",
            ".orrrooorRo.",
            "..orrrrrRo..",
            "...orrrRo...",
            "..orrrrrRo..",
            ".orrRoorrRo.",
            "orrRo..orrRo",
            "oRRo....oRRo",
            ".oo......oo.",
        ],

        // Un triángulo amarillo con exclamación: algo pide que se haga (la versión nueva, §202).
        ["IconWarning"] =
        [
            ".....oo.....",
            "....oggo....",
            "....oggo....",
            "...oggggo...",
            "...ogoogo...",
            "..oggooggo..",
            "..oggooggo..",
            ".oggggggggo.",
            ".ogggooggGo.",
            "oggggggggGGo",
            "oGGGGGGGGGGo",
            ".oooooooooo.",
        ],

        ["IconDot"] =
        [
            "............",
            "............",
            "............",
            "....oooo....",
            "...ovvvvo...",
            "...ovwvvo...",
            "...ovvvVo...",
            "...ovvVVo...",
            "....oooo....",
            "............",
            "............",
            "............",
        ],
    };

    /// <summary>The drawing for an icon key, or the plain dot for a key nobody drew.</summary>
    public static string[] For(string? key) =>
        key is not null && Drawings.TryGetValue(key, out var rows) ? rows : Drawings["IconDot"];

    public static bool TryColour(char letter, out Color colour) => Key.TryGetValue(letter, out colour);

    /// <summary>Every drawing, for the test that checks they are all twelve by twelve and use known colours.</summary>
    public static IEnumerable<KeyValuePair<string, string[]>> All => Drawings;
}
