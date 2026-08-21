using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/penalties.json: what losing costs.</summary>
public sealed class JsonPenaltyCatalog(PenaltyRules rules) : IPenaltyCatalog
{
    public PenaltyRules Rules { get; } = rules;

    /// <summary>
    /// The rules the competition agreed, and the ones used when the file is missing.
    /// </summary>
    /// <remarks>
    /// Falling back to the real numbers rather than to zero: a missing file must not quietly turn
    /// deaths free. If the penalties ever stop applying, that has to be somebody's decision.
    /// </remarks>
    public static JsonPenaltyCatalog Default { get; } = new(new PenaltyRules(25, 100, 4));

    public static JsonPenaltyCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Default;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<PenaltyFile>(stream);

        if (file is null)
        {
            return Default;
        }

        return new JsonPenaltyCatalog(new PenaltyRules(
            Math.Max(0, file.PerDeath ?? Default.Rules.PerDeath),
            Math.Max(0, file.PerWipe ?? Default.Rules.PerWipe),
            Math.Max(0, file.MaxWipes ?? Default.Rules.MaxWipes)));
    }

    private sealed record PenaltyFile(
        [property: JsonPropertyName("porMuerte")] int? PerDeath,
        [property: JsonPropertyName("porEquipoCaido")] int? PerWipe,
        [property: JsonPropertyName("maximoEquiposCaidos")] int? MaxWipes);
}
