using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Los ochenta iconos de objeto que añade el mod de gen 8 y 9.
/// </summary>
/// <remarks>
/// El ancla son los objetos de <b>evolución</b>, no las megapiedras: entre cuarenta y tres esferas
/// con franjas de dos o tres tonos, «esta es la de Dragonite» es una corazonada, y un icono
/// equivocado no se nota (§45). Dos aros, tres manzanas, dos teteras, dos pergaminos, dos
/// armaduras, dos tazas y una moneda de oro sí se reconocen de un vistazo.
/// </remarks>
public sealed class ExpansionItemIconTests
{
    private const int Mod = ItemIconIndex.ExpansionIcons;

    /// <summary>Las nueve parejas inconfundibles que dan el desfase.</summary>
    [Theory]
    [InlineData(971, 780)]   // Galarica Cuff, un aro
    [InlineData(972, 781)]   // Galarica Wreath, el otro
    [InlineData(973, 782)]   // Tart Apple
    [InlineData(974, 783)]   // Sweet Apple
    [InlineData(975, 784)]   // Cracked Pot, una tetera
    [InlineData(976, 785)]   // Chipped Pot, la otra
    [InlineData(984, 793)]   // Darkness Scroll, un pergamino
    [InlineData(985, 794)]   // Scroll of Waters, el otro
    [InlineData(988, 797)]   // Auspicious Armor, la dorada
    [InlineData(989, 798)]   // Malicious Armor, la oscura
    [InlineData(991, 800)]   // Syrupy Apple
    [InlineData(992, 801)]   // Unremarkable Cup
    [InlineData(993, 802)]   // Masterpiece Cup
    [InlineData(994, 803)]   // Gimmighoul Coin, la moneda
    public void Los_objetos_de_evolucion_anclan_el_desfase(int item, int icon)
    {
        Assert.True(ItemIconIndex.TryGet(item, out var got, Mod));
        Assert.Equal(icon, got);
    }

    /// <summary>Y entonces las piedras caen solas, cada una del color de su Pokémon.</summary>
    [Theory]
    [InlineData(997, 806)]    // Clefablite, rosa
    [InlineData(1000, 809)]   // Dragoninite, naranja y crema
    [InlineData(1011, 820)]   // Chesnaughtite, verde y marrón
    [InlineData(1019, 828)]   // Falinksite, amarillo y negro
    [InlineData(1023, 832)]   // Darkranite, negro y blanco
    public void Las_megapiedras_nuevas_siguen_el_mismo_bloque(int item, int icon)
    {
        Assert.True(ItemIconIndex.TryGet(item, out var got, Mod));
        Assert.Equal(icon, got);
    }

    /// <summary>Los dieciséis que el mod reutiliza van en su propio bloque, al final.</summary>
    [Theory]
    [InlineData(505, 833)]   // Golurkite
    [InlineData(508, 836)]   // Zeraorite, amarillo y azul
    [InlineData(509, 837)]   // Scovillainite, rojo y verde
    [InlineData(514, 842)]   // Lucarionite Z, azul y amarillo
    [InlineData(517, 845)]   // Zygardite, verde y rojo
    [InlineData(520, 848)]   // Tatsugirinite, el último icono del contenedor
    public void Los_renombrados_van_en_el_bloque_final(int item, int icon)
    {
        Assert.True(ItemIconIndex.TryGet(item, out var got, Mod));
        Assert.Equal(icon, got);
    }

    /// <summary>
    /// La comprobación que convierte esto en una medida: el reparto <b>cierra</b>.
    /// </summary>
    /// <remarks>
    /// Los objetos 960-1023 son 64 y ocupan 64 iconos; los renombrados son 16 y ocupan 16; y el
    /// contenedor crece exactamente 80. Ni un icono libre ni uno repetido. Si cualquiera de los dos
    /// bloques se corriera un puesto, esta cuenta dejaría de cuadrar.
    /// </remarks>
    [Fact]
    public void El_reparto_cierra_sin_hueco_ni_repetido()
    {
        int[] items = [.. Enumerable.Range(960, 64), .. Enumerable.Range(505, 16)];
        var icons = items.Select(i => ItemIconIndex.Of(i, Mod)).ToArray();

        Assert.Equal(80, items.Length);
        Assert.Equal(80, icons.Distinct().Count());
        Assert.Equal(ItemIconIndex.ExpansionIcons - ItemIconIndex.CartridgeIcons, icons.Length);

        // Y ocupan justo el tramo añadido, sin salirse por ningún lado.
        Assert.Equal(ItemIconIndex.CartridgeIcons, icons.Min());
        Assert.Equal(ItemIconIndex.ExpansionIcons - 1, icons.Max());
    }

    /// <summary>
    /// Sin el mod, esas reglas no se aplican: sus iconos no existen en el cartucho.
    /// </summary>
    /// <remarks>
    /// Es lo que impide que un jugador sin mod vea la Tarjeta de Datos 01 dibujada como una
    /// megapiedra, o que se pida un icono más allá del final del contenedor.
    /// </remarks>
    [Fact]
    public void Sin_mod_los_dos_bloques_no_existen()
    {
        Assert.False(ItemIconIndex.TryGet(505, out _));
        Assert.False(ItemIconIndex.TryGet(1000, out _));
        Assert.False(ItemIconIndex.TryGet(520, out _));
    }

    /// <summary>Lo medido a mano del cartucho sigue mandando y no se mueve con el mod.</summary>
    [Fact]
    public void El_mod_no_mueve_ni_un_icono_del_cartucho()
    {
        foreach (var item in (int[])[4, 656, 685, 752, 770, 234])
        {
            Assert.True(ItemIconIndex.TryGet(item, out var plain));
            Assert.True(ItemIconIndex.TryGet(item, out var expanded, Mod));
            Assert.Equal(plain, expanded);
        }
    }
}
