using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.Core.Tests;

/// <summary>
/// The regional form the gacha and the wonder trade hand out (§139).
/// </summary>
public sealed class FormDrawTests
{
    private static SpeciesStats Meowth() =>
        new(52, "Meowth", 290, false, [], [new SpeciesForm(1, "Alola"), new SpeciesForm(2, "Galar")]);

    [Fact]
    public void A_species_without_regional_forms_is_always_its_ordinary_self()
    {
        var pikachu = new SpeciesStats(25, "Pikachu", 320, false, []);

        Assert.Equal((0, string.Empty), FormDraw.Roll(new SeededRandomSource(1), pikachu));
    }

    /// <summary>
    /// The form comes from a derived stream, so the roll's own numbers are untouched: everything
    /// else in a pull comes out exactly as it did before forms existed.
    /// </summary>
    [Fact]
    public void Rolling_a_form_does_not_move_the_rolls_own_numbers()
    {
        var used = new SeededRandomSource(4242);
        var untouched = new SeededRandomSource(4242);

        FormDraw.Roll(used, Meowth());

        Assert.Equal(untouched.Next(1_000_000), used.Next(1_000_000));
    }

    [Fact]
    public void Every_form_comes_out_about_as_often_and_carries_its_name()
    {
        var seen = new Dictionary<int, int>();

        for (var seed = 0UL; seed < 3000; seed++)
        {
            var (form, name) = FormDraw.Roll(new SeededRandomSource(seed), Meowth());
            seen[form] = seen.GetValueOrDefault(form) + 1;

            Assert.Equal(form switch { 1 => "Alola", 2 => "Galar", _ => string.Empty }, name);
        }

        Assert.All(seen.Values, count => Assert.InRange(count, 880, 1120));
        Assert.Equal(3, seen.Count);
    }
}
