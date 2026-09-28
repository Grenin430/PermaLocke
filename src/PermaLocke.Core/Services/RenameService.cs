using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Renames a Pokémon from the viewer: writes the nickname into the save and records it (2026-09-26).
/// </summary>
/// <remarks>
/// The order of the move reminder: write first, record after, so the log never claims a name the save refused. The
/// run's own register takes the new name too, so the rest of the app shows it without waiting for the next read.
/// </remarks>
public sealed class RenameService(IPokemonRenamer renamer, IEventStore events, IPokemonRepository pokemon, IClock clock)
{
    /// <summary>The longest nickname Ultra Moon takes.</summary>
    public const int MaxLength = 12;

    public bool CanRenameNow(out string reason) => renamer.CanRenameNow(out reason);

    /// <summary>
    /// What the game's own keyboard can type for a nickname in Spanish (2026-09-28): letters with the accents of the
    /// Latin keyboard, digits, space and its symbols. Anything else (emoji, other alphabets) the game would not show, so it
    /// is refused before it reaches the save or the live party.
    /// </summary>
    /// <remarks>Taken from the Gen 7 Latin keyboard pages (letters, accented letters, symbols), not measured key by key.</remarks>
    public const string Allowed =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 "
        + "ÀÁÂÄÇÈÉÊËÌÍÎÏÑÒÓÔÖÙÚÛÜàáâäçèéêëìíîïñòóôöùúûüßŒœ"
        + "!?¡¿,.…·/-'\"‘’“”()&+~:;%#@*=♂♀";

    /// <summary>Why this name cannot be written, or null when it can.</summary>
    public static string? Problem(string nickname)
    {
        var name = nickname.Trim();
        if (name.Length > MaxLength) return $"Como mucho {MaxLength} caracteres (llevas {name.Length}).";

        var wrong = name.Where(c => !Allowed.Contains(c)).Distinct().ToList();
        return wrong.Count > 0 ? $"El juego no tiene: {string.Join(" ", wrong)}" : null;
    }

    public async Task<DeliveryResult> RenameAsync(Run run, BoxedPokemon target, string nickname,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(target);
        nickname = nickname.Trim();

        if (!target.IsIntact) return Refused($"{target.DisplayName} está dañado en la partida y no se toca.");
        if (target.IsEgg) return Refused("Un huevo no tiene mote.");
        if (Problem(nickname) is { } problem) return Refused(problem);

        var old = target.DisplayName;
        var result = await renamer.ApplyAsync(new NicknameChange(target.Box, target.Slot, target.Pid, old, nickname), ct)
            .ConfigureAwait(false);

        if (!result.Delivered)
        {
            return result;
        }

        await RecordAsync(run, target.Pid, old, nickname.Length == 0 ? target.SpeciesName : nickname, nickname, ct)
            .ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Leaves the rename in the log and the register once the name is in the game: through the save above, or live in
    /// the party by the voted nicknames (2026-09-28).
    /// </summary>
    public async Task RecordAsync(Run run, uint pid, string old, string shown, string nickname, CancellationToken ct = default)
    {
        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.PokemonRenamed,
            Source = EventSource.Player,
            Actor = run.PlayerName,
            Description = $"{old} ahora se llama {shown}.",
            Data = new Dictionary<string, string>
            {
                ["pid"] = pid.ToString("X8"),
                ["antes"] = old,
                ["ahora"] = shown
            }
        }, ct).ConfigureAwait(false);

        var registered = (await pokemon.GetAllAsync(run.Id, ct).ConfigureAwait(false)).FirstOrDefault(p => p.Pid == pid);
        if (registered is not null)
        {
            await pokemon.SaveAsync(registered with { Nickname = nickname.Length == 0 ? null : nickname }, ct).ConfigureAwait(false);
        }
    }

    private static DeliveryResult Refused(string why) => new(DeliveryOutcome.Failed, why);
}
