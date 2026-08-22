namespace PermaLocke.Core.Domain;

/// <summary>
/// The six effort values of one Pokémon.
/// </summary>
/// <remarks>
/// <para>
/// The two ceilings are not PermaLocke's idea of fair play, they are the format: generation 7
/// stores each EV in one byte and the game refuses to hand out more than <b>252 per stat</b> and
/// <b>510 in total</b>. Writing past either produces a Pokémon the cartridge treats as illegal.
/// </para>
/// <para>
/// The two are enforced very differently, and on purpose. <b>252 per stat is clamped</b>, because
/// there is never a reason to want more and clamping is instant and obvious. <b>510 in total is
/// only checked</b>, because clamping it would decide the order in which somebody has to work:
/// with 252 in HP and 252 in Attack there is no budget left, so typing 252 into Speed would come
/// back as 6 and the player would have to remember to empty HP <em>first</em>. Instead the spread
/// is allowed over the line while it is being edited, says so through <see cref="Over"/>, and
/// whoever writes refuses until it is legal again.
/// </para>
/// <para>
/// Immutable: <see cref="With"/> returns a new spread, so a screen can offer an edit, show what it
/// would become, and still have the original to go back to.
/// </para>
/// </remarks>
public sealed class EvSpread : IEquatable<EvSpread>
{
    /// <summary>Most the game will put into a single stat. Clamped.</summary>
    public const int PerStatMax = 252;

    /// <summary>Most the game will spread across all six. Checked, not clamped.</summary>
    public const int TotalMax = 510;

    public const int StatCount = 6;

    /// <summary>Four EVs are one point of stat at level 100; below that it takes more.</summary>
    public const int PerStatPoint = 4;

    private readonly int[] _values;

    private EvSpread(int[] values) => _values = values;

    /// <summary>Every stat at zero.</summary>
    public static EvSpread Empty { get; } = new([0, 0, 0, 0, 0, 0]);

    /// <summary>
    /// Takes six values from wherever they came from — a save, or somebody typing — clamping each
    /// to 0..252 and leaving the total alone.
    /// </summary>
    /// <remarks>
    /// Never throws and never silently rebalances. A save edited elsewhere can arrive over 510, and
    /// the honest thing is to show what is really there and refuse to write it back until somebody
    /// fixes it, rather than quietly deciding which stats to rob.
    /// </remarks>
    public static EvSpread Of(IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var result = new int[StatCount];

        for (var index = 0; index < StatCount; index++)
        {
            var wanted = index < values.Count ? values[index] : 0;
            result[index] = Math.Clamp(wanted, 0, PerStatMax);
        }

        return new EvSpread(result);
    }

    public IReadOnlyList<int> Values => _values;

    public int this[int index] => _values[index];

    public int Total => _values.Sum();

    /// <summary>How much budget is left. Negative once the spread is over the line.</summary>
    public int Remaining => TotalMax - Total;

    /// <summary>How far past 510 this spread is, or zero when it is within it.</summary>
    public int Over => Math.Max(0, Total - TotalMax);

    /// <summary>True when the game would accept this spread.</summary>
    public bool IsLegal => Total <= TotalMax;

    /// <summary>
    /// The same spread with one stat changed, clamped to 0..252 and nothing else.
    /// </summary>
    /// <remarks>
    /// Deliberately allowed to push the total past 510: see the class remarks. Moving 252 points
    /// from one stat to another is two edits, and forcing them into a particular order is the kind
    /// of thing that makes an editor annoying to use.
    /// </remarks>
    public EvSpread With(int index, int value)
    {
        if (index < 0 || index >= StatCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var copy = (int[])_values.Clone();
        copy[index] = Math.Clamp(value, 0, PerStatMax);

        return new EvSpread(copy);
    }

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
