using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Core.Abstractions;
using PermaLocke.GameLink.Data;
using PermaLocke.GameLink.Rpc;

namespace PermaLocke.GameLink.Tests;

public sealed class GameLinkRecoveryTests : IAsyncLifetime
{
    private readonly UdpClient _server = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stop = new();
    private Task _serving = Task.CompletedTask;
    private uint _selected = uint.MaxValue;
    private int _selections;
    private int _memoryReads;
    private int _dropSelectionAfterReads = int.MaxValue;

    /// <summary>Whether the fake answers searches, as the fork does; the official build replies empty.</summary>
    private bool _fork = true;

    private int _searches;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PermaLockeRecovery-" + Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_folder);
        var token = _stop.Token;
        _serving = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var request = await _server.ReceiveAsync(token);
                    var response = Answer(request.Buffer);
                    await _server.SendAsync(response, request.RemoteEndPoint, token);
                }
            }
            catch (OperationCanceledException) { }
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _stop.Cancel();
        await _serving;
        _server.Dispose();
        _stop.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private byte[] Answer(byte[] request)
    {
        var type = (RpcRequestType)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(8));
        var argument = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(16));
        byte[] data;
        switch (type)
        {
            case RpcRequestType.ProcessList:
                data = new byte[argument == 0 ? 24 : 4];
                if (argument == 0)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 42);
                    BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(8), AzaharGameStateProvider.UltraMoonTitleId);
                }
                break;
            case RpcRequestType.SetGetProcess:
                if (argument == 1)
                {
                    _selected = BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20));
                    Interlocked.Increment(ref _selections);
                }
                data = BitConverter.GetBytes(_selected);
                break;
            case RpcRequestType.ReadMemory:
                data = new byte[BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(20))];
                // El puntero fijo al GameManager (code.bin, siempre mapeado) no cuenta: es una lectura de 4 bytes que aquí
                // devuelve cero y no lleva a ninguna otra.
                if (BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(16)) == 0x006A3984)
                    break;
                if (Interlocked.Increment(ref _memoryReads) >= _dropSelectionAfterReads)
                    _selected = uint.MaxValue;
                break;
            case RpcRequestType.SearchMemory:
            {
                var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(28));
                var probe = request.AsSpan(32 + length, length).IndexOfAnyExcept((byte)0) < 0;
                if (!_fork)
                {
                    data = [];
                    break;
                }
                if (!probe) Interlocked.Increment(ref _searches);
                var found = probe;
                data = new byte[found ? 8 : 4];
                BinaryPrimitives.WriteUInt32LittleEndian(data, found ? 1u : 0u);
                if (found) BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0x33F807C4);
                break;
            }
            default:
                throw new InvalidOperationException($"Unexpected RPC: {type}");
        }
        var reply = new byte[16 + data.Length];
        request.AsSpan(0, 12).CopyTo(reply);
        BinaryPrimitives.WriteUInt32LittleEndian(reply.AsSpan(12), (uint)data.Length);
        data.CopyTo(reply, 16);
        return reply;
    }

    private AzaharRpcClient Client() =>
        new("127.0.0.1", ((IPEndPoint)_server.Client.LocalEndPoint!).Port, 500);

    private AzaharGameStateProvider Provider(AzaharRpcClient client, Scanner scanner, ManualTime clock,
        Func<IReadOnlyList<uint>?>? savedPartyKeys = null) =>
        new(client, null!, null!, Path.Combine(_folder, "party.txt"),
            NullLogger<AzaharGameStateProvider>.Instance, scanner, clock, savedPartyKeys);

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now += TimeSpan.FromSeconds(seconds);
    }

    private sealed class Scanner : IPartyLayoutLocator
    {
        public int Calls { get; private set; }
        public int KeyCalls { get; private set; }
        public Action? OnScan { get; set; }
        public IReadOnlyList<PartyLayout> LocateAll(string? trainer, CancellationToken ct = default)
        {
            Calls++;
            OnScan?.Invoke();
            return [];
        }

        public IReadOnlyList<PartyLayout> LocateByKeys(IReadOnlyCollection<uint> keys, CancellationToken ct = default)
        {
            KeyCalls++;
            return [];
        }
    }

    [Fact]
    public async Task Fresh_install_waits_for_loading_before_its_first_sweep()
    {
        using var client = Client();
        var scan = new Scanner();
        var clock = new ManualTime();
        var provider = Provider(client, scan, clock);
        for (var i = 0; i < 19; i++)
        {
            Assert.False((await provider.ReadAsync()).Connected);
            clock.Advance(1);
        }
        Assert.Equal(0, scan.Calls);
        Assert.Equal(0, _memoryReads);
        await provider.ReadAsync();
        Assert.Equal(1, scan.Calls);
    }

    [Fact]
    public async Task Failed_sweeps_obey_cooldown_even_when_another_sweep_is_requested()
    {
        using var client = Client();
        var clock = new ManualTime();
        var scan = new Scanner { OnScan = () => clock.Advance(6) };
        var provider = Provider(client, scan, clock);
        for (var i = 0; i < 20; i++) await provider.ReadAsync();
        Assert.Equal(1, scan.Calls);

        // Tras un barrido en balde la espera se dobla, así que los 30 s de antes ya no bastan.
        for (var i = 0; i < 59; i++)
        {
            clock.Advance(1);
            provider.SweepAgain();
            await provider.ReadAsync();
        }
        Assert.Equal(1, scan.Calls);
        clock.Advance(1);
        await provider.ReadAsync();
        Assert.Equal(2, scan.Calls);
    }

    /// <summary>
    /// Cada barrido en balde dobla la espera, hasta cuatro minutos.
    /// </summary>
    /// <remarks>
    /// Con la espera fija, un juego que aún no ha cargado se barría cada 30 s para siempre: 226 barridos en un
    /// día, y 22 seguidos en un rato. Tres de los cinco cierres de Azahar del 19 al 21 de septiembre de 2026
    /// ocurrieron entre 3 y 6 s después de un barrido, y el log del propio emulador se corta a media línea
    /// dentro de la ráfaga de lecturas.
    /// </remarks>
    [Fact]
    public async Task Each_fruitless_sweep_doubles_the_wait()
    {
        using var client = Client();
        var clock = new ManualTime();
        var scan = new Scanner { OnScan = () => clock.Advance(6) };
        var provider = Provider(client, scan, clock);

        for (var i = 0; i < 20; i++) await provider.ReadAsync();
        Assert.Equal(1, scan.Calls);

        // 60 s para el segundo, 120 para el tercero, 240 para el cuarto.
        foreach (var wait in new[] { 60, 120, 240 })
        {
            var before = scan.Calls;

            for (var second = 0; second < wait - 1; second++)
            {
                clock.Advance(1);
                await provider.ReadAsync();
            }

            Assert.Equal(before, scan.Calls);

            clock.Advance(2);
            await provider.ReadAsync();
            Assert.Equal(before + 1, scan.Calls);
        }
    }

    /// <summary>
    /// With no save there is nothing to find: the intro and the name screen, where the fourth crash happened on
    /// 2026-09-21 with a sweep 19 s after the game opened. Not one sweep, however long it waits, and the player is told
    /// what to do.
    /// </summary>
    [Fact]
    public async Task Without_a_save_the_memory_is_never_swept()
    {
        using var client = Client();
        var clock = new ManualTime();
        var scan = new Scanner();
        var provider = Provider(client, scan, clock, () => null);

        GameSnapshot? last = null;
        for (var second = 0; second < 600; second++)
        {
            last = await provider.ReadAsync();
            clock.Advance(1);
        }

        Assert.Equal(0, scan.Calls);
        Assert.Equal(0, _memoryReads);
        Assert.Contains("Guarda la partida", last?.Problem);
    }

    /// <summary>
    /// A save whose Pokémon are not in memory yet — the title screen, a load — is not swept either, and the cheap
    /// check that says so runs once every twenty seconds, not every second.
    /// </summary>
    [Fact]
    public async Task A_saved_party_that_is_not_in_memory_is_not_swept()
    {
        using var client = Client();
        var clock = new ManualTime();
        var scan = new Scanner();
        var provider = Provider(client, scan, clock, () => [0x1234ABCD]);

        for (var second = 0; second < 200; second++)
        {
            await provider.ReadAsync();
            clock.Advance(1);
        }

        Assert.Equal(0, scan.Calls);
        Assert.InRange(scan.KeyCalls, 1, (200 / 20) + 1);
    }

    /// <summary>
    /// The official Azahar answers every search empty, which would read as «not there» forever; there the check stands
    /// aside and the sweep runs as before.
    /// </summary>
    [Fact]
    public async Task An_emulator_that_cannot_search_is_swept_as_before()
    {
        using var client = Client();
        var clock = new ManualTime();
        var scan = new Scanner();
        _fork = false;
        var provider = Provider(client, scan, clock, () => [0x1234ABCD]);

        for (var second = 0; second < 25; second++)
        {
            await provider.ReadAsync();
            clock.Advance(1);
        }

        Assert.Equal(1, scan.Calls);
    }

    [Fact]
    public async Task Lost_session_reattaches_without_starting_another_sweep_immediately()
    {
        using var client = Client();
        var clock = new ManualTime();
        var scan = new Scanner { OnScan = () => throw new AzaharRpcException("Session lost") };
        var provider = Provider(client, scan, clock);
        for (var i = 0; i < 20; i++) await provider.ReadAsync();
        var selections = _selections;
        _selected = uint.MaxValue; // A restarted emulator answers RPC again but forgot its target.
        await provider.ReadAsync();
        Assert.True(_selections > selections);
        Assert.Equal(42u, _selected);
        Assert.Equal(1, scan.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Search_without_a_selected_process_never_reads_memory(bool bag)
    {
        using var client = Client();
        Assert.Throws<AzaharRpcException>(() =>
        {
            if (bag) new BagLocator(client).LocateAll();
            else new PartyLayoutLocator(client).LocateAll("Test");
        });
        Assert.Equal(0, _memoryReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_restart_during_search_stops_the_sweep_within_one_small_window(bool bag)
    {
        using var client = Client();
        client.AttachTo(AzaharGameStateProvider.UltraMoonTitleId);
        _dropSelectionAfterReads = 1;
        Assert.Throws<AzaharRpcException>(() =>
        {
            if (bag) new BagLocator(client).LocateAll();
            else new PartyLayoutLocator(client).LocateAll("Test");
        });
        Assert.InRange(_memoryReads, 1, 68);
    }
}
