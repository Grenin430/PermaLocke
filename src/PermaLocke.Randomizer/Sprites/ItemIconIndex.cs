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

        // Las diecisiete megapiedras del segundo bloque (752-764 y 767-770) NO estan aqui, y no
        // es un olvido. El bloque empieza bien -752 Swampertita es el icono 617, azul- pero el
        // desfase se rompe antes de acabarlo: con -135 la ultima caeria en el icono 629 y el 629
        // es una herramienta, no una piedra. Donde se rompe exactamente no se ha medido, y una
        // piedra con el dibujo de la de al lado es justo el error que esta tabla existe para
        // impedir. Sin icono se dibuja el hueco; con uno adivinado, nadie se entera (§45).
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
