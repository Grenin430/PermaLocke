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
    public PlayNotifications(GameLinkMonitor monitor, Notifier notifier, PokemonSpriteService sprites, EncounterGuard encounters,
        BallControlService balls, CatchCeremony catches)
    {
        // Una captura salvaje: su carta vuela al álbum encima del juego (§190).
        encounters.Caught += (_, caught) => catches.Celebrate(caught);

        // Y un huevo, un fósil o un regalo que aparece en el equipo, también (2026-09-28).
        monitor.NewcomerArrived += (_, pokemon) => catches.CelebrateNewcomer(pokemon);

        // Un objeto del suelo, comprado o regalado salta a la mochila encima del juego (2026-09-28).
        monitor.ItemGained += (_, item) => catches.CelebrateItem(item.ItemId, item.Amount);

        balls.FirstBallDetected += (_, _) => notifier.Say(
            ToastKind.Info, "¡Primeras Poké Balls!",
            "Tu PermaLocke empieza: desde ahora cuentan las capturas y las muertes.",
            sprites.GetBall());

        // Que te quiten las Poké Balls sin decirte por qué se lee como un fallo del juego (§117).
        encounters.Said += (_, notice) => notifier.Say(notice.Kind, notice.Title, notice.Message,
            SpriteFor(notice, sprites));

        monitor.PokemonDied += (_, fallen) =>
            notifier.Say(ToastKind.Death, $"{fallen.Name} ha caído", string.Empty, sprites.Get(fallen.Species, fallen.Form, fallen.Shiny));

        // Esto salta al CERRAR el emulador, que es cuando la marca se puede escribir en la partida.
        // Llega con el juego ya cerrado y por tanto sin nada tapando la pantalla, que es justo
        // cuando un aviso se lee: es lo único que dice que se ha hecho.
        // BAJA y no EQUIPO CAÍDO: casi siempre es uno solo, y la otra etiqueta hacía pensar en un wipe.
        monitor.DeathMarked += (_, notice) => notifier.Say(ToastKind.Death, "Caídos", notice);

        // Las muertes del wipe se ven en la plantilla una detrás de otra; el aviso dice que se acabó.
        monitor.TeamWiped += (_, _) => notifier.Say(ToastKind.TeamWipe, "Equipo caído", "Fin de la partida.");

        monitor.RewardGiven += (_, given) => notifier.Say(
            ToastKind.Reward,
            "Premio entregado",
            given.Message,
            First(given, sprites));
    }

    /// <summary>
    /// The Pokémon of the battle when it is known; otherwise, for the balls, the Poké Ball itself.
    /// </summary>
    private static BitmapSource? SpriteFor(EncounterNotice notice, PokemonSpriteService sprites)
    {
        if (notice.Species is { } species && sprites.Get(species) is { } pokemon)
        {
            return pokemon;
        }

        return notice.Kind is ToastKind.BallsTaken or ToastKind.BallsBack ? sprites.GetBall() : null;
    }

    /// <summary>The icon of the first item of a prize, when the prize hands over items at all.</summary>
    private static BitmapSource? First(RewardResult given, PokemonSpriteService sprites) =>
        given.Reward?.Items is { Count: > 0 } items ? sprites.GetItem(items[0].Id) : null;
}
