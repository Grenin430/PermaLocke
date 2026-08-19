namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Regla 3 del proyecto: nada falso. Un módulo pedido y no implementado tiene que aparecer en el
/// informe, no desaparecer sin más. Hoy no queda ninguno, y el test lo fija.
/// </summary>
public class RandomizerServiceTests
{
    [Fact]
    public void Every_module_the_options_can_request_is_implemented()
    {
        var options = new RandomizerOptions
        {
            WildEncounters = true,
            StaticEncounters = true,
            Trainers = true,
            PokemonData = true,
            SpecialMarts = true,
        };

        Assert.Empty(RandomizerService.NotImplemented(options));
    }
}
