using System.Buffers.Binary;
using System.Diagnostics;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.Probe;

/// <summary>
/// Hunts for where the game really keeps a Pokémon's current HP, by elimination across sightings.
/// </summary>
/// <remarks>
/// <para>
/// The question this exists to answer: a dead Pokémon should stay dead <b>as itself</b>, at zero
/// HP, instead of being turned into a Shedinja. That needs the address the game reads HP from, and
/// §93 measured that it is <b>not</b> where PermaLocke writes — seven HP were written into the
/// Gyarados at <c>0x330128E4</c>, they stuck for fifteen seconds, and the party menu went on saying
/// 131 of 131.
/// </para>
/// <para>
/// The values are given on the command line, <b>read off the screen by the player</b>. Every run
/// takes one sweep of memory and keeps only the addresses that held the right value <b>every time
/// so far</b>, so the answer comes out of agreement across sightings rather than out of one lucky
/// hit. Change the HP in the game, run it again, and the survivors collapse.
/// </para>
/// <para>
/// Three earlier mistakes are designed out of this, and all three were mine. The first needle was
/// built from <c>Stat_HPCurrent</c> of a save-block copy, which holds no stats, so it searched for a
/// Gyarados of 42649 and its zero hits proved nothing — §53 again, a field only means something
/// where the structure is identified. The second only compared slot zero. The third leaned on the
/// emulator's native search, which <b>truncates at 255 hits per call</b> and says so in its own
/// summary: the totals came back as exactly 510 and then exactly 765, two and three times the cap,
/// and were read as measurements. Nothing here uses it — the sweep reads the memory and does the
/// comparing on this side, where nothing is dropped silently.
/// </para>
/// <para>
/// Four readings of the same bytes are tracked at once, because assuming the shape is how the
/// earlier passes went wrong: aligned and unaligned, and the HP itself as well as the damage taken
/// (<c>max - current</c>), which is the other natural way to store it and which a search for the HP
/// can never find.
/// </para>
/// </remarks>
public static class HpProbe
{
    /// <summary>
    /// Where to look. The first two are what the rest of PermaLocke searches; the others were
    /// added here after checking by hand which addresses the emulator answers with real data. The
    /// process image is where a game keeps a global, and nothing had ever swept it.
    /// </summary>
    private static readonly MemoryRegion[] Regions =
    [
        new(0x08000000, 0x10000000, "monton"),
        new(0x30000000, 0x40000000, "linear"),
        new(0x00100000, 0x01000000, "imagen"),
        new(0x10000000, 0x11000000, "compartida")
    ];

    private const int BlockSize = 0x1000;

    public static int Run(int current, int max, bool restart)
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("No hay juego cargado en Azahar: " + ex.Message);
            return 1;
        }

        var layouts = PartyLayoutLocator.Distinct(new PartyLayoutLocator(client).LocateAll(string.Empty));

        Console.WriteLine($"PS {current} de {max}, leidos de la pantalla y no de la memoria.");
        Console.WriteLine();

        Decrypted(client, layouts);
        Console.WriteLine();

        var readings = Readings(current, max);
        var clock = Stopwatch.StartNew();
        var seen = Sweep(client, readings);

        Console.WriteLine($"Barrido en {clock.Elapsed.TotalSeconds:F1} s.");
        Console.WriteLine();

        var survivors = 0;
        var crossed = false;

        foreach (var reading in readings)
        {
            var (kept, compared) = Cross(reading, seen[reading.Key], restart, layouts);

            survivors += kept;
            crossed |= compared;
        }

        Console.WriteLine();

        if (!crossed)
        {
            Console.WriteLine("Primera vista: no hay nada con que cruzar todavia. Cambia los PS en el");
            Console.WriteLine("juego SIN entrar en combate -una pocion, una baya- y repite con el numero");
            Console.WriteLine("nuevo. Ahi es donde empieza a eliminar.");
        }
        else if (survivors == 0)
        {
            Console.WriteLine("No queda ni un candidato en ninguna lectura. Eso significa que ningun");
            Console.WriteLine("sitio de la memoria ha seguido a los PS, asi que el juego los DERIVA al");
            Console.WriteLine("dibujar y no hay direccion que clavar.");
        }
        else
        {
            Console.WriteLine("Cambia los PS en el juego SIN entrar en combate -una pocion, una baya- y");
            Console.WriteLine("repite con el numero nuevo. Cada vez sobreviven menos.");
        }

        return 0;
    }

    /// <summary>
    /// What every known structure says the battle stats are, once decrypted.
    /// </summary>
    /// <remarks>
    /// A party <c>PK7</c> keeps its battle stats <b>encrypted</b> as well — PKHeX crypts the tail
    /// in a second pass with the same seed — so a sweep of memory for the HP in plain sight could
    /// never have found them there, however wide it went. That is worth printing before any sweep:
    /// if one of these rows matches the screen, the answer was inside a structure PermaLocke
    /// already knows, and the whole search was looking for something that cannot look like itself.
    /// </remarks>
    private static void Decrypted(AzaharRpcClient client, IReadOnlyList<PartyLayout> layouts)
    {
        var writer = new AzaharGameWriter(client, Path.Combine(Path.GetTempPath(), "permalocke-probe"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AzaharGameWriter>.Instance);

        var names = PKHeX.Core.GameInfo.GetStrings("es").specieslist;

        foreach (var layout in layouts)
        {
            Console.WriteLine($"0x{layout.Address:X8} salto 0x{layout.Stride:X}");

            for (var slot = 0; slot < 6; slot++)
            {
                if (writer.Read(layout.SlotAddress(slot)) is not { Species: > 0 } pokemon
                    || !pokemon.ChecksumValid)
                {
                    continue;
                }

                Console.WriteLine($"   hueco {slot}  {names[pokemon.Species],-12} Nv{pokemon.Stat_Level,-3}"
                                  + $" PS {pokemon.Stat_HPCurrent}/{pokemon.Stat_HPMax}");
            }
        }
    }

    /// <summary>
    /// Writes an HP into one party slot through the proper path, so the screen can be looked at.
    /// </summary>
    /// <remarks>
    /// §93 wrote a plaintext byte into <c>0xF0</c>, the menu did not change, and it was written down
    /// that HP could not be pinned. The byte landed; it just was not the number, because that offset
    /// is encrypted with the rest of the entry. This goes through <see cref="AzaharGameWriter"/>,
    /// which decrypts, changes the field, re-encrypts and reads back — and it exists so the claim
    /// gets checked against the party menu instead of against a comment.
    /// </remarks>
    public static int Write(int slot, int hp)
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("No hay juego cargado en Azahar: " + ex.Message);
            return 1;
        }

        var layouts = PartyLayoutLocator.Distinct(new PartyLayoutLocator(client).LocateAll(string.Empty));
        var writer = new AzaharGameWriter(client, Path.Combine(AppContext.BaseDirectory, "copias-ps"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AzaharGameWriter>.Instance);

        var names = PKHeX.Core.GameInfo.GetStrings("es").specieslist;
        var written = 0;

        foreach (var layout in layouts)
        {
            var address = layout.SlotAddress(slot);

            if (writer.Read(address) is not { ChecksumValid: true } pokemon
                || !PartyStats.AreHere(pokemon))
            {
                Console.WriteLine($"0x{address:X8}  la cola no son las estadisticas; no se toca");
                continue;
            }

            Console.WriteLine($"0x{address:X8}  {names[pokemon.Species]} "
                              + $"PS {pokemon.Stat_HPCurrent}/{pokemon.Stat_HPMax} -> {hp}");

            var result = writer.SetHp(address, hp, pokemon.PID);

            if (writer.Read(address) is { } after)
            {
                Console.WriteLine($"   releido: PS {after.Stat_HPCurrent}/{after.Stat_HPMax}"
                                  + $"  ({result.Verified} de {result.Written} bytes confirmados)");
            }

            written += result.Applied ? 1 : 0;
        }

        Console.WriteLine();
        Console.WriteLine(written > 0
            ? "Escrito. AHORA ABRE EL MENU DEL EQUIPO y mira si lo refleja: eso es lo que el §93 no comprobo."
            : "No se ha escrito en ninguna copia.");

        return 0;
    }

    /// <summary>
    /// Looks for the party's battle stats as a <b>table</b>: everybody's HP at a constant stride.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists because narrowing by intersection failed, and failed for a reason worth writing
    /// down: the working copies of the party are <b>heap allocations</b> — the entries at the
    /// <c>0x1E4</c> stride are followed by allocator headers, marks <c>DU</c> and <c>RF</c> with a
    /// size and two pointers — and a heap allocation moves. Between two sightings the player opened
    /// the bag and used a potion, which allocates and frees, so any candidate living there was
    /// discarded for having moved rather than for being wrong.
    /// </para>
    /// <para>
    /// So this asks a question that one photograph can answer. Every party member's HP is known —
    /// the mirror at <c>0x330128E4</c> reports them exactly, it just is not what the game reads —
    /// and a table holding <b>five different Pokémon's HP at one constant stride</b> is not a
    /// coincidence, where a single 128 in four hundred megabytes is nothing at all. It is §22's
    /// method: find the structure by its internal agreement, not the value by its face.
    /// </para>
    /// </remarks>
    public static int Table(int maxStride)
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("No hay juego cargado en Azahar: " + ex.Message);
            return 1;
        }

        var layouts = PartyLayoutLocator.Distinct(new PartyLayoutLocator(client).LocateAll(string.Empty));
        var party = Mirror(client, layouts);

        if (party.Count < 3)
        {
            Console.WriteLine("Hacen falta al menos tres del equipo con estadisticas legibles.");
            return 1;
        }

        Console.WriteLine("El equipo, segun el espejo que si los refleja:");

        foreach (var member in party)
        {
            Console.WriteLine($"   hueco {member.Slot}  {member.Name,-12} PS {member.Hp}/{member.Max}");
        }

        var wanted = party.SelectMany(m => new[] { m.Hp, m.Max }).Distinct().ToArray();
        var clock = Stopwatch.StartNew();
        var where = Locate(client, wanted);

        Console.WriteLine();
        Console.WriteLine($"Barrido en {clock.Elapsed.TotalSeconds:F1} s.");

        foreach (var value in wanted.Order())
        {
            Console.WriteLine($"   {value,5} aparece {where[value].Count,7} veces");
        }

        Console.WriteLine();

        var hits = Arrange(party, where, maxStride, member => member.Hp, "los PS actuales")
                   + Arrange(party, where, maxStride, member => member.Max, "los PS maximos");

        Console.WriteLine();
        Console.WriteLine(hits > 0
            ? "Cada linea es una tabla candidata. La buena tiene que seguir siendo tabla despues de"
              + "\ncurar a alguien, y ahi es donde se confirma."
            : "Ninguna tabla. El juego no guarda los PS del equipo seguidos a un paso constante,"
              + "\nal menos no en claro y no dentro de " + maxStride + " bytes por Pokemon.");

        return 0;
    }

    /// <summary>
    /// Looks for each Pokémon's <b>own</b> stat block: its six values inside one small window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table search asked for the party at a constant stride and found none, and the reason is
    /// visible in the structures themselves: the party's working copies are separate heap
    /// allocations — followed by allocator headers, and appearing and disappearing between passes,
    /// five one minute and two the next. Separate allocations have no stride between them at all,
    /// so a search that needs one cannot find them however wide it looks.
    /// </para>
    /// <para>
    /// This drops that requirement and keeps the specificity somewhere better: <b>six stats of one
    /// Pokémon within a hundred and twenty eight bytes</b>. That is the §22 method — find the
    /// structure by its internal agreement — and it is what the very first attempt tried, except
    /// that one built its needle out of a copy that holds no stats and went looking for a Gyarados
    /// of 42649. The numbers now come from the mirror, which is measured to be right.
    /// </para>
    /// </remarks>
    public static int Block(int window)
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("No hay juego cargado en Azahar: " + ex.Message);
            return 1;
        }

        var layouts = PartyLayoutLocator.Distinct(new PartyLayoutLocator(client).LocateAll(string.Empty));
        var party = Blocks(client, layouts);

        if (party.Count == 0)
        {
            Console.WriteLine("No se ha podido leer ninguna estructura con estadisticas.");
            return 1;
        }

        var values = party.SelectMany(m => m.Stats).Distinct().Where(v => v > 0).ToArray();
        var clock = Stopwatch.StartNew();
        var where = Locate(client, values);

        Console.WriteLine();
        Console.WriteLine($"Barrido en {clock.Elapsed.TotalSeconds:F1} s. Ventana de {window} bytes.");
        Console.WriteLine();

        var total = 0;

        foreach (var member in party)
        {
            total += Near(member, where, window, layouts);
        }

        Console.WriteLine();
        Console.WriteLine(total > 0
            ? "Cada acierto es un sitio donde los seis valores de ese Pokemon estan juntos."
            : "Ni un sitio con los seis valores juntos, fuera de donde ya se sabia. Entonces el juego"
              + "\nno tiene las estadisticas del equipo en claro, y clavar los PS por memoria no se"
              + "\npuede hacer sin descifrar lo que el juego usa de verdad.");

        return 0;
    }

    /// <summary>
    /// Finds every copy of a party Pokémon in memory by its encryption constant, and decrypts each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what the whole search should have started from. The battle stats are <b>encrypted</b>
    /// with the rest of a party entry, so no sweep for the numbers in plain sight can ever find the
    /// store — and none did: not as a table at any stride, not as a six stat block, nowhere but the
    /// health bar, which is paint. But the <b>encryption constant is the one field kept in the
    /// clear</b>, in the first four bytes, so the copies can be found by identity and then read.
    /// </para>
    /// <para>
    /// It is worth doing now and was not before, because there is a disagreement to exploit: the
    /// mirror at <c>0x330128E4</c> was written to 120 and the game went on saying 128, so whichever
    /// copy reads <b>128</b> is the one the game actually keeps. Before that write every copy agreed
    /// and none of them could be told apart.
    /// </para>
    /// </remarks>
    public static int Copies()
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("No hay juego cargado en Azahar: " + ex.Message);
            return 1;
        }

        var layouts = PartyLayoutLocator.Distinct(new PartyLayoutLocator(client).LocateAll(string.Empty));
        var writer = new AzaharGameWriter(client, Path.Combine(Path.GetTempPath(), "permalocke-probe"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AzaharGameWriter>.Instance);

        var names = PKHeX.Core.GameInfo.GetStrings("es").specieslist;
        var wanted = new Dictionary<uint, string>();

        foreach (var layout in layouts)
        {
            for (var slot = 0; slot < 6; slot++)
            {
                if (writer.Read(layout.SlotAddress(slot)) is { Species: > 0 } pokemon
                    && PartyStats.AreHere(pokemon))
                {
                    wanted[pokemon.EncryptionConstant] = names[pokemon.Species];
                }
            }
        }

        if (wanted.Count == 0)
        {
            Console.WriteLine("No se ha podido leer ninguna estructura con estadisticas.");
            return 1;
        }

        foreach (var (constant, name) in wanted)
        {
            Console.WriteLine($"   {name,-12} EC {constant:X8}");
        }

        var clock = Stopwatch.StartNew();
        var sightings = Constants(client, [.. wanted.Keys]);

        Console.WriteLine();
        Console.WriteLine($"Barrido en {clock.Elapsed.TotalSeconds:F1} s.");

        foreach (var (constant, name) in wanted)
        {
            Console.WriteLine();
            Console.WriteLine($"-- {name}, {sightings[constant].Count} copias");

            foreach (var address in sightings[constant].Order())
            {
                var pokemon = writer.Read(address);

                Console.WriteLine($"   0x{address:X8}  "
                                  + (pokemon is null ? "ilegible"
                                      : $"{(pokemon.ChecksumValid ? "firma OK " : "firma MAL")} "
                                        + $"#{pokemon.Species} Nv{pokemon.Stat_Level,-3} "
                                        + $"PS {pokemon.Stat_HPCurrent}/{pokemon.Stat_HPMax}"
                                        + (PartyStats.AreHere(pokemon) ? "  <-- estadisticas reales" : ""))
                                  + $"  {Where(layouts, address)}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Looks for the battle stats inside the <b>decrypted</b> entries, at any offset at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every search so far read these structures raw, and they are encrypted, so the numbers could
    /// not have been there to find. And the one search that did decrypt them only ever looked at
    /// <c>0xF0</c>, where a PK7 keeps HP — but §53 already said the authoritative copy «keeps its
    /// battle stats somewhere else», and nobody went looking for where.
    /// </para>
    /// <para>
    /// What makes it answerable now is a disagreement we made on purpose: the mirror was written to
    /// <b>120</b> and the game kept saying <b>128</b>. So an offset holding 128 for the Gyarados,
    /// and each of the others' own HP at that same offset, is the store the game reads — and the
    /// mirror, which says 120, cannot be mistaken for it.
    /// </para>
    /// </remarks>
    public static int Tail(int trueHp)
    {
        var client = new AzaharRpcClient();

        try
        {
            client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("No hay juego cargado en Azahar: " + ex.Message);
            return 1;
        }

        var layouts = PartyLayoutLocator.Distinct(new PartyLayoutLocator(client).LocateAll(string.Empty));
        var writer = new AzaharGameWriter(client, Path.Combine(Path.GetTempPath(), "permalocke-probe"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AzaharGameWriter>.Instance);

        var names = PKHeX.Core.GameInfo.GetStrings("es").specieslist;
        var expected = new Dictionary<uint, int>();
        var owner = new Dictionary<uint, string>();

        // Lo que cada uno tiene DE VERDAD, por su constante de encriptacion, que es lo unico que
        // identifica a un Pokemon en cualquier estructura.
        foreach (var layout in layouts)
        {
            for (var slot = 0; slot < 6; slot++)
            {
                if (writer.Read(layout.SlotAddress(slot)) is { Species: > 0 } pokemon
                    && PartyStats.AreHere(pokemon))
                {
                    expected[pokemon.EncryptionConstant] = pokemon.Stat_HPCurrent;
                    owner[pokemon.EncryptionConstant] = names[pokemon.Species];
                }
            }
        }

        if (expected.Count == 0)
        {
            Console.WriteLine("No se ha podido leer ninguna estructura con estadisticas.");
            return 1;
        }

        // Y el del hueco cero es el que sabemos que el espejo miente: manda la pantalla.
        var gyarados = expected.First();

        expected[gyarados.Key] = trueHp;

        foreach (var (constant, hp) in expected)
        {
            Console.WriteLine($"   {owner[constant],-12} EC {constant:X8}  PS de verdad {hp}");
        }

        foreach (var layout in layouts)
        {
            Console.WriteLine();
            Console.WriteLine($"-- 0x{layout.Address:X8} salto 0x{layout.Stride:X}");

            Inside(client, writer, layout, expected, owner);
        }

        return 0;
    }

    /// <summary>Offsets where every slot of a structure holds its own occupant's true HP.</summary>
    private static void Inside(AzaharRpcClient client, AzaharGameWriter writer, PartyLayout layout,
        Dictionary<uint, int> expected, Dictionary<uint, string> owner)
    {
        var seats = new List<(byte[] Bytes, int Hp, string Name)>();

        for (var slot = 0; slot < 6; slot++)
        {
            var address = layout.SlotAddress(slot);

            if (!client.TryReadMemory(address, (int)layout.Stride, out var raw)
                || writer.Read(address) is not { } pokemon
                || !expected.TryGetValue(pokemon.EncryptionConstant, out var hp))
            {
                continue;
            }

            // PK7 descifra EN EL SITIO el array que se le da, asi que una copia queda descifrada.
            // Aqui eso es justo lo que se quiere; en el vigilante fue lo que corrompio una partida.
            var view = new byte[layout.Stride];

            raw.CopyTo(view, 0);

            var head = view.AsSpan(0, PartyBytes).ToArray();

            _ = new PKHeX.Core.PK7(head);
            head.CopyTo(view, 0);

            seats.Add((view, hp, owner[pokemon.EncryptionConstant]));
        }

        if (seats.Count < 3)
        {
            Console.WriteLine($"   solo {seats.Count} identificados; no basta para decidir");
            return;
        }

        Console.WriteLine($"   {seats.Count} identificados: "
                          + string.Join(", ", seats.Select(s => $"{s.Name} {s.Hp}")));

        var found = 0;

        for (var offset = 0; offset + 2 <= layout.Stride; offset++)
        {
            if (seats.All(seat => BinaryPrimitives.ReadUInt16LittleEndian(seat.Bytes.AsSpan(offset))
                                  == seat.Hp))
            {
                Console.WriteLine($"   0x{offset:X3}  todos con sus PS de verdad  <-- AQUI");
                found++;
            }
        }

        if (found == 0)
        {
            Console.WriteLine("   ningun offset con los PS de verdad de todos.");
        }
    }

    private static readonly int PartyBytes = new PKHeX.Core.PK7().SIZE_PARTY;

    /// <summary>Every place a four byte encryption constant appears, at any alignment.</summary>
    private static Dictionary<uint, HashSet<uint>> Constants(AzaharRpcClient client, uint[] constants)
    {
        var found = constants.ToDictionary(constant => constant, _ => new HashSet<uint>());
        var requests = 0;

        foreach (var region in Regions)
        {
            for (var address = region.Start; address < region.End; address += BlockSize - 4)
            {
                var size = (int)Math.Min(BlockSize, region.End - address);

                if (!client.TryReadMemory(address, size, out var data))
                {
                    continue;
                }

                for (var offset = 0; offset + 4 <= data.Length; offset++)
                {
                    var value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));

                    if (found.TryGetValue(value, out var set))
                    {
                        set.Add(address + (uint)offset);
                    }
                }

                if (++requests % 256 == 0)
                {
                    Thread.Sleep(1);
                }
            }
        }

        return found;
    }

    /// <summary>Each party member with its six battle stats, from the structure that has them.</summary>
    private static List<Block6> Blocks(AzaharRpcClient client, IReadOnlyList<PartyLayout> layouts)
    {
        var writer = new AzaharGameWriter(client, Path.Combine(Path.GetTempPath(), "permalocke-probe"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AzaharGameWriter>.Instance);

        var names = PKHeX.Core.GameInfo.GetStrings("es").specieslist;
        var best = new List<Block6>();

        foreach (var layout in layouts)
        {
            var party = new List<Block6>();

            for (var slot = 0; slot < 6; slot++)
            {
                if (writer.Read(layout.SlotAddress(slot)) is not { Species: > 0 } pokemon
                    || !PartyStats.AreHere(pokemon))
                {
                    continue;
                }

                party.Add(new Block6(names[pokemon.Species],
                [
                    pokemon.Stat_HPMax, pokemon.Stat_ATK, pokemon.Stat_DEF,
                    pokemon.Stat_SPE, pokemon.Stat_SPA, pokemon.Stat_SPD
                ]));
            }

            if (party.Count > best.Count)
            {
                best = party;
            }
        }

        foreach (var member in best)
        {
            Console.WriteLine($"   {member.Name,-12} {string.Join(" ", member.Stats)}");
        }

        return best;
    }

    /// <summary>Where all six of one Pokémon's stats sit inside a single window.</summary>
    private static int Near(Block6 member, Dictionary<int, HashSet<uint>> where, int window,
        IReadOnlyList<PartyLayout> layouts)
    {
        // Anclado en el valor mas raro de los seis, que es la lista mas corta que recorrer.
        var anchor = member.Stats.Where(v => v > 0).MinBy(value => where[value].Count);
        var others = member.Stats.Where(v => v > 0 && v != anchor).Distinct().ToArray();
        var found = 0;

        foreach (var seat in where[anchor])
        {
            if (!others.All(value => Within(where[value], seat, window)))
            {
                continue;
            }

            Console.WriteLine($"   {member.Name,-12} 0x{seat:X8}  {Where(layouts, seat)}");

            if (++found >= 12)
            {
                Console.WriteLine($"   {member.Name,-12} ... y mas");
                break;
            }
        }

        if (found == 0)
        {
            Console.WriteLine($"   {member.Name,-12} en ningun sitio con los otros cinco al lado");
        }

        return found;
    }

    private static bool Within(HashSet<uint> places, uint seat, int window)
    {
        for (var offset = -window; offset <= window; offset++)
        {
            if (offset != 0 && seat + (uint)offset is var at && places.Contains(at))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record Block6(string Name, int[] Stats);

    /// <summary>The party as the mirror reports it, which is accurate even though it is not read.</summary>
    private static List<Member> Mirror(AzaharRpcClient client, IReadOnlyList<PartyLayout> layouts)
    {
        var writer = new AzaharGameWriter(client, Path.Combine(Path.GetTempPath(), "permalocke-probe"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AzaharGameWriter>.Instance);

        var names = PKHeX.Core.GameInfo.GetStrings("es").specieslist;

        // TODAS, y se elige la que mas miembros trae. Coger «la primera con tres» daba una lista
        // distinta en cada pasada -una vez sin Tinkaton en su hueco y con un Dracovish que la
        // partida guardada no tiene-, y una busqueda alimentada con un equipo que no es el equipo
        // no puede encontrar nada aunque lo tenga delante.
        var best = new List<Member>();

        foreach (var layout in layouts)
        {
            var party = new List<Member>();

            for (var slot = 0; slot < 6; slot++)
            {
                if (writer.Read(layout.SlotAddress(slot)) is { Species: > 0 } pokemon
                    && PartyStats.AreHere(pokemon))
                {
                    party.Add(new Member(slot, names[pokemon.Species],
                        pokemon.Stat_HPCurrent, pokemon.Stat_HPMax));
                }
            }

            Console.WriteLine($"0x{layout.Address:X8} salto 0x{layout.Stride:X}: "
                              + (party.Count == 0
                                  ? "ninguno con estadisticas legibles"
                                  : string.Join(", ", party.Select(m =>
                                      $"[{m.Slot}] {m.Name} {m.Hp}/{m.Max}"))));

            if (party.Count > best.Count)
            {
                best = party;
            }
        }

        return best;
    }

    /// <summary>One pass, collecting where each of a handful of values lives, at any alignment.</summary>
    private static Dictionary<int, HashSet<uint>> Locate(AzaharRpcClient client, int[] values)
    {
        var found = values.ToDictionary(value => value, _ => new HashSet<uint>());
        var requests = 0;

        foreach (var region in Regions)
        {
            for (var address = region.Start; address < region.End; address += BlockSize - 2)
            {
                var size = (int)Math.Min(BlockSize, region.End - address);

                if (!client.TryReadMemory(address, size, out var data))
                {
                    continue;
                }

                for (var offset = 0; offset + 2 <= data.Length; offset++)
                {
                    var value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));

                    if (found.TryGetValue(value, out var set))
                    {
                        set.Add(address + (uint)offset);
                    }
                }

                if (++requests % 256 == 0)
                {
                    Thread.Sleep(1);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Every base and stride at which the whole party's chosen value lines up in slot order.
    /// </summary>
    /// <remarks>
    /// Anchored on whichever member's number is rarest in memory, because that is the shortest list
    /// to walk and the answer does not depend on which one is used. A candidate has to satisfy
    /// <b>every</b> member that could be read, which is what makes a hit mean something.
    /// </remarks>
    private static int Arrange(List<Member> party, Dictionary<int, HashSet<uint>> where, int maxStride,
        Func<Member, int> pick, string what)
    {
        var anchor = party.MinBy(member => where[pick(member)].Count)!;
        var others = party.Where(member => member.Slot != anchor.Slot).ToList();

        // Ni todo ni nada. Exigir los cinco no encuentra una tabla a la que le falte uno -porque
        // ese viva aparte, o porque su hueco sea el que esta roto-, y aceptar dos es ruido. Se
        // cuentan y se ordena por cuantos cuadran, que ademas dice cuanto vale cada candidata.
        var least = Math.Max(2, others.Count - 1);
        var found = new List<(long Start, int Stride, int Agree)>();

        Console.WriteLine($"{what}: anclado en {anchor.Name} ({pick(anchor)}), "
                          + $"{where[pick(anchor)].Count} sitios, paso hasta {maxStride}");

        foreach (var seat in where[pick(anchor)])
        {
            for (var stride = 2; stride <= maxStride; stride += 2)
            {
                // De donde arrancaria la tabla si esta fuese la entrada de este Pokemon.
                var start = (long)seat - ((long)anchor.Slot * stride);

                if (start < 0)
                {
                    continue;
                }

                var agree = others.Count(member =>
                    where[pick(member)].Contains((uint)(start + (member.Slot * stride))));

                if (agree >= least)
                {
                    found.Add((start, stride, agree + 1));
                }
            }
        }

        foreach (var candidate in found.OrderByDescending(c => c.Agree).ThenBy(c => c.Stride).Take(30))
        {
            Console.WriteLine($"   0x{candidate.Start:X8}  paso {candidate.Stride}"
                              + $" (0x{candidate.Stride:X})  cuadran {candidate.Agree} de {party.Count}");
        }

        if (found.Count == 0)
        {
            Console.WriteLine("   ninguna.");
        }
        else if (found.Count > 30)
        {
            Console.WriteLine($"   ... y {found.Count - 30} mas.");
        }

        return found.Count;
    }

    private sealed record Member(int Slot, string Name, int Hp, int Max);

    /// <summary>The four ways the same two numbers could be written down.</summary>
    private static Reading[] Readings(int current, int max) =>
    [
        new("par-ps", "u16 alineado, los PS", current, 2),
        new("impar-ps", "u16 desalineado, los PS", current, 1),
        new("par-dano", "u16 alineado, el dano recibido", max - current, 2),
        new("impar-dano", "u16 desalineado, el dano recibido", max - current, 1)
    ];

    /// <summary>
    /// One pass over memory, testing every reading, with the comparing done on this side.
    /// </summary>
    /// <remarks>
    /// Chunks overlap by two bytes so a value straddling a boundary is not lost, and the alignment
    /// of an address is judged on the address itself rather than on its offset inside the chunk.
    /// </remarks>
    private static Dictionary<string, HashSet<uint>> Sweep(AzaharRpcClient client, Reading[] readings)
    {
        var found = readings.ToDictionary(reading => reading.Key, _ => new HashSet<uint>());
        var requests = 0;

        foreach (var region in Regions)
        {
            for (var address = region.Start; address < region.End; address += BlockSize - 2)
            {
                var size = (int)Math.Min(BlockSize, region.End - address);

                if (client.TryReadMemory(address, size, out var data))
                {
                    Match(data, address, readings, found);
                }

                // Decenas de miles de peticiones seguidas han llegado a tumbar el emulador.
                if (++requests % 256 == 0)
                {
                    Thread.Sleep(1);
                }
            }
        }

        return found;
    }

    private static void Match(byte[] data, uint address, Reading[] readings,
        Dictionary<string, HashSet<uint>> found)
    {
        for (var offset = 0; offset + 2 <= data.Length; offset++)
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
            var at = address + (uint)offset;

            foreach (var reading in readings)
            {
                if (value == reading.Value && at % 2 == reading.Alignment % 2)
                {
                    found[reading.Key].Add(at);
                }
            }
        }
    }

    /// <summary>Keeps only what has held the right value on every sighting so far.</summary>
    private static (int Kept, bool Compared) Cross(Reading reading, HashSet<uint> now, bool restart,
        IReadOnlyList<PartyLayout> layouts)
    {
        var file = Path.Combine(AppContext.BaseDirectory, $"ps-{reading.Key}.txt");
        var had = !restart && File.Exists(file)
            ? File.ReadAllLines(file).Select(uint.Parse).ToHashSet()
            : null;

        var survivors = had is null ? now : now.Intersect(had).ToHashSet();

        File.WriteAllLines(file, survivors.Select(address => address.ToString()));

        Console.WriteLine($"{reading.Name,-32} valor {reading.Value,5}"
                          + $"  {now.Count,6} ahora"
                          + (had is null ? "   (primera vez)" : $"  ->{survivors.Count,6} que ya lo tenian"));

        foreach (var address in survivors.Take(had is null ? 0 : 12).Order())
        {
            Console.WriteLine($"      0x{address:X8}  {Where(layouts, address)}");
        }

        return (survivors.Count, had is not null);
    }

    /// <summary>Which known party entry an address falls in, and how far into it.</summary>
    private static string Where(IReadOnlyList<PartyLayout> layouts, uint address)
    {
        foreach (var layout in layouts)
        {
            var span = layout.Stride * 6;

            if (address >= layout.Address && address < layout.Address + span)
            {
                var into = address - layout.Address;

                return $"en 0x{layout.Address:X8} (salto 0x{layout.Stride:X}), "
                       + $"hueco {into / layout.Stride}, desplazamiento 0x{into % layout.Stride:X}";
            }
        }

        return "fuera de las estructuras conocidas";
    }

    /// <param name="Alignment">1 for odd addresses, 2 for even ones.</param>
    private sealed record Reading(string Key, string Name, int Value, int Alignment);
}
