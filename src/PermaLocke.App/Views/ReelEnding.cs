namespace PermaLocke.App.Views;

/// <summary>
/// One way the reel can come to a stop: where it plants, and when each click lands.
/// </summary>
/// <param name="Stops">
/// Cell offsets from the winner, in the order they are reached. A positive number is a cell short
/// of the winner; a <b>negative</b> one is past it, which is how a genuine false finish is written.
/// The last is always zero.
/// </param>
/// <param name="At">When each stop happens, as a fraction of the whole spin.</param>
/// <param name="Overshoot">Whether the final move overshoots and springs back.</param>
public sealed record ReelEnding(string Name, double[] Stops, double[] At, bool Overshoot)
{
    /// <summary>
    /// Every closing this screen knows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There used to be one: plant three cells short, click three times, spring back on the last.
    /// It is a good ending and it was the same every single pull, so anybody who plays enough
    /// learns exactly where the reel is going to land three clicks before it lands there — which
    /// is the one thing a gacha animation must not do.
    /// </para>
    /// <para>
    /// The rule they all obey: <b>none of them may correlate with what came out.</b> The number of
    /// clicks, the plant distance and the overshoot are all things a player would learn to read,
    /// so which ending plays is drawn from its own stream — not from the tier, not from the
    /// species, not from the duration. Any of these can precede any result.
    /// </para>
    /// </remarks>
    public static readonly ReelEnding[] All =
    [
        // El de siempre. Se planta a tres, tres clics y el último se pasa y vuelve.
        new("tres clics", [3, 2, 1, 0], [0.72, 0.84, 0.93, 1.00], Overshoot: true),

        // Dos clics largos y una entrada limpia: parece que va a fallar el último y no falla.
        new("dos largos", [2, 1, 0], [0.70, 0.86, 1.00], Overshoot: false),

        // Cuatro rápidos que se van frenando. Llega antes y se arrastra más.
        new("cuatro cortos", [4, 3, 2, 1, 0], [0.66, 0.77, 0.86, 0.93, 1.00], Overshoot: false),

        // Casi sin clics: un arrastre largo hasta la casilla de al lado y un único golpe final.
        new("arrastre", [1, 0], [0.74, 1.00], Overshoot: true),

        // La pasada de verdad: se va UNA CASILLA MÁS ALLÁ del ganador y vuelve. Es la única que
        // enseña otro Pokémon bajo el marcador antes de dar el bueno, y por eso es la que más
        // engaña -- y por eso mismo no puede depender del resultado.
        new("se pasa y vuelve", [3, 1, -1, 0], [0.70, 0.83, 0.93, 1.00], Overshoot: false),
    ];

    /// <summary>
    /// Picks one, from a stream of its own.
    /// </summary>
    /// <remarks>
    /// Seeded so the same pull always animates the same way and can be replayed, and salted so it
    /// shares nothing with the draw that chose the Pokémon: two streams from one seed that are
    /// used for different things is exactly how §27 leaked one module into another.
    /// </remarks>
    public static ReelEnding For(ulong seed, int number) =>
        All[new Random(unchecked((int)(seed ^ (ulong)number * 2654435761UL) ^ 0x5E4L.GetHashCode()))
            .Next(All.Length)];

    /// <summary>
    /// Checks every profile the moment the class is first used.
    /// </summary>
    /// <remarks>
    /// A profile whose last stop is not the winner lands the reel on the <b>wrong Pokémon</b>, and
    /// the screen would still announce the right one — the result was decided long before the
    /// animation. That is a lie the player would see and nothing would report, so a malformed
    /// profile stops the screen instead of playing wrong.
    /// </remarks>
    static ReelEnding()
    {
        foreach (var ending in All)
        {
            if (ending.Stops.Length != ending.At.Length || ending.Stops.Length < 2)
            {
                throw new InvalidOperationException(
                    $"El cierre «{ending.Name}» tiene {ending.Stops.Length} paradas y "
                    + $"{ending.At.Length} tiempos.");
            }

            if (ending.Stops[^1] != 0)
            {
                throw new InvalidOperationException(
                    $"El cierre «{ending.Name}» acaba en la casilla {ending.Stops[^1]} y no en el "
                    + "ganador: la ruleta pararía en un Pokémon distinto del que se anuncia.");
            }

            if (Math.Abs(ending.At[^1] - 1) > 0.0001)
            {
                throw new InvalidOperationException(
                    $"El cierre «{ending.Name}» acaba en el {ending.At[^1]:P0} de la tirada.");
            }

            for (var i = 1; i < ending.At.Length; i++)
            {
                if (ending.At[i] <= ending.At[i - 1])
                {
                    throw new InvalidOperationException(
                        $"El cierre «{ending.Name}» tiene los tiempos desordenados en la parada {i}.");
                }
            }
        }
    }

    /// <summary>When the marker should be punched: every stop after the first plant.</summary>

    public double[] Clicks => [.. At.Skip(1)];
}
