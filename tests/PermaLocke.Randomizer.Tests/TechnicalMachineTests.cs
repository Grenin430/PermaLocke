using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// Which items are TMs, and why the answer cannot be a range.
/// </summary>
/// <remarks>
/// This cost a real bug, reported from play: a gold Poké Ball handed over a Potion. The hundred
/// machines are <b>not</b> contiguous — 328-419, 618-620 and 690-694 — so the range that stood here
/// recognised only ninety-two, and the eight it missed fell into the ordinary item shuffle.
/// </remarks>
public sealed class TechnicalMachineTests
{
    /// <summary>The cartridge's real names at the ids that matter, read with RomTool items.</summary>
    private static string[] Names()
    {
        var names = new string[1024];
        Array.Fill(names, string.Empty);

        for (var i = 0; i < 92; i++)
        {
            names[328 + i] = $"MT{i + 1:00}";
        }

        names[420] = "MO01";           // las MO no son MT
        names[421] = "MO02";
        names[616] = "Orbe Claro";
        names[617] = "Orbe Oscuro";
        names[618] = "MT93";
        names[619] = "MT94";
        names[620] = "MT95";
        names[621] = "Videomisor";
        names[689] = "Lotadgadera";
        names[690] = "MT96";
        names[691] = "MT97";
        names[692] = "MT98";
        names[693] = "MT99";
        names[694] = "MT100";
        names[695] = "Pase Central";
        names[4] = "Poké Ball";

        return names;
    }

    [Fact]
    public void Son_exactamente_cien_y_no_las_noventa_y_dos_del_rango()
    {
        var machines = ShopTable.TechnicalMachines(Names());

        Assert.Equal(100, machines.Length);
        Assert.Equal(100, machines.Distinct().Count());
    }

    /// <summary>The eight the old range missed, which are the ones the bug was about.</summary>
    [Fact]
    public void Las_ocho_que_el_rango_dejaba_fuera_ahora_entran()
    {
        var machines = ShopTable.TechnicalMachines(Names()).ToHashSet();

        foreach (var id in (int[])[618, 619, 620, 690, 691, 692, 693, 694])
        {
            Assert.True(machines.Contains(id), $"el objeto {id} es una MT y no se reconoce");
            Assert.False(ShopTable.IsInMachineRange(id), $"{id} sigue fuera del rango, que es el punto");
        }
    }

    /// <summary>Neither the HMs nor the things sitting between the blocks.</summary>
    [Fact]
    public void Ni_las_MO_ni_los_vecinos_cuentan_como_MT()
    {
        var machines = ShopTable.TechnicalMachines(Names()).ToHashSet();

        foreach (var id in (int[])[4, 420, 421, 616, 617, 621, 689, 695])
        {
            Assert.False(machines.Contains(id), $"el objeto {id} no es una MT");
        }
    }

    /// <summary>
    /// A name is a machine only when everything after the two letters is a digit.
    /// </summary>
    /// <remarks>
    /// «Multiexp» starts with MT in neither language, but «MTarjeta» would, and matching on the
    /// prefix alone would have swallowed anything that happened to begin that way.
    /// </remarks>
    [Fact]
    public void Solo_dos_letras_y_luego_numeros()
    {
        string[] names = ["", "MT01", "TM100", "MTarjeta", "MT", "MT1a", "Poción", "MOTOR"];

        var machines = ShopTable.TechnicalMachines(names).ToHashSet();

        Assert.Contains(1, machines);   // MT01
        Assert.Contains(2, machines);   // TM100
        Assert.DoesNotContain(3, machines);
        Assert.DoesNotContain(4, machines);
        Assert.DoesNotContain(5, machines);
        Assert.DoesNotContain(7, machines);
    }
}
