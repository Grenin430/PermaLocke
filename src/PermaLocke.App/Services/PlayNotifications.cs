using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <summary>
/// What the watcher does on its own that the app answers outside the cap panel.
/// </summary>
/// <remarks>
/// Nothing is said on top of the game any more (2026-10-17 in the single-player version, in the tournament from 1.0.19 at the player's word):
/// no notices, no card flying to the album, no death scene over the game, no ghosts nor blood rain on the others' screens; the deaths
/// happen inside the cap panel (<c>CapDeath</c>) and what the notices said goes to the log. Better ones may come back later. What stays: an
/// item from the ground, bought or given jumps into the bag over the game, and the others vote the nickname of every wild catch and every
/// newcomer that is not the starter (2026-09-28).
/// </remarks>
public sealed class PlayNotifications
{
    public PlayNotifications(GameLinkMonitor monitor, CatchCeremony catches, EncounterGuard encounters, NicknameVoteService votes)
    {
        encounters.Caught += (_, caught) => votes.Offer(caught.Pokemon);
        monitor.NewcomerArrived += (_, pokemon) =>
        {
            if (!monitor.IsStarter(pokemon.PID)) votes.Offer(pokemon);
        };

        // Un objeto del suelo, comprado o regalado salta a la mochila encima del juego (2026-09-28).
        monitor.ItemGained += (_, item) => catches.CelebrateItem(item.ItemId, item.Amount);
    }
}
