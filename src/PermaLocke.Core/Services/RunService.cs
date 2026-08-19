using System.Security.Cryptography;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Services;

public sealed record CreateRunRequest(
    string Name,
    string PlayerName,
    string RoleId,
    GameVersion Game,
    ulong? Seed = null,
    string? RomHash = null,
    string? TitleId = null);

/// <summary>Creates and loads runs. Creation is the only place a seed is decided.</summary>
public sealed class RunService(IRunRepository runs, IEventStore events, IRunContext context, IClock clock)
{
    /// <summary>The four islands of the Alola tour, in the order they are played.</summary>
    private static readonly (string Id, string Name)[] IslandOrder =
    [
        ("melemele", "Melemele"),
        ("akala", "Akala"),
        ("ulaula", "Ula-Ula"),
        ("poni", "Poni")
    ];

    public async Task<Run> CreateAsync(CreateRunRequest request, CancellationToken ct = default)
    {
        var seed = request.Seed ?? GenerateSeed();

        var run = new Run
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            PlayerName = request.PlayerName,
            RoleId = request.RoleId,
            Game = request.Game,
            Seed = seed,
            SeedLabel = FormatSeed(seed),
            CreatedAt = clock.Now,
            RomHash = request.RomHash,
            TitleId = request.TitleId,
            AppVersion = typeof(RunService).Assembly.GetName().Version?.ToString(),
            Islands = [.. IslandOrder.Select((island, index) => new Island(
                island.Id,
                island.Name,
                index == 0 ? IslandState.InProgress : IslandState.Locked))]
        };

        await runs.SaveAsync(run, ct).ConfigureAwait(false);

        await events.AppendAsync(new GameEvent
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            Timestamp = clock.Now,
            Type = GameEventType.RunCreated,
            Source = EventSource.System,
            Actor = run.PlayerName,
            Description = $"Run «{run.Name}» creada con seed {run.SeedLabel}.",
            Seed = seed,
            Data = new Dictionary<string, string>
            {
                ["game"] = run.Game.ToString(),
                ["role"] = run.RoleId,
                ["titleId"] = run.TitleId ?? string.Empty,
                ["romHash"] = run.RomHash ?? string.Empty
            }
        }, ct).ConfigureAwait(false);

        context.SetCurrent(run);
        return run;
    }

    /// <summary>Loads the most recently created run, if there is one. Used at startup.</summary>
    public async Task<Run?> LoadMostRecentAsync(CancellationToken ct = default)
    {
        var all = await runs.GetAllAsync(ct).ConfigureAwait(false);
        var latest = all.OrderByDescending(r => r.CreatedAt).FirstOrDefault();
        context.SetCurrent(latest);
        return latest;
    }

    /// <summary>Cryptographically random so two players never share a seed by accident.</summary>
    private static ulong GenerateSeed()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        return BitConverter.ToUInt64(buffer);
    }

    /// <summary>Human-facing label, e.g. PERMA-839421. The full seed is kept in run.json.</summary>
    public static string FormatSeed(ulong seed) => $"PERMA-{seed % 1_000_000:D6}";
}
