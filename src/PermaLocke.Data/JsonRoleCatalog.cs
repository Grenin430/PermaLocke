using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/roles.json: the ways the competition can be played.</summary>
/// <remarks>
/// Empty when the file is missing. That is on purpose and it is not a quiet failure: a run cannot
/// be created without a role, so an empty catalogue stops the competition at the front door
/// instead of starting everyone on invented rules.
/// </remarks>
public sealed class JsonRoleCatalog(IReadOnlyList<Role> all, IReadOnlySet<int> important) : IRoleCatalog
{
    public IReadOnlyList<Role> All { get; } = all;

    public IReadOnlySet<int> ImportantTrainerClasses { get; } = important;

    public static JsonRoleCatalog Empty { get; } = new([], new HashSet<int>());

    public Role? Find(string? id) => string.IsNullOrWhiteSpace(id)
        ? null
        : All.FirstOrDefault(role => string.Equals(role.Id, id, StringComparison.OrdinalIgnoreCase));

    public static JsonRoleCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<RoleFile>(stream);

        if (file?.Roles is not { Count: > 0 } entries)
        {
            return Empty;
        }

        var important = file.ImportantClasses is { Count: > 0 } classes
            ? classes.ToHashSet()
            : new HashSet<int>();

        return new JsonRoleCatalog(
        [
            .. entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
                .Select(entry => new Role(
                    entry.Id,
                    string.IsNullOrWhiteSpace(entry.Name) ? entry.Id.ToUpperInvariant() : entry.Name,
                    entry.Summary ?? string.Empty,
                    entry.Description ?? string.Empty,

                    // Los multiplicadores se acotan a algo defendible: un rol que multiplicase por
                    // cien no sería una dificultad, sería un error de tecleo.
                    Math.Clamp(entry.Earn ?? 1.0, 0, 10),
                    Math.Clamp(entry.Lose ?? 1.0, 0, 10),
                    Math.Clamp(entry.EnemyLevelPercent ?? entry.LegacyTrainerLevelPercent ?? 0, 0, 200),
                    Math.Clamp(entry.PlayerCapPercent ?? 0, 0, 200),
                    Math.Clamp(entry.ExtraTrainerPokemon ?? 0, 0, 5)))
        ], important);
    }

    private sealed record RoleFile(
        [property: JsonPropertyName("roles")] IReadOnlyList<RoleEntry>? Roles,
        [property: JsonPropertyName("clasesImportantes")] IReadOnlyList<int>? ImportantClasses);

    private sealed record RoleEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("ganancia")] double? Earn,
        [property: JsonPropertyName("perdida")] double? Lose,
        [property: JsonPropertyName("nivelEnemigos")] int? EnemyLevelPercent,
        [property: JsonPropertyName("nivelEntrenadores")] int? LegacyTrainerLevelPercent,
        [property: JsonPropertyName("capDelJugador")] int? PlayerCapPercent,
        [property: JsonPropertyName("pokemonExtra")] int? ExtraTrainerPokemon);
}
