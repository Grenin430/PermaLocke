namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// Which icon of <c>a/0/6/1</c> belongs to which item.
/// </summary>
/// <remarks>
/// <para>
/// It is <b>not</b> <c>id - 1</c>. That holds for the first hundred items — which is why the
/// sixteen Poké Balls made it look like the whole rule (§34) — and then drifts, because the
/// cartridge has <b>960 items and only 769 icons</b>: whole blocks share one picture. The hundred
/// TMs, for instance, occupy twenty discs, one per type.
/// </para>
/// <para>
/// The drift is a step function and nobody publishes it, so it is <b>measured</b>, zone by zone,
/// by rendering a window of icons and recognising things that cannot be mistaken: the four Mulch
/// bags in a row, the seventeen type Gems in a row, Quick Claw, Soothe Bell, Amulet Coin, Rocky
/// Helmet, Air Balloon, Red Card, Safety Goggles. Each entry below was seen on screen, not
/// worked out.
/// </para>
/// <para>
/// Deliberately a table of what has been checked and not a formula: an item PermaLocke never
/// shows has no business getting a guessed icon, and <see cref="Of"/> throws rather than draw the
/// wrong picture. Adding one is a two minute job — render the neighbourhood and recognise it.
/// </para>
/// </remarks>
public static class ItemIconIndex
{
    /// <summary>Last item id whose icon really is <c>id - 1</c>. Verified up to the four Mulches.</summary>
    public const int LastDirectItem = 100;

    private static readonly Dictionary<int, int> Measured = new()
    {
        // 1-100: el indice es id-1. Comprobado con las dieciseis balls, las cinco vitaminas,
        // el Caramelo Raro, el Trozo Estrella, la Escama Corazon y los cuatro Abonos.

        // Zona de -18: bayas y objetos equipados tempranos.
        [157] = 139, // Baya Ziuela
        [158] = 140, // Baya Zidra
        [214] = 196, // Hierba Blanca
        [220] = 202, // Cinta Elegida
        [234] = 216, // Restos

        // Zona de -19.
        [269] = 250, // Refleluz
        [270] = 251, // Vidasfera
        [271] = 252, // Hierba Unica
        [272] = 253, // Toxisfera
        [275] = 256, // Banda Focus
        [287] = 268, // Panuelo Elegido
        [297] = 278, // Gafas Elegidas

        // Zona de -127: despues de las cien MT, que gastan solo veinte iconos.
        [538] = 411, // Mineral Evolutivo
        [540] = 413, // Casco Dentado

        // Zona de -135.
        [640] = 505, // Chaleco Asalto
        [645] = 510, // Capsula Habilidad
        [650] = 515, // Gafa Protectora

        // Las treinta megapiedras de 656 a 685, seguidas, en los iconos 521 a 550. El desfase
        // sigue siendo -135 y el bloque esta anclado por los DOS extremos mirando los dibujos:
        // 521 es morada (Gengarita), 523 amarilla (Ampharosita), 524 verde (Venusaurita) y 550
        // azul (Latiosita). Y el icono 551, el siguiente, ya es una baya, que es lo que confirma
        // donde acaba: el objeto 686 es la Baya Hibis.
        [656] = 521, [657] = 522, [658] = 523, [659] = 524, [660] = 525,
        [661] = 526, [662] = 527, [663] = 528, [664] = 529, [665] = 530,
        [666] = 531, [667] = 532, [668] = 533, [669] = 534, [670] = 535,
        [671] = 536, [672] = 537, [673] = 538, [674] = 539, [675] = 540,
        [676] = 541, [677] = 542, [678] = 543, [679] = 544, [680] = 545,
        [681] = 546, [682] = 547, [683] = 548, [684] = 549, [685] = 550,
        // Zona de -146: el SEGUNDO bloque de megapiedras. El desfase cambia otra vez, y el
        // comentario que estuvo aqui decia que 752 era el icono 617; era una lectura equivocada.
        //
        // Se midio rindiendo los iconos 604-626 y mirandolos. La forma del bloque encaja por si
        // sola: TRECE piedras seguidas (606-618), un hueco de dos, y CUATRO mas (621-624). Los
        // objetos tienen exactamente esa forma -752-764 son trece, 765 y 766 no son piedras, y
        // 767-770 son cuatro-, y eso ya seria dificil de atribuir a la casualidad.
        //
        // Pero ademas cuadra el COLOR de las diecisiete, una a una, que es el ancla independiente
        // del §61: 606 azul y naranja (Swampert), 607 verde y rojo (Sceptile), 611 rosa y crema
        // (Audino), 615 gris acero (Steelix), 617 blanco, azul y negro (Glalie), 618 rosa
        // (Diancie), 622 marron y crema (Lopunny), 623 azul y rojo (Salamence), 624 amarillo y
        // negro (Beedrill).
        //
        // Y el remache que caza un desfase de UNO, que es el error que el color no distingue: el
        // objeto 766 es el MEGABRAZALETE, y el icono 620 -uno de los dos del hueco- es un
        // brazalete rojo y negro. Ese ancla no es una piedra, asi que confirma el -146 por fuera
        // del bloque que se esta midiendo.
        [752] = 606, [753] = 607, [754] = 608, [755] = 609, [756] = 610,
        [757] = 611, [758] = 612, [759] = 613, [760] = 614, [761] = 615,
        [762] = 616, [763] = 617, [764] = 618,

        [766] = 620, // Megabrazalete, el ancla del hueco

        [767] = 621, [768] = 622, [769] = 623, [770] = 624,
    };

    /// <summary>True when this item's icon has been checked and can be drawn.</summary>
    public static bool TryGet(int itemId, out int icon)
    {
        if (itemId > 0 && itemId <= LastDirectItem)
        {
            icon = itemId - 1;
            return true;
        }

        return Measured.TryGetValue(itemId, out icon);
    }

    /// <summary>
    /// The icon index of an item, or an exception naming the problem.
    /// </summary>
    /// <remarks>
    /// Throwing beats returning a plausible number: a wrong icon looks perfectly fine and would
    /// sell the player one thing while showing another.
    /// </remarks>
    public static int Of(int itemId) => TryGet(itemId, out var icon)
        ? icon
        : throw new KeyNotFoundException(
            $"No se ha medido qué icono le toca al objeto {itemId}. El índice no es id-1 salvo en " +
            $"los primeros {LastDirectItem}: hay 960 objetos y 769 iconos. Ver ARCHITECTURE.md §45.");

    /// <summary>Every item whose icon is known, for whoever has to extract them.</summary>
    public static IEnumerable<int> KnownItems => Measured.Keys;
}
