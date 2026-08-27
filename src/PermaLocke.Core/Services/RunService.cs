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

/// <param name="Events">How many events went with it.</param>
/// <param name="Pokemon">How many registered Pokémon went with it.</param>
public sealed record RunDeletion(bool Deleted, string Name, int Events, int Pokemon);

/// <summary>Creates and loads runs. Creation is the only place a seed is decided.</summary>
public sealed class RunService(IRunRepository runs, IEventStore events, IPokemonRepository pokemon,
    IRunContext context, IClock clock)
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

    /// <summary>
    /// Changes a role only through an explicit, auditable migration.
    /// </summary>
    /// <remarks>
    /// This method deliberately does not generate or install a ROM mod: that potentially lengthy
    /// and visible operation must have succeeded <em>before</em> the role moves. It then saves the
    /// new run and appends the matching event; should appending fail, it puts the previous run back
    /// rather than leaving a role change with no history behind it.
    /// </remarks>
    public async Task<Run> ChangeRoleAsync(Run run, string targetRoleId, string reason,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (string.IsNullOrWhiteSpace(targetRoleId))
        {
            throw new ArgumentException("El rol de destino es obligatorio.", nameof(targetRoleId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("El motivo de la migración es obligatorio.", nameof(reason));
        }

        var target = targetRoleId.Trim();

        if (string.Equals(run.RoleId, target, StringComparison.OrdinalIgnoreCase))
        {
            return run;
        }

        var updated = run with { RoleId = target };
        await runs.SaveAsync(updated, ct).ConfigureAwait(false);

        try
        {
            await events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = clock.Now,
                Type = GameEventType.RoleChanged,
                Source = EventSource.Player,
                Actor = run.PlayerName,
                Description = $"Rol cambiado de «{run.RoleId}» a «{target}». {reason}",
                Data = new Dictionary<string, string>
                {
                    ["desde"] = run.RoleId,
                    ["hasta"] = target,
                    ["motivo"] = reason
                }
            }, ct).ConfigureAwait(false);
        }
        catch
        {
            await runs.SaveAsync(run, ct).ConfigureAwait(false);
            throw;
        }

        context.SetCurrent(updated);
        return updated;
    }

    /// <summary>
    /// Throws a run away whole: its events, its Pokémon and its folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart of <see cref="CreateAsync"/>, and the only thing in PermaLocke that removes
    /// run data. It is not a correction and leaves no compensating event, because there would be
    /// nowhere to put one: the chain it would belong to is what is being deleted. Whoever calls
    /// this has to have asked the player first, in words that say the run is not coming back.
    /// </para>
    /// <para>
    /// Order matters. The rows go before the folder, so a failure half way leaves a run that still
    /// has its <c>run.json</c> and can be deleted again -- rather than a folder-less run whose
    /// events sit in the database with nothing naming them.
    /// </para>
    /// </remarks>
    public async Task<RunDeletion> DeleteAsync(Guid runId, CancellationToken ct = default)
    {
        if (await runs.GetAsync(runId, ct).ConfigureAwait(false) is not { } run)
        {
            return new RunDeletion(false, string.Empty, 0, 0);
        }

        var goneEvents = await events.DeleteRunAsync(runId, ct).ConfigureAwait(false);
        var gonePokemon = await pokemon.DeleteRunAsync(runId, ct).ConfigureAwait(false);

        await runs.DeleteAsync(runId, ct).ConfigureAwait(false);

        // Si era la run cargada, la aplicación se queda sin run en vez de apuntando a una que ya no
        // existe, que es como se llega a un fallo cinco pantallas más allá sin saber por qué.
        if (context.Current?.Id == runId)
        {
            context.SetCurrent(null);
        }

        return new RunDeletion(true, run.Name, goneEvents, gonePokemon);
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
