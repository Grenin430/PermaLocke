using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <summary>
/// What the watcher does on its own that the app answers outside the cap panel.
/// </summary>
/// <remarks>
/// Nothing is said on top of the game any more (2026-10-17 in the single-player version, in the tournament from 1.1.0 at the player's word):
/// no notices, no card flying to the album, no death scene over the game, no ghosts nor blood rain on the others' screens; the deaths
/// happen inside the cap panel (<c>CapDeath</c>) and what the notices said goes to the log. Better ones may come back later. What stays: the
/// others vote the nickname of every wild catch and every newcomer that is not the starter (2026-09-28); items got go into the sea from the
/// wormhole by the tower (CapItemRain, 1.1.2).
/// </remarks>
public sealed class PlayNotifications
{
    public PlayNotifications(GameLinkMonitor monitor, EncounterGuard encounters, NicknameVoteService votes)
    {
        encounters.Caught += (_, caught) => votes.Offer(caught.Pokemon);
        monitor.NewcomerArrived += (_, pokemon) =>
        {
            if (!monitor.IsStarter(pokemon.PID)) votes.Offer(pokemon);
        };

        // El objeto que saltaba a la mochila encima del juego (2026-09-28), quitado en la 1.1.2 a petición del jugador: ahora lo escupe el
        // ultraumbral pequeño junto a la torre, en el panel del cap (CapItemRain).
    }
}
