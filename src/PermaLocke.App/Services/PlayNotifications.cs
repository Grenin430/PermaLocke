using PermaLocke.Core.Domain;

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
/// </remarks>
public sealed class PlayNotifications
{
    public PlayNotifications(GameLinkMonitor monitor, Notifier notifier, PokemonSpriteService sprites)
    {
        monitor.PokemonDied += (_, name) => notifier.Say(
            $"{name} ha caído",
            "Registrado en la run. −25 puntos.",
            ToastTone.Bad);

        // Esto salta al CERRAR el emulador, que es cuando la marca se puede escribir en la partida.
        // Llega con el juego ya cerrado y por tanto sin nada tapando la pantalla, que es justo
        // cuando un aviso se lee: es lo único que dice que se ha hecho.
        monitor.DeathMarked += (_, notice) => notifier.Say(
            "Caídos marcados en la partida",
            notice,
            ToastTone.Bad);

        monitor.TeamWiped += (_, penalty) => notifier.Say(
            "Equipo caído",
            $"{penalty.Points} puntos.",
            ToastTone.Bad);

        monitor.RewardGiven += (_, given) => notifier.Say(
            "Premio entregado",
            given.Message,
            ToastTone.Good,
            First(given, sprites));
    }

    /// <summary>The icon of the first item of a prize, when the prize hands over items at all.</summary>
    private static System.Windows.Media.Imaging.BitmapSource? First(RewardResult given,
        PokemonSpriteService sprites) =>
        given.Reward?.Items is { Count: > 0 } items ? sprites.GetItem(items[0].Id) : null;
}
