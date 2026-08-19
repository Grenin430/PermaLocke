using System.Security.Cryptography;
using System.Text;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

/// <summary>
/// Chains events by hash so that an edited or deleted entry becomes detectable.
/// See docs/ARCHITECTURE.md §9 for the honest limits of what this protects against:
/// it catches casual tampering, not someone who rehashes the whole chain.
/// </summary>
public static class EventHasher
{
    /// <summary>Computes the hash of an event given the hash of the one before it.</summary>
    public static string Compute(GameEvent gameEvent, string previousHash)
    {
        var canonical = new StringBuilder()
            .Append(previousHash).Append('|')
            .Append(gameEvent.Id.ToString("N")).Append('|')
            .Append(gameEvent.RunId.ToString("N")).Append('|')
            .Append(gameEvent.Timestamp.ToUniversalTime().ToString("O")).Append('|')
            .Append((int)gameEvent.Type).Append('|')
            .Append((int)gameEvent.Source).Append('|')
            .Append(gameEvent.Actor).Append('|')
            .Append(gameEvent.Description).Append('|')
            .Append(gameEvent.PointsDelta).Append('|')
            .Append(gameEvent.PokemonId?.ToString("N") ?? string.Empty).Append('|')
            .Append(gameEvent.LocationId ?? string.Empty).Append('|')
            .Append(gameEvent.Seed?.ToString() ?? string.Empty).Append('|')
            .Append(gameEvent.Reason ?? string.Empty).Append('|');

        // Ordered so that dictionary enumeration order cannot change the hash.
        foreach (var pair in gameEvent.Data.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            canonical.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    /// <summary>Returns the event with <see cref="GameEvent.PreviousHash"/> and <see cref="GameEvent.Hash"/> filled in.</summary>
    public static GameEvent Seal(GameEvent gameEvent, string previousHash)
    {
        var withPrevious = gameEvent with { PreviousHash = previousHash };
        return withPrevious with { Hash = Compute(withPrevious, previousHash) };
    }
}
