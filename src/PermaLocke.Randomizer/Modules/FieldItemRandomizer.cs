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
        var machines = ValidTechnicalMachines(itemNames);

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

        // The ordinary items get redistributed among their own positions.
        var pool = found
            .Select(f => FieldItemTable.GetItem(f.Environment, f.Slot))
            .Where(item => item != 0 && !ShopTable.IsTechnicalMachine(item))
            .ToList();
        Shuffle(pool, random);

        var machineCount = 0;
        var regularCount = 0;
        var next = 0;
        var touchedZones = new HashSet<int>();

        foreach (var (zone, environment, slot) in found)
        {
            var item = FieldItemTable.GetItem(environment, slot);
            if (item == 0)
            {
                continue;
            }

            if (ShopTable.IsTechnicalMachine(item))
            {
                FieldItemTable.SetItem(environment, slot, machines[random.Next(machines.Length)]);
                machineCount++;
            }
            else
            {
                FieldItemTable.SetItem(environment, slot, pool[next++]);
                regularCount++;
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

    /// <summary>
    /// The TMs this cartridge actually has, checked against the item names: the ids right after
    /// the range are the gen 6 HM slots, which do nothing in Ultra Moon.
    /// </summary>
    private static int[] ValidTechnicalMachines(string[] itemNames)
    {
        var machines = Enumerable
            .Range(ShopTable.FirstTechnicalMachine,
                ShopTable.LastTechnicalMachine - ShopTable.FirstTechnicalMachine + 1)
            .Where(id => id < itemNames.Length
                         && !string.IsNullOrWhiteSpace(itemNames[id])
                         && itemNames[id] != "(?)")
            .ToArray();

        if (machines.Length == 0)
        {
            throw new InvalidDataException(
                "No se encontró ninguna MT con nombre en la ROM; se aborta antes de tocar los objetos del suelo.");
        }

        return machines;
    }
}
