using System.IO;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Field;
using PermaLocke.GameLink.Rpc;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>
/// The Repelente Infinito and the Incubadora Turbo as the game has them now, for the overlay (2026-10-16): whether the bag holds each and whether
/// each is on.
/// </summary>
/// <remarks>
/// <para>
/// The Repel lives in <c>Field::EventWork</c> (see <c>RulePatches.InfiniteRepel</c>): the item that set it at +0xA3E and the steps left at +0xA58.
/// Measured with the Probe (--repelente, 2026-10-16): with it on, item 114 and one step at the save data's +0x2016 and +0x2030, so the object starts
/// at +0x15D8, four bytes before where the save's EventWork block was found (+0x15DC). On: set by this item, with steps. Any other item there than
/// a Repel or nothing means it is not where it is believed to be, and it reads as off.
/// </para>
/// <para>
/// The Incubadora Turbo is a byte the game itself writes in the rule block (<see cref="RuleBlock.EggTurbo"/>), 1 while it is on. The save does
/// not keep it: a game started again had it off (2026-10-16). So it is kept for the run here (<c>Saves/turbo-&lt;run&gt;.txt</c>) and, in a game
/// just started (<see cref="RuleBlock.EggTurboKept"/> at 0), put back. Its ceiling: it follows what the bag did, not what was saved, so turning
/// it on and leaving without saving still finds it on.
/// </para>
/// </remarks>
public sealed class FieldItemsService(AzaharRpcClient client, LiveSave live, BagService bag, GameLinkMonitor monitor, IRunContext runContext,
    AppPaths paths, ILogger<FieldItemsService> logger)
    : IDisposable
{
    private const uint RepelItemInSave = 0x2016, RepelStepsInSave = 0x2030;
    private static readonly int[] Repels = [0, 76, 77, 79, InfiniteRepel.ItemId];
    private static readonly TimeSpan Look = TimeSpan.FromMilliseconds(400), BagEvery = TimeSpan.FromSeconds(4);

    private Timer? _timer;
    private int _busy;
    private DateTime _bagAt = DateTime.MinValue;
    private bool _warned;

    /// <summary>What the overlay shows.</summary>
    public (bool RepelOwned, bool RepelOn, bool TurboOwned, bool TurboOn) Now { get; private set; }

    /// <summary>Something of it changed. Off the UI thread.</summary>
    public event Action? Changed;

    public void Start() => _timer ??= new Timer(_ => Tick(), null, Look, Look);

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            if (monitor.Latest is not { Connected: true }) return;
            var now = Now;

            if (DateTime.UtcNow - _bagAt > BagEvery)
            {
                _bagAt = DateTime.UtcNow;
                now = now with { RepelOwned = bag.Carried(InfiniteRepel.ItemId) > 0, TurboOwned = bag.Carried(EggTurbo.ItemId) > 0 };
            }

            KeepTurbo();
            now = now with { RepelOn = now.RepelOwned && RepelOn(), TurboOn = now.TurboOwned && TurboOn() };
            if (now == Now) return;
            Now = now;
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogDebug(ex, "Objetos de campo: no se ha podido leer el repelente o la incubadora");
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private bool RepelOn()
    {
        if (live.Base() is not { } save || !client.TryReadMemory(save + RepelItemInSave, 2, out var item)
            || !client.TryReadMemory(save + RepelStepsInSave, 2, out var steps))
        {
            return Now.RepelOn;
        }

        var setBy = BitConverter.ToUInt16(item);
        if (!Repels.Contains(setBy))
        {
            if (!_warned) logger.LogWarning("Repelente Infinito: en su sitio de EventWork hay el objeto {Item}, no un repelente; se da por apagado", setBy);
            _warned = true;
            return false;
        }

        return setBy == InfiniteRepel.ItemId && BitConverter.ToUInt16(steps) > 0;
    }

    private bool TurboOn() => client.TryReadMemory(RuleBlock.EggTurbo, 1, out var on) ? on[0] == 1 : Now.TurboOn;

    /// <summary>The run's Incubadora Turbo: put back into a game just started, and remembered when the bag turns it on or off.</summary>
    private void KeepTurbo()
    {
        if (runContext.Current is not { } run || !client.TryReadMemory(RuleBlock.EggTurbo, 4, out var block)) return;
        var file = Path.Combine(paths.Saves, $"turbo-{run.Id}.txt");
        var kept = File.Exists(file) && File.ReadAllText(file).Trim() == "1";
        var turbo = block[0] == 1;
        var marked = block[RuleBlock.EggTurboKeptOffset - RuleBlock.EggTurboOffset] == 1;

        if (!marked)
        {
            if (kept && !turbo)
            {
                client.WriteMemory(RuleBlock.EggTurbo, [1]);
                logger.LogInformation("Incubadora Turbo: el juego ha arrancado sin ella; vuelve a estar encendida como en la run");
            }

            client.WriteMemory(RuleBlock.EggTurboKept, [1]);
            return;
        }

        if (turbo == kept) return;
        try
        {
            Directory.CreateDirectory(paths.Saves);
            File.WriteAllText(file, turbo ? "1" : "0");
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Incubadora Turbo: no se ha podido guardar si está encendida");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
