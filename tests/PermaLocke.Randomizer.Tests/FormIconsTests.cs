using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The icon of each form: the cartridge's Alolan ones and the expansion's, from its own table (§139).
/// </summary>
public sealed class FormIconsTests
{
    /// <summary>The ordinary icons of a few species, as the §30 table gives them.</summary>
    private static Dictionary<int, int> Species() => new() { [37] = 61, [51] = 81, [52] = 83, [128] = 200 };

    private const int Expansion14 = PokemonIconIndex.FormBlockStart + 136;

    /// <summary>
    /// Checked by eye on a contact sheet: Vulpix 60 is the Alolan one and 61 the ordinary one;
    /// Dugtrio has two identical Alolan icons, 79 and 80, before its ordinary 81.
    /// </summary>
    [Fact]
    public void An_alolan_form_is_the_icon_right_before_the_ordinary_one()
    {
        var forms = PokemonIconIndex.FormIcons(Species(), Expansion14);

        Assert.Equal(60, forms[(37, 1)]);
        Assert.Equal(80, forms[(51, 1)]);
        Assert.Equal(82, forms[(52, 1)]);
    }

    /// <summary>
    /// From the key table the mod's own lookup walks: Galarian Meowth is its fifth key, the Paldean
    /// Aqua breed of Tauros its twenty-first, the Stellar Terapagos its last.
    /// </summary>
    [Fact]
    public void The_expansions_forms_come_from_its_own_table()
    {
        var forms = PokemonIconIndex.FormIcons(Species(), Expansion14);

        Assert.Equal(1376, forms[(52, 2)]);
        Assert.Equal(1392, forms[(128, 3)]);
        Assert.Equal(1451, forms[(724, 1)]);
        Assert.Equal(1507, forms[(1024, 2)]);
    }

    /// <summary>
    /// Another container could lay its forms out differently, and a form icon one slot off draws
    /// somebody else: then no expansion form gets an icon, and they fall back to their species'.
    /// </summary>
    [Fact]
    public void Another_container_gets_no_expansion_form_icons()
    {
        var forms = PokemonIconIndex.FormIcons(Species(), 1400);

        Assert.False(forms.ContainsKey((52, 2)));
        Assert.Equal(60, forms[(37, 1)]);
    }
}
