using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>Walks a run's events and checks that each one is sealed against the one before.</summary>
/// <remarks>
/// It lived inside the SQLite store and moved here when a chain started arriving from somebody
/// else's machine as a file (§123): the check has to be the same one whether the events came from
/// the local database or from a friend's folder, and two copies of it would eventually disagree.
/// </remarks>
public static class EventChain
{
    public static IntegrityReport Verify(IReadOnlyList<GameEvent> events)
    {
        var expectedPrevious = string.Empty;
        var checkedCount = 0;

        foreach (var storedEvent in events)
        {
            checkedCount++;

            if (storedEvent.PreviousHash != expectedPrevious)
            {
                return new IntegrityReport(false, checkedCount, storedEvent.Id,
                    "El encadenado se rompe: falta un evento anterior o fue alterado.");
            }

            if (EventHasher.Compute(storedEvent, expectedPrevious) != storedEvent.Hash)
            {
                return new IntegrityReport(false, checkedCount, storedEvent.Id,
                    "El contenido del evento no coincide con su hash: fue modificado.");
            }

            expectedPrevious = storedEvent.Hash;
        }

        return new IntegrityReport(true, checkedCount, null, null);
    }
}
