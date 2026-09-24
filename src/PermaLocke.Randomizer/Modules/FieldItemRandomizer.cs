using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core;
using pk3DS.Core.CTR;

namespace PermaLocke.Randomizer.Modules;

/// <param name="TechnicalMachines">Gold Poké Balls, the ones holding a TM.</param>
/// <param name="RegularItems">Ordinary Poké Balls and berry piles.</param>
/// <param name="Zones">Zones that had something on the ground.</param>
public sealed record FieldItemResult(int TechnicalMachines, int RegularItems, int Zones);

/// <summary>
/// Rewrites the items lying on the ground: ordinary Poké Balls for items, gold ones for TMs.
/// <para>
/// In both modes a TM becomes another TM and an ordinary item another ordinary item, and only the
/// spots the cartridge already has are touched.
/// </para>
/// <para>
/// <see cref="FieldItemsMode.Shuffle"/> shuffles what is already there among itself, so no id ever
/// appears that the cartridge did not already place somewhere and the count of each thing is kept.
/// <see cref="FieldItemsMode.Random"/> draws every spot, with a cap on repeats, from the general,
/// medicine and berry pockets — the shape of the reference's world (§163). Key items are out in
/// both: the cartridge places none in a ball, and the random pool leaves their pocket out, so a key
/// item never replaces a Potion and a Potion never replaces one.
/// </para>
/// </summary>
public sealed class FieldItemRandomizer(RomWorkspace workspace, RandomizerOptions? options = null)
{
    /// <summary>The cartridge's own pocket numbers (<c>PocketField</c>, bits 7-10 of the item's packed field).</summary>
    /// <remarks>
    /// Measured on 2026-09-22 against PKHeX's pouch lists, all 716 of them: 0 general, 1 medicine, 2 TMs and HMs,
    /// 3 berries, 4 key items, 5 and 7 Z-Crystals, 6 Roto powers. Not one disagreed.
    /// </remarks>
    public const int GeneralPocket = 0, MedicinePocket = 1, BerryPocket = 3;

    public FieldItemResult Apply(IRandomSource random, GARC.LazyGARC encounterData, CancellationToken ct = default)
    {
        var itemNames = workspace.Config.GetText(TextName.ItemNames);

        // Qué es una MT lo dice el cartucho por su nombre, no un rango escrito aquí. Las cien no
        // son un tramo seguido -son 328-419, 618-620 y 690-694-, y darlo por seguido dejaba ocho
        // fuera: una Poké Ball dorada con la MT97 no se reconocía, caía al barajado normal y
        // entregaba una Poción donde tenía que haber una MT.
        var machines = ShopTable.TechnicalMachines(itemNames);

        if (machines.Length == 0)
        {
            throw new InvalidDataException(
                "No se encontró ninguna MT con nombre en la ROM; se aborta antes de tocar los objetos del suelo.");
        }

        var isMachine = machines.ToHashSet();

        var zones = encounterData.FileCount / FieldItemTable.SubfilesPerZone;
        var found = new List<(int Zone, byte[] Environment, int Slot)>();

        for (var zone = 0; zone < zones; zone++)
        {
            ct.ThrowIfCancellationRequested();

            var environment = encounterData[zone * FieldItemTable.SubfilesPerZone];
            foreach (var slot in FieldItemTable.Locate(environment))
            {
                found.Add((zone, environment, slot));
            }
        }

        // DOS sacos, y los dos se construyen con lo que el cartucho ya tenía puesto en esos mismos
        // sitios: se baraja y se reparte, nunca se sortea de la tabla de objetos entera. Así ningún
        // id aparece donde el juego no lo ponía ya, y el número de cada cosa se conserva exacto.
        //
        // Las MT van aparte porque su hueco es un hueco de MT -la Poké Ball dorada-, así que una MT
        // se cambia por otra MT y nunca por una Poción. Barajar las cuarenta que hay entre sí, en
        // vez de sortear del catálogo de cien, es lo que hace el Universal Pokémon Randomizer y es
        // lo que mantiene la cuenta: cuarenta huecos, las mismas cuarenta MT, en otro orden.
        var placed = found
            .Select(f => FieldItemTable.GetItem(f.Environment, f.Slot))
            .Where(item => item != 0)
            .ToList();

        var machinePool = placed.Where(isMachine.Contains).ToList();
        var regularPool = placed.Where(item => !isMachine.Contains(item)).ToList();

        if (options?.FieldItemsMode == FieldItemsMode.Random)
        {
            // Al azar, como el mundo de la referencia (§163): cada hueco se sortea, con tope de repeticiones. Los mismos
            // huecos que antes y de la misma clase -una Poké Ball dorada sigue dando una MT-, pero de todo el catálogo.
            var pockets = ReadPockets(ItemEntries());
            var pool = RandomPool(pockets, itemNames, options.FieldItemsBanned.ToHashSet());

            regularPool = DrawCapped(regularPool.Count, pool, options.FieldItemsMaxRepeats, random.Derive("normales"));
            machinePool = DrawCapped(machinePool.Count, machines, options.FieldItemsMaxRepeats, random.Derive("mt"));
        }
        else
        {
            Shuffle(machinePool, random);
            Shuffle(regularPool, random);
        }

        var machineCount = 0;
        var regularCount = 0;
        var touchedZones = new HashSet<int>();

        foreach (var (zone, environment, slot) in found)
        {
            var item = FieldItemTable.GetItem(environment, slot);
            if (item == 0)
            {
                continue;
            }

            if (isMachine.Contains(item))
            {
                FieldItemTable.SetItem(environment, slot, machinePool[machineCount++]);
            }
            else
            {
                FieldItemTable.SetItem(environment, slot, regularPool[regularCount++]);
            }

            touchedZones.Add(zone);
        }

        // The payload is edited in place, so writing it back cannot change any length.
        foreach (var zone in touchedZones)
        {
            var index = zone * FieldItemTable.SubfilesPerZone;
            encounterData[index] = found.First(f => f.Zone == zone).Environment;
        }

        return new FieldItemResult(machineCount, regularCount, touchedZones.Count);
    }

    private static void Shuffle(List<int> values, IRandomSource random)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private byte[][] ItemEntries()
    {
        using var items = new GarcPatcher(workspace.PathOf(GameFiles.Item));
        return [.. Enumerable.Range(0, items.FileCount).Select(items.Read)];
    }

    /// <summary>The field pocket of every item, by id, from the cartridge's item table; -1 where an entry is too short.</summary>
    public static int[] ReadPockets(IReadOnlyList<byte[]> items)
    {
        var pockets = new int[items.Count];

        for (var id = 0; id < pockets.Length; id++)
        {
            var entry = items[id];
            pockets[id] = entry.Length >= 0x0A ? (BitConverter.ToUInt16(entry, 8) >> 7) & 0xF : -1;
        }

        return pockets;
    }

    /// <summary>
    /// What an ordinary ball or a berry pile may hold in the random mode: every general item, medicine and berry with a
    /// real name, minus the banned ones.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured on the reference world before writing it: 493 ordinary spots holding general items, berries, medicine,
    /// mega stones, Poké Balls, mail and mulch, and never a key item, a Z-Crystal or a TM. That is exactly pockets 0, 1
    /// and 3: all 335 distinct items it placed are inside this pool, measured against the cartridge. The cartridge
    /// fills unused ids with entries named «(?)», and those are out: whatever an empty entry does, it does not belong on
    /// the ground. Read from the item table rather than a list, so the expansion's 63 new items come in too.
    /// </para>
    /// <para>
    /// The Master Ball is banned in <c>Data/randomizer.json</c>, not here: in a Nuzlocke it is a guaranteed catch of the
    /// one encounter a route gives, and the reference never placed one.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<int> RandomPool(IReadOnlyList<int> pockets, IReadOnlyList<string> names,
        IReadOnlySet<int> banned) =>
    [
        .. Enumerable.Range(1, Math.Min(pockets.Count, names.Count) - 1)
            .Where(id => pockets[id] is GeneralPocket or MedicinePocket or BerryPocket
                         && !banned.Contains(id)
                         && !string.IsNullOrWhiteSpace(names[id])
                         && names[id] is not ("(?)" or "???"))
    ];

    /// <summary>
    /// <paramref name="count"/> items drawn from <paramref name="pool"/>, none more than <paramref name="maxRepeats"/> times.
    /// </summary>
    /// <remarks>
    /// The shape of the reference world, measured: of its 493 ordinary spots, 177 items once, 158 twice and none three
    /// times, and its TMs 39 once and 3 twice. A cap and not an even spread — an even spread uses every item before
    /// repeating one, and the reference left 199 of the 534 possible ones out, which is what a uniform draw with a cap
    /// gives. Only if every item has reached the cap before the spots run out does the count start again, rather than
    /// leaving a spot empty.
    /// </remarks>
    public static List<int> DrawCapped(int count, IReadOnlyList<int> pool, int maxRepeats, IRandomSource random)
    {
        if (pool.Count == 0)
        {
            throw new InvalidDataException("No queda ningún objeto que poner en el suelo.");
        }

        var cap = Math.Max(1, maxRepeats);
        var available = pool.ToList();
        var used = new Dictionary<int, int>();
        var drawn = new List<int>(count);

        for (var spot = 0; spot < count; spot++)
        {
            if (available.Count == 0)
            {
                available = pool.ToList();
                used.Clear();
            }

            var index = random.Next(available.Count);
            var item = available[index];
            drawn.Add(item);

            used[item] = used.GetValueOrDefault(item) + 1;
            if (used[item] >= cap)
            {
                available[index] = available[^1];
                available.RemoveAt(available.Count - 1);
            }
        }

        return drawn;
    }
}
