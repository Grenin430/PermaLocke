using System.Windows.Media.Imaging;
using PermaLocke.Core.Domain;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <summary>
/// Turns what the watcher does on its own into notices on top of the game.
/// </summary>
/// <remarks>
/// <para>
/// A separate class and not a couple of lines in the startup, because the list of things worth
/// interrupting somebody for is a decision, and decisions belong somewhere they can be read. The
/// rule used here: <b>a notice is for something that already happened and cannot be undone</b> — a
/// death, a penalty, a prize handed over. Not for states, not for warnings, not for «still no
/// connection»: those live on HOME, where you go to look, instead of on top of a battle.
/// </para>
/// <para>
/// Nothing here decides anything. Every one of these events is raised <b>after</b> the thing was
/// written and verified, so a notice can never claim something that did not happen.
/// </para>
/// <para>
/// A death gets the corner notice, which stays in the list of notices. The moment itself is in the cap panel
/// beside the game (2026-10-15, <c>CapDeath</c>): nothing covers the game any more, neither the old scene
/// over it nor the blood rain of a wipe.
/// </para>
/// <para>
/// Every notice carries a sprite from the player's own cartridge when there is one to carry: the Pokémon of the
/// battle, the fallen one, the Poké Ball that was taken or given back, the item of a prize.
/// </para>
/// </remarks>
public sealed class PlayNotifications
{
    public PlayNotifications(GameLinkMonitor monitor, CatchCeremony catches)
    {
        // La carta que volaba al álbum y los avisos encima del juego, quitados (2026-10-17, pedido del jugador).

        // Un objeto del suelo, comprado o regalado salta a la mochila encima del juego (2026-09-28).
        monitor.ItemGained += (_, item) => catches.CelebrateItem(item.ItemId, item.Amount);
    }
}
