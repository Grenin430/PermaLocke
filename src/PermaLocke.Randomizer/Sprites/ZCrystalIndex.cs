namespace PermaLocke.Randomizer.Sprites;

/// <summary>
/// Which of the eighteen carved Z-Crystals belongs to which item.
/// </summary>
/// <remarks>
/// <para>
/// The layout that holds them names them <c>item_807.bflim</c> … <c>item_824.bflim</c>, so the set
/// is certain: items 807-824 are the type crystals, in type order — Normal, Fuego, Agua,
/// Eléctrico, Planta, Hielo, Lucha, Veneno, Tierra, Volador, Psíquico, Bicho, Roca, Fantasma,
/// Dragón, Siniestro, Acero, Hada. The twelve trials confirm that from outside: every crystal
/// named in <c>Data/achievements.json</c> lands on the type its trial really is.
/// </para>
/// <para>
/// What is <b>not</b> certain is the order the images sit in. An ALYT keeps its name table and its
/// data in different orders and publishes no map between them — the first image is a golden
/// crystal while the first name is the Normalium, which is pale — so the pairing below is by
/// <b>colour</b>: each crystal is its type's colour, and the eighteen sort cleanly into families:
/// one yellow, one red, two greens, three browns, two pinks, two purples, five blues, one nearly
/// grey and one nearly black.
/// </para>
/// <para>
/// That is a weaker anchor than the rest of this codebase uses, and it is said out loud rather
/// than dressed up. §34 turned the same job down for exactly that reason; it is done now because
/// the achievements screen asked for it, and here a picture one shade off costs nothing, whereas a
/// wrong <em>item</em> would have cost the player the wrong thing in their bag.
/// </para>
/// <para>
/// The calls inside each family: the palest crystal of all is Normal and the palest blue is
/// Volador, the darkest is Siniestro, the yellower green is Bicho, the redder purple is Fantasma,
/// the browner orange is Tierra, and the deeper of the two clear blues is Dragón.
/// </para>
/// </remarks>
public static class ZCrystalIndex
{
    /// <summary>Item id to position in the carved list, which is the order the layout stores them.</summary>
    private static readonly Dictionary<int, int> Measured = new()
    {
        [807] = 1,   // Normastal Z     el más pálido de los dieciocho
        [808] = 12,  // Pirostal Z      el único rojo
        [809] = 15,  // Hidrostal Z     azul limpio
        [810] = 0,   // Electrostal Z   el único amarillo
        [811] = 5,   // Fitostal Z      verde puro
        [812] = 13,  // Criostal Z      cian
        [813] = 14,  // Lizastal Z      naranja rojizo
        [814] = 11,  // Toxistal Z      violeta azulado
        [815] = 8,   // Geostal Z       ocre
        [816] = 9,   // Aerostal Z      el azul más pálido
        [817] = 2,   // Psicostal Z     rosa fuerte
        [818] = 3,   // Insectostal Z   verde amarillento
        [819] = 7,   // Litostal Z      pardo apagado
        [820] = 10,  // Espectrostal Z  púrpura rojizo
        [821] = 17,  // Dracostal Z     azul profundo
        [822] = 16,  // Nictostal Z     el más oscuro
        [823] = 6,   // Metalostal Z    azul acero
        [824] = 4,   // Feeristal Z     rosa apagado
    };

    /// <summary>True when this item is one of the eighteen type crystals.</summary>
    public static bool TryGet(int itemId, out int index) => Measured.TryGetValue(itemId, out index);

    /// <summary>Every crystal item id, lowest first.</summary>
    public static IEnumerable<int> All => Measured.Keys.Order();
}
