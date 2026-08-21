using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink;

/// <summary>
/// Hands an item over by writing it into the bag of the running game.
/// </summary>
/// <remarks>
/// <para>
/// Everything hard about this was already solved for the Rare Candy button: the bag block is
/// found by its own structure — seven pockets in a row followed by a table of pointers, each of
/// which has to point at its own pocket — so an item can be added even when the player carries
/// none of it, and there is no searching for a value that may not exist (§22).
/// </para>
/// <para>
/// What this adds is the part a shop needs: <b>the bag is read back afterwards</b> and the
/// delivery is reported only if the item is really there with the expected count. A purchase that
/// charged points for nothing would be exactly the kind of quietly-wrong behaviour rule 3 forbids.
/// </para>
/// </remarks>
public sealed class BagItemDelivery(
    BagService bag,
    Rpc.AzaharRpcClient client,
    ILogger<BagItemDelivery> logger) : IItemDelivery
{
    public Task<ItemDeliveryResult> GiveAsync(int itemId, int amount = 1, CancellationToken ct = default) =>
        Task.Run(() => Give(itemId, amount, ct), ct);

    public Task<int> CarriedAsync(int itemId, CancellationToken ct = default) =>
        Task.Run(() => Reachable(out _) && bag.Locate(ct) is not null ? bag.CountOf(itemId, ct) : -1, ct);

    public Task<IReadOnlyDictionary<int, int>> CarriedAllAsync(
        IReadOnlyList<int> itemIds, CancellationToken ct = default) =>
        Task.Run(() => CarriedAll(itemIds, ct), ct);

    private IReadOnlyDictionary<int, int> CarriedAll(IReadOnlyList<int> itemIds, CancellationToken ct)
    {
        if (!Reachable(out _) || bag.Locate(ct) is null)
        {
            return new Dictionary<int, int>();
        }

        // Un solo recorrido de la mochila, y de ahí salen todas las cuentas.
        var carried = bag.Read(ct)
            .Where(slot => slot.Entry.ItemId != 0)
            .GroupBy(slot => slot.Entry.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(slot => slot.Entry.Count));

        return itemIds.Distinct().ToDictionary(item => item, item => carried.GetValueOrDefault(item));
    }

    /// <summary>
    /// A single, cheap question before anything else: is the emulator even answering?
    /// </summary>
    /// <remarks>
    /// Without this, a closed emulator meant a full sweep of the game's memory that could only
    /// end in failure, one per call. The shop asked eighteen times on opening and once more per
    /// click, and simply stopped responding. A ping costs one datagram and a timeout.
    /// </remarks>
    private bool Reachable(out string problem)
    {
        if (client.TryPing(out _))
        {
            problem = string.Empty;
            return true;
        }

        problem = "Azahar no responde. Ábrelo, carga la partida y activa Configuración → "
                  + "Depuración → Activar servidor RPC.";
        return false;
    }

    private ItemDeliveryResult Give(int itemId, int amount, CancellationToken ct)
    {
        try
        {
            if (!Reachable(out var unreachable))
            {
                return ItemDeliveryResult.Unreachable(unreachable);
            }

            if (bag.Locate(ct) is null)
            {
                return ItemDeliveryResult.Failed(
                    "No se encuentra la mochila del juego. Abre Azahar con la partida cargada y vuelve a intentarlo.");
            }

            var before = bag.CountOf(itemId, ct);
            var result = bag.SetCount(itemId, before + amount, ct);

            if (!result.Succeeded)
            {
                return ItemDeliveryResult.Failed(Explain(result));
            }

            // La comprobación que convierte esto en una entrega y no en un intento: se relee.
            var after = bag.CountOf(itemId, ct);

            if (after <= before)
            {
                logger.LogWarning("Se escribió el objeto {Item} pero al releer sigue en {After}", itemId, after);
                return new ItemDeliveryResult(false, after,
                    "Se escribió en la mochila pero al releerla el objeto no está. No se ha cobrado nada.");
            }

            logger.LogInformation("Entregado el objeto {Item}: de {Before} a {After}", itemId, before, after);
            return new ItemDeliveryResult(true, after, string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la entrega del objeto {Item}", itemId);
            return ItemDeliveryResult.Failed(
                "No se pudo escribir en la mochila. El detalle está en la carpeta Logs.");
        }
    }

    /// <summary>Turns the bag's own outcome into something the player can act on.</summary>
    private static string Explain(BagWriteResult result) => result.Outcome switch
    {
        BagWriteOutcome.BagNotFound =>
            "No se encuentra la mochila del juego. Abre Azahar con la partida cargada.",
        BagWriteOutcome.PocketFull =>
            "Ese bolsillo de la mochila está lleno. Haz sitio y vuelve a intentarlo.",
        BagWriteOutcome.UnknownPocket =>
            "El juego no sabe en qué bolsillo va ese objeto, así que no se escribe nada.",
        BagWriteOutcome.NotApplied =>
            "El emulador no aceptó la escritura. ¿Está usando el fork propio de Azahar?",
        _ => "No se pudo entregar el objeto."
    };
}
