using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Rules.Services;

/// <summary>
/// Says whether a zone's wild battles still belong to its trial, so they are not the route's first encounter.
/// </summary>
/// <remarks>
/// <para>
/// Found playing on 2026-09-21 in the Cueva Sotobosque. Ilima's trial makes the player beat three wild Pokémon in the
/// dens and then the Totem, and the game does not let a ball be thrown at any of them: only once the Z-Crystal is won
/// can anything be caught there. PermaLocke took the first den battle for the zone's encounter, spent the route and
/// marked it «debilitado» on the map, before the player had had a single chance to catch.
/// </para>
/// <para>
/// A trial is over when its Z-Crystal is in the bag, which is exactly how its achievement is counted, so the crystal is
/// read from the achievement and not written twice. A bag that cannot be read answers «not pending», and the battle
/// counts as it always did: better an argued route than a free one on a failed read.
/// </para>
/// </remarks>
public sealed class TrialZoneService(RulesConfiguration rules, IAchievementCatalog achievements, IItemDelivery bag,
    ILogger<TrialZoneService> logger)
{
    /// <summary>
    /// What the trial is, when <paramref name="zoneId"/> is a trial zone and its trial is not passed yet; otherwise null.
    /// </summary>
    public async Task<string?> PendingAsync(string zoneId, CancellationToken ct = default)
    {
        if (rules.BallControl.TrialZones.FirstOrDefault(trial => trial.Zone == zoneId) is not { } trial)
        {
            return null;
        }

        if (achievements.All.FirstOrDefault(achievement => achievement.Id == trial.Achievement)?.Item is not { } crystal)
        {
            logger.LogWarning("La zona de prueba {Zona} nombra el logro {Logro}, que no existe o no tiene cristal: cuenta como una ruta normal",
                zoneId, trial.Achievement);
            return null;
        }

        var carried = await bag.CarriedAllAsync([crystal], ct).ConfigureAwait(false);

        // Vacío es «no se sabe» (§68), no «no lo lleva».
        if (!carried.TryGetValue(crystal, out var count))
        {
            return null;
        }

        return count > 0 ? null : trial.Note;
    }
}
