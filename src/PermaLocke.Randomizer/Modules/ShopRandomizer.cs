using PermaLocke.Core.Abstractions;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;
using pk3DS.Core;

namespace PermaLocke.Randomizer.Modules;

/// <param name="TechnicalMachineShops">Shops that sell TMs and got a new set of them.</param>
/// <param name="RestockedShops">Shops filled with the configured fallback item.</param>
/// <param name="Slots">Individual shop slots rewritten.</param>
/// <param name="MedicineSlots">Ordinary counter slots that stopped selling a status medicine.</param>
/// <param name="SpecialItems">Objetos de evolucion del mod puestos a la venta.</param>
public sealed record ShopResult(
    int TechnicalMachineShops, int RestockedShops, int Slots, int MedicineSlots = 0,
    int SpecialItems = 0);

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
        // Por nombre y no por rango: las cien MT no son un tramo seguido, y el rango dejaba fuera
        // las ocho ultimas -MT93 a MT100-, que asi no aparecian nunca en una tienda.
        var machines = ShopTable.TechnicalMachines(itemNames);

        var path = mod.Stage(GameFiles.Shop);
        var cro = await File.ReadAllBytesAsync(path, ct);
        var shops = ShopTable.Read(cro);

        var machineShops = 0;
        var restocked = 0;
        var slots = 0;

        // Se comprueban los nombres ANTES de tocar una sola tienda. Un id que haya caído en otro
        // objeto surtiría la tienda con otra cosa y no fallaría nunca (§52), y aquí es peor de lo
        // normal: son los objetos con los que evolucionan los Pokémon del mod, así que vender el
        // equivocado deja una evolución sin forma de conseguirse y nadie sabría por qué.
        var wanted = options.SpecialMartItems;
        VerifyNames(wanted, itemNames);

        var stocked = 0;
        var medicine = ReplaceMedicines(options, cro, shops, itemNames);

        // Por donde se empieza a surtir, y eso decide cuanto tiene que andar el jugador.
        //
        // En orden de indice se empieza por el 8, que es Konikoni -- ya bien entrada la region --
        // y la lista se reparte entre tres mostradores. Con el orden puesto en el fichero se
        // empieza por Hau'oli (8 huecos) y sigue por la Ruta 2 (12): veinte huecos para dieciocho
        // objetos, o sea la lista entera en los dos primeros mostradores del juego.
        //
        // Los indices salen de MEDIRLOS en la partida, que es la unica via: el cartucho no dice
        // que mostrador es de que pueblo. Un indice que no exista se ignora en vez de fallar: la
        // lista es una preferencia, y lo que no se nombre sigue yendo en orden.
        var preferred = options.SpecialMartOrder;

        var special = shops
            .Where(s => s.Index >= ShopTable.RegularMartCount)
            .OrderBy(s =>
            {
                var wish = Array.IndexOf(preferred, s.Index);
                return wish >= 0 ? wish : preferred.Length + s.Index;
            })
            .ToList();

        foreach (var shop in special)
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
                    // Los objetos de la lista primero, en orden y pasando de tienda cuando una se
                    // llena; lo que sobre de estanterías vuelve al artículo de relleno.
                    ShopTable.SetItem(cro, shop, slot,
                        stocked < wanted.Count ? wanted[stocked++].Id : options.NonMachineMartItem);
                }
                restocked++;
            }

            slots += shop.Count;
        }

        await File.WriteAllBytesAsync(path, cro, ct);
        await VerifyAsync(path, machines, ct);

        if (stocked < wanted.Count)
        {
            throw new InvalidDataException(
                $"Solo cupieron {stocked} de los {wanted.Count} objetos en las tiendas especiales. "
                + "Quita objetos de specialMartItems o el resto no se podrá comprar en ninguna parte.");
        }

        await PriceAsync(mod, wanted, ct);
        return new ShopResult(machineShops, restocked, slots, medicine, stocked);
    }

    /// <summary>
    /// Refuses to touch anything when an id and the name written beside it disagree.
    /// </summary>
    private static void VerifyNames(IReadOnlyList<MartItem> items, string[] names)
    {
        foreach (var item in items)
        {
            if (item.Id < 0 || item.Id >= names.Length || names[item.Id] != item.Name)
            {
                throw new InvalidDataException(
                    $"El objeto {item.Id} debería llamarse «{item.Name}» y la ROM dice "
                    + $"«{(item.Id >= 0 && item.Id < names.Length ? names[item.Id] : "fuera de rango")}». "
                    + "No se toca ninguna tienda.");
            }
        }
    }

    /// <summary>
    /// Writes the configured price into the item table, and reads it back.
    /// </summary>
    /// <remarks>
    /// The price is the first field of each 36 byte entry, a <c>ushort</c> holding the price
    /// divided by ten. A value that does not divide cleanly, or one past 655350, would be truncated
    /// into some other number and the shop would quietly charge it, so both are refused rather than
    /// rounded.
    /// </remarks>
    private async Task PriceAsync(LayeredFsMod mod, IReadOnlyList<MartItem> items,
        CancellationToken ct)
    {
        const int max = ushort.MaxValue * 10;
        var price = options.SpecialMartItemPrice;

        if (price <= 0 || items.Count == 0)
        {
            return;
        }

        if (price % 10 != 0 || price > max)
        {
            throw new ArgumentOutOfRangeException(nameof(options),
                $"El precio {price} no vale: tiene que ser múltiplo de 10 y como mucho {max}.");
        }

        var path = mod.Stage(GameFiles.Item);

        using (var patcher = new GarcPatcher(path))
        {
            foreach (var item in items)
            {
                var entry = patcher.Read(item.Id);
                BitConverter.GetBytes((ushort)(price / 10)).CopyTo(entry, 0);
                patcher.Write(item.Id, entry);
            }
        }

        await Task.Run(() =>
        {
            using var back = new GarcPatcher(path);

            foreach (var item in items)
            {
                var written = BitConverter.ToUInt16(back.Read(item.Id), 0) * 10;

                if (written != price)
                {
                    throw new InvalidDataException(
                        $"El objeto {item.Id} quedó a {written} y se pedían {price}.");
                }
            }
        }, ct);
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
                // Una tienda que no es de MT vende, o uno de los objetos de la lista, o el relleno.
                // Cualquier otra cosa significa que algo escribió donde no debía.
                if (!wasMachineShop
                    && item != options.NonMachineMartItem
                    && !options.SpecialMartItems.Any(m => m.Id == item))
                {
                    throw new InvalidDataException(
                        $"La tienda {shop.Index} acabó vendiendo el objeto {item}, que no está en "
                        + $"specialMartItems ni es {options.NonMachineMartItem}.");
                }
            }
        }
    }
}
