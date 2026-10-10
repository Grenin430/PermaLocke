using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace PermaLocke.GameLink.Rpc;

/// <summary>Reads several small pieces of the emulated memory in one go; the thing the fast reading is written against, so that tests can fake it.</summary>
public interface IGameMemory
{
    /// <summary>
    /// Each range as it is now, or null where the emulator did not answer it (unmapped, lost, or the emulator is gone). The array always has as many
    /// entries as there were ranges.
    /// </summary>
    byte[]?[] ReadMany(IReadOnlyList<(uint Address, int Size)> ranges);
}

/// <summary>
/// Reads the memory of the game for the fast path (2026-10-11): <b>all the requests are sent first and the answers collected after</b>, so that six
/// reads cost one wait instead of six. It has a socket of its own, apart from <see cref="AzaharRpcClient"/>'s, whose one-conversation-at-a-time lock
/// the slow readers (the party every second, the bag, the shop) keep busy: the fast path never queues behind them and they never queue behind it.
/// The emulator answers in the order it gets them; which process is read is the one the main client attached to, the emulator keeps that for everyone.
/// </summary>
/// <remarks>
/// A late answer is thrown away by its number, never taken for another's (the lesson of the main client, §168). A request that is not answered inside
/// the timeout comes back as null, and the caller decides what that means; this class never throws for a lost datagram. Not for several threads at
/// once: the fast reader is one thread.
/// </remarks>
public sealed class AzaharBatchReader : IGameMemory, IDisposable
{
    private const int HeaderSize = 16;
    private const int MaxPayload = 1024;

    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    private readonly EndPoint _server;
    private readonly byte[] _buffer = new byte[2048];
    private uint _next = (uint)Random.Shared.Next(1 << 20, 1 << 28);

    /// <param name="timeoutMilliseconds">How long to wait for the answers of a batch: short, because a fast read that is not answered at once is better dropped.</param>
    public AzaharBatchReader(int timeoutMilliseconds = 100, string host = "127.0.0.1", int port = AzaharRpcClient.DefaultPort)
    {
        _server = new IPEndPoint(IPAddress.Parse(host), port);
        _socket.ReceiveTimeout = timeoutMilliseconds;
        _socket.SendTimeout = timeoutMilliseconds;
    }

    /// <summary>How many batches were answered in part or not at all: the health of the fast link, for the log.</summary>
    public int Incomplete { get; private set; }

    public byte[]?[] ReadMany(IReadOnlyList<(uint Address, int Size)> ranges)
    {
        var result = new byte[ranges.Count][];
        if (ranges.Count == 0) return result;

        var first = _next;
        try
        {
            for (var i = 0; i < ranges.Count; i++)
            {
                var (address, size) = ranges[i];
                ArgumentOutOfRangeException.ThrowIfGreaterThan(size, MaxPayload);

                var request = new byte[HeaderSize + 8];
                BinaryPrimitives.WriteUInt32LittleEndian(request, 1);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), _next++);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(8), (uint)RpcRequestType.ReadMemory);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(12), 8);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(16), address);
                BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(20), (uint)size);
                _socket.SendTo(request, _server);
            }

            var pending = ranges.Count;
            var from = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
            while (pending > 0)
            {
                int n;
                try
                {
                    n = _socket.ReceiveFrom(_buffer, ref from);
                }
                catch (SocketException)
                {
                    break;
                }

                if (n < HeaderSize) continue;
                var index = unchecked((int)(BinaryPrimitives.ReadUInt32LittleEndian(_buffer.AsSpan(4)) - first));
                if (index < 0 || index >= ranges.Count || result[index] is not null) continue;

                var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(_buffer.AsSpan(12));
                pending--;

                // An empty answer is memory that is not there: the same as no answer, for whoever reads it.
                result[index] = size > 0 && size == n - HeaderSize ? _buffer.AsSpan(HeaderSize, size).ToArray() : [];
            }

            if (pending > 0) Incomplete++;
        }
        catch (SocketException)
        {
            Incomplete++;
        }

        for (var i = 0; i < result.Length; i++)
        {
            if (result[i] is { Length: 0 }) result[i] = null!;
        }

        return result!;
    }

    public void Dispose() => _socket.Dispose();
}
