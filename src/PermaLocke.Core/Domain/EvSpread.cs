namespace PermaLocke.Core.Domain;

/// <summary>
/// The six effort values of one Pokémon, kept inside what the game itself accepts.
/// </summary>
/// <remarks>
/// <para>
/// The two ceilings are not PermaLocke's idea of fair play, they are the format: generation 7
/// stores each EV in one byte and the game refuses to hand out more than <b>252 per stat</b> and
/// <b>510 in total</b>. Writing past either produces a Pokémon the cartridge treats as illegal and
/// PKHeX flags, so the type clamps instead of trusting the caller.
/// </para>
/// <para>
/// Immutable on purpose: <see cref="With"/> returns a new spread, so a screen can offer an edit,
/// show what it would become, and still have the original to go back to.
/// </para>
/// </remarks>
public sealed class EvSpread : IEquatable<EvSpread>
{
    /// <summary>Most the game will put into a single stat.</summary>
    public const int PerStatMax = 252;

    /// <summary>Most the game will spread across all six.</summary>
    public const int TotalMax = 510;

    public const int StatCount = 6;

    /// <summary>Four EVs are one point of stat at level 100; below that it takes more.</summary>
    public const int PerStatPoint = 4;

    private readonly int[] _values;

    private EvSpread(int[] values) => _values = values;

    /// <summary>Every stat at zero.</summary>
    public static EvSpread Empty { get; } = new([0, 0, 0, 0, 0, 0]);

    /// <summary>
    /// Reads a spread from whatever a save happens to hold, clamping it into legality.
    /// </summary>
    /// <remarks>
    /// A save edited elsewhere can legitimately arrive over the total, so this never throws: it
    /// takes the values in order and stops giving out budget once 510 is spent. That way the screen
    /// shows a legal spread and saving it back fixes the Pokémon rather than propagating the
    /// problem.
    /// </remarks>
    public static EvSpread Of(IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var result = new int[StatCount];
        var spent = 0;

        for (var index = 0; index < StatCount; index++)
        {
            var wanted = index < values.Count ? values[index] : 0;
            var allowed = Math.Min(Math.Clamp(wanted, 0, PerStatMax), TotalMax - spent);

            result[index] = allowed;
            spent += allowed;
        }

        return new EvSpread(result);
    }

    public IReadOnlyList<int> Values => _values;

    public int this[int index] => _values[index];

    public int Total => _values.Sum();

    /// <summary>How much budget is left to hand out.</summary>
    public int Remaining => TotalMax - Total;

    /// <summary>
    /// The same spread with one stat changed, clamped to what is actually available.
    /// </summary>
    /// <remarks>
    /// Clamped against the <em>other five</em> rather than against the current total, so raising a
    /// stat that already holds EVs does not count its own share twice.
    /// </remarks>
    public EvSpread With(int index, int value)
    {
        if (index < 0 || index >= StatCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var others = Total - _values[index];
        var ceiling = Math.Min(PerStatMax, TotalMax - others);

        var copy = (int[])_values.Clone();
        copy[index] = Math.Clamp(value, 0, ceiling);

        return new EvSpread(copy);
    }

    /// <summary>The most this stat could hold without taking budget from the others.</summary>
    public int CeilingFor(int index) =>
        Math.Min(PerStatMax, TotalMax - (Total - _values[index]));

    public bool Equals(EvSpread? other) =>
        other is not null && _values.AsSpan().SequenceEqual(other._values);

    public override bool Equals(object? obj) => Equals(obj as EvSpread);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var value in _values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join('/', _values);
}
