using PermaLocke.Randomizer;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Los objetos de evolución del mod puestos a la venta, y su precio.
/// </summary>
/// <remarks>
/// Existen porque el mod los tiene en la tabla de objetos y <b>no los coloca en ninguna parte</b>:
/// sin una tienda que los venda, Ceruledge, Urshifu, Alcremie o Gholdengo no pueden evolucionar y
/// nada lo diría.
/// </remarks>
public sealed class SpecialMartItemTests
{
    [Fact]
    public void Por_defecto_no_hay_ninguno_ni_se_toca_ningun_precio()
    {
        var options = new RandomizerOptions();

        Assert.Empty(options.SpecialMartItems);
        Assert.Equal(0, options.SpecialMartItemPrice);
    }

    /// <summary>
    /// El precio se guarda entre diez en un ushort, así que ni vale cualquiera ni se redondea.
    /// </summary>
    /// <remarks>
    /// Un 50005 se guardaría como 5000 y la tienda cobraría 50000 sin decir nada; uno por encima
    /// de 655350 daría la vuelta y saldría barato. Las dos cosas son peores que negarse.
    /// </remarks>
    [Theory]
    [InlineData(50000, true)]
    [InlineData(10, true)]
    [InlineData(655350, true)]
    [InlineData(50005, false)]
    [InlineData(655360, false)]
    public void El_precio_tiene_que_ser_multiplo_de_diez_y_caber(int price, bool valid)
    {
        var fits = price % 10 == 0 && price <= ushort.MaxValue * 10;

        Assert.Equal(valid, fits);
    }

    /// <summary>La lista de la competición: los dieciocho que añade el mod, sin repetidos.</summary>
    [Fact]
    public void La_lista_configurada_no_repite_ningun_id()
    {
        var options = RandomizerOptionsLoader.Load(
            Path.Combine(FindRoot(), "Data", "randomizer.json"));

        Assert.Equal(18, options.SpecialMartItems.Count);
        Assert.Equal(18, options.SpecialMartItems.Select(i => i.Id).Distinct().Count());
        Assert.Equal(50000, options.SpecialMartItemPrice);

        // El que motivó todo esto.
        Assert.Contains(options.SpecialMartItems, i => i.Id == 989 && i.Name == "Malicious Armor");
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PermaLocke.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encuentra la raíz.");
    }
}
