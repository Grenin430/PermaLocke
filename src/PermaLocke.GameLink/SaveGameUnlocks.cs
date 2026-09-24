using Microsoft.Extensions.Logging;
using PKHeX.Core;
using PermaLocke.Core.Abstractions;

namespace PermaLocke.GameLink;

/// <summary>
/// Turns on the saved game's own unlock flags.
/// </summary>
/// <remarks>
/// <para>
/// Today there is one: Mega Evolution. Ultra Moon does not gate it on carrying the Key Stone — the
/// item was written into the bag and read back, the Pokémon held its stone, and no button appeared.
/// What gates it is a field in the trainer block, sitting next to the one for Z-moves that was
/// already on. That pairing is what identified it, and it was confirmed by turning it on and seeing
/// the button.
/// </para>
/// <para>
/// Writes the save file, so it needs the game closed, copies the whole partida first and reads the
/// flag back before reporting success. A prize that says it unlocked something and did not would be
/// worse than one that refused.
/// </para>
/// </remarks>
public sealed class SaveGameUnlocks(PlayerSave save, string backupFolder,
    ILogger<SaveGameUnlocks> logger) : IGameUnlocks
{
    /// <summary>The key <c>Data/rewards.json</c> uses for Mega Evolution.</summary>
    public const string MegaEvolution = "megaevolucion";

    public IReadOnlySet<string> Known { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { MegaEvolution };

    public bool CanApplyNow(out string reason)
    {
        if (save.IsGameLoaded())
        {
            reason = "El juego está abierto. Guarda y cierra Azahar.";
            return false;
        }

        if (save.Find() is null)
        {
            reason = "No se encuentra la partida de Ultra Luna.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public Task<UnlockResult> ApplyAsync(IReadOnlyList<string> keys, CancellationToken ct = default) =>
        Task.Run(() => Apply(keys), ct);

    private UnlockResult Apply(IReadOnlyList<string> keys)
    {
        if (keys.Count == 0)
        {
            return new UnlockResult(true, string.Empty);
        }

        // Una llave que este escritor no conoce se rechaza en vez de ignorarse: una errata en el
        // JSON que no hiciera nada dejaría un premio que se cobra y no desbloquea.
        if (keys.FirstOrDefault(key => !Known.Contains(key)) is { } unknown)
        {
            return new UnlockResult(false, $"«{unknown}» no es nada que se sepa desbloquear.");
        }

        if (!CanApplyNow(out var reason))
        {
            return new UnlockResult(false, reason);
        }

        var path = save.Find()!;

        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var loaded) || loaded is not SAV7USUM game)
            {
                return new UnlockResult(false,
                    "No se ha podido leer tu partida.");
            }

            Backup(path);

            foreach (var key in keys)
            {
                if (string.Equals(key, MegaEvolution, StringComparison.OrdinalIgnoreCase))
                {
                    game.Blocks.MyStatus.MegaUnlocked = true;
                }
            }

            File.WriteAllBytes(path, game.Write().ToArray());

            // Releído del fichero, nunca del objeto que se acaba de escribir.
            if (!SaveUtil.TryGetSaveFile(path, out var again) || again is not SAV7USUM written
                || !Applied(written, keys))
            {
                return new UnlockResult(false,
                    "No se ha podido guardar el cambio. Vuelve a intentarlo.");
            }

            logger.LogWarning("Desbloqueado en la partida: {Keys}", string.Join(", ", keys));

            return new UnlockResult(true,
                "Megaevolución desbloqueada.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el desbloqueo de {Keys}", string.Join(", ", keys));

            return new UnlockResult(false,
                "No se pudo escribir en la partida.");
        }
    }

    private static bool Applied(SAV7USUM game, IReadOnlyList<string> keys) =>
        keys.All(key => !string.Equals(key, MegaEvolution, StringComparison.OrdinalIgnoreCase)
                        || game.Blocks.MyStatus.MegaUnlocked);

    /// <summary>
    /// Copies the whole save before touching it, into PermaLocke's own backup folder.
    /// </summary>
    /// <remarks>
    /// Never next to the partida: that folder is the emulator's save-data archive and it keeps its
    /// own count of what is inside (§67). Leaving a stray file there is putting something into a
    /// structure that is not ours.
    /// </remarks>
    private void Backup(string path)
    {
        Directory.CreateDirectory(backupFolder);

        var name = $"main-antes-de-desbloquear-{DateTime.Now:yyyyMMdd-HHmmss}.sav";
        File.Copy(path, Path.Combine(backupFolder, name), overwrite: false);

        logger.LogInformation("Copia de la partida guardada en {Name}", name);
    }
}
