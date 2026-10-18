using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Admin.Services;
using PermaLocke.Admin.ViewModels;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.Admin.Tests;

/// <summary>The control centre of Admin (2026-10-08): the list, the log, what was sent and the confirmations.</summary>
public sealed class ControlCentreTests
{
    private static GiftDesk Desk(ITournamentServer server) => new(server, NullLogger<GiftDesk>.Instance);

    private static AdminGift Order(Guid to, string kind = AdminOrderKinds.Message) => new()
    {
        Schema = AdminGift.OrderSchema,
        Id = Guid.NewGuid(),
        From = "Orga",
        To = to.ToString(),
        Reason = "prueba",
        CreatedAt = DateTimeOffset.Now,
        Order = new AdminOrder(kind, new Dictionary<string, string> { ["texto"] = "hola" }, "Mensaje privado")
    };

    // ============================================================ LA LISTA

    [Fact]
    public async Task The_list_has_everybody_including_who_never_signed_in_and_who_is_off_the_list()
    {
        var players = await Desk(new DemoServer()).PlayersAsync();

        Assert.Contains(players, p => !p.HasAccount && p.Standing == "NUNCA HA ENTRADO");
        Assert.Contains(players, p => p.Suspended && p.NeedsLook);
        Assert.Contains(players, p => !p.InList && p.Standing == "FUERA DE LA LISTA");
        Assert.Contains(players, p => p.Rewinds > 0 && p.NeedsLook);
        Assert.Contains(players, p => p.Version is null);
    }

    [Fact]
    public void The_search_ignores_case_and_accents_and_looks_at_the_discord_id()
    {
        var player = new PlayerLine(Guid.NewGuid(), "Íñigo", 10, 1, 0) { DiscordId = "123456789012345678", Role = "Monotype Agua" };

        Assert.True(AdminViewModel.Matches(player, "inigo"));
        Assert.True(AdminViewModel.Matches(player, "4567890"));
        Assert.True(AdminViewModel.Matches(player, "agua"));
        Assert.True(AdminViewModel.Matches(player, "  "));
        Assert.False(AdminViewModel.Matches(player, "pablo"));
    }

    [Fact]
    public void Each_filter_shows_only_its_players()
    {
        var playing = new PlayerLine(Guid.NewGuid(), "A", 0, 0, 0, PresenceState.Playing);
        var suspended = new PlayerLine(Guid.NewGuid(), "B", 0, 0, 0) { Suspended = true };
        var noRun = new PlayerLine(Guid.Empty, "C", 0, 0, 0) { HasRun = false };

        Assert.True(AdminViewModel.Matches(playing, PlayerFilter.Conectados));
        Assert.False(AdminViewModel.Matches(suspended, PlayerFilter.Conectados));
        Assert.True(AdminViewModel.Matches(suspended, PlayerFilter.ARevisar));
        Assert.True(AdminViewModel.Matches(suspended, PlayerFilter.Suspendidos));
        Assert.True(AdminViewModel.Matches(noRun, PlayerFilter.SinRun));
        Assert.False(AdminViewModel.Matches(playing, PlayerFilter.SinRun));
        Assert.True(AdminViewModel.Matches(noRun, PlayerFilter.Todos));
    }

    [Fact]
    public void Ago_says_it_in_words_and_a_dash_for_never()
    {
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("—", PlayerLine.Ago(null, now));
        Assert.Equal("ahora", PlayerLine.Ago(now.AddSeconds(-20), now));
        Assert.Equal("hace 5 min", PlayerLine.Ago(now.AddMinutes(-5), now));
        Assert.Equal("hace 3 h", PlayerLine.Ago(now.AddHours(-3), now));
        Assert.Equal("hace 2 d", PlayerLine.Ago(now.AddDays(-2), now));
    }

    // ============================================================ MANDAR

    [Fact]
    public async Task Sending_the_same_orders_twice_adds_nothing_the_second_time()
    {
        var server = new DemoServer();
        var desk = Desk(server);
        var to = (await desk.PlayersAsync()).First(p => p.HasAccount).Id;
        var orders = new[] { Order(to), Order(to) };

        Assert.Equal(2, await desk.SendManyAsync(orders));
        Assert.Equal(0, await desk.SendManyAsync(orders));
    }

    [Fact]
    public async Task Without_21_admin_sql_the_list_comes_from_the_leaderboard_and_sending_skips_what_is_there()
    {
        var server = new OldServer();
        var desk = Desk(server);

        var players = await desk.PlayersAsync();
        Assert.False(desk.ControlCentre);
        Assert.Single(players);

        var order = Order(players[0].Id);
        Assert.Equal(1, await desk.SendManyAsync([order]));
        Assert.Equal(0, await desk.SendManyAsync([order]));
        Assert.Single(server.Posted);
    }

    // ============================================================ LO MANDADO Y SU ESTADO

    [Fact]
    public void An_order_is_open_until_the_players_app_closes_it_and_then_says_its_result()
    {
        var gift = Order(Guid.NewGuid(), AdminOrderKinds.Revive);
        var open = OrderLine.From(gift, null);

        Assert.False(open.Closed);
        Assert.Equal("PENDIENTE", open.State);
        Assert.Equal("Orden: revivir", open.Kind);
        Assert.False(open.Stuck(gift.CreatedAt.AddMinutes(10)));
        Assert.True(open.Stuck(gift.CreatedAt.AddMinutes(31)));

        var closing = new GameEvent
        {
            Id = Guid.NewGuid(), RunId = Guid.NewGuid(), Timestamp = gift.CreatedAt.AddMinutes(1), Type = GameEventType.AdminGiftClaimed,
            Source = EventSource.Admin, Actor = "Orga", Description = "x",
            Data = new Dictionary<string, string> { ["regalo"] = gift.Id.ToString(), ["resultado"] = "Ese Pokémon no está en la run." }
        };
        var closed = OrderLine.From(gift, closing);

        Assert.True(closed.Closed);
        Assert.Equal("Ese Pokémon no está en la run.", closed.Result);
        Assert.False(closed.Stuck(gift.CreatedAt.AddDays(1)));
    }

    [Fact]
    public void What_was_sent_before_their_run_began_is_not_waiting()
    {
        var gift = Order(Guid.NewGuid());
        var before = OrderLine.From(gift, null, runStart: gift.CreatedAt.AddHours(1));
        var withoutRun = OrderLine.From(gift, null, hasRun: false);
        var during = OrderLine.From(gift, null, runStart: gift.CreatedAt.AddHours(-1));

        Assert.Equal("ANTERIOR A SU RUN", before.State);
        Assert.False(before.Stuck(gift.CreatedAt.AddDays(1)));
        Assert.True(withoutRun.Earlier);
        Assert.True(during.Stuck(gift.CreatedAt.AddDays(1)));
    }

    [Fact]
    public async Task A_cancelled_message_keeps_what_was_written()
    {
        var desk = Desk(new DemoServer());
        var player = (await desk.PlayersAsync()).First(p => p.HasAccount && p.HasRun);
        var sheet = new PlayerSheetViewModel(desk, player, "Orga", [], [], NullLogger.Instance) { Message = "Hola" };

        try
        {
            Confirm.Handler = _ => null;
            await sheet.SendMessageCommand.ExecuteAsync(null);
            Assert.Equal("Hola", sheet.Message);

            Confirm.Handler = _ => new ConfirmAnswer("x");
            await sheet.SendMessageCommand.ExecuteAsync(null);
            Assert.Equal(string.Empty, sheet.Message);
        }
        finally
        {
            Confirm.Handler = _ => null;
        }
    }

    // ============================================================ EL REGISTRO

    [Theory]
    [InlineData("orden:revivir", "Orden: revivir")]
    [InlineData("retirar:regalo", "Retirado · Regalo")]
    [InlineData("lista:suspender", "Suspendido")]
    [InlineData("reglas:cambiar", "Reglas cambiadas")]
    [InlineData("algo:nuevo", "algo:nuevo")]
    public void The_log_says_each_action_in_words(string action, string words) => Assert.Equal(words, AdminLog.Describe(action));

    [Fact]
    public void Only_a_rules_change_that_kept_the_old_file_can_be_put_back()
    {
        static LogLine Line(string action, string detail) =>
            new(1, DateTimeOffset.Now, "Orga", action, null, "roles.json", null, JsonDocument.Parse(detail).RootElement);

        Assert.True(Line("reglas:cambiar", """{"antes":"{}","bytesAntes":2}""").CanRevert);
        Assert.True(Line("reglas:quitar", """{"antes":"{}"}""").CanRevert);
        Assert.False(Line("reglas:publicar", """{"bytesDespues":2}""").CanRevert);
        Assert.False(Line("anuncio:retirar", """{"antes":"x"}""").CanRevert);
    }

    [Fact]
    public void The_log_summary_of_a_gift_lists_what_was_in_it()
    {
        var line = new LogLine(1, DateTimeOffset.Now, "Orga", "regalo", null, "x", "premio",
            JsonDocument.Parse("""{"para":"todos","puntos":50,"tiradas":{"a":2,"b":1}}""").RootElement);

        Assert.Equal("+50 puntos · 3 tiradas · a todos", line.Summary);
    }

    [Fact]
    public void The_kind_filter_groups_actions_by_family()
    {
        Assert.Equal("Órdenes", LogViewModel.KindOf("orden:matar"));
        Assert.Equal("Regalos y ajustes", LogViewModel.KindOf("retirar:ajuste"));
        Assert.Equal("Lista", LogViewModel.KindOf("lista:baja"));
    }

    // ============================================================ CONFIRMAR

    [Fact]
    public async Task An_order_from_the_sheet_needs_the_confirmation_and_carries_its_reason()
    {
        var server = new DemoServer();
        var desk = Desk(server);
        var player = (await desk.PlayersAsync()).First(p => p.HasAccount && p.HasRun);
        var sheet = new PlayerSheetViewModel(desk, player, "Orga", [], [], NullLogger.Instance) { Message = "Hola" };
        var asked = new List<ConfirmRequest>();

        try
        {
            Confirm.Handler = request =>
            {
                asked.Add(request);
                return null;
            };
            await sheet.SendMessageCommand.ExecuteAsync(null);
            var before = (await desk.OrdersOfAsync(player.Id, [])).Count;

            Confirm.Handler = request =>
            {
                asked.Add(request);
                return new ConfirmAnswer("porque sí");
            };
            sheet.Message = "Hola";
            await sheet.SendMessageCommand.ExecuteAsync(null);
            var orders = await desk.OrdersOfAsync(player.Id, []);

            Assert.Equal(2, asked.Count);
            Assert.All(asked, request => Assert.True(request.NeedsReason));
            Assert.Equal(before + 1, orders.Count);
            Assert.Equal("porque sí", orders[0].Reason);
            Assert.False(orders[0].Closed);
        }
        finally
        {
            Confirm.Handler = _ => null;
        }
    }

    [Fact]
    public async Task Resetting_a_run_asks_to_type_the_word_and_is_dangerous()
    {
        var desk = Desk(new DemoServer());
        var player = (await desk.PlayersAsync()).First(p => p.HasAccount && p.HasRun);
        var sheet = new PlayerSheetViewModel(desk, player, "Orga", [], [], NullLogger.Instance);
        ConfirmRequest? asked = null;

        try
        {
            Confirm.Handler = request =>
            {
                asked = request;
                return null;
            };
            await sheet.ResetRunCommand.ExecuteAsync(null);

            Assert.NotNull(asked);
            Assert.True(asked.Danger);
            Assert.Equal("REINICIAR", asked.TypeToConfirm);
            Assert.True((await desk.PlayersAsync()).First(p => p.Id == player.Id).HasRun);
        }
        finally
        {
            Confirm.Handler = _ => null;
        }
    }

    [Fact]
    public async Task A_player_who_never_signed_in_cannot_be_sent_anything()
    {
        var desk = Desk(new DemoServer());
        var player = (await desk.PlayersAsync()).First(p => !p.HasAccount);
        var sheet = new PlayerSheetViewModel(desk, player, "Orga", [], [], NullLogger.Instance) { Message = "Hola" };
        var asked = false;

        try
        {
            Confirm.Handler = _ =>
            {
                asked = true;
                return new ConfirmAnswer("x");
            };
            await sheet.SendMessageCommand.ExecuteAsync(null);

            Assert.False(asked);
            Assert.True(sheet.StatusIsError);
            Assert.False(sheet.CanOrder);
        }
        finally
        {
            Confirm.Handler = _ => null;
        }
    }

    [Fact]
    public void A_refusal_from_the_server_is_shown_with_its_own_words()
    {
        Assert.Equal("Solo el organizador puede mandar regalos",
            DiscordLogin.ServerMessage("""{"code":"P0001","message":"Solo el organizador puede mandar regalos","details":null}"""));
        Assert.Null(DiscordLogin.ServerMessage("<html>502</html>"));
        Assert.Null(DiscordLogin.ServerMessage("[]"));
    }

    // ============================================================ UN SERVIDOR SIN 21-admin.sql

    /// <summary>A server from before 21-admin.sql: no admin_jugadores, no mandar_regalos; a repeated id is a 409.</summary>
    private sealed class OldServer : ITournamentServer
    {
        private readonly Guid _player = Guid.NewGuid();

        public List<string> Posted { get; } = [];

        public DiscordAccount? Saved { get; } = new("Orga", null, "x");

        public Task<DiscordAccount> SignInAsync(CancellationToken cancel = default) => Task.FromResult(Saved!);

        public void SignOut()
        {
        }

        public Task<string?> GetAsync(string path, CancellationToken cancel = default) =>
            Task.FromResult<string?>(path.StartsWith("clasificacion", StringComparison.Ordinal)
                ? $$"""[{"user_id":"{{_player}}","jugador":"Uno","puntos":5,"vivos":1,"caidos":0,"estado":0}]"""
                : "[]");

        public Task<bool> PostAsync(string path, string json, string? prefer = null, CancellationToken cancel = default)
        {
            var id = JsonDocument.Parse(json).RootElement.GetProperty("id").GetString()!;
            if (Posted.Contains(id)) throw new HttpRequestException("duplicate", null, HttpStatusCode.Conflict);
            Posted.Add(id);
            return Task.FromResult(true);
        }

        public Task<string?> CallAsync(string function, string json = "{}", CancellationToken cancel = default) =>
            throw new HttpRequestException("Could not find the function", null, HttpStatusCode.NotFound);

        public Task<string?> DeleteAsync(string path, CancellationToken cancel = default) => Task.FromResult<string?>("[]");

        public Task<string?> PatchAsync(string path, string json, CancellationToken cancel = default) => Task.FromResult<string?>("[]");

        public Task<byte[]?> DownloadAsync(string bucket, string path, CancellationToken cancel = default) => Task.FromResult<byte[]?>(null);

        public Task<string?> ListAsync(string bucket, string folder, CancellationToken cancel = default) => Task.FromResult<string?>("[]");

        public Task<bool> RemoveAsync(string bucket, IReadOnlyList<string> paths, CancellationToken cancel = default) => Task.FromResult(true);
    }
}
