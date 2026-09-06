using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Reading back what the emulator noted down about who writes where.
/// </summary>
/// <remarks>
/// <para>
/// Against a fake server, because the real one is patch 3 of the fork and does not exist yet. What
/// is worth pinning here is the parsing, and above all <b>the truncation</b>: a reply says how many
/// entries it carries and how many are still waiting, and the client has to believe the bytes it
/// actually received rather than the count in the header.
/// </para>
/// <para>
/// That is not a hypothetical. The whole of §98 was derailed for hours because
/// <c>SearchMemory</c> truncates at 255 hits per call — it says so in its own summary — and the
/// totals came back as exactly 510 and then exactly 765, two and three times the cap, and were read
/// as measurements. A protocol that can drop things has to say so out loud, and the client has to
/// be tested against a server that drops them.
/// </para>
/// </remarks>
public sealed class WriteLogTests : IDisposable
{
    private const int HeaderSize = 16;

    private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stopping = new();

    private int Port => ((IPEndPoint)_server.Client.LocalEndPoint!).Port;

    public void Dispose()
    {
        _stopping.Cancel();
        _server.Dispose();
        _stopping.Dispose();
    }

    /// <param name="claimed">What the reply's header says it carries.</param>
    /// <param name="sent">How many entries it really puts on the wire.</param>
    private void Serve(uint claimed, int sent, uint remaining, uint accept = 1)
    {
        _ = Task.Run(() =>
        {
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

                var id = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(4));
                var type = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(8));

                var body = type == (uint)RpcRequestType.WatchWrites
                    ? Watched(accept)
                    : Log(claimed, sent, remaining);

                var reply = new byte[HeaderSize + body.Length];

                BinaryPrimitives.WriteUInt32LittleEndian(reply, 1);
                BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(4), id);
                BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(8), type);
                BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(12), (uint)body.Length);
                body.CopyTo(reply.AsSpan(HeaderSize));

                _server.Send(reply, reply.Length, from);
            }
        });
    }

    private static byte[] Watched(uint accept)
    {
        var body = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(body, accept);
        return body;
    }

    private static byte[] Log(uint claimed, int sent, uint remaining)
    {
        var body = new byte[(sizeof(uint) * 2) + (sent * AzaharRpcClient.WriteLogEntrySize)];

        BinaryPrimitives.WriteUInt32LittleEndian(body, claimed);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(4), remaining);

        for (var i = 0; i < sent; i++)
        {
            var at = (sizeof(uint) * 2) + (i * AzaharRpcClient.WriteLogEntrySize);

            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(at), 0x00123400u + (uint)i);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(at + 4), 0x330129D4u);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(at + 8), 2u);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(at + 12), 0xA6EFu);

            for (var r = 0; r < AzaharRpcClient.WriteLogRegisters; r++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(
                    body.AsSpan(at + (sizeof(uint) * 4) + (r * sizeof(uint))),
                    0x30000000u + ((uint)r * 0x100));
            }
        }

        return body;
    }

    private AzaharRpcClient Client() => new("127.0.0.1", Port, 300);

    [Fact]
    public void An_entry_comes_back_with_its_pc_and_its_sixteen_registers()
    {
        Serve(claimed: 2, sent: 2, remaining: 0);
        using var client = Client();

        var writes = client.ReadWriteLog(out var remaining);

        Assert.Equal(2, writes.Count);
        Assert.Equal(0, remaining);

        Assert.Equal(0x00123400u, writes[0].Pc);
        Assert.Equal(0x330129D4u, writes[0].Address);
        Assert.Equal(2u, writes[0].Size);
        Assert.Equal(0xA6EFu, writes[0].Value);

        Assert.Equal(AzaharRpcClient.WriteLogRegisters, writes[0].Registers.Length);
        Assert.Equal(0x30000000u, writes[0].Registers[0]);
        Assert.Equal(0x30000F00u, writes[0].Registers[15]);
    }

    /// <summary>
    /// The emulator saying "there are more" has to reach the caller, so nobody reads one page as
    /// the whole answer.
    /// </summary>
    [Fact]
    public void What_did_not_fit_is_reported_and_not_swallowed()
    {
        Serve(claimed: 3, sent: 3, remaining: 40);
        using var client = Client();

        var writes = client.ReadWriteLog(out var remaining);

        Assert.Equal(3, writes.Count);
        Assert.Equal(40, remaining);
    }

    /// <summary>
    /// And a reply that claims more entries than it carries is believed for its bytes, not for its
    /// header: the ones that arrived whole are returned, and the rest are counted as pending.
    /// </summary>
    [Fact]
    public void A_reply_that_claims_more_than_it_sends_never_invents_the_difference()
    {
        Serve(claimed: 5, sent: 2, remaining: 0);
        using var client = Client();

        var writes = client.ReadWriteLog(out var remaining);

        Assert.Equal(2, writes.Count);
        Assert.Equal(3, remaining);
    }

    [Fact]
    public void An_emulator_that_refuses_the_range_says_so()
    {
        Serve(claimed: 0, sent: 0, remaining: 0, accept: 0);
        using var client = Client();

        Assert.False(client.WatchWrites(0x330129D4, 2));
    }

    [Fact]
    public void An_accepted_range_answers_true()
    {
        Serve(claimed: 0, sent: 0, remaining: 0);
        using var client = Client();

        Assert.True(client.WatchWrites(0x330129D4, 2));
    }
}
