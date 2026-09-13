using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/gacha.json: the banners, their cost and their odds.</summary>
public sealed class JsonGachaCatalog : IGachaCatalog
{
    private JsonGachaCatalog(IReadOnlyList<GachaTier> tiers, IReadOnlyList<GachaBanner> banners,
        IReadOnlyList<StageOdds> stageOdds)
    {
        Tiers = tiers;
        Banners = banners;
        StageOdds = stageOdds;
    }

    public IReadOnlyList<GachaTier> Tiers { get; }

    public IReadOnlyList<GachaBanner> Banners { get; }

    public IReadOnlyList<StageOdds> StageOdds { get; }

    /// <summary>Empty when the file is missing, so the section says so instead of inventing odds.</summary>
    public static JsonGachaCatalog Empty { get; } = new([], [], []);

    public static JsonGachaCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<GachaFile>(stream);

        if (file?.Tiers is not { Count: > 0 } tiers || file.Banners is not { Count: > 0 } banners)
        {
            return Empty;
        }

        return new JsonGachaCatalog(
            [.. tiers.Select(t => new GachaTier(t.Id, t.Name, t.MaxBaseStatTotal, t.LegendaryChance,
                t.MinLevel, t.MaxLevel, t.PerfectIvs, t.ShinyChance))],
            [.. banners.Select(b => new GachaBanner(b.Id, b.Name, b.Description, b.Cost,
                b.Chances ?? new Dictionary<string, double>()))],
            [.. (file.StageOdds ?? []).Select(s => new StageOdds(s.Cleared, s.Second, s.Final))
                .OrderBy(s => s.Cleared)]);
    }

    private sealed record GachaFile(
        [property: JsonPropertyName("tiers")] IReadOnlyList<TierFile>? Tiers,
        [property: JsonPropertyName("banners")] IReadOnlyList<BannerFile>? Banners,
        [property: JsonPropertyName("etapaPorProgreso")] IReadOnlyList<StageOddsFile>? StageOdds);

    private sealed record StageOddsFile(
        [property: JsonPropertyName("etapas")] int Cleared,
        [property: JsonPropertyName("segunda")] int Second,
        [property: JsonPropertyName("final")] int Final);

    private sealed record TierFile(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("maxBaseStatTotal")] int MaxBaseStatTotal,
        [property: JsonPropertyName("legendaryChance")] double LegendaryChance,
        [property: JsonPropertyName("minLevel")] int MinLevel,
        [property: JsonPropertyName("maxLevel")] int MaxLevel,
        [property: JsonPropertyName("perfectIvs")] int PerfectIvs,
        [property: JsonPropertyName("shinyChance")] double ShinyChance);

    private sealed record BannerFile(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("cost")] int Cost,
        [property: JsonPropertyName("chances")] Dictionary<string, double>? Chances);
}

/// <summary>
/// Reads Data/species.json, the base stat totals the gacha sorts its tiers by.
/// </summary>
/// <remarks>
/// Generated from the ROM by <c>PermaLocke.RomTool species</c>. Missing file means an empty
/// catalog and a gacha that refuses to roll, which is better than rolling from a made-up list.
/// </remarks>
public sealed class JsonSpeciesStatsCatalog : ISpeciesStatsCatalog
{
    private JsonSpeciesStatsCatalog(IReadOnlyList<SpeciesStats> all, IReadOnlyList<string> natures,
        IReadOnlyList<string> abilities, IReadOnlyList<EvolutionLine> lines)
    {
        All = all;
        Natures = natures;
        Abilities = abilities;
        Lines = lines;
    }

    public IReadOnlyList<SpeciesStats> All { get; }

    public IReadOnlyList<string> Natures { get; }

    public IReadOnlyList<string> Abilities { get; }

    public IReadOnlyList<EvolutionLine> Lines { get; }

    public static JsonSpeciesStatsCatalog Empty { get; } = new([], [], [], []);

    public static JsonSpeciesStatsCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<SpeciesFile>(stream);

        if (file?.Species is not { Count: > 0 } species)
        {
            return Empty;
        }

        // Una familia sin etapas no es una familia. Se descarta en vez de guardarse porque
        // StageAt indexa la ultima, y una lista vacia ahi seria una excepcion en mitad de una
        // tirada -- en un fichero generado, o sea el sitio donde nadie va a mirar.
        var lines = (file.Lines ?? [])
            .Where(line => line.Count > 0 && line.All(stage => stage.Count > 0))
            .Select(line => new EvolutionLine([.. line.Select(stage => (IReadOnlyList<int>)stage)]))
            .ToList();

        return new JsonSpeciesStatsCatalog(
            [.. species.Select(s => new SpeciesStats(s.Id, s.Name, s.BaseStatTotal, s.Legendary,
                s.Abilities ?? []))],
            file.Natures ?? [],
            file.Abilities ?? [],
            lines);
    }

    private sealed record SpeciesFile(
        [property: JsonPropertyName("species")] IReadOnlyList<SpeciesEntry>? Species,
        [property: JsonPropertyName("natures")] IReadOnlyList<string>? Natures,
        [property: JsonPropertyName("abilities")] IReadOnlyList<string>? Abilities,
        [property: JsonPropertyName("lines")] IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? Lines);

    private sealed record SpeciesEntry(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("baseStatTotal")] int BaseStatTotal,
        [property: JsonPropertyName("legendary")] bool Legendary,
        [property: JsonPropertyName("abilities")] IReadOnlyList<string>? Abilities);
}
