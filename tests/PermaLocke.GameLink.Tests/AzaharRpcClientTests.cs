using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// How the RPC client behaves when the link misbehaves, which over UDP it does.
/// </summary>
/// <remarks>
/// Against a fake server on localhost, not against Azahar: the point is to reproduce on demand the
/// two things that killed the link mid-session — a lost datagram and a late reply — and neither can
/// be asked of a real emulator.
/// </remarks>
public sealed class AzaharRpcClientTests : IDisposable
{
    private const int HeaderSize = 16;

    private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stopping = new();
    private Task? _serving;

    private int Port => ((IPEndPoint)_server.Client.LocalEndPoint!).Port;

    public void Dispose()
    {
        _stopping.Cancel();
        _server.Dispose();
        _stopping.Dispose();
    }

    /// <summary>
    /// Answers requests the way Azahar does, and misbehaves to order.
    /// </summary>
    /// <param name="ignoreFirst">Datagrams swallowed before answering anything.</param>
    /// <param name="stalePrefix">Replies with somebody else's id sent before each real one.</param>
    private void Serve(int ignoreFirst = 0, int stalePrefix = 0)
    {
        _serving = Task.Run(() =>
        {
            var seen = 0;

            while (!_stopping.IsCancellationRequested)
            {
                IPEndPoint? from = null;
                byte[] request;

                try
                {
                    request = _server.Receive(ref from);
                }
                catch (Exception)
                {
                    return;
                }

                if (seen++ < ignoreFirst)
                {
                    continue;
                }

                var id = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(4));
                var type = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(8));

                // Lo que dejaba el socket desincronizado: la respuesta de otra petición, que llega
                // tarde y se queda en la cola.
                for (var i = 0; i < stalePrefix; i++)
                {
                    _server.Send(Reply(id + 0xABCD + (uint)i, type), HeaderSize + 4, from);
                }

                _server.Send(Reply(id, type), HeaderSize + 4, from);
            }
        });
    }

    private static byte[] Reply(uint id, uint type)
    {
        var reply = new byte[HeaderSize + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(reply, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(4), id);
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(8), type);
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(12), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(HeaderSize), 0xDEADBEEF);
        return reply;
    }

    private AzaharRpcClient Client(int timeout = 300) =>
        new("127.0.0.1", Port, timeout);

    [Fact]
    public void A_normal_exchange_comes_back_with_its_payload()
    {
        Serve();
        using var client = Client();

        Assert.Equal(4, client.ReadMemory(0x30000000, 4).Length);
        Assert.Equal(0, client.Retries);
        Assert.Equal(0, client.Discarded);
    }

    /// <summary>
    /// One lost datagram used to be reported to the player as "Azahar no responde". It is a
    /// hiccup, not a disconnection, and asking again costs one packet.
    /// </summary>
    [Fact]
    public void A_lost_datagram_is_asked_for_again_instead_of_failing()
    {
        Serve(ignoreFirst: 1);
        using var client = Client();

        Assert.Equal(4, client.ReadMemory(0x30000000, 4).Length);
        Assert.True(client.Retries > 0, "no llegó a reintentar");
    }

    /// <summary>
    /// The one that killed the session. A reply that arrives after its request timed out stays
    /// queued; reading it as the current answer leaves the socket one behind <b>for ever</b>, so a
    /// single hiccup turned into a dead link until the application was restarted.
    /// </summary>
    [Fact]
    public void A_late_reply_is_discarded_and_the_link_keeps_working()
    {
        Serve(stalePrefix: 1);
        using var client = Client();

        // Varias veces seguidas: si el socket se quedara desincronizado, la segunda ya fallaría.
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(4, client.ReadMemory(0x30000000, 4).Length);
        }

        Assert.Equal(5, client.Discarded);
    }

    /// <summary>
    /// The client is a singleton and the poller asks every second while screens ask from their own
    /// threads. Overlapping requests over one socket read each other's answers.
    /// </summary>
    [Fact]
    public void Callers_from_several_threads_each_get_their_own_answer()
    {
        Serve();
        using var client = Client();

        var failures = 0;

        Parallel.For(0, 40, _ =>
        {
            try
            {
                if (client.ReadMemory(0x30000000, 4).Length != 4)
                {
                    Interlocked.Increment(ref failures);
                }
            }
            catch (Exception)
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.Equal(0, failures);
    }

    [Fact]
    public void A_server_that_never_answers_gives_up_and_says_so()
    {
        // Sin Serve(): nadie contesta.
        using var client = Client(timeout: 120);

        var problem = Assert.Throws<AzaharRpcException>(() => client.ReadMemory(0x30000000, 4));

        Assert.Contains("intentos", problem.Message);
        Assert.False(client.TryPing(out _));
    }
}
