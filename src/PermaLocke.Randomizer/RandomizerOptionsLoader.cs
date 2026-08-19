using System.Text.Json;
using System.Text.Json.Serialization;

namespace PermaLocke.Randomizer;

/// <summary>Reads <c>Data/randomizer.json</c> into a <see cref="RandomizerOptions"/>.</summary>
public static class RandomizerOptionsLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    /// <summary>
    /// Returns the defaults when the file is absent, and throws on a malformed one: randomizing
    /// with rules other than the ones the file states would be worse than refusing to start.
    /// </summary>
    public static RandomizerOptions Load(string path)
    {
        if (!File.Exists(path))
        {
            return new RandomizerOptions();
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<RandomizerOptions>(stream, Options)
               ?? throw new InvalidDataException($"{path} está vacío o no es JSON válido.");
    }
}
