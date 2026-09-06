using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Asks the emulator who writes into a range, and turns the answer into candidate addresses.
/// </summary>
/// <remarks>
/// <para>
/// The client half of patch 3 (<c>docs/fork/03-donde-vive-el-ps.md</c>), written before the patch
/// exists so that building the fork is the only thing left to do.
/// </para>
/// <para>
/// Why it is the last resort: everything answerable from outside has been answered and it all came
/// back no. The party mirror at <c>0x330128E4</c> is written by the game and <b>never read</b> by
/// it — 120 HP written there, the screen saying 128, and neither opening the party menu nor
/// entering and leaving a battle changed that — and the values are not in plain sight anywhere in
/// four hundred megabytes. But the game does <b>write</b> the true HP into that mirror, so the
/// instruction doing it knows where they come from, and the emulator can name it.
/// </para>
/// <para>
/// The registers are not interpreted: all sixteen are tried as addresses, and each is also tried
/// as a pointer <b>into</b> a party entry rather than to its start, because a copy loop is as
/// likely to hold a pointer to the field as to the record. Whichever reads back as a Pokémon with
/// the right HP is the answer. Picking one by looking clever is how §98 lost a night.
/// </para>
/// </remarks>
public static class WriteWatchProbe
{
    /// <summary>Offset of the current HP inside a party entry, where PK7 keeps it.</summary>
    private const uint HpOffset = 0xF0;

    private static readonly int PartySize = new PKHeX.Core.PK7().SIZE_PARTY;

    public static int Watch(uint address, uint size)
    {
        var client = Attach();

        if (client is null)
        {
            return 1;
        }

        try
        {
            if (!client.WatchWrites(address, size))
            {
                Console.WriteLine("El emulador ha rechazado el rango. O no es el fork con el parche 3,");
                Console.WriteLine("o el rango es mayor de lo que acepta.");
                return 1;
            }
        }
        catch (AzaharRpcException ex)
        {
            Console.WriteLine("El emulador no entiende el paquete: " + ex.Message);
            Console.WriteLine("Hace falta el parche 3. Ver docs/fork/03-donde-vive-el-ps.md.");
            return 1;
        }

        Console.WriteLine(size == 0
            ? $"Dejando de vigilar 0x{address:X8}."
            : $"Vigilando {size} bytes en 0x{address:X8}. Juega hasta que a ese Pokemon le cambien"
              + " los PS -un golpe basta- y luego: Probe --escrituras --leer");

        return 0;
    }

    public static int Read()
    {
        var client = Attach();

        if (client is null)
        {
            return 1;
        }

        var writer = new AzaharGameWriter(client, Path.Combine(Path.GetTempPath(), "permalocke-probe"),
            NullLogger<AzaharGameWriter>.Instance);

        var names = PKHeX.Core.GameInfo.GetStrings("es").specieslist;
        var seen = 0;
        var found = 0;

        while (true)
        {
            IReadOnlyList<MemoryWrite> batch;
            int pending;

            try
            {
                batch = client.ReadWriteLog(out pending);
            }
            catch (AzaharRpcException ex)
            {
                Console.WriteLine("El emulador no entiende el paquete: " + ex.Message);
                return 1;
            }

            foreach (var write in batch)
            {
                seen++;
                Console.WriteLine();
                Console.WriteLine($"pc 0x{write.Pc:X8}  escribio {write.Size} bytes"
                                  + $" en 0x{write.Address:X8}  valor 0x{write.Value:X}");

                found += Candidates(writer, names, write);
            }

            if (pending == 0 || batch.Count == 0)
            {
                break;
            }
        }

        Console.WriteLine();

        if (seen == 0)
        {
            Console.WriteLine("Ni una escritura apuntada. Dos causas posibles, y hay que distinguirlas:");
            Console.WriteLine("  - el juego no ha tocado esos bytes todavia: juega y vuelve a mirar;");
            Console.WriteLine("  - o el enganche no ve las escrituras porque el JIT las hace por su");
            Console.WriteLine("    cuenta. Entonces toca desactivar el JIT en Azahar y repetir.");
            return 1;
        }

        Console.WriteLine(found > 0
            ? $"{found} candidato(s). Ahi es donde el juego guarda los PS de verdad."
            : "Ninguno de los registros lleva a un Pokemon legible. Puede que el juego lo descifre"
              + "\nen una pila para usarlo y la tire, y entonces no hay direccion que clavar.");

        return 0;
    }

    /// <summary>
    /// Every register, printed, and then the ones that read back as a party Pokémon.
    /// </summary>
    /// <remarks>
    /// All sixteen get printed even when none of them recognises anything, and that was not the
    /// first design. The first one only showed the registers that passed the Pokémon test — and the
    /// first real measurement caught the write with a single hit, <c>r6</c>, which turned out to be
    /// the <b>destination</b>. The source could never have passed: it is plaintext, the game
    /// encrypts as it copies, and a decrypting test cannot see plaintext. Filtering the output by a
    /// test that cannot match the thing being looked for is how a search comes back empty and
    /// sounds conclusive.
    /// </remarks>
    private static int Candidates(AzaharGameWriter writer, string[] names, MemoryWrite write)
    {
        var found = 0;

        Console.WriteLine("   " + string.Join("  ",
            write.Registers.Select((value, i) => $"r{i}={value:X8}")));

        for (var r = 0; r < write.Registers.Length; r++)
        {
            var value = write.Registers[r];

            // El registro puede apuntar al principio de la entrada o al campo mismo. Se prueban
            // las dos, porque un bucle de copia lleva tantas veces una cosa como la otra.
            foreach (var start in new[] { value, value - HpOffset })
            {
                if (start < 0x1000 || writer.Read(start) is not { Species: > 0 } pokemon
                    || !pokemon.ChecksumValid || !PartyStats.AreHere(pokemon))
                {
                    continue;
                }

                Console.WriteLine($"   r{r,-2} 0x{value:X8}  ->  entrada en 0x{start:X8}: "
                                  + $"{names[pokemon.Species]} Nv{pokemon.Stat_Level} "
                                  + $"PS {pokemon.Stat_HPCurrent}/{pokemon.Stat_HPMax}");

                found++;
            }
        }

        return found;
    }

    private static AzaharRpcClient? Attach()
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
            return client;
        }
        catch (Exception ex)
        {
            Console.WriteLine("No hay juego cargado en Azahar: " + ex.Message);
            return null;
        }
    }
}
