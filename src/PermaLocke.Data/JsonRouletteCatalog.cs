using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/roulette.json: the faces of the LUDÓPATA wheel and what they do.</summary>
/// <remarks>
/// <para>
/// A face whose <c>efecto</c> is not one this build knows is <b>dropped</b>, not defaulted: a face
/// that lands and does nothing would look exactly like a wheel that works.
/// </para>
/// <para>
/// The abilities are named in the file and resolved here against the cartridge's own table, so a
/// misspelled one disappears at load instead of turning into whatever ability that id happens to
/// be. Anything past the last ability this game knows goes the same way.
/// </para>
/// </remarks>
public sealed class JsonRouletteCatalog : IRouletteCatalog
{
    private JsonRouletteCatalog(
        IReadOnlyList<RouletteFace> faces,
        IReadOnlyList<int> good,
        IReadOnlyList<int> bad,
        IReadOnlyList<int> healing,
        int perTrial,
        int league,
        int rematch,
        IReadOnlyList<string> trials,
        string leagueAchievement,
        string rematchAchievement)
    {
        Faces = faces;
        GoodAbilities = good;
        BadAbilities = bad;
        HealingItems = healing;
        SpinsPerTrial = perTrial;
        SpinsForLeague = league;
        SpinsForRematch = rematch;
        TrialAchievements = trials;
        LeagueAchievement = leagueAchievement;
        RematchAchievement = rematchAchievement;
    }

    public IReadOnlyList<RouletteFace> Faces { get; }

    public IReadOnlyList<int> GoodAbilities { get; }

    public IReadOnlyList<int> BadAbilities { get; }

    public IReadOnlyList<int> HealingItems { get; }

    public int SpinsPerTrial { get; }

    public int SpinsForLeague { get; }

    public int SpinsForRematch { get; }

    public IReadOnlyList<string> TrialAchievements { get; }

    public string LeagueAchievement { get; }

    public string RematchAchievement { get; }

    /// <summary>Empty when the file is missing, so the screen says so instead of inventing a wheel.</summary>
    public static JsonRouletteCatalog Empty { get; } =
        new([], [], [], [], 1, 3, 2, [], string.Empty, string.Empty);

    /// <summary>Ability names that the cartridge does not have, for whoever has to fix the file.</summary>
    public IReadOnlyList<string> UnknownAbilities { get; private init; } = [];

    public static JsonRouletteCatalog Load(string path, IAbilityLookup abilities)
    {
        ArgumentNullException.ThrowIfNull(abilities);

        if (!File.Exists(path))
        {
            return Empty;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<RouletteFile>(stream);

        if (file is null)
        {
            return Empty;
        }

        var unknown = new List<string>();

        return new JsonRouletteCatalog(
            [.. (file.Faces ?? []).Select(Read).OfType<RouletteFace>()],
            Resolve(file.GoodAbilities, abilities, unknown),
            Resolve(file.BadAbilities, abilities, unknown),
            [.. (file.HealingItems ?? []).Where(item => item.Id > 0).Select(item => item.Id)],
            Math.Max(0, file.SpinsPerTrial ?? 1),
            Math.Max(0, file.SpinsForLeague ?? 3),
            Math.Max(0, file.SpinsForRematch ?? 2),
            file.TrialAchievements ?? [],
            file.LeagueAchievement ?? string.Empty,
            file.RematchAchievement ?? string.Empty)
        {
            UnknownAbilities = unknown
        };
    }

    private static RouletteFace? Read(FaceEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id)
            || !Enum.TryParse<RouletteEffect>(entry.Effect, ignoreCase: true, out var effect))
        {
            return null;
        }

        return new RouletteFace(
            entry.Id,
            string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name,
            entry.Detail ?? string.Empty,
            entry.Good ?? true,
            effect,
            entry.Amount ?? 0,
            Math.Max(1, entry.Each ?? 1),
            entry.Banners,
            entry.Short ?? string.Empty,
            entry.Figure ?? string.Empty,
            // Un id negativo se descarta aqui en vez de llegar a la pantalla: lo que pide un
            // dibujo solo tiene que saber que cero es «no hay».
            Math.Max(0, entry.ItemIcon ?? 0),
            Math.Max(0, entry.SpeciesIcon ?? 0));
    }

    private static List<int> Resolve(IReadOnlyList<string>? names, IAbilityLookup abilities,
        List<string> unknown)
    {
        var ids = new List<int>();

        foreach (var name in names ?? [])
        {
            var id = abilities.GetId(name);

            if (id is <= 0 || id > abilities.LastAbility)
            {
                unknown.Add(name);
                continue;
            }

            ids.Add(id);
        }

        return ids;
    }

    private sealed record RouletteFile(
        [property: JsonPropertyName("tiradasPorPrueba")] int? SpinsPerTrial,
        [property: JsonPropertyName("tiradasPorLiga")] int? SpinsForLeague,
        [property: JsonPropertyName("tiradasPorRematch")] int? SpinsForRematch,
        [property: JsonPropertyName("logrosDePrueba")] IReadOnlyList<string>? TrialAchievements,
        [property: JsonPropertyName("logroDeLiga")] string? LeagueAchievement,
        [property: JsonPropertyName("logroDeRematch")] string? RematchAchievement,
        [property: JsonPropertyName("objetosCurativos")] IReadOnlyList<HealingEntry>? HealingItems,
        [property: JsonPropertyName("habilidadesBuenas")] IReadOnlyList<string>? GoodAbilities,
        [property: JsonPropertyName("habilidadesMalas")] IReadOnlyList<string>? BadAbilities,
        [property: JsonPropertyName("caras")] IReadOnlyList<FaceEntry>? Faces);

    private sealed record HealingEntry(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("nombre")] string? Name);

    private sealed record FaceEntry(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("nombre")] string? Name,
        [property: JsonPropertyName("detalle")] string? Detail,
        [property: JsonPropertyName("buena")] bool? Good,
        [property: JsonPropertyName("efecto")] string? Effect,
        [property: JsonPropertyName("cantidad")] int? Amount,
        [property: JsonPropertyName("cada")] int? Each,
        [property: JsonPropertyName("banners")] IReadOnlyList<string>? Banners,
        [property: JsonPropertyName("corto")] string? Short,
        [property: JsonPropertyName("cifra")] string? Figure,
        [property: JsonPropertyName("icono")] int? ItemIcon,
        [property: JsonPropertyName("iconoEspecie")] int? SpeciesIcon);
}
