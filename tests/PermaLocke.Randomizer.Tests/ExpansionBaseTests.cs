using PermaLocke.Randomizer.Modules;
using PermaLocke.Randomizer.Sprites;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The groundwork for randomizing on top of a mod that adds Pokémon, measured against the gen 8-9
/// expansion. See <c>docs/MOD-EXPANSION.md</c>.
/// </summary>
public sealed class ExpansionBaseTests
{
    /// <summary>Builds a packed personal table whose species/form boundary is where we say.</summary>
    private static byte[] Table(int species, int forms)
    {
        var rows = species + 1 + forms;
        var packed = new byte[rows * PersonalEntry7.Size];

        // Solo una especie declara forma, y con eso basta: la cuenta sale del índice MÁS BAJO, así
        // que el caso de una sola es el que de verdad ejercita la búsqueda del mínimo.
        BitConverter.GetBytes((ushort)(species + 1))
            .CopyTo(packed, (1 * PersonalEntry7.Size) + PersonalEntry7.FormStatsIndexOffset);

        return packed;
    }

    [Fact]
    public void La_cuenta_de_especies_sale_del_primer_indice_de_forma()
    {
        Assert.Equal(807, PersonalEntry7.SpeciesCount(Table(807, 168)));
        Assert.Equal(1025, PersonalEntry7.SpeciesCount(Table(1025, 304)));
    }

    /// <summary>
    /// Several species declare forms; the answer is the lowest index, not the first one seen.
    /// </summary>
    [Fact]
    public void Manda_el_indice_mas_bajo_y_no_el_primero_que_aparece()
    {
        var packed = new byte[900 * PersonalEntry7.Size];

        void Declare(int row, int index) =>
            BitConverter.GetBytes((ushort)index)
                .CopyTo(packed, (row * PersonalEntry7.Size) + PersonalEntry7.FormStatsIndexOffset);

        Declare(3, 850);    // el primero que se encuentra, y no es el menor
        Declare(6, 808);
        Declare(9, 812);

        Assert.Equal(807, PersonalEntry7.SpeciesCount(packed));
    }

    /// <summary>
    /// A table where nothing declares a form has to throw, not guess.
    /// </summary>
    /// <remarks>
    /// Falling back to the row count would answer 976 for the cartridge — species plus forms mixed
    /// together — and every module that picks a species would start handing out form entries as if
    /// they were Pokémon.
    /// </remarks>
    [Fact]
    public void Una_tabla_sin_formas_lanza_en_vez_de_adivinar()
    {
        Assert.Throws<InvalidDataException>(
            () => PersonalEntry7.SpeciesCount(new byte[100 * PersonalEntry7.Size]));
    }

    [Fact]
    public void Sin_mod_la_tabla_de_iconos_es_exactamente_la_de_siempre()
    {
        var index = PokemonIconIndex.Build(CartridgeFormCounts.Of);

        Assert.Equal(PokemonIconIndex.LastSpecies, index.Count);
        Assert.False(index.ContainsKey(808));
    }

    /// <summary>
    /// The appended run, anchored at both ends against the container that was measured.
    /// </summary>
    /// <remarks>
    /// Meltan is the first icon after the cartridge's 1154 and Pecharunt the last of the run. Those
    /// two were read off the decoded images, and they are what makes this a measurement rather than
    /// an assumption that the mod appended in order.
    /// </remarks>
    [Fact]
    public void Con_mod_las_especies_nuevas_van_seguidas_desde_Meltan()
    {
        var index = PokemonIconIndex.Build(CartridgeFormCounts.Of, maxSpecies: 1025);

        Assert.Equal(1154, index[808]);    // Meltan, el primero del bloque añadido
        Assert.Equal(1155, index[809]);    // Melmetal
        Assert.Equal(1156, index[810]);    // Grookey
        Assert.Equal(1371, index[1025]);   // Pecharunt, el último de la novena generación
    }

    /// <summary>
    /// The run closes with no slack, which is what says the alignment is right.
    /// </summary>
    /// <remarks>
    /// 1025 − 808 = 217 species and 1371 − 1154 = 217 icons. If either end moved by one the two
    /// counts would stop matching, and that is the only cheap way to catch an off-by-one here: one
    /// Pokémon quietly drawing its neighbour is invisible (§45).
    /// </remarks>
    [Fact]
    public void El_bloque_anadido_cierra_sin_hueco_ni_solape()
    {
        var index = PokemonIconIndex.Build(CartridgeFormCounts.Of, maxSpecies: 1025);
        var added = Enumerable.Range(808, 1025 - 807).Select(s => index[s]).ToArray();

        Assert.Equal(218, added.Length);
        Assert.Equal(218, added.Distinct().Count());
        Assert.Equal(added.OrderBy(i => i), added);

        // Y no pisa ni un icono del cartucho.
        var vanilla = Enumerable.Range(1, PokemonIconIndex.LastSpecies).Select(s => index[s]);
        Assert.Empty(added.Intersect(vanilla));
    }

    /// <summary>The cartridge's own species keep the icons the hand-built table gives them.</summary>
    [Fact]
    public void El_mod_no_mueve_ni_un_icono_de_los_de_siempre()
    {
        var plain = PokemonIconIndex.Build(CartridgeFormCounts.Of);
        var expanded = PokemonIconIndex.Build(CartridgeFormCounts.Of, maxSpecies: 1025);

        foreach (var (species, icon) in plain)
        {
            Assert.Equal(icon, expanded[species]);
        }
    }
}
