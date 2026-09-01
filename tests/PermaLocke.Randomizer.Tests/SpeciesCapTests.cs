using PermaLocke.Randomizer;
using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The three ceilings the randomizer honours, and what zero means in each.
/// </summary>
/// <remarks>
/// Zero meaning "whatever the game has" is the whole point: the previous default was a hardcoded
/// 807, which is right on the cartridge and silently wrong on a mod that adds Pokémon. Under that
/// default the pool built fine, the randomization reported success, and none of the new species
/// could ever be picked.
/// </remarks>
public sealed class SpeciesCapTests
{
    private static int[] Totals(int species) =>
        [.. Enumerable.Range(0, species + 1).Select(s => s == 0 ? 0 : 400)];

    [Fact]
    public void Cero_significa_las_que_tenga_el_juego()
    {
        var options = new RandomizerOptions();

        Assert.Equal(807, options.EffectiveMaxSpecies(807));
        Assert.Equal(1025, options.EffectiveMaxSpecies(1025));
    }

    [Fact]
    public void Un_tope_declarado_manda_sobre_el_juego()
    {
        var options = new RandomizerOptions { MaxSpecies = 151 };

        Assert.Equal(151, options.EffectiveMaxSpecies(1025));
    }

    /// <summary>A ceiling above what the game holds cannot invent species.</summary>
    [Fact]
    public void Un_tope_mayor_que_el_juego_no_inventa_nada()
    {
        var options = new RandomizerOptions { MaxSpecies = 2000 };

        Assert.Equal(807, options.EffectiveMaxSpecies(807));
    }

    /// <summary>
    /// The pool actually reaches the new species, which is what the old clamp prevented.
    /// </summary>
    [Fact]
    public void El_saco_llega_hasta_la_ultima_especie_del_mundo()
    {
        var pool = new SpeciesPool(Totals(1025), new RandomizerOptions());

        Assert.Equal(1025, pool.Count);
        Assert.Equal(400, pool.BaseStatTotal(1025));
    }

    [Fact]
    public void Con_tope_el_saco_se_queda_donde_se_le_dice()
    {
        var pool = new SpeciesPool(Totals(1025), new RandomizerOptions { MaxSpecies = 807 });

        Assert.Equal(807, pool.Count);
    }

    [Fact]
    public void El_tope_de_habilidades_recorta_las_que_el_mod_no_puede_dar()
    {
        // El cartucho declara 233 y el mod llena el byte hasta 255. Con el tope puesto en 233,
        // ninguna de las 22 que no funcionan se reparte.
        var options = new RandomizerOptions { MaxAbility = 233 };

        Assert.Equal(233, options.EffectiveMaxAbility(255));
        Assert.Equal(233, options.EffectiveMaxAbility(233));
    }

    [Fact]
    public void Sin_tope_de_habilidades_se_reparten_todas_las_del_juego()
    {
        Assert.Equal(255, new RandomizerOptions().EffectiveMaxAbility(255));
    }

    [Fact]
    public void El_tope_de_movimientos_funciona_igual()
    {
        Assert.Equal(729, new RandomizerOptions { MaxMove = 729 }.EffectiveMaxMove(921));
        Assert.Equal(921, new RandomizerOptions().EffectiveMaxMove(921));
    }

    /// <summary>
    /// Banned species stay banned above 807 too.
    /// </summary>
    /// <remarks>
    /// Worth pinning: the ban list is applied after the ceiling, so a bug in the ceiling could have
    /// let the new range in through a path the ban never saw.
    /// </remarks>
    public sealed class Prohibidas
    {
        [Fact]
        public void La_lista_de_prohibidas_tambien_alcanza_a_las_nuevas()
        {
            var pool = new SpeciesPool(Totals(1025),
                new RandomizerOptions { BannedSpecies = [1000, 1001, 1025] });

            Assert.Equal(1022, pool.Count);
        }
    }
}
