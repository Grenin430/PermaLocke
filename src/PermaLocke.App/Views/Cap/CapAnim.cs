namespace PermaLocke.App.Views;

/// <summary>
/// What the panel remembers between frames so that what changes is seen changing (2026-10-10): the HP that drop and rise, the level
/// that goes up, the one that falls. Pure: it is told the data and the time, never reads a clock, and everything it answers is a
/// function of the two, so a frame is the same wherever and whenever it is asked for.
/// </summary>
public sealed class CapAnim
{
    /// <summary>The HP bar fraction to show for a niche at a moment, or null to show the real one.</summary>
    public double? HpFraction(int index, double t) => null;

    /// <summary>Tells it what the panel says now, at a moment: it notices what changed since the last time.</summary>
    public void Update(CapPlaqueData data, double now)
    {
    }
}
