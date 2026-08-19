using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core;

namespace PermaLocke.Randomizer.Modules;

/// <param name="TechnicalMachineShops">Shops that sell TMs and got a new set of them.</param>
/// <param name="RestockedShops">Shops filled with the configured fallback item.</param>
/// <param name="Slots">Individual shop slots rewritten.</param>
public sealed record ShopResult(int TechnicalMachineShops, int RestockedShops, int Slots);

/// <summary>
/// Rewrites the special mart counters of the Pokémon Centers, inside <c>Shop.cro</c>.
/// <para>
/// The eight ordinary inventories, which grow as trials are cleared, are left alone: they are
/// where a player buys potions and balls, and a Nuzlocke needs them working.
/// </para>
/// </summary>
public sealed class ShopRandomizer(RomWorkspace workspace, RandomizerOptions options)
{
    public async Task<ShopResult> ApplyAsync(IRandomSource random, LayeredFsMod mod,
        CancellationToken ct = default)
    {
        var itemNames = workspace.Config.GetText(TextName.ItemNames);
        var machines = ValidTechnicalMachines(itemNames);

        var path = mod.Stage(GameFiles.Shop);
        var cro = await File.ReadAllBytesAsync(path, ct);
        var shops = ShopTable.Read(cro);

        var machineShops = 0;
        var restocked = 0;
        var slots = 0;

        foreach (var shop in shops.Where(s => s.Index >= ShopTable.RegularMartCount))
        {
            ct.ThrowIfCancellationRequested();

            if (ShopTable.SellsTechnicalMachines(cro, shop))
            {
                FillWithMachines(cro, shop, random, machines);
                machineShops++;
            }
            else
            {
                for (var slot = 0; slot < shop.Count; slot++)
                {
                    ShopTable.SetItem(cro, shop, slot, options.NonMachineMartItem);
                }
                restocked++;
            }

            slots += shop.Count;
        }

        await File.WriteAllBytesAsync(path, cro, ct);
        await VerifyAsync(path, machines, ct);
        return new ShopResult(machineShops, restocked, slots);
    }

    /// <summary>Gives a TM shop a fresh set of TMs, without repeating one within the same shop.</summary>
    private static void FillWithMachines(byte[] cro, ShopInventory shop, IRandomSource random, int[] machines)
    {
        var used = new HashSet<int>();
        for (var slot = 0; slot < shop.Count; slot++)
        {
            var pick = machines[random.Next(machines.Length)];
            // Bounded, and a repeat is acceptable if the pool is somehow smaller than the shop.
            for (var attempt = 0; attempt < 64 && !used.Add(pick); attempt++)
            {
                pick = machines[random.Next(machines.Length)];
            }
            ShopTable.SetItem(cro, shop, slot, pick);
        }
    }

    /// <summary>
    /// The TMs this cartridge actually has. The range is checked against the item name list
    /// rather than trusted: the ids right after it are the gen 6 HM slots and two unnamed
    /// entries, and stocking a shop with those would sell the player nothing.
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
                "No se encontró ninguna MT con nombre en la ROM; se aborta antes de tocar las tiendas.");
        }

        return machines;
    }

    /// <summary>
    /// Reads the written CRO back and checks the three things that would break a shop: a changed
    /// file size, a TM shop holding something that is not a TM, and a restocked shop holding
    /// anything but the configured item.
    /// </summary>
    private async Task VerifyAsync(string path, int[] machines, CancellationToken ct)
    {
        var vanilla = await File.ReadAllBytesAsync(workspace.PathOf(GameFiles.Shop), ct);
        var patched = await File.ReadAllBytesAsync(path, ct);

        if (patched.Length != vanilla.Length)
        {
            throw new InvalidDataException(
                $"Shop.cro cambió de tamaño: {vanilla.Length} -> {patched.Length}.");
        }

        var before = ShopTable.Read(vanilla);
        var after = ShopTable.Read(patched);
        if (before.Count != after.Count)
        {
            throw new InvalidDataException(
                $"Shop.cro se quedó con {after.Count} inventarios en vez de {before.Count}.");
        }

        var valid = machines.ToHashSet();
        foreach (var shop in after.Where(s => s.Index >= ShopTable.RegularMartCount))
        {
            var wasMachineShop = ShopTable.SellsTechnicalMachines(vanilla, before[shop.Index]);
            for (var slot = 0; slot < shop.Count; slot++)
            {
                var item = ShopTable.GetItem(patched, shop, slot);
                if (wasMachineShop && !valid.Contains(item))
                {
                    throw new InvalidDataException(
                        $"La tienda de MT {shop.Index} acabó vendiendo el objeto {item}, que no es una MT válida.");
                }
                if (!wasMachineShop && item != options.NonMachineMartItem)
                {
                    throw new InvalidDataException(
                        $"La tienda {shop.Index} acabó vendiendo el objeto {item} en vez de {options.NonMachineMartItem}.");
                }
            }
        }
    }
}
