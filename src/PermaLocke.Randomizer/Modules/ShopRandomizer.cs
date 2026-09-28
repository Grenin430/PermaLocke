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
/// <param name="ShelfItems">Objects sold from a counter's own fixed list (<see cref="MartShelf"/>).</param>
public sealed record ShopResult(
    int TechnicalMachineShops, int RestockedShops, int Slots, int MedicineSlots = 0,
    int SpecialItems = 0, int PricedMachines = 0, int ShelfItems = 0, int BattlePointItems = 0);

/// <summary>What stocking the special counters did, before anything is priced or saved.</summary>
/// <param name="Stocked">How many of <see cref="RandomizerOptions.SpecialMartItems"/> found a slot.</param>
/// <param name="Shelved">How many items went out from the counters' own lists.</param>
/// <param name="MachinesOnSale">Every TM left on a shelf, which is what gets its price changed.</param>
public sealed record SpecialStock(
    int MachineShops, int RestockedShops, int Slots, int Stocked, int Shelved, IReadOnlySet<int> MachinesOnSale);

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

        // Se comprueban los nombres ANTES de tocar una sola tienda. Un id que haya caído en otro
        // objeto surtiría la tienda con otra cosa y no fallaría nunca (§52), y aquí es peor de lo
        // normal: son los objetos con los que evolucionan los Pokémon, así que vender el
        // equivocado deja una evolución sin forma de conseguirse y nadie sabría por qué.
        var wanted = options.SpecialMartItems;
        VerifyNames(wanted, itemNames);
        VerifyNames([.. options.SpecialMartShelves.SelectMany(shelf => shelf.Items)], itemNames);
        ValidateShelves(options, cro, shops);

        var medicine = ReplaceMedicines(options, cro, shops, itemNames);
        var battlePoints = ReplaceBattlePointItems(options, cro, itemNames);
        var stock = StockSpecials(options, cro, shops, random, machines);

        await File.WriteAllBytesAsync(path, cro, ct);
        await VerifyAsync(path, machines, ct);

        if (stock.Stocked < wanted.Count)
        {
            throw new InvalidDataException(
                $"Solo cupieron {stock.Stocked} de los {wanted.Count} objetos en las tiendas especiales. "
                + "Quita objetos de specialMartItems o el resto no se podrá comprar en ninguna parte.");
        }

        // Cada objeto a su precio propio si lo lleva, y si no al de su lista.
        await PriceAsync(mod,
            [
                .. wanted.Select(item => (item.Id, item.Price > 0 ? item.Price : options.SpecialMartItemPrice)),
                .. options.SpecialMartShelves.SelectMany(shelf =>
                    shelf.Items.Select(item => (item.Id, item.Price > 0 ? item.Price : shelf.Price))),
                // Las megapiedras, para poder venderlas (2026-09-27); una que ya tenga precio de tienda se queda el suyo.
                .. MegaStones(wanted.Select(item => item.Id)
                    .Concat(options.SpecialMartShelves.SelectMany(shelf => shelf.Items.Select(item => item.Id))).ToHashSet()),
            ], ct);
        await PriceMachinesAsync(mod, stock.MachinesOnSale, ct);
        return new ShopResult(stock.MachineShops, stock.RestockedShops, stock.Slots, medicine, stock.Stocked,
            options.MachineMartPrice > 0 ? stock.MachinesOnSale.Count : 0, stock.Shelved, battlePoints);
    }

    /// <summary>
    /// The counter of the Mantine Surf beaches that sells for BP (2026-09-27): each of its items becomes
    /// <see cref="RandomizerOptions.RegularMartReplacement"/>, at 1 BP each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is not one of the 28 inventories: the BP counters live after them in <c>Shop.cro</c> as item and price pairs
    /// (two u16 each), with no table saying where one ends. So it is found by what it sells: the whole list of
    /// <see cref="RandomizerOptions.BattlePointShopReplaced"/>, in order, four bytes apart, has to appear exactly once.
    /// Anywhere else, or twice, and nothing is written (§52). Measured on the installed world: at <c>0x54AA</c>, 13 items
    /// from Zumo de Baya to Más PP, just before the tutors' price list.
    /// </para>
    /// <para>
    /// Static and without a cartridge so it can be tested on a made-up <c>Shop.cro</c>.
    /// </para>
    /// </remarks>
    /// <returns>How many items were replaced.</returns>
    public static int ReplaceBattlePointItems(RandomizerOptions options, byte[] cro, string[] itemNames)
    {
        var wanted = options.BattlePointShopReplaced;
        if (wanted.Count == 0)
        {
            return 0;
        }

        VerifyNames([.. wanted.Append(options.RegularMartReplacement)], itemNames);

        var found = new List<int>();
        for (var at = 0; at + (wanted.Count * 4) <= cro.Length; at += 2)
        {
            var match = true;
            for (var i = 0; i < wanted.Count && match; i++)
            {
                match = BitConverter.ToUInt16(cro, at + (i * 4)) == wanted[i].Id;
            }

            if (match) found.Add(at);
        }

        if (found.Count != 1)
        {
            throw new InvalidDataException(
                $"La tienda de PB de las playas aparece {found.Count} veces en Shop.cro, y tiene que ser una. No se toca.");
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            BitConverter.GetBytes((ushort)options.RegularMartReplacement.Id).CopyTo(cro, found[0] + (i * 4));

            // A 1 PB todas: una Poké Ball a 48 PB (lo que costaba el Caramelo Raro de ese hueco) no tiene sentido.
            BitConverter.GetBytes((ushort)1).CopyTo(cro, found[0] + (i * 4) + 2);
        }

        return wanted.Count;
    }

    /// <summary>
    /// Stocks every special counter: TMs where TMs were, a counter's own list where it has one, and
    /// the spilling list of <see cref="RandomizerOptions.SpecialMartItems"/> through the rest.
    /// </summary>
    /// <remarks>
    /// Static and without a cartridge so it can be tested on a made-up <c>Shop.cro</c>. It trusts
    /// <see cref="ValidateShelves"/> to have run: a shelf naming a TM counter would be ignored here,
    /// not refused.
    /// </remarks>
    public static SpecialStock StockSpecials(RandomizerOptions options, byte[] cro,
        IReadOnlyList<ShopInventory> shops, IRandomSource random, int[] machines)
    {
        var wanted = options.SpecialMartItems;
        var shelves = options.SpecialMartShelves.ToDictionary(shelf => shelf.Shop);
        var machineShops = 0;
        var restocked = 0;
        var slots = 0;
        var stocked = 0;
        var shelved = 0;
        var priced = new HashSet<int>();

        // Por donde se empieza a surtir la lista que se derrama, y eso decide cuanto tiene que
        // andar el jugador. Los indices salen de MEDIRLOS en la partida o de las etiquetas de pk3DS
        // (§145): el cartucho no dice que mostrador es de que pueblo. Un indice que no exista se
        // ignora en vez de fallar: la lista es una preferencia, y lo que no se nombre sigue yendo
        // en orden. Los mostradores con lista propia se saltan: su sitio ya esta decidido.
        var preferred = options.SpecialMartOrder;

        var special = shops
            .Where(s => s.Index >= ShopTable.RegularMartCount)
            .OrderBy(s =>
            {
                var wish = Array.IndexOf(preferred, s.Index);
                return wish >= 0 ? wish : preferred.Length + s.Index;
            })
            .ToList();

        // Las MT de los mostradores salen de las que el CARTUCHO vendía, una vez cada una entre todos (2026-09-28): sorteadas
        // de las cien se repetían con las del suelo y las que regala un NPC, y los jugadores encontraban la misma MT dos veces.
        // Las del suelo se barajan entre las del suelo (FieldItemRandomizer), así que ninguna MT sale en dos sitios.
        var soldByCartridge = special.Where(shop => ShopTable.SellsTechnicalMachines(cro, shop))
            .SelectMany(shop => Enumerable.Range(0, shop.Count).Select(slot => ShopTable.GetItem(cro, shop, slot)))
            .Where(machines.Contains).Distinct().Order().ToArray();
        var machineBag = new List<int>();

        foreach (var shop in special)
        {
            if (ShopTable.SellsTechnicalMachines(cro, shop))
            {
                // Las de MT gastan numeros aleatorios en su orden de siempre: un mostrador con lista
                // propia no gasta ninguno, asi que ponerle una no mueve las MT de nadie.
                FillWithMachines(cro, shop, random, soldByCartridge.Length > 0 ? soldByCartridge : machines, machineBag);
                machineShops++;

                // Solo las que acaban en un mostrador: reponer el precio de las cien tocaria
                // tambien las que se encuentran por el suelo, y esas no se compran.
                for (var slot = 0; slot < shop.Count; slot++)
                {
                    priced.Add(ShopTable.GetItem(cro, shop, slot));
                }
            }
            else if (shelves.TryGetValue(shop.Index, out var shelf))
            {
                // Su lista, en orden, y lo que sobre de estanteria al articulo de relleno.
                for (var slot = 0; slot < shop.Count; slot++)
                {
                    ShopTable.SetItem(cro, shop, slot,
                        slot < shelf.Items.Count ? shelf.Items[slot].Id : options.NonMachineMartItem);
                }

                shelved += Math.Min(shelf.Items.Count, shop.Count);
                restocked++;
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

        return new SpecialStock(machineShops, restocked, slots, stocked, shelved, priced);
    }

    /// <summary>
    /// Refuses, before anything is written, any shelf that would leave an item unbuyable or priced
    /// twice.
    /// </summary>
    /// <remarks>
    /// Each item may appear once across the shelves and the spilling list: the price is a field of
    /// the item, not of the shelf, so an item in two lists at two prices would end up costing
    /// whichever was written last, and nobody would be told.
    /// </remarks>
    public static void ValidateShelves(RandomizerOptions options, byte[] cro, IReadOnlyList<ShopInventory> shops)
    {
        const int maxPrice = ushort.MaxValue * 10;
        var seenShops = new HashSet<int>();
        var seenItems = options.SpecialMartItems.Select(item => item.Id).ToHashSet();

        // El precio propio de un objeto pasa por la misma puerta que el de su lista, y aqui, antes
        // de escribir: un 35 se guardaria como 30 sin decir nada.
        foreach (var item in options.SpecialMartItems.Concat(options.SpecialMartShelves.SelectMany(s => s.Items)))
        {
            if (item.Price < 0 || item.Price % 10 != 0 || item.Price > maxPrice)
            {
                throw new InvalidDataException(
                    $"El precio {item.Price} de «{item.Name}» no vale: múltiplo de 10 y como mucho {maxPrice}.");
            }
        }

        foreach (var shelf in options.SpecialMartShelves)
        {
            var shop = shops.FirstOrDefault(s => s.Index == shelf.Shop)
                ?? throw new InvalidDataException(
                    $"La tienda {shelf.Shop} ({shelf.Place}) no existe: hay {shops.Count}. No se toca ninguna.");

            if (shop.Index < ShopTable.RegularMartCount)
            {
                throw new InvalidDataException(
                    $"La tienda {shelf.Shop} ({shelf.Place}) es un mostrador normal, no uno especial.");
            }

            if (ShopTable.SellsTechnicalMachines(cro, shop))
            {
                throw new InvalidDataException(
                    $"La tienda {shelf.Shop} ({shelf.Place}) es de MT: sus huecos los sortea el módulo de MT.");
            }

            if (!seenShops.Add(shelf.Shop))
            {
                throw new InvalidDataException($"La tienda {shelf.Shop} ({shelf.Place}) tiene dos listas.");
            }

            if (shelf.Items.Count == 0 || shelf.Items.Count > shop.Count)
            {
                throw new InvalidDataException(
                    $"La tienda {shelf.Shop} ({shelf.Place}) tiene {shop.Count} huecos y la lista trae "
                    + $"{shelf.Items.Count} objetos. Lo que no cabe no se podría comprar en ninguna parte.");
            }

            if (shelf.Price < 0 || shelf.Price % 10 != 0 || shelf.Price > maxPrice)
            {
                throw new InvalidDataException(
                    $"El precio {shelf.Price} de {shelf.Place} no vale: múltiplo de 10 y como mucho {maxPrice}.");
            }

            foreach (var item in shelf.Items)
            {
                if (!seenItems.Add(item.Id))
                {
                    throw new InvalidDataException(
                        $"«{item.Name}» ({item.Id}) está en dos listas de tiendas. El precio es del objeto "
                        + "y no de la tienda, así que solo puede tener uno.");
                }
            }
        }
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
    private IEnumerable<(int Id, int Price)> MegaStones(IReadOnlySet<int> priced) =>
        options.MegaStonePrice <= 0
            ? []
            : MegaTrainerRandomizer.ReadStones(workspace.PathOf(GameFiles.MegaEvolution))
                .Where(stone => !priced.Contains(stone) && stone < ItemCount())
                .Order()
                .Select(stone => (stone, options.MegaStonePrice));

    private int ItemCount()
    {
        using var items = new GarcPatcher(workspace.PathOf(GameFiles.Item));
        return items.FileCount;
    }

    private static async Task PriceAsync(LayeredFsMod mod, IReadOnlyList<(int Id, int Price)> prices,
        CancellationToken ct)
    {
        const int max = ushort.MaxValue * 10;

        // Un precio de cero deja el objeto como este: es lo que significa en la configuracion.
        var items = prices.Where(entry => entry.Price > 0).ToList();

        if (items.Count == 0)
        {
            return;
        }

        foreach (var (_, price) in items)
        {
            if (price % 10 != 0 || price > max)
            {
                throw new ArgumentOutOfRangeException(nameof(prices),
                    $"El precio {price} no vale: tiene que ser múltiplo de 10 y como mucho {max}.");
            }
        }

        var path = mod.Stage(GameFiles.Item);

        using (var patcher = new GarcPatcher(path))
        {
            foreach (var (id, price) in items)
            {
                var entry = patcher.Read(id);
                BitConverter.GetBytes((ushort)(price / 10)).CopyTo(entry, 0);
                patcher.Write(id, entry);
            }
        }

        await Task.Run(() =>
        {
            using var back = new GarcPatcher(path);

            foreach (var (id, price) in items)
            {
                var written = BitConverter.ToUInt16(back.Read(id), 0) * 10;

                if (written != price)
                {
                    throw new InvalidDataException(
                        $"El objeto {id} quedó a {written} y se pedían {price}.");
                }
            }
        }, ct);
    }

    /// <summary>
    /// Puts one price on every TM that ended up on a Pokémon Center shelf.
    /// </summary>
    /// <remarks>
    /// Same field and same guards as the evolution items — the price is the first <c>ushort</c> of
    /// the item entry and holds a tenth of it — and it is read back before being called done. The
    /// ids come from the cartridge's own shop table rather than from anything written by hand, so
    /// there is nothing here to check a name against: a TM id that reached a shelf is a TM id.
    /// </remarks>
    private async Task PriceMachinesAsync(LayeredFsMod mod, IReadOnlyCollection<int> machines,
        CancellationToken ct)
    {
        const int max = ushort.MaxValue * 10;
        var price = options.MachineMartPrice;

        if (price <= 0 || machines.Count == 0)
        {
            return;
        }

        if (price % 10 != 0 || price > max)
        {
            throw new ArgumentOutOfRangeException(nameof(options),
                $"El precio de MT {price} no vale: tiene que ser múltiplo de 10 y como mucho {max}.");
        }

        var path = mod.Stage(GameFiles.Item);

        using (var patcher = new GarcPatcher(path))
        {
            foreach (var machine in machines)
            {
                var entry = patcher.Read(machine);
                BitConverter.GetBytes((ushort)(price / 10)).CopyTo(entry, 0);
                patcher.Write(machine, entry);
            }
        }

        await Task.Run(() =>
        {
            using var back = new GarcPatcher(path);

            foreach (var machine in machines)
            {
                var written = BitConverter.ToUInt16(back.Read(machine), 0) * 10;

                if (written != price)
                {
                    throw new InvalidDataException(
                        $"La MT {machine} quedó a {written} y se pedían {price}.");
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

    /// <summary>
    /// Gives a TM shop its TMs from <paramref name="bag"/>, shared by every TM shop so none is sold in two; only when
    /// <paramref name="machines"/> is used up does the bag refill (a repeat then, never within the same shop if avoidable).
    /// </summary>
    private static void FillWithMachines(byte[] cro, ShopInventory shop, IRandomSource random, int[] machines, List<int> bag)
    {
        var used = new HashSet<int>();
        for (var slot = 0; slot < shop.Count; slot++)
        {
            if (bag.Count == 0) bag.AddRange(machines);

            var index = random.Next(bag.Count);
            for (var attempt = 0; attempt < 64 && used.Contains(bag[index]); attempt++)
            {
                index = random.Next(bag.Count);
            }

            var pick = bag[index];
            bag.RemoveAt(index);
            used.Add(pick);
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
        var shelves = options.SpecialMartShelves.ToDictionary(shelf => shelf.Shop);

        foreach (var shop in after.Where(s => s.Index >= ShopTable.RegularMartCount))
        {
            var wasMachineShop = ShopTable.SellsTechnicalMachines(vanilla, before[shop.Index]);

            // Un mostrador con lista propia tiene que venderla tal cual, hueco por hueco.
            if (shelves.TryGetValue(shop.Index, out var shelf))
            {
                for (var slot = 0; slot < shop.Count; slot++)
                {
                    var expected = slot < shelf.Items.Count ? shelf.Items[slot].Id : options.NonMachineMartItem;

                    if (ShopTable.GetItem(patched, shop, slot) != expected)
                    {
                        throw new InvalidDataException(
                            $"La tienda {shop.Index} ({shelf.Place}) no vende en el hueco {slot} lo que dice su lista.");
                    }
                }

                continue;
            }

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
