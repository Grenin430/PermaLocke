using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink;

/// <summary>
/// Says which area of the game the player is standing in, or says it does not know.
/// </summary>
/// <remarks>
/// The bag block is the anchor: it is the one structure that can be found on its own, and the
/// zone copies sit at a fixed distance from it. That means locating the zone costs nothing once
/// the bag is cached — a single read of 48 bytes — instead of a sweep.
///
/// Nothing here falls back to a guess. A null answer is a real answer: the rule that takes Poké
/// Balls away must not fire on a zone PermaLocke is not sure about.
/// </remarks>
public sealed class ZoneService(BagService bag, AzaharRpcClient client, ILogger<ZoneService> logger)
    : IZoneProvider
{
    private readonly ZoneLocator _locator = new(client);

    private int? _last;

    /// <summary>
    /// The area index the player is in, in the numbering of <c>encdata</c>, or null when it
    /// cannot be established.
    /// </summary>
    public int? CurrentArea() => CurrentArea(CancellationToken.None);

    /// <inheritdoc cref="CurrentArea()" />
    public int? CurrentArea(CancellationToken ct)
    {
        if (bag.Locate(ct) is not { } block)
        {
            return null;
        }

        var area = _locator.Read(block);

        if (area is null)
        {
            // Se distinguen los dos motivos: que el emulador no conteste no es lo mismo que
            // encontrar copias que se contradicen, y confundirlos manda a investigar al sitio
            // equivocado.
            logger.LogWarning(client.TryReadMemory(block.BaseAddress, 4, out _)
                ? "Las copias de la zona no concuerdan; PermaLocke no sabe dónde está el jugador"
                : "No se pudo leer la zona: el emulador no responde");
        }
        else if (area != _last)
        {
            logger.LogInformation("Zona: área {Area} (antes {Previous})", area, _last?.ToString() ?? "ninguna");
        }

        _last = area;
        return area;
    }
}
