using PermaLocke.Randomizer.Modules;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// La tabla de las cien MT, que vive dentro del ejecutable y no en el RomFS.
/// </summary>
/// <remarks>
/// Salió de jugar: tras la segunda prueba Kukui regala una MT y era Falso Tortazo, la misma que
/// trae el cartucho. PermaLocke movía las MT de sitio pero nunca cambiaba lo que enseñan, porque
/// esa lista no está en ningún GARC.
/// </remarks>
public sealed class MachineTableTests
{
    private static readonly int[] Hms = [15, 19, 57, 70, 127, 249, 291];

    /// <summary>Un ejecutable de mentira con la tabla enterrada en medio de ruido.</summary>
    private static byte[] Fake(int at, IReadOnlyList<int> moves, IReadOnlyList<int> hms)
    {
        var code = new byte[at + ((moves.Count + hms.Count) * 2) + 64];

        // Ruido que NO puede confundirse con la tabla: valores muy por encima de un id de
        // movimiento, para que un falso positivo sea culpa del buscador y no del relleno.
        for (var i = 0; i < code.Length; i += 2)
        {
            BitConverter.TryWriteBytes(code.AsSpan(i), (ushort)60000);
        }

        for (var i = 0; i < moves.Count; i++)
        {
            BitConverter.TryWriteBytes(code.AsSpan(at + (i * 2)), (ushort)moves[i]);
        }

        for (var i = 0; i < hms.Count; i++)
        {
            BitConverter.TryWriteBytes(code.AsSpan(at + ((moves.Count + i) * 2)), (ushort)hms[i]);
        }

        return code;
    }

    /// <summary>La lista del cartucho: empieza en Machaca y acaba en Confidencia.</summary>
    private static int[] Cartridge()
    {
        var moves = new int[MachineTable.Count];
        moves[0] = 526; moves[1] = 337; moves[2] = 473; moves[3] = 347;

        // El resto, ids válidos y distintos. Lo que importa aquí es la forma, no cuáles son.
        for (var i = 4; i < moves.Length; i++)
        {
            moves[i] = 100 + i;
        }

        moves[53] = 206;   // Falso Tortazo, la que reportó el jugador
        moves[99] = 590;   // Confidencia
        return moves;
    }

    [Fact]
    public void Encuentra_la_tabla_por_su_firma()
    {
        var code = Fake(4096, Cartridge(), Hms);

        Assert.Equal(4096, MachineTable.Find(code, Hms));
    }

    [Fact]
    public void Lee_las_cien_y_la_54_es_Falso_Tortazo()
    {
        var code = Fake(1024, Cartridge(), Hms);
        var moves = MachineTable.Read(code, MachineTable.Find(code, Hms));

        Assert.Equal(100, moves.Length);
        Assert.Equal(206, moves[53]);
        Assert.Equal(590, moves[99]);
    }

    /// <summary>
    /// Y la vuelve a encontrar después de barajarla, que es lo que permite verificar lo escrito.
    /// </summary>
    /// <remarks>
    /// La firma ya no sirve: se ha sobrescrito. Lo que la identifica entonces es la forma — cien
    /// ids válidos, todos distintos, seguidos de las siete MO, que este módulo nunca toca
    /// precisamente para que sigan sirviendo de marca.
    /// </remarks>
    [Fact]
    public void La_sigue_encontrando_una_vez_barajada()
    {
        var code = Fake(2048, Cartridge(), Hms);
        var shuffled = Cartridge().Reverse().ToArray();

        MachineTable.Write(code, 2048, shuffled);

        Assert.Equal(2048, MachineTable.Find(code, Hms));
        Assert.Equal(shuffled, MachineTable.Read(code, 2048));
    }

    /// <summary>Escribir no cambia ni un byte fuera de las cien entradas.</summary>
    /// <remarks>
    /// Es un ejecutable: un byte de más en el sitio equivocado no da un juego con las MT raras, da
    /// un juego que no arranca.
    /// </remarks>
    [Fact]
    public void No_toca_ni_un_byte_fuera_de_la_tabla()
    {
        var code = Fake(512, Cartridge(), Hms);
        var before = code.ToArray();

        MachineTable.Write(code, 512, Cartridge().Reverse().ToArray());

        for (var i = 0; i < code.Length; i++)
        {
            var inside = i >= 512 && i < 512 + (MachineTable.Count * 2);

            if (!inside)
            {
                Assert.Equal(before[i], code[i]);
            }
        }
    }

    [Fact]
    public void Escribir_un_numero_de_MT_distinto_de_cien_se_niega()
    {
        var code = Fake(256, Cartridge(), Hms);

        Assert.Throws<ArgumentException>(() => MachineTable.Write(code, 256, [1, 2, 3]));
    }

    /// <summary>Sin tabla no se inventa una posición.</summary>
    [Fact]
    public void Sin_tabla_devuelve_menos_uno()
    {
        var code = new byte[8192];
        Array.Fill(code, (byte)0xFF);

        Assert.Equal(-1, MachineTable.Find(code, Hms));
    }

    /// <summary>Una tabla con una MT repetida no es la tabla: se rechaza por la forma.</summary>
    [Fact]
    public void Una_repetida_no_cuela_como_tabla()
    {
        var moves = Cartridge();
        moves[0] = moves[1];              // rompe la firma y duplica
        var code = Fake(4096, moves, Hms);

        Assert.Equal(-1, MachineTable.Find(code, Hms));
    }
}
