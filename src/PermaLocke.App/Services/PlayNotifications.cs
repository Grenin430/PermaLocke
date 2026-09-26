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
/// A death gets both: the <see cref="DeathCeremony"/> over the game, which is the moment, and the
/// corner notice, which stays in the list of notices once the moment has gone.
/// </para>
/// <para>
/// Every notice carries a sprite from the player's own cartridge when there is one to carry: the Pokémon of the
/// battle, the fallen one, the Poké Ball that was taken or given back, the item of a prize.
/// </para>
/// </remarks>
public sealed class PlayNotifications
{
    public PlayNotifications(GameLinkMonitor monitor, MaintenanceService maintenance, Notifier notifier,
        DeathCeremony ceremony, PokemonSpriteService sprites, EncounterGuard encounters, BallControlService balls,
        GhostService ghosts)
    {
        balls.FirstBallDetected += (_, _) => notifier.Say(
            ToastKind.Info, "¡Primeras Poké Balls!",
            "Tu PermaLocke empieza: desde ahora cuentan las capturas y las muertes.",
            sprites.GetBall());

        // Que te quiten las Poké Balls sin decirte por qué se lee como un fallo del juego (§117).
        encounters.Said += (_, notice) => notifier.Say(notice.Kind, notice.Title, notice.Message,
            SpriteFor(notice, sprites));

        monitor.PokemonDied += (_, fallen) =>
        {
            ceremony.Mourn(fallen);

            // Al momento, a las pantallas de los demás (§183). Solo las vistas en directo, no las marcadas a mano.
            ghosts.Send(fallen, "Jugador");

            // Lo que costó DE VERDAD. Ponía «−25 puntos» escrito a mano, que es mentira para el
            // CAGONETA -no pierde puntos- y para cualquier rol que multiplique las pérdidas.
            notifier.Say(
                ToastKind.Death,
                $"{fallen.Name} ha caído",
                fallen.Penalty > 0 ? $"−{fallen.Penalty} puntos." : string.Empty,
                sprites.Get(fallen.Species, fallen.Form, fallen.Shiny));
        };

        // Las marcadas a mano en MANTENIMIENTO son justo las que la app no llegó a ver: tienen su
        // momento igual. Sin aviso de esquina, porque quien las marca acaba de pulsar el botón.
        maintenance.MarkedDead += (_, fallen) => ceremony.Mourn(fallen);

        // Esto salta al CERRAR el emulador, que es cuando la marca se puede escribir en la partida.
        // Llega con el juego ya cerrado y por tanto sin nada tapando la pantalla, que es justo
        // cuando un aviso se lee: es lo único que dice que se ha hecho.
        monitor.DeathMarked += (_, notice) => notifier.Say(ToastKind.TeamWipe, "Caídos", notice);

        monitor.TeamWiped += (_, penalty) =>
        {
            // Detrás de las muertes que lo formaron, que ya están en la cola.
            ceremony.TeamFell(penalty.Points);

            // Llueve sangre aquí y en el emulador de los demás (§184).
            ghosts.Rain("Jugador");

            notifier.Say(ToastKind.TeamWipe, "Equipo caído", $"{penalty.Points} puntos.");
        };

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
