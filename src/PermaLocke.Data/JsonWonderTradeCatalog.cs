using System.Text.Json;
using System.Text.Json.Serialization;
using PermaLocke.Core.Domain;

namespace PermaLocke.Data;

/// <summary>Reads Data/wondertrade.json: how wide the band of an acceptable trade is.</summary>
public sealed class JsonWonderTradeCatalog(WonderTradeWindow window) : IWonderTradeCatalog
{
    public WonderTradeWindow Window { get; } = window;

    /// <summary>
    /// The band the file describes, and the one used when there is no file.
    /// </summary>
    /// <remarks>
    /// Falling back to a working default rather than to nothing: a missing file should not turn
    /// the wonder trade into a screen that refuses to explain itself. -8% / +20% is what the
    /// competition asked for, so it is also what the code assumes.
    /// </remarks>
    public static JsonWonderTradeCatalog Default { get; } = new(new WonderTradeWindow(0.08, 0.10, true));

    public static JsonWonderTradeCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return Default;
        }

        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<WonderTradeFile>(stream);

        if (file?.Band is not { } band)
        {
            return Default;
        }

        // Una banda negativa o absurda deja el intercambio sin candidatos y sin explicación, así
        // que se acota aquí: como mucho la mitad por abajo y el doble por arriba.
        return new JsonWonderTradeCatalog(new WonderTradeWindow(
            Math.Clamp(band.Below, 0, 0.5),
            Math.Clamp(band.Above, 0, 1.0),
            file.AllowLegendaries ?? true));
    }

    private sealed record WonderTradeFile(
        [property: JsonPropertyName("banda")] BandFile? Band,
        [property: JsonPropertyName("permitirLegendarios")] bool? AllowLegendaries);

    private sealed record BandFile(
        [property: JsonPropertyName("porDebajo")] double Below,
        [property: JsonPropertyName("porEncima")] double Above);
}
