using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/guarderia.json: when the NURSERY pays a spin and how strong its eggs are (§221).</summary>
/// <remarks>
/// Missing or unreadable file: the defaults of the competition (the ones of the roulette: 1 per trial, 3 for the league,
/// 2 for the rematch). The nursery belongs to roles that were promised it, so it does not vanish with a bad file.
/// </remarks>
public sealed class JsonNurseryCatalog : INurseryCatalog
{
    private static readonly string[] DefaultTrials =
        [.. Enumerable.Range(1, 12).Select(n => $"prueba-{n:00}")];

    public int SpinsPerTrial { get; private init; } = 1;

    public int SpinsForLeague { get; private init; } = 3;

    public int SpinsForRematch { get; private init; } = 2;

    public IReadOnlyList<string> TrialAchievements { get; private init; } = DefaultTrials;

    public string LeagueAchievement { get; private init; } = "alto-mando-campeon";

    public string RematchAchievement { get; private init; } = "alto-mando-otra-vez";

    public int TopBaseStatTotal { get; private init; } = 540;

    public int Divisions { get; private init; } = 12;

    public int MinCandidates { get; private init; } = 15;

    public static JsonNurseryCatalog Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new JsonNurseryCatalog();
            }

            using var stream = File.OpenRead(path);
            var file = JsonSerializer.Deserialize<NurseryFile>(stream);

            if (file is null)
            {
                return new JsonNurseryCatalog();
            }

            return new JsonNurseryCatalog
            {
                SpinsPerTrial = Math.Clamp(file.PerTrial ?? 1, 0, 20),
                SpinsForLeague = Math.Clamp(file.ForLeague ?? 3, 0, 20),
                SpinsForRematch = Math.Clamp(file.ForRematch ?? 2, 0, 20),
                TrialAchievements = file.Trials is { Count: > 0 } trials ? [.. trials] : DefaultTrials,
                LeagueAchievement = file.LeagueAchievement ?? "alto-mando-campeon",
                RematchAchievement = file.RematchAchievement ?? "alto-mando-otra-vez",
                TopBaseStatTotal = Math.Clamp(file.Top ?? 540, 100, 800),
                Divisions = Math.Clamp(file.Divisions ?? 12, 1, 100),
                MinCandidates = Math.Clamp(file.MinCandidates ?? 15, 1, 200)
            };
        }
        catch (JsonException)
        {
            return new JsonNurseryCatalog();
        }
    }

    private sealed record NurseryFile(
        [property: JsonPropertyName("tiradasPorPrueba")] int? PerTrial,
        [property: JsonPropertyName("tiradasPorLiga")] int? ForLeague,
        [property: JsonPropertyName("tiradasPorRematch")] int? ForRematch,
        [property: JsonPropertyName("logrosDePrueba")] IReadOnlyList<string>? Trials,
        [property: JsonPropertyName("logroDeLiga")] string? LeagueAchievement,
        [property: JsonPropertyName("logroDeRematch")] string? RematchAchievement,
        [property: JsonPropertyName("totalMaximo")] int? Top,
        [property: JsonPropertyName("divisiones")] int? Divisions,
        [property: JsonPropertyName("minimoDeCandidatos")] int? MinCandidates);
}
