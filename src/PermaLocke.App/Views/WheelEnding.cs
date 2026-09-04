namespace PermaLocke.App.Views;

/// <summary>
/// One way the wheel can come to a stop: where it plants, and when each notch lands.
/// </summary>
/// <param name="Stops">
/// Wedge offsets from the winner, in the order they are reached. A positive number is a wedge
/// short of the winner; a <b>negative</b> one is past it, which is how a genuine false finish is
/// written. The last is always zero.
/// </param>
/// <param name="At">When each stop happens, as a fraction of the whole spin.</param>
/// <param name="Overshoot">Whether the final notch goes a touch past and springs back.</param>
/// <remarks>
/// The same idea as the gacha's <see cref="ReelEnding"/>, and for the same reason: one easing used
/// every time is an easing anybody learns. The difference is what a wedge offset means here. The
/// wheel is turned by an angle and each wedge is sixty degrees, so an offset of <c>k</c> is the
/// resting angle minus <c>k</c> times sixty — which for a positive <c>k</c> is short of the
/// winner and keeps the wheel moving forwards, and for a negative one is past it, so the wheel has
/// to come back. Coming back is not a cheat: a real wheel that overshoots gets pulled back by the
/// pawl.
/// </remarks>
public sealed record WheelEnding(string Name, int[] Stops, double[] At, bool Overshoot)
{
    /// <summary>How many degrees one wedge is. Six of them make the wheel.</summary>
    public const double WedgeDegrees = 60;

    /// <summary>
    /// Every closing this screen knows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There used to be none of this: twelve seconds of a single <c>PowerEase</c> to the answer,
    /// identical on every spin. It reads well once and then it reads like a progress bar.
    /// </para>
    /// <para>
    /// The rule they all obey is the gacha's: <b>none of them may correlate with what came out.</b>
    /// The plant distance, the number of notches and the bounce are all things a player would
    /// learn to read, so which one plays is drawn from its own stream — not from the face, not
    /// from whether it is a good one, not from the duration. Any of these can precede any result.
    /// </para>
    /// </remarks>
    public static readonly WheelEnding[] All =
    [
        // Se planta a tres cunas, tres golpes y el ultimo se pasa y vuelve.
        new("tres golpes", [3, 2, 1, 0], [0.70, 0.83, 0.92, 1.00], Overshoot: true),

        // Dos tirones largos y una entrada limpia: parece que se queda corta y no se queda.
        new("dos largos", [2, 1, 0], [0.68, 0.86, 1.00], Overshoot: false),

        // Cuatro cortos que se van muriendo. Llega antes y se arrastra mas.
        new("cuatro cortos", [4, 3, 2, 1, 0], [0.62, 0.75, 0.85, 0.93, 1.00], Overshoot: false),

        // Casi sin golpes: un arrastre larguisimo hasta la cuna de al lado y un solo empujon.
        new("arrastre", [1, 0], [0.76, 1.00], Overshoot: true),

        // La que de verdad engana: se va UNA CUNA MAS ALLA del ganador, se queda ahi el tiempo
        // justo para leerla, y la rueda retrocede. Es la unica que ensena otra cara bajo la marca
        // antes de dar la buena, y por eso mismo no puede depender del resultado.
        new("se pasa y vuelve", [3, 1, -1, 0], [0.66, 0.80, 0.91, 1.00], Overshoot: false),
    ];

    /// <summary>
    /// Picks one, from a stream of its own.
    /// </summary>
    /// <remarks>
    /// Seeded so the same spin always animates the same way and can be replayed, and salted so it
    /// shares nothing with the draw that chose the six faces or the one that chose the winner: two
    /// streams from one seed used for different things is exactly how §27 leaked one module into
    /// another.
    /// </remarks>
    public static WheelEnding For(ulong seed, int number) =>
        All[new Random(unchecked((int)(seed ^ ((ulong)number * 2654435761UL)) ^ 0x2A17))
            .Next(All.Length)];

    /// <summary>
    /// Checks every profile the moment the class is first used.
    /// </summary>
    /// <remarks>
    /// A profile whose last stop is not the winner parks the wheel on the <b>wrong wedge</b>, and
    /// the screen would still announce the right face — the result was decided, written to the
    /// save and recorded long before the animation. That is a lie the player would see and nothing
    /// would report, which already happened once on this screen for a different reason. So a
    /// malformed profile stops the screen instead of playing wrong.
    /// </remarks>
    static WheelEnding()
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
                    $"El cierre «{ending.Name}» acaba en la cuña {ending.Stops[^1]} y no en el "
                    + "ganador: la rueda pararía en una cara distinta de la que se anuncia.");
            }

            if (ending.Stops[0] <= 0)
            {
                throw new InvalidOperationException(
                    $"El cierre «{ending.Name}» se planta en {ending.Stops[0]}, que no está antes "
                    + "del ganador: la rueda tendría que retroceder nada más frenar.");
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

    /// <summary>
    /// Where the wheel has to be at each stop, given where it finally rests.
    /// </summary>
    /// <param name="resting">The absolute angle that puts the winning wedge under the marker.</param>
    public IEnumerable<double> Angles(double resting) =>
        Stops.Select(stop => resting - (stop * WedgeDegrees));

    /// <summary>How long each leg lasts, in order, out of a whole spin of <paramref name="total"/>.</summary>
    public IEnumerable<TimeSpan> Legs(TimeSpan total) =>
        At.Select((at, i) => TimeSpan.FromMilliseconds(
            (at - (i == 0 ? 0 : At[i - 1])) * total.TotalMilliseconds));
}
