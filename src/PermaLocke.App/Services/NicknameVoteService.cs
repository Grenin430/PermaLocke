using System.Globalization;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.App.ViewModels;
using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Services;

namespace PermaLocke.App.Services;

/// <summary>
/// Nicknames voted by the others (2026-09-28, players' list item 9), as the organiser described it. Four small windows in
/// the left of the emulator, half way down, each with its bar:
/// <list type="number">
/// <item>the catcher: «¿Quieres que los otros pongan el mote a este Pokémon?» SÍ / NO (NO or the bar running out closes it);</item>
/// <item>the others: «X quiere ponerle un mote a POKÉMON», and each writes one;</item>
/// <item>the others: the proposals, to vote;</item>
/// <item>everyone: the name that won.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// The vote lives on the server (<c>tools/supabase/20-motes.sql</c>), which sets both deadlines when the catcher says
/// yes, so every app shows the same times.
/// </para>
/// <para>
/// The winner goes in <b>at once</b> if the Pokémon is in the party and the game is open: the name is written into every
/// party copy in memory (<see cref="GameLinkMonitor.RenameLive"/>), only the stored block, never the stats (the current
/// PS write is what corrupted a save in 1.0.6.2), and the next in-game save keeps it. Otherwise (in a box, game closed) it
/// goes into the save file the next time the game is closed, like the viewer's MOTE. The server row is marked applied
/// only once the game has it.
/// </para>
/// </remarks>
public sealed class NicknameVoteService
{
    private static readonly TimeSpan AskFor = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ResultFor = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly DiscordLogin _discord;
    private readonly PokemonSpriteService _sprites;
    private readonly Notifier _notifier;
    private readonly RenameService _rename;
    private readonly GameLinkMonitor _monitor;
    private readonly IBoxReader _boxes;
    private readonly IRunContext _runs;
    private readonly ILogger<NicknameVoteService> _logger;
    private readonly PermaLocke.GameLink.LiveBoxRenamer _boxRenamer;
    private readonly PermaLocke.GameLink.Field.FieldZoneReader _field;

    /// <summary>Encryption constant of each Pokémon offered this session, by PID: what finds it in a box in memory.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, uint> _encryption = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly HashSet<uint> _asked = [];
    private readonly HashSet<long> _followed = [];
    private readonly HashSet<long> _waitingSaid = [];
    private readonly Queue<NicknameVoteViewModel> _queue = new();
    private NicknameVoteWindow? _window;
    private bool _reading;
    private bool _writes;
    private string? _lastProblem;

    public NicknameVoteService(DiscordLogin discord, PokemonSpriteService sprites, Notifier notifier, RenameService rename,
        GameLinkMonitor monitor, IBoxReader boxes, IRunContext runs, PermaLocke.GameLink.LiveBoxRenamer boxRenamer,
        PermaLocke.GameLink.Field.FieldZoneReader field,
        ILogger<NicknameVoteService> logger)
    {
        _boxRenamer = boxRenamer;
        _field = field;
        _discord = discord;
        _sprites = sprites;
        _notifier = notifier;
        _rename = rename;
        _monitor = monitor;
        _boxes = boxes;
        _runs = runs;
        _logger = logger;
        _timer.Tick += (_, _) => _ = ReadAsync();
    }

    /// <summary>
    /// CONFIGURACIÓN › «Motes entre todos» (1.0.7.2). Off: no question when capturing and no windows for the others' votes.
    /// A vote already won is still written into this player's Pokémon.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <param name="writes">False for the read-only copy (<c>--sin-juego</c>): it never opens a vote nor renames.</param>
    public void Start(bool writes)
    {
        _writes = writes;
        _timer.Start();
    }

    /// <summary>A Pokémon of this player's just arrived: ask whether the others name it. Called from any thread.</summary>
    public void Offer(PKHeX.Core.PK7? pokemon)
    {
        if (!Enabled || !_writes || _discord.Saved is null || pokemon is null || pokemon.IsEgg) return;

        // Una captura salvaje llega dos veces (la captura y el registro en el equipo): una pregunta por PID.
        lock (_asked)
        {
            if (!_asked.Add(pokemon.PID)) return;
        }

        _encryption[pokemon.PID] = pokemon.EncryptionConstant;

        _ = Task.Run(async () =>
        {
            await WaitOutOfMenusAsync();
            await _timer.Dispatcher.InvokeAsync(() => AskAsync(pokemon)).Task.Unwrap();
        });
    }

    private static readonly TimeSpan MenusAtMost = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Until the player takes a step (2026-09-28, asked by the organiser): after a capture come the nickname prompt, party or
    /// PC, the summary and back, and none of them moves the player, so the first step means they are all closed. Looking
    /// at the summary and going back does not count. Gives up after <see cref="MenusAtMost"/> for someone standing still.
    /// </summary>
    private async Task WaitOutOfMenusAsync()
    {
        var since = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - since < MenusAtMost)
        {
            await Task.Delay(500);
            try
            {
                if (_field.MovedSince(since)) return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No se pudo mirar si el jugador se ha movido");
            }
        }
    }

    private async Task AskAsync(PKHeX.Core.PK7 pokemon)
    {
        {
            await _sprites.PrepareAsync();
            var ask = new NicknameVoteViewModel(NicknameStage.Ask, pokemon.Nickname,
                _sprites.Get(pokemon.Species, pokemon.Form, pokemon.IsShiny), DateTimeOffset.Now + AskFor);
            // Aparte de la cola y arriba: la pregunta es solo de quien captura y no debe esperar ni tapar las votaciones.
            var window = new NicknameVoteWindow(ask) { AtTop = true };
            ask.Answered += (_, answer) =>
            {
                if (answer is true) _ = OpenAsync(pokemon);
                window.Close();
            };
            ask.Expired += (_, _) => window.Close();
            window.Show();
        }
    }

    /// <summary>The four windows one after another with made-up names, for <c>--ensayar-mote</c>. Nothing is sent.</summary>
    public async Task RehearseAsync()
    {
        await _sprites.PrepareAsync();
        var sprite = _sprites.Get(744, 0, false);
        var soon = DateTimeOffset.Now;
        Show(new NicknameVoteViewModel(NicknameStage.Ask, "Rockruff", sprite, soon.AddSeconds(15)));
        Show(new NicknameVoteViewModel(NicknameStage.Propose, "Rockruff", sprite, soon.AddSeconds(30), "Iñigo"));
        var vote = new NicknameVoteViewModel(NicknameStage.Vote, "Rockruff", sprite, soon.AddSeconds(45), "Iñigo", ["Firulais", "Toby", "Croqueta"]);
        vote.SetCounts(new Dictionary<string, int> { ["FIRULAIS"] = 1, ["CROQUETA"] = 2 });
        Show(vote);
        Show(new NicknameVoteViewModel(NicknameStage.Result, "Rockruff", sprite, soon.AddSeconds(53), "Iñigo", result: "Croqueta"));
    }

    private void Show(NicknameVoteViewModel model)
    {
        model.Expired += (_, _) => Next();
        _queue.Enqueue(model);
        if (_window is null) Next();
    }

    /// <summary>Closes the window on screen and opens the next one still in time.</summary>
    private void Next()
    {
        _window?.Close();
        _window = null;

        while (_queue.TryDequeue(out var model))
        {
            if (model.Ends <= DateTimeOffset.Now) continue;

            _window = new NicknameVoteWindow(model);
            _window.Show();
            return;
        }
    }

    private async Task OpenAsync(PKHeX.Core.PK7 pokemon)
    {
        try
        {
            await _discord.PostAsync("motes", JsonSerializer.Serialize(new
            {
                nombre = _discord.Saved?.Name ?? "Jugador",
                pokemon = pokemon.Nickname,
                especie = (int)pokemon.Species,
                forma = (int)pokemon.Form,
                shiny = pokemon.IsShiny,
                pid = pokemon.PID.ToString("X8")
            }));
            _notifier.Say(ToastKind.Info, $"Tus amigos van a elegir el mote de {pokemon.Nickname}",
                "Si hay otra votación en marcha, la tuya va justo detrás.", _sprites.Get(pokemon.Species, pokemon.Form, pokemon.IsShiny));
            _logger.LogInformation("Votación de mote abierta para {Pokemon} ({Pid:X8})", pokemon.Nickname, pokemon.PID);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo abrir la votación del mote de {Pokemon}", pokemon.Nickname);
        }
    }

    private sealed record VoteRow(long Id, Guid Jugador, string Nombre, string Pokemon, int Especie, int Forma, bool Shiny,
        string Pid, DateTimeOffset Creado, DateTimeOffset Propuestas_Hasta, DateTimeOffset Cierra, DateTimeOffset? Empieza = null);

    private sealed record BallotRow(long Mote, Guid Jugador, string Nombre, string? Propuesta, string? Voto, DateTimeOffset Creado);

    private const string Columns = "id,jugador,nombre,pokemon,especie,forma,shiny,pid,creado,propuestas_hasta,cierra,empieza";

    /// <summary>
    /// Every screen opens the proposals this long after the vote was created, by the server's clock: long enough for
    /// every app to have read it (they read every second), so they all open at the same moment.
    /// </summary>
    private static readonly TimeSpan CommonStart = TimeSpan.FromSeconds(3);

    /// <summary>The vote window on screen, to keep its counts live.</summary>
    private (long Id, NicknameVoteViewModel Model)? _counting;

    private string Stamp(DateTimeOffset at) => (at.UtcDateTime + _discord.ServerOffset).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private async Task ReadAsync()
    {
        if (_reading || _discord.Saved is not { } me) return;
        _reading = true;

        try
        {
            if (Enabled)
            {
                await FollowOthersAsync(me.UserId);
                await CountAsync();
            }
            if (_writes) await FinishMineAsync(me.UserId);
            _lastProblem = null;
        }
        catch (Exception ex)
        {
            if (ex.Message != _lastProblem)
            {
                _lastProblem = ex.Message;
                _logger.LogWarning(ex, "No se pudieron leer las votaciones de motes");
            }
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>The others' votes still running: each one is followed once, on this PC's clock turned into the server's.</summary>
    private async Task FollowOthersAsync(Guid me)
    {
        var json = await _discord.GetAsync(
            $"motes?select={Columns}&cierra=gt.{Stamp(DateTimeOffset.Now)}&order=id.asc&limit=10");

        foreach (var row in JsonSerializer.Deserialize<List<VoteRow>>(json ?? "[]", Json) ?? [])
        {
            if (_followed.Add(row.Id)) _ = FollowAsync(row, row.Jugador == me);
        }
    }

    /// <summary>
    /// One vote, at the same moments on every screen: the proposals open <see cref="CommonStart"/> after it was created,
    /// the vote when the proposals close, the winner when the vote closes; all three by the server's clock.
    /// </summary>
    /// <param name="mine">
    /// The catcher's own vote (1.0.7.3): the same windows at the same moments, only to watch (no writing, no voting), so the
    /// winner comes up for them at the same time as for everyone else.
    /// </param>
    private async Task FollowAsync(VoteRow row, bool mine)
    {
        // La cola la ordena el servidor (20-motes.sql): cada votación trae cuándo empieza.
        var start = _discord.ToLocal(row.Empieza ?? row.Creado + CommonStart);
        var proposalsEnd = _discord.ToLocal(row.Propuestas_Hasta);
        var close = _discord.ToLocal(row.Cierra);
        var sprite = _sprites.Get(row.Especie, row.Forma, row.Shiny);

        try
        {
            await Until(start);
            if (DateTimeOffset.Now < proposalsEnd)
            {
                var propose = new NicknameVoteViewModel(NicknameStage.Propose, row.Pokemon, sprite, proposalsEnd, row.Nombre, starts: start,
                    watching: mine);
                if (!mine) propose.Answered += (_, name) => _ = BallotAsync(row.Id, "propuesta", (string)name);
                Show(propose);
            }

            // Un poco después del cierre, para que la última propuesta haya llegado al servidor.
            await Until(proposalsEnd.AddMilliseconds(400));
            var options = NicknameVote.Options(await BallotsAsync(row.Id));
            if (options.Count == 0)
            {
                // Nadie propuso nada: se dice, y el Pokémon se queda con su nombre.
                Show(new NicknameVoteViewModel(NicknameStage.Result, row.Pokemon, sprite, proposalsEnd + ResultFor, mine ? "" : row.Nombre,
                    starts: proposalsEnd));
                return;
            }

            if (DateTimeOffset.Now < close)
            {
                var vote = new NicknameVoteViewModel(NicknameStage.Vote, row.Pokemon, sprite, close, row.Nombre, options, starts: proposalsEnd,
                    watching: mine);
                if (!mine) vote.Answered += (_, name) => _ = BallotAsync(row.Id, "voto", (string)name);
                _counting = (row.Id, vote);
                Show(vote);
            }

            await Until(close.AddMilliseconds(400));
            _counting = null;
            if (NicknameVote.Winner(await BallotsAsync(row.Id)) is { } winner)
            {
                Show(new NicknameVoteViewModel(NicknameStage.Result, row.Pokemon, sprite, close + ResultFor, mine ? "" : row.Nombre,
                    result: winner, starts: close));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló seguir la votación del mote de {Pokemon}", row.Pokemon);
        }
    }

    private static Task Until(DateTimeOffset moment)
    {
        var wait = moment - DateTimeOffset.Now;
        return wait > TimeSpan.Zero ? Task.Delay(wait) : Task.CompletedTask;
    }

    /// <summary>The votes of the vote on screen, every second.</summary>
    private async Task CountAsync()
    {
        if (_counting is not { } counting) return;

        var counts = (await BallotsAsync(counting.Id))
            .Where(b => !string.IsNullOrWhiteSpace(b.Vote))
            .GroupBy(b => b.Vote!.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Count());
        counting.Model.SetCounts(counts);
    }

    /// <summary>Writes this player's proposal or vote; the row is shared, so one never erases the other.</summary>
    private async Task BallotAsync(long id, string field, string name)
    {
        try
        {
            var row = new Dictionary<string, object> { ["mote"] = id, ["nombre"] = _discord.Saved?.Name ?? "Jugador", [field] = name };
            await _discord.PostAsync("votos_mote?on_conflict=mote,jugador", JsonSerializer.Serialize(row), "resolution=merge-duplicates");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo mandar el mote o el voto");
        }
    }

    private async Task<List<NicknameBallot>> BallotsAsync(long id) =>
        [
            .. (JsonSerializer.Deserialize<List<BallotRow>>(
                    await _discord.GetAsync($"votos_mote?select=mote,jugador,nombre,propuesta,voto,creado&mote=eq.{id}") ?? "[]", Json) ?? [])
                .Select(b => new NicknameBallot(b.Nombre, b.Propuesta, b.Voto, b.Creado))
        ];

    /// <summary>This player's closed votes not applied yet: the winner goes in, live if possible, else with the game closed.</summary>
    private async Task FinishMineAsync(Guid me)
    {
        if (_runs.Current is not { } run) return;

        var json = await _discord.GetAsync(
            $"motes?select={Columns}&jugador=eq.{me}&aplicado=is.false&cierra=lt.{Stamp(DateTimeOffset.Now)}&order=id.asc&limit=5");

        foreach (var row in JsonSerializer.Deserialize<List<VoteRow>>(json ?? "[]", Json) ?? [])
        {
            var ballots = await BallotsAsync(row.Id);
            var sprite = _sprites.Get(row.Especie, row.Forma, row.Shiny);

            if (NicknameVote.Winner(ballots) is not { } winner)
            {
                // Su ventana de espectador ya le dijo «SIN MOTE».
                await _discord.PatchAsync($"motes?id=eq.{row.Id}", """{"aplicado":true}""");
                continue;
            }

            var pid = uint.Parse(row.Pid, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            bool done;

            // En directo: en el equipo, o en una caja si se encuentra en la memoria del juego (1.0.7.3).
            if (await Task.Run(() => _monitor.RenameLive(pid, winner)
                    || (_encryption.TryGetValue(pid, out var ec) && _boxRenamer.Rename(pid, ec, winner))))
            {
                await _rename.RecordAsync(run, pid, row.Pokemon, winner, winner);
                done = true;
            }
            else if (_rename.CanRenameNow(out _)
                     && (await _boxes.ReadAsync()).Boxes.SelectMany(b => b.Pokemon).FirstOrDefault(p => p.Pid == pid) is { } target)
            {
                done = (await _rename.RenameAsync(run, target, winner)).Delivered;
            }
            else
            {
                if (_waitingSaid.Add(row.Id))
                {
                    _notifier.Say(ToastKind.Info, $"Tus amigos han llamado «{winner}» a {row.Pokemon}",
                        "No está en tu equipo: se pondrá cuando guardes y cierres el juego.", sprite);
                }

                continue;
            }

            if (!done) continue;

            // El ganador ya lo vio en su ventana, a la vez que los demás (1.0.7.3): aquí solo se confirma que está puesto.
            await _discord.PatchAsync($"motes?id=eq.{row.Id}", JsonSerializer.Serialize(new { aplicado = true, ganador = winner }));
            _notifier.Say(ToastKind.Reward, $"{row.Pokemon} ya se llama {winner}", "Puesto en tu partida.", sprite);
        }
    }
}
