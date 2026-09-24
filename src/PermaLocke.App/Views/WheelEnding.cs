namespace PermaLocke.App.Views;

/// <summary>
/// One way the wheel can come to a stop: how hard it brakes, how far it travels, and how much it
/// settles back at the very end.
/// </summary>
/// <param name="Power">
/// Exponent of the deceleration. Higher brakes earlier and crawls longer, which is where the
/// interest is — seeing the wedges arrive one at a time and being able to read them.
/// </param>
/// <param name="ExtraTurns">Whole turns added to the base sweep, so the distance is not always the same.</param>
/// <param name="Bounce">
/// Degrees the wheel carries past the winner before settling back onto it, or zero. <b>Always less
/// than half a wedge</b>, so the marker never leaves the winning wedge: see <see cref="MaxBounce"/>.
/// </param>
/// <remarks>
/// <para>
/// This used to be a list of <b>stops</b>: the wheel planted itself two or three wedges short of
/// the winner and then advanced a wedge at a time, with a profile that even went one wedge <i>past</i>
/// and came back. It was built to keep the ending from being learnable, and it worked, but the
/// first real spin showed what it costs: the wheel came to rest in the middle of «IV AL MÁXIMO»,
/// sat there long enough to read it, and then moved on to the next wedge. The player's verdict was
/// «en el que pare, paró», and they are right — a wheel that stops on an answer and then changes
/// it is not building tension, it is lying about the result, and the result was already written to
/// the save before the wheel started turning.
/// </para>
/// <para>
/// So the variety moved from <i>where</i> it stops to <i>how</i> it brakes. Every profile is a
/// single monotone sweep onto the winner: the wedge under the marker is never anything but the
/// winner once the wheel is slow enough to read. The only motion after the sweep is the settle,
/// and that is bounded so tightly it cannot show a neighbour.
/// </para>
/// </remarks>
public sealed record WheelEnding(string Name, double Power, int ExtraTurns, double Bounce)
{
    /// <summary>How many degrees one wedge is. Six of them make the wheel.</summary>
    public const double WedgeDegrees = 60;

    /// <summary>
    /// The most the wheel may carry past the winner.
    /// </summary>
    /// <remarks>
    /// Half a wedge is thirty degrees, which is the exact point where the neighbour arrives under
    /// the marker. Twenty leaves ten degrees of daylight, so a settle reads as weight and never as
    /// a second answer. This is the number the whole file exists to respect.
    /// </remarks>
    public const double MaxBounce = 20;

    /// <summary>
    /// Every closing this screen knows.
    /// </summary>
    /// <remarks>
    /// The rule they all obey, the same the gacha's old reel obeyed: <b>none of them
    /// may correlate with what came out.</b> The brake, the distance and the settle are all things
    /// a player would learn to read, so which one plays is drawn from its own stream — not from the
    /// face, not from whether it is a good one. Any of these can precede any result.
    /// </remarks>
    public static readonly WheelEnding[] All =
    [
        // La de siempre: frena pronto y se arrastra, con un asentamiento corto al final.
        new("larga", Power: 5, ExtraTurns: 0, Bounce: 8),

        // Frena mas tarde y de golpe. Se lee menos por el camino y llega antes.
        new("seca", Power: 3, ExtraTurns: 1, Bounce: 0),

        // La mas larga de todas, arrastrandose casi hasta pararse.
        new("agonica", Power: 7, ExtraTurns: 0, Bounce: 5),

        // Da una vuelta de mas y se planta con un rebote claro.
        new("vuelta de mas", Power: 4, ExtraTurns: 2, Bounce: 14),

        // Sin rebote y con freno medio: entra limpia, como si alguien la parase con la mano.
        new("limpia", Power: 6, ExtraTurns: 1, Bounce: 0),
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
    /// The one that matters is the bounce. A profile allowed to carry the wheel half a wedge past
    /// the winner would park a <b>different face</b> under the marker while the card announced the
    /// right one — the result was decided, written to the save and recorded long before the
    /// animation — and nothing would report it. That is the exact lie this screen was just told to
    /// stop telling, so a malformed profile stops the screen instead of playing wrong.
    /// </remarks>
    static WheelEnding()
    {
        foreach (var ending in All)
        {
            if (ending.Bounce < 0 || ending.Bounce > MaxBounce)
            {
                throw new InvalidOperationException(
                    $"El cierre «{ending.Name}» se pasa {ending.Bounce}° del ganador y el máximo "
                    + $"es {MaxBounce}°: a partir de {WedgeDegrees / 2}° la marca ya está sobre "
                    + "otra cuña.");
            }

            if (ending.Power < 1 || ending.Power > 12)
            {
                throw new InvalidOperationException(
                    $"El cierre «{ending.Name}» frena con potencia {ending.Power}.");
            }

            if (ending.ExtraTurns < 0 || ending.ExtraTurns > 4)
            {
                throw new InvalidOperationException(
                    $"El cierre «{ending.Name}» añade {ending.ExtraTurns} vueltas.");
            }
        }
    }
}
