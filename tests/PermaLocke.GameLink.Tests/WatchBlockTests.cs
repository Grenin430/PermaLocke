using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Rpc;
using PKHeX.Core;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// What the emulator is asked to keep frozen, against a fake server that writes down the request.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of a save it corrupted. The guard registered the whole 260-byte party
/// entry, and only the first 232 of those are the Pokémon: what follows is a tail that in the
/// authoritative structures belongs to something the game updates continuously. So the emulator
/// saw a difference at all times and rewrote the entry five times a second, for ever — and while
/// it was doing that the game read the slot to serialise the save and stored an entry whose
/// halves came from two different moments. The game drew the result as a Bad Egg.
/// </para>
/// <para>
/// The size is the whole finding, so the size is what is pinned here.
/// </para>
/// </remarks>
public sealed class WatchBlockTests : IDisposable
{
    private const int HeaderSize = 16;

    private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stopping = new();

    private byte[] _entry = [];
    private byte[]? _registered;
    private uint _registeredTag;

    private int Port => ((IPEndPoint)_server.Client.LocalEndPoint!).Port;

    public void Dispose()
    {
        _stopping.Cancel();
        _server.Dispose();
        _stopping.Dispose();
    }

    [Fact]
    public void Only_the_encrypted_block_is_handed_to_the_emulator()
    {
        var pokemon = Shedinja(pid: 0x8EC2769F);
        Serve(pokemon);

        var written = Write();

        Assert.True(written.Watch(0x330128E4, 0x8EC2769F), "el emulador rechazó la vigilancia");
        Assert.NotNull(_registered);

        // 232 y no 260: el Pokémon acaba donde acaba el bloque cifrado.
        Assert.Equal(new PK7().SIZE_STORED, _registered!.Length);
        Assert.Equal(_entry.AsSpan(0, _registered.Length).ToArray(), _registered);

        // La etiqueta es la constante de encriptación, que va en claro al principio.
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(_entry), _registeredTag);
    }

    /// <summary>
    /// The identity check that was missing the night the guard was written, kept here so the size
    /// fix cannot quietly undo it.
    /// </summary>
    [Fact]
    public void A_slot_holding_somebody_else_is_not_watched()
    {
        Serve(Shedinja(pid: 0x8EC2769F));

        Assert.False(Write().Watch(0x330128E4, expectedPid: 0x40B2EC56));
        Assert.Null(_registered);
    }

    private AzaharGameWriter Write() => new(
        new AzaharRpcClient("127.0.0.1", Port, 300),
        Path.Combine(Path.GetTempPath(), "permalocke-tests", Guid.NewGuid().ToString("N")),
        NullLogger<AzaharGameWriter>.Instance);

    private static PK7 Shedinja(uint pid) => new()
    {
        Species = 292,
        PID = pid,
        EncryptionConstant = 0xDE5EBE08,
        CurrentLevel = 1,
        Nickname = "MUERTO",
        IsNicknamed = true
    };

    /// <summary>
    /// Answers reads with the entry and writes down whatever the guard registers.
    /// </summary>
    private void Serve(PK7 pokemon)
    {
        pokemon.RefreshChecksum();

        _entry = new byte[pokemon.SIZE_PARTY];
        pokemon.WriteEncryptedDataParty(_entry);

        Task.Run(() =>
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

                var payload = type switch
                {
                    (uint)RpcRequestType.ReadMemory => _entry,
                    (uint)RpcRequestType.WatchBlock => Registered(request),
                    _ => [0, 0, 0, 0]
                };

                var reply = new byte[HeaderSize + payload.Length];
                BinaryPrimitives.WriteUInt32LittleEndian(reply, 1);
                BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(4), id);
                BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(8), type);
                BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(12), (uint)payload.Length);
                payload.CopyTo(reply.AsSpan(HeaderSize));

                _server.Send(reply, reply.Length, from);
            }
        });
    }

    private byte[] Registered(byte[] request)
    {
        var body = request.AsSpan(HeaderSize);

        _registeredTag = BinaryPrimitives.ReadUInt32LittleEndian(body[12..]);
        _registered = body[16..].ToArray();

        return [1, 0, 0, 0];
    }
}
