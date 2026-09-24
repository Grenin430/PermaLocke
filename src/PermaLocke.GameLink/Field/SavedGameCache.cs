using PKHeX.Core;

namespace PermaLocke.GameLink.Field;

/// <summary>
/// The player's last save, parsed once and parsed again only when the file changes. Only reads.
/// </summary>
/// <remarks>
/// Three readers take their ground truth from the save — where the player stood when saving, the trainer card
/// counters and the Pokédex — and a save is 445 KB that PKHeX has to decrypt. Asking the file's write time
/// first makes the common case nothing.
/// </remarks>
public sealed class SavedGameCache(PlayerSave save)
{
    private readonly object _gate = new();
    private string? _path;
    private DateTime _writtenAt;
    private SAV7USUM? _loaded;

    /// <summary>The last save and when it was written, or null when there is none PKHeX recognises.</summary>
    public (SAV7USUM Save, DateTime WrittenAt)? Load()
    {
        lock (_gate)
        {
            try
            {
                if (save.Find() is not { } path || !File.Exists(path))
                {
                    return null;
                }

                var writtenAt = File.GetLastWriteTimeUtc(path);

                if (_loaded is null || path != _path || writtenAt != _writtenAt)
                {
                    var bytes = File.ReadAllBytes(path);

                    if (!SaveUtil.TryGetSaveFile(bytes, out var parsed) || parsed is not SAV7USUM usum)
                    {
                        return null;
                    }

                    _loaded = usum;
                    _path = path;
                    _writtenAt = writtenAt;
                }

                return (_loaded, _writtenAt);
            }
            catch (IOException)
            {
                // El juego está guardando justo ahora: la próxima vuelta lo leerá entero.
                return _loaded is null ? null : (_loaded, _writtenAt);
            }
        }
    }
}
