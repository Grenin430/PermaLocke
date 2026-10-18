using System.Net.Http;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.Admin.Services;

/// <summary>
/// A tournament server that lives in memory, with made-up players (<c>--demo</c>, 2026-10-08): to look at Admin, take its
/// screenshots and try every button without reading or touching the real tournament.
/// </summary>
/// <remarks>
/// It answers the same paths Admin asks the real one, with rows shaped like the real ones (21-admin.sql included). What
/// Admin sends is kept here, logged like the server would, and gone when Admin closes. The window says it is the demo.
/// The players include the awkward cases: a very long name, somebody who never signed in, a suspended one, one off the
/// list with a run, one with rewinds, one on an old version.
/// </remarks>
public sealed class DemoServer : ITournamentServer
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string[] Names =
    [
        "Juanito", "Iñigo", "SaintPablo", "Juanmaa", "Grenin430", "Lucía", "Martita_99", "ElDelBidoof", "Álvaro", "Noa",
        "Pedricure", "Carlota", "Rubén", "Txema", "Sofía", "Kike", "MiauMiau", "Dani G.", "Irene", "Bea",
        "Hugo", "Paula", "Marcos", "Nerea", "Óscar", "Elena", "Raúl", "Clara", "Sergio", "Vega",
        "Maximiliano Alejandro de la Fuente", "Lola"
    ];

    private static readonly (int Species, string Name)[] Mons =
    [
        (25, "Pikachu"), (722, "Rowlet"), (725, "Litten"), (728, "Popplio"), (734, "Yungoos"), (736, "Grubbin"), (744, "Rockruff"),
        (746, "Wishiwashi"), (751, "Dewpider"), (757, "Salandit"), (761, "Bounsweet"), (765, "Oranguru"), (769, "Sandygast"),
        (775, "Komala"), (778, "Mimikyu"), (782, "Jangmo-o"), (133, "Eevee"), (147, "Dratini"), (246, "Larvitar"), (374, "Beldum"),
        (214, "Heracross"), (130, "Gyarados"), (94, "Gengar"), (448, "Lucario")
    ];

    private static readonly string[] Zones = ["Ruta 1", "Ruta 2", "Ruta 3", "Colina Cenefa", "Cueva Sendero", "Ruta 4", "Ruta 5", "Bosque Umbrío"];

    private sealed class Player
    {
        public required string Discord { get; init; }
        public required string Name { get; init; }
        public Guid? Id { get; init; }
        public bool InList { get; set; } = true;
        public bool Suspended { get; set; }
        public int State { get; init; }
        public string? Version { get; init; }
        public DateTimeOffset Seen { get; init; }
        public RunSnapshot? Snapshot { get; init; }
        public RunHistory? History { get; init; }
        public DateTimeOffset Uploaded { get; init; }
        public int Rewinds { get; init; }
        public int Archived { get; set; }
    }

    private readonly List<Player> _players = [];
    private readonly List<JsonObject> _gifts = [];
    private readonly List<JsonObject> _log = [];
    private readonly List<JsonObject> _notes = [];
    private long _nextId = 1;
    private readonly DateTimeOffset _now = DateTimeOffset.Now;

    public DemoServer()
    {
        var random = new Random(430);

        for (var i = 0; i < Names.Length; i++)
        {
            var neverSigned = i == 9;
            var id = neverSigned ? (Guid?)null : new Guid(i + 1, 0x7e57, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, (byte)i });
            var hasRun = id is not null && i != 13;
            var seen = _now - TimeSpan.FromMinutes(random.Next(0, i < 8 ? 3 : 4000));
            var uploaded = i == 21 ? _now - TimeSpan.FromDays(4) : seen - TimeSpan.FromMinutes(random.Next(0, 5));
            var (snapshot, history) = hasRun ? Run(id!.Value, Names[i], random, uploaded) : (null, null);

            _players.Add(new Player
            {
                Discord = (400000000000000000L + i * 7919L).ToString(CultureInfo.InvariantCulture),
                Name = Names[i],
                Id = id,
                InList = i != 17,
                Suspended = i == 12,
                State = i < 5 ? 2 : i < 8 ? 1 : 0,
                Version = i == 9 ? null : i % 7 == 3 ? "1.0.15" : i % 11 == 5 ? null : "1.0.16",
                Seen = seen,
                Snapshot = snapshot,
                History = history,
                Uploaded = uploaded,
                Rewinds = i == 2 ? 2 : 0,
                Archived = i == 6 ? 1 : 0
            });
        }

        // Unos envíos y unas líneas de registro, para que MANDADO, INICIO y REGISTRO no salgan vacíos.
        var first = _players[0];
        AddGift(new AdminGift
        {
            Id = Guid.NewGuid(), From = "Organizador", To = first.Id!.Value.ToString(), Reason = "Fallo del cap en la Prueba 3",
            CreatedAt = _now - TimeSpan.FromHours(3), Points = 150
        }, by: "Organizador", at: _now - TimeSpan.FromHours(3));
        AddGift(new AdminGift
        {
            Schema = AdminGift.OrderSchema, Id = Guid.NewGuid(), From = "Organizador", To = first.Id!.Value.ToString(),
            Reason = "Se le cerró Azahar en mitad del combate", CreatedAt = _now - TimeSpan.FromHours(2),
            Order = new AdminOrder(AdminOrderKinds.GiveItem, new Dictionary<string, string> { ["objeto"] = "50", ["cantidad"] = "2", ["nombre"] = "Caramelo Raro" },
                "Dar 2 x Caramelo Raro")
        }, by: "Organizador", at: _now - TimeSpan.FromHours(2));
        AddGift(new AdminGift
        {
            Id = Guid.NewGuid(), From = "Organizador", To = AdminGift.Everybody, Reason = "Fin de semana de gacha",
            CreatedAt = _now - TimeSpan.FromHours(1), Rolls = new Dictionary<string, int> { ["normal"] = 3 }
        }, by: "Organizador", at: _now - TimeSpan.FromHours(1));
        Log("lista:suspender", _players[12].Id, _players[12].Discord, null, new JsonObject { ["antes"] = new JsonObject { ["nombre"] = _players[12].Name, ["suspendido"] = false } }, _now - TimeSpan.FromDays(1));
        Log("reglas:cambiar", null, "roles.json", null, new JsonObject { ["antes"] = "{\"version\":1}", ["bytesAntes"] = 14, ["bytesDespues"] = 16063 }, _now - TimeSpan.FromHours(20));
        Log("anuncio:publicar", null, "7", null, new JsonObject { ["texto"] = "El torneo se pausa el domingo a las 22:00 para la final." }, _now - TimeSpan.FromHours(6));
        _notes.Add(new JsonObject
        {
            ["id"] = _nextId++, ["discord_id"] = _players[2].Discord, ["texto"] = "Dice que restauró una copia porque se le corrompió la partida. Pedirle el log de Azahar.",
            ["por_nombre"] = "Organizador", ["creado"] = _now - TimeSpan.FromHours(30)
        });
    }

    private static (RunSnapshot, RunHistory) Run(Guid player, string name, Random random, DateTimeOffset uploaded)
    {
        var runId = Guid.NewGuid();
        var start = uploaded - TimeSpan.FromDays(random.Next(3, 12));
        var events = new List<GameEvent>();
        var pokemon = new List<PokemonEntry>();
        var at = start;

        GameEvent Event(GameEventType type, string text, int points = 0, Dictionary<string, string>? data = null, string? location = null, EventSource source = EventSource.AutoDetect)
        {
            at += TimeSpan.FromMinutes(random.Next(4, 50));
            var e = new GameEvent
            {
                Id = Guid.NewGuid(), RunId = runId, Timestamp = at, Type = type, Source = source, Actor = name, Description = text,
                PointsDelta = points, LocationId = location, Data = data ?? new Dictionary<string, string>()
            };
            events.Add(e);
            return e;
        }

        Event(GameEventType.RunCreated, $"Run creada por {name}", source: EventSource.System);
        var count = random.Next(4, 14);
        for (var i = 0; i < count; i++)
        {
            var (species, speciesName) = Mons[random.Next(Mons.Length)];
            var zone = Zones[i % Zones.Length];
            Event(GameEventType.ZoneEncounterSpent, $"Primer encuentro en {zone}", data: new() { ["zona"] = zone }, location: $"z{i % Zones.Length}");
            var caught = Event(GameEventType.PokemonCaught, $"{speciesName} capturado en {zone}", 10);
            var dead = random.Next(5) == 0;
            pokemon.Add(new PokemonEntry
            {
                Id = Guid.NewGuid(), RunId = runId, Species = species, SpeciesName = speciesName,
                Nickname = random.Next(3) == 0 ? $"{speciesName[..3]}{random.Next(10, 99)}" : null, Level = random.Next(5, 52),
                IsShiny = random.Next(25) == 0, Status = dead ? PokemonStatus.Dead : PokemonStatus.Alive, Origin = PokemonOrigin.Capture,
                EncounterType = EncounterType.Wild, ObtainedAt = caught.Timestamp, DiedAt = dead ? caught.Timestamp + TimeSpan.FromHours(2) : null
            });
            if (dead) Event(GameEventType.PokemonDied, $"{speciesName} ha caído", -25);
            if (random.Next(4) == 0) Event(GameEventType.AchievementUnlocked, $"Logro: {(i % 2 == 0 ? "Primera captura" : "Coleccionista")}", 20, new() { ["logro"] = $"l{i}", ["nombre"] = "Logro" });
        }

        if (random.Next(6) == 0) Event(GameEventType.TeamWiped, "Equipo caído en la Prueba 2", -100);
        if (random.Next(5) == 0) Event(GameEventType.IntegrityFlag, "Recarga de estado guardado durante un combate", source: EventSource.System);

        var points = events.Sum(e => e.PointsDelta) + 300;
        var snapshot = new RunSnapshot
        {
            RunId = runId, PlayerName = name, PlayerId = player, RunName = "Torneo", RoleName = random.Next(3) == 0 ? "Monotype Agua" : "Normal",
            Points = Math.Max(0, points), Registered = pokemon.Count, Alive = pokemon.Count(p => p.Status == PokemonStatus.Alive),
            Dead = pokemon.Count(p => p.Status == PokemonStatus.Dead), StagesCleared = random.Next(0, 8), LevelCap = 12 + random.Next(0, 8) * 5,
            AchievementsUnlocked = events.Count(e => e.Type == GameEventType.AchievementUnlocked), AchievementsTotal = 64,
            EventCount = events.Count, RunCreatedAt = start, Pokemon = pokemon, PlayedHours = Math.Round(random.NextDouble() * 40, 1), PublishedAt = uploaded
        };

        return (snapshot, new RunHistory { RunId = runId, PlayerId = player, ExportedAt = uploaded, Events = events });
    }

    // ============================================================ LA CUENTA

    public DiscordAccount? Saved { get; } = new("Organizador (demo)", null, "demo", "111", true);

    public Task<DiscordAccount> SignInAsync(CancellationToken cancel = default) => Task.FromResult(Saved!);

    public void SignOut()
    {
    }

    // ============================================================ REST

    public Task<string?> GetAsync(string path, CancellationToken cancel = default) => Task.FromResult<string?>(Get(path));

    public Task<bool> PostAsync(string path, string json, string? prefer = null, CancellationToken cancel = default)
    {
        var body = JsonNode.Parse(json)!.AsObject();
        switch (Table(path))
        {
            case "regalos":
                if (_gifts.Any(g => g["id"]!.GetValue<string>() == body["id"]!.ToString()))
                {
                    throw new HttpRequestException("duplicate key value violates unique constraint \"regalos_pkey\"", null, System.Net.HttpStatusCode.Conflict);
                }

                AddGift(body["regalo"].Deserialize<AdminGift>(Json)!, "Organizador (demo)", DateTimeOffset.Now);
                break;
            case "admin_notas":
                body["id"] = _nextId++;
                body["creado"] = DateTimeOffset.Now;
                _notes.Add(body);
                Log("nota:escribir", Find(body["discord_id"]!.GetValue<string>())?.Id, body["discord_id"]!.GetValue<string>(), null, new JsonObject { ["texto"] = body["texto"]!.GetValue<string>() });
                break;
            case "reglas":
                Log("reglas:cambiar", null, body["fichero"]!.GetValue<string>(), null, new JsonObject());
                break;
        }

        return Task.FromResult(true);
    }

    public Task<string?> CallAsync(string function, string json = "{}", CancellationToken cancel = default)
    {
        var body = JsonNode.Parse(json)?.AsObject() ?? [];
        string answer = function switch
        {
            "admin_jugadores" => PlayersJson(),
            "mandar_regalos" => Send(body["p_regalos"]!.AsArray()).ToString(CultureInfo.InvariantCulture),
            "informes_lista" => JsonSerializer.Serialize(new[]
            {
                new { nombre = $"{_players[1].Id}/cierre-azahar-1.zip", jugador = _players[1].Id, bytes = 84000, creado = _now - TimeSpan.FromHours(5) },
                new { nombre = $"{_players[3].Id}/cierre-azahar-2.zip", jugador = _players[3].Id, bytes = 91000, creado = _now - TimeSpan.FromHours(2) }
            }),
            "reiniciar_run" => Reset(Guid.Parse(body["jugador"]!.GetValue<string>())),
            "limpieza" or "limpiar" => """{"cosas":{},"bytes":0}""",
            "consumo" => """{"baseDeDatos":41943040,"tablas":[],"runs":[]}""",
            _ => "null"
        };
        return Task.FromResult<string?>(answer);
    }

    public Task<string?> DeleteAsync(string path, CancellationToken cancel = default)
    {
        var id = Value(path, "id");
        switch (Table(path))
        {
            case "regalos" when _gifts.FirstOrDefault(g => g["id"]!.GetValue<string>() == id) is { } gift:
                _gifts.Remove(gift);
                Log("retirar:regalo", null, id, null, new JsonObject());
                break;
            case "admin_notas" when _notes.FirstOrDefault(n => n["id"]!.ToString() == id) is { } note:
                _notes.Remove(note);
                Log("nota:borrar", Find(note["discord_id"]!.GetValue<string>())?.Id, note["discord_id"]!.GetValue<string>(), null,
                    new JsonObject { ["texto"] = note["texto"]!.GetValue<string>() });
                break;
        }

        return Task.FromResult<string?>("[]");
    }

    public Task<string?> PatchAsync(string path, string json, CancellationToken cancel = default)
    {
        if (Table(path) == "whitelist" && Find(Value(path, "discord_id")) is { } player
                                       && JsonNode.Parse(json)?["suspendido"]?.GetValue<bool>() is { } suspended)
        {
            player.Suspended = suspended;
            Log(suspended ? "lista:suspender" : "lista:reactivar", player.Id, player.Discord, null, new JsonObject());
        }

        return Task.FromResult<string?>("[]");
    }

    public Task<byte[]?> DownloadAsync(string bucket, string path, CancellationToken cancel = default) =>
        Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes("copia de demostración"));

    public Task<string?> ListAsync(string bucket, string folder, CancellationToken cancel = default) =>
        Task.FromResult<string?>(JsonSerializer.Serialize(Enumerable.Range(0, 3).Select(i => new
        {
            name = $"{(_now - TimeSpan.FromHours(6 * i)):yyyyMMdd-HHmm}.zip",
            id = Guid.NewGuid(),
            created_at = _now - TimeSpan.FromHours(6 * i),
            metadata = new { size = 2_400_000 + i * 13_000 }
        })));

    public Task<bool> RemoveAsync(string bucket, IReadOnlyList<string> paths, CancellationToken cancel = default) => Task.FromResult(true);

    // ============================================================ LO QUE RESPONDE

    private string Get(string path)
    {
        var active = _players.Where(p => p.Snapshot is not null && p.Id is not null).ToList();

        return Table(path) switch
        {
            "runs" when Value(path, "user_id") is { Length: > 0 } user => JsonSerializer.Serialize(
                active.Where(p => p.Id.ToString() == user).Select(p => new { run_id = p.Snapshot!.RunId, snapshot = p.Snapshot, history = p.History, subida = p.Uploaded }), Json),
            "runs" => JsonSerializer.Serialize(active.Select(p => new
            {
                user_id = p.Id, run_id = p.Snapshot!.RunId, activa = true, snapshot = p.Snapshot, history = p.History, subida = p.Uploaded
            }), Json),
            "clasificacion" => JsonSerializer.Serialize(active.Select(p => new { user_id = p.Id, jugador = p.Name, puntos = p.Snapshot!.Points, vivos = p.Snapshot.Alive, caidos = p.Snapshot.Dead, estado = p.State })),
            "regalos" => GiftsJson(path),
            "admin_registro" => JsonSerializer.Serialize(_log.Where(l => Value(path, "jugador") is not { Length: > 0 } who || l["jugador"]?.ToString() == who)
                .OrderByDescending(l => l["id"]!.GetValue<long>())),
            "admin_notas" => JsonSerializer.Serialize(_notes.Where(n => n["discord_id"]!.GetValue<string>() == Value(path, "discord_id"))
                .OrderByDescending(n => n["id"]!.GetValue<long>())),
            "sugerencias" => JsonSerializer.Serialize(new[]
            {
                new { id = 1, nombre = "Lucía", texto = "Que el panel del cap enseñe también el próximo Kahuna.", version = "1.0.16", creado = _now - TimeSpan.FromHours(8) },
                new { id = 2, nombre = "Rubén", texto = "Más ranuras de wonder trade los fines de semana.", version = "1.0.15", creado = _now - TimeSpan.FromHours(30) }
            }),
            "whitelist" => JsonSerializer.Serialize(_players.Where(p => p.InList).Select(p => new { discord_id = p.Discord, nombre = p.Name, alta = _now - TimeSpan.FromDays(20), suspendido = p.Suspended })),
            "anuncios" => JsonSerializer.Serialize(new[] { new { id = 7, texto = "El torneo se pausa el domingo a las 22:00 para la final.", creado = _now - TimeSpan.FromHours(6) } }),
            _ => "[]"
        };
    }

    private string PlayersJson() => JsonSerializer.Serialize(_players.Select(p => new
    {
        discord_id = p.Discord,
        en_lista = p.InList,
        suspendido = p.Suspended,
        user_id = p.Id,
        nombre = p.Name,
        avatar_url = (string?)null,
        estado = p.State,
        latido = p.Seen,
        version = p.Version,
        run_id = p.Snapshot?.RunId,
        subida = p.Snapshot is null ? (DateTimeOffset?)null : p.Uploaded,
        puntos = p.Snapshot?.Points,
        vivos = p.Snapshot?.Alive,
        caidos = p.Snapshot?.Dead,
        capturados = p.Snapshot?.Registered,
        etapas = p.Snapshot?.StagesCleared,
        nivel_maximo = p.Snapshot?.LevelCap,
        logros = p.Snapshot?.AchievementsUnlocked,
        logros_total = p.Snapshot?.AchievementsTotal,
        rol = p.Snapshot?.RoleName,
        horas = p.Snapshot?.PlayedHours,
        eventos = p.Snapshot?.EventCount,
        archivadas = p.Archived,
        notas = _notes.Count(n => n["discord_id"]!.GetValue<string>() == p.Discord),
        retrocesos = p.Rewinds
    }));

    private string GiftsJson(string path)
    {
        var to = path.Contains("para=in.(", StringComparison.Ordinal)
            ? path[(path.IndexOf("para=in.(", StringComparison.Ordinal) + 9)..].Split(')')[0].Split(',')
            : null;
        return new JsonArray([.. _gifts.Where(g => to is null || to.Contains(g["para"]!.GetValue<string>()))
            .OrderByDescending(g => g["creado"]!.GetValue<DateTimeOffset>())
            .Select(g => (JsonNode)new JsonObject { ["regalo"] = g["regalo"]!.DeepClone(), ["creado"] = g["creado"]!.DeepClone() })]).ToJsonString();
    }

    private int Send(JsonArray rows)
    {
        var added = 0;
        foreach (var row in rows)
        {
            if (_gifts.Any(g => g["id"]!.GetValue<string>() == row!["id"]!.ToString())) continue;
            AddGift(row!["regalo"].Deserialize<AdminGift>(Json)!, "Organizador (demo)", DateTimeOffset.Now);
            added++;
        }

        return added;
    }

    private void AddGift(AdminGift gift, string by, DateTimeOffset at)
    {
        _gifts.Add(new JsonObject
        {
            ["id"] = gift.Id.ToString(), ["para"] = gift.To, ["regalo"] = JsonSerializer.SerializeToNode(gift, Json), ["creado"] = at
        });

        var kind = gift.Order is { } order ? $"orden:{order.Kind}" : gift.Adjustment ? "ajuste" : "regalo";
        Log(kind, Guid.TryParse(gift.To, out var to) ? to : null, gift.Id.ToString(), gift.Reason, new JsonObject
        {
            ["para"] = gift.To,
            ["resumen"] = gift.Order?.Summary,
            ["puntos"] = gift.Points == 0 ? null : gift.Points
        }, at, by);
    }

    private string Reset(Guid player)
    {
        if (_players.FirstOrDefault(p => p.Id == player) is { Snapshot: not null } found)
        {
            var index = _players.IndexOf(found);
            _players[index] = new Player
            {
                Discord = found.Discord, Name = found.Name, Id = found.Id, InList = found.InList, Suspended = found.Suspended,
                State = found.State, Version = found.Version, Seen = found.Seen, Archived = found.Archived + 1
            };
            Log("run:reinicio", player, found.Snapshot.RunId.ToString(), null, new JsonObject { ["puntos"] = found.Snapshot.Points, ["eventos"] = found.Snapshot.EventCount });
            return "1";
        }

        return "0";
    }

    private void Log(string action, Guid? player, string? target, string? reason, JsonObject detail, DateTimeOffset? at = null, string by = "Organizador (demo)") =>
        _log.Add(new JsonObject
        {
            ["id"] = _nextId++, ["cuando"] = at ?? DateTimeOffset.Now, ["por_nombre"] = by, ["accion"] = action,
            ["jugador"] = player?.ToString(), ["objetivo"] = target, ["motivo"] = reason, ["detalle"] = detail
        });

    private Player? Find(string discord) => _players.FirstOrDefault(p => p.Discord == discord);

    private static string Table(string path) => path.Split('?')[0];

    /// <summary>The value of a «campo=eq.valor» filter in a REST path, or empty.</summary>
    private static string Value(string path, string field)
    {
        var key = $"{field}=eq.";
        var at = path.IndexOf(key, StringComparison.Ordinal);
        return at < 0 ? string.Empty : Uri.UnescapeDataString(path[(at + key.Length)..].Split('&')[0]);
    }
}
