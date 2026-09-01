using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Putting a mod's English-only names into the player's language. See <c>docs/MOD-EXPANSION.md</c>.
/// </summary>
public sealed class NameLocalizerTests
{
    [Fact]
    public void Anade_las_que_faltan_con_el_nombre_oficial()
    {
        var result = NameLocalizer.Extend(
            current: ["Huevo", "Bulbasaur"],
            english: ["Egg", "Bulbasaur", "Grookey", "Thwackey"],
            official: ["---", "Bulbasaur", "Grookey", "Thwackey"]);

        Assert.Equal(["Huevo", "Bulbasaur", "Grookey", "Thwackey"], result.Lines);
        Assert.Equal(2, result.Kept);
        Assert.Equal(2, result.Translated);
        Assert.Equal(0, result.Borrowed);
    }

    /// <summary>
    /// The decision that matters: what the game already says is never rewritten.
    /// </summary>
    /// <remarks>
    /// The cartridge abbreviates to fit its own boxes and uses its generation's translations. Its
    /// "Picotazo Ven" is not a mistake to be corrected into "Picotazo Veneno" — correcting it would
    /// rename the game under a player who knows it, and might not fit on screen.
    /// </remarks>
    [Fact]
    public void Nunca_reescribe_lo_que_el_juego_ya_dice()
    {
        var result = NameLocalizer.Extend(
            current: ["Picotazo Ven", "Golpe"],
            english: ["Poison Sting", "Spite", "Nuevo"],
            official: ["Picotazo Veneno", "Saña", "Nuevo"]);

        Assert.Equal("Picotazo Ven", result.Lines[0]);
        Assert.Equal("Golpe", result.Lines[1]);
        Assert.Equal("Nuevo", result.Lines[2]);
        Assert.Equal(2, result.Kept);
    }

    /// <summary>
    /// When the official list runs out, the mod's English is copied instead of leaving a blank.
    /// </summary>
    /// <remarks>
    /// It happens for real: the mod's ability list has 320 entries and PKHeX's Spanish one 311. A
    /// blank there is not neutral — the lists are indexed by id, so it is an empty label on screen.
    /// </remarks>
    [Fact]
    public void Sin_nombre_oficial_copia_el_ingles_en_vez_de_dejarlo_en_blanco()
    {
        var result = NameLocalizer.Extend(
            current: ["Espesura"],
            english: ["Overgrow", "Hunger Switch", "Orichalcum Pulse"],
            official: ["Espesura", "Cambio Hambre"]);

        Assert.Equal("Cambio Hambre", result.Lines[1]);
        Assert.Equal("Orichalcum Pulse", result.Lines[2]);
        Assert.Equal(1, result.Translated);
        Assert.Equal(1, result.Borrowed);
        Assert.Equal(2, result.Added);
    }

    /// <summary>An empty official entry counts as absent, not as a name.</summary>
    [Fact]
    public void Una_entrada_oficial_vacia_no_vale_como_nombre()
    {
        var result = NameLocalizer.Extend(
            current: ["A"],
            english: ["A", "Something"],
            official: ["A", ""]);

        Assert.Equal("Something", result.Lines[1]);
        Assert.Equal(1, result.Borrowed);
    }

    /// <summary>
    /// A list the mod does not lengthen is left exactly alone.
    /// </summary>
    /// <remarks>
    /// Padding it back up to some other length would invent entries, and this code has no business
    /// deciding what a game means by a shorter list than it used to have.
    /// </remarks>
    [Fact]
    public void Si_el_mod_no_alarga_la_lista_no_se_toca_nada()
    {
        var result = NameLocalizer.Extend(
            current: ["A", "B", "C"],
            english: ["A", "B"],
            official: ["X", "Y", "Z"]);

        Assert.Equal(["A", "B", "C"], result.Lines);
        Assert.Equal(0, result.Added);
    }

    /// <summary>
    /// The alignment check, which is the gate for the whole operation.
    /// </summary>
    /// <remarks>
    /// These lists are addressed by index. An official list offset by one would give every new
    /// Pokémon its neighbour's name, and nothing would fail: the count would be right, the text
    /// would be Spanish, and it would all be wrong.
    /// </remarks>
    [Fact]
    public void El_ancla_cuenta_cuantos_coinciden_sobre_el_tramo_comun()
    {
        var (same, compared) = NameLocalizer.Alignment(
            ["Huevo", "Bulbasaur", "Ivysaur"],
            ["---", "Bulbasaur", "Ivysaur", "Venusaur"]);

        Assert.Equal(3, compared);
        Assert.Equal(2, same);
    }

    [Fact]
    public void Un_desplazamiento_de_uno_hunde_el_ancla()
    {
        string[] game = ["Bulbasaur", "Ivysaur", "Venusaur", "Charmander"];
        string[] shifted = ["Ivysaur", "Venusaur", "Charmander", "Charmeleon"];

        var (same, compared) = NameLocalizer.Alignment(game, shifted);

        Assert.Equal(4, compared);
        Assert.Equal(0, same);
    }
}
