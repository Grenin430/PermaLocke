using System.Buffers.Binary;
using System.Globalization;

namespace PermaLocke.GameLink.Rpc;

/// <param name="At">When the request went out.</param>
/// <param name="Type">What was asked.</param>
/// <param name="First">The first word of the request: the address, for a memory request.</param>
/// <param name="Second">The second word: the size, for a memory request.</param>
/// <param name="Answered">Whether a reply came back.</param>
/// <param name="Milliseconds">How long it took, retries included.</param>
public readonly record struct RpcTraceEntry(DateTimeOffset At, RpcRequestType Type, uint First, uint Second,
    bool Answered, double Milliseconds);

/// <summary>
/// The last requests sent to the emulator, kept so that a crash report can say what PermaLocke was doing at the
/// moment Azahar went down.
/// </summary>
/// <remarks>
/// Both crashes of the emulator that were ever explained were explained by what PermaLocke was reading at the time: a
/// search of memory that did not exist yet during the intro (§159), and a burst of reads during a loading screen. They
/// were found from the application's log, by luck, because those requests happened to be logged. This keeps the last
/// ones always, whether or not anything logged them, and costs one small struct per request (§168).
/// </remarks>
public sealed class RpcTrace(int capacity = 128)
{
    private readonly RpcTraceEntry[] _entries = new RpcTraceEntry[capacity];
    private readonly object _gate = new();
    private int _next;
    private int _count;

    public void Record(RpcRequestType type, ReadOnlySpan<byte> payload, bool answered, double milliseconds)
    {
        var first = payload.Length >= 4 ? BinaryPrimitives.ReadUInt32LittleEndian(payload) : 0;
        var second = payload.Length >= 8 ? BinaryPrimitives.ReadUInt32LittleEndian(payload[4..]) : 0;

        lock (_gate)
        {
            _entries[_next] = new RpcTraceEntry(DateTimeOffset.Now, type, first, second, answered, milliseconds);
            _next = (_next + 1) % _entries.Length;
            _count = Math.Min(_count + 1, _entries.Length);
        }
    }

    /// <summary>The kept requests, oldest first.</summary>
    public IReadOnlyList<RpcTraceEntry> Snapshot()
    {
        lock (_gate)
        {
            var start = (_next - _count + _entries.Length) % _entries.Length;
            return [.. Enumerable.Range(0, _count).Select(i => _entries[(start + i) % _entries.Length])];
        }
    }

    /// <summary>One line per request, for a person to read.</summary>
    public IReadOnlyList<string> Describe() =>
    [
        .. Snapshot().Select(entry => string.Create(CultureInfo.InvariantCulture,
            $"{entry.At:HH:mm:ss.fff}  {entry.Type,-14} 0x{entry.First:X8}  {entry.Second,10}  " +
            $"{(entry.Answered ? "respondió" : "SIN RESPUESTA"),-13} {entry.Milliseconds,7:F1} ms"))
    ];
}
