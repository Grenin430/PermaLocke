using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core;

namespace PermaLocke.Randomizer.Modules;

/// <param name="TechnicalMachineShops">Shops that sell TMs and got a new set of them.</param>
/// <param name="RestockedShops">Shops filled with the configured fallback item.</param>
/// <param name="Slots">Individual shop slots rewritten.</param>
/// <param name="MedicineSlots">Ordinary counter slots that stopped selling a status medicine.</param>
public sealed record ShopResult(
    int TechnicalMachineShops, int RestockedShops, int Slots, int MedicineSlots = 0);

/// <summary>
/// Rewrites the mart inventories of the Pokémon Centers, inside <c>Shop.cro</c>.
/// <para>
/// The eight ordinary inventories, which grow as trials are cleared, keep their shape: they are
/// where a player buys balls, and a Nuzlocke needs them working. The only thing that changes there
/// is the six status medicines, which become more balls.
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

        var medicine = ReplaceMedicines(options, cro, shops, itemNames);

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
        return new ShopResult(machineShops, restocked, slots, medicine);
    }

    /// <summary>
    /// Turns the status medicines of the ordinary counters into the replacement item.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the eight ordinary inventories, and only the ids the options name: the special counters
    /// are handled below and a shop that sells nothing but Antidotes elsewhere is somebody else's
    /// problem. Slots are matched by id, so it does not matter that the medicines sit in a different
    /// place in each of the eight, nor that the later ones sell more things.
    /// </para>
    /// <para>
    /// Every id is checked against the cartridge's own item table first, and a mismatch <b>throws</b>
    /// rather than writing: swapping the wrong item into a shop produces a game that works, sells
    /// the wrong thing, and never reports anything. The same guard the prizes use (§52).
    /// </para>
    /// </remarks>
    /// <param name="options">Passed in, and the method is static, so a test needs no cartridge.</param>
    public static int ReplaceMedicines(RandomizerOptions options, byte[] cro,
        IReadOnlyList<ShopInventory> shops, string[] itemNames)
    {
        if (options.RegularMartReplaced.Count == 0)
        {
            return 0;
        }

        foreach (var expected in options.RegularMartReplaced.Append(options.RegularMartReplacement))
        {
            var actual = expected.Id >= 0 && expected.Id < itemNames.Length
                ? itemNames[expected.Id]
                : "(fuera de la tabla)";

            if (!string.Equals(actual, expected.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"El objeto {expected.Id} debería ser «{expected.Name}» y el cartucho dice "
                    + $"«{actual}». No se toca ninguna tienda.");
            }
        }

        var replaced = options.RegularMartReplaced.Select(item => item.Id).ToHashSet();
        var changed = 0;

        foreach (var shop in shops.Where(s => s.Index < ShopTable.RegularMartCount))
        {
            for (var slot = 0; slot < shop.Count; slot++)
            {
                if (!replaced.Contains(ShopTable.GetItem(cro, shop, slot)))
                {
                    continue;
                }

                ShopTable.SetItem(cro, shop, slot, options.RegularMartReplacement.Id);
                changed++;
            }
        }

        return changed;
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
