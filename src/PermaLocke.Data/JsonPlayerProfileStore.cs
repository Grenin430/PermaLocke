using System.Text.Json;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>
/// This machine's player, as <c>Config/jugador.json</c>.
/// </summary>
/// <remarks>
/// In <c>Config/</c> and not in <c>Saves/</c> on purpose: EMPEZAR DE CERO deletes the run and the
/// game save, and a player who starts again is still the same player. Their row in the standings
/// has to survive that, which is the whole reason the profile exists (§123).
/// </remarks>
public sealed class JsonPlayerProfileStore(string configFolder) : IPlayerProfileStore
{
    public const string FileName = "jugador.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private string PathOf => Path.Combine(configFolder, FileName);

    public async Task<PlayerProfile?> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(PathOf))
        {
            return null;
        }

        await using var stream = File.OpenRead(PathOf);
        var profile = await JsonSerializer.DeserializeAsync<PlayerProfile>(stream, Options, ct).ConfigureAwait(false);

        // Un perfil sin id no identifica a nadie. Devolverlo seria publicar bajo Guid.Empty, que es
        // exactamente el mismo jugador para todo el que tenga el fichero roto.
        return profile is null || profile.Id == Guid.Empty
            ? throw new InvalidDataException($"{PathOf} no tiene un id de jugador válido.")
            : profile;
    }

    public async Task SaveAsync(PlayerProfile profile, CancellationToken ct = default)
    {
        Directory.CreateDirectory(configFolder);

        var temporary = PathOf + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, profile, Options, ct).ConfigureAwait(false);
        }

        File.Move(temporary, PathOf, overwrite: true);
    }
}
