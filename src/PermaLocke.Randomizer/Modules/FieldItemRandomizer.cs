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
/// TMs become other TMs. Ordinary items are <em>shuffled among themselves</em> rather than rolled
/// from the full item list, so no id ever appears that the cartridge did not already place
/// somewhere: whatever the game hands out, it could already hand out. That keeps a key item from
/// being replaced by a Potion and quietly stalling the story.
/// </para>
/// </summary>
public sealed class FieldItemRandomizer(RomWorkspace workspace)
{
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

        Shuffle(machinePool, random);
        Shuffle(regularPool, random);

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


}
