using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Data;
using PermaLocke.Infrastructure;
using PermaLocke.Rules.Services;

namespace PermaLocke.App.Services;

/// <summary>
/// The Pokévial's side in PermaLocke (2026-10-16). The bag takes a use and asks for the healing (<see cref="RuleBlock.PokeVialRequest"/>); this
/// answers: it heals every Pokémon of the party in the game's memory, HP, status and PP, except the run's fallen, and puts the request back. It
/// also keeps the uses of the run (in <c>Saves/pokevial-&lt;run&gt;.json</c>) and writes them where the bag reads them, because the game starts
/// with that byte at 0.
/// </summary>
/// <remarks>
/// <para>
/// The uses: one at the start, one more after the fourth trial and one more after the sixth (<see cref="PokeVial.MaxUses"/>); a trial that
/// raises the most also gives that use at once. Healing at a Pokémon Centre fills them again.
/// </para>
/// <para>
/// The Pokémon Centre (2026-10-16): the game's record 10, «Pokémon healed at Pokémon Centres», read live. The nurse raises it each time she
/// heals the party, full or not; a bed, a potion or the vial itself do not. Reading the party for a heal missed the nurse after a vial use (the
/// party was already full), and a zone learned as a Centre refilled the vial right after using it there.
/// </para>
/// </remarks>
public sealed class PokeVialService(
    AzaharGameStateProvider provider,
    AzaharGameWriter writer,
    GameLinkMonitor monitor,
    GameWatcher watcher,
    ProgressService progress,
    IMoveCatalog moves,
    BagService bag,
    IBattleCounters counters,
    IRunContext runContext,
    AppPaths paths,
    ILogger<PokeVialService> logger) : IDisposable
{
    private const int PartySlots = 6;
    private static readonly TimeSpan Look = TimeSpan.FromMilliseconds(400);

    private Timer? _timer;
    private int _busy;
    private State? _state;
    private Guid _stateRun;
    private int? _centreHeals;

    private sealed record State(int Uses, int Max);

    /// <summary>The uses left and the most it holds, and whether the bag has it: what the overlay shows (2026-10-16).</summary>
    public (int Uses, int Max, bool Owned) Now => (_state?.Uses ?? 0, _state?.Max ?? 0, _owned);

    /// <summary>Something of it changed (uses, most, owned). Off the UI thread.</summary>
    public event Action? Changed;

    /// <summary>It was used: the uses before and after. Off the UI thread.</summary>
    public event Action<int, int>? Used;

    private bool _owned;
    private DateTime _bagAt = DateTime.MinValue;

    public void Start()
    {
        _timer ??= new Timer(_ => _ = TickAsync(), null, Look, Look);
    }

    private async Task TickAsync()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            if (runContext.Current is not { } run || monitor.Latest is not { Connected: true }) return;
            var state = await StateAsync(run);
            if (writer.ReadVial() is not { } vial) return;

            // Whether the bag has it, every few seconds: the overlay shows the flask only then.
            if (DateTime.UtcNow - _bagAt > TimeSpan.FromSeconds(4))
            {
                _bagAt = DateTime.UtcNow;
                var owned = bag.Carried(PokeVial.ItemId) > 0;
                if (owned != _owned) { _owned = owned; Changed?.Invoke(); }
            }

            if (vial.Asked)
            {
                // The bag has already taken the use: what it has now is what is left.
                var before = state.Uses;
                Save(run, state = state with { Uses = Math.Min(vial.Uses, state.Max) });
                Used?.Invoke(before, state.Uses);
                await HealAsync(run);
                writer.ClearVialRequest();
                return;
            }

            // The nurse: her record went up since the last reading.
            if (counters.Read()?.CentreHeals is { } heals)
            {
                if (heals > _centreHeals && state.Uses < state.Max)
                {
                    Save(run, state = state with { Uses = state.Max });
                    logger.LogInformation("Pokévial recargado en el Centro Pokémon ({Uses} usos)", state.Max);
                }

                _centreHeals = heals;
            }

            if (vial.Uses != state.Uses) writer.WriteRuleVialUses((byte)state.Uses);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogDebug(ex, "Pokévial: no se ha podido mirar la petición");
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    /// <summary>Every Pokémon of the party, in every copy the game reads, healed; the fallen left as they are.</summary>
    private async Task HealAsync(Run run)
    {
        var fallen = await watcher.FallenPidsAsync(run.Id);
        var party = monitor.Latest?.Party ?? [];
        var healed = 0;
        foreach (var member in party.Where(m => !m.IsEgg && !fallen.Contains(m.Pid)))
        {
            foreach (var layout in provider.AllLayouts.Where(l => l.Stride == PartyLayoutLocator.AuthoritativeStride))
            {
                for (var slot = 0; slot < PartySlots; slot++)
                {
                    var at = layout.SlotAddress(slot);
                    if (writer.ReadAuthoritative(at) is not { ChecksumValid: true } found || found.PID != member.Pid) continue;
                    if (writer.HealLive(at, member.Pid, move => moves.Describe(move)?.PP).Applied) healed++;
                }
            }
        }

        logger.LogInformation("Pokévial usado: {Healed} copias curadas, {Fallen} caídos sin tocar, quedan {Uses} usos", healed,
            party.Count(m => fallen.Contains(m.Pid)), _state?.Uses);
    }

    /// <summary>The run's uses, with the trials cleared since: a trial that raises the most gives its use at once.</summary>
    private async Task<State> StateAsync(Run run)
    {
        if (_state is null || _stateRun != run.Id)
        {
            _stateRun = run.Id;
            _state = Load(run) ?? new State(PokeVial.MaxUses(0), PokeVial.MaxUses(0));
        }

        var max = PokeVial.MaxUses(await progress.ClearedAsync(run));
        if (max > _state.Max)
        {
            Save(run, _state with { Uses = _state.Uses + (max - _state.Max), Max = max });
            logger.LogInformation("Pokévial: {Max} usos como máximo desde ahora", max);
        }

        return _state;
    }

    private string FileOf(Run run) => Path.Combine(paths.Saves, $"pokevial-{run.Id}.json");

    private State? Load(Run run)
    {
        try
        {
            return File.Exists(FileOf(run)) ? JsonSerializer.Deserialize<State>(File.ReadAllText(FileOf(run))) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning(ex, "Pokévial: no se han podido leer los usos de la run");
            return null;
        }
    }

    private void Save(Run run, State state)
    {
        _state = state;
        Changed?.Invoke();
        try
        {
            Directory.CreateDirectory(paths.Saves);
            File.WriteAllText(FileOf(run), JsonSerializer.Serialize(state));
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Pokévial: no se han podido guardar los usos de la run");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
