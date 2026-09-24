using System.ComponentModel;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;
using System.Text.Json;

namespace PermaLocke.App.Services;

/// <summary>
/// The gifts waiting for this player on the tournament server, and collecting them (§129; the shared folder is gone).
/// </summary>
/// <remarks>
/// <para>
/// It reads the folder and nothing else on its own: <b>nothing is applied until the player presses the button</b>.
/// That is the shape the player asked for — a gift in the corner of the window that fills up — and it is also the
/// honest one, because a gift can change the balance of a run and a run is somebody's work.
/// </para>
/// <para>
/// What is already collected comes from the run's own history, so reinstalling, restoring a copy of the shared folder
/// or reading the same gift on two machines cannot give it twice.
/// </para>
/// </remarks>
public sealed class GiftInbox : INotifyPropertyChanged
{
    private static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(20);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly DiscordLogin _discord;
    private readonly GiftService _gifts;
    private readonly IRunContext _runContext;
    private readonly ILogger<GiftInbox> _logger;
    private readonly Notifier _notifier;

    /// <summary>Gifts already announced over the game, so each one is said once per session.</summary>
    private readonly HashSet<Guid> _told = [];
    private readonly DispatcherTimer _timer = new() { Interval = LookEvery };
    private bool _busy;

    public GiftInbox(DiscordLogin discord, GiftService gifts, IRunContext runContext, Notifier notifier,
        ILogger<GiftInbox> logger)
    {
        _discord = discord;
        _gifts = gifts;
        _runContext = runContext;
        _logger = logger;
        _notifier = notifier;

        _timer.Tick += (_, _) => _ = LookAsync();
        _runContext.CurrentChanged += (_, _) => _ = LookAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>What is there to collect, newest first.</summary>
    public IReadOnlyList<AdminGift> Pending { get; private set; } = [];

    /// <summary>The last few collected, so the inbox is not empty the moment after pressing the button.</summary>
    public IReadOnlyList<AdminGift> Collected { get; private set; } = [];

    public void Start()
    {
        _timer.Start();
        _ = LookAsync();
    }

    /// <summary>Reads the folder and works out what is still to collect.</summary>
    public async Task LookAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            // El servidor ya filtra: a cada jugador solo le llegan los suyos y los de todos (08-regalos.sql).
            // El organizador ve todos los regalos (para retirarlos); como jugador solo cuentan los suyos y los de todos.
            var me = _discord.Saved?.UserId ?? Guid.Empty;

            if (_runContext.Current is not { } run
                || await _discord.GetAsync($"regalos?select=regalo&or=(para.eq.todos,para.eq.{me})&order=creado.desc") is not { } json)
            {
                Set([], []);
                return;
            }

            var mine = (JsonSerializer.Deserialize<List<JsonElement>>(json, Json) ?? [])
                .Select(row => row.GetProperty("regalo").Deserialize<AdminGift>(Json))
                .Where(gift => gift is not null && gift.Id != Guid.Empty && gift.Schema <= AdminGift.CurrentSchema)
                .Select(gift => gift!)
                .ToList();
            var collected = (await _gifts.CollectedAsync(run.Id)).ToHashSet();

            // Los ajustes del organizador no se recogen: se aplican solos, con su evento como cualquier regalo.
            foreach (var adjustment in mine.Where(gift => gift.Adjustment && !collected.Contains(gift.Id)))
            {
                var applied = await _gifts.ClaimAsync(run, adjustment);

                if (applied.Collected)
                {
                    collected.Add(adjustment.Id);
                    _told.Add(adjustment.Id);
                    _logger.LogInformation("Ajuste {Id} de {From} aplicado: {What}", adjustment.Id, adjustment.From, adjustment.Say());
                    _notifier.Say(ToastKind.Warning, $"Ajuste de {adjustment.From}", $"{adjustment.Say()}. {adjustment.Reason}");
                }
            }

            Set([.. mine.Where(gift => !gift.Adjustment && !collected.Contains(gift.Id))],
                [.. mine.Where(gift => collected.Contains(gift.Id)).Take(6)]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo leyendo los regalos del servidor del torneo");
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Collects one gift, and says what happened.</summary>
    public async Task<GiftResult> ClaimAsync(AdminGift gift)
    {
        if (_runContext.Current is not { } run)
        {
            return new GiftResult(false, "No hay ninguna run cargada.");
        }

        try
        {
            var result = await _gifts.ClaimAsync(run, gift);

            if (result.Collected)
            {
                _logger.LogInformation("Regalo {Id} de {From} recogido: {What}", gift.Id, gift.From, gift.Say());
            }

            await LookAsync();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló recoger el regalo {Id}", gift.Id);
            return new GiftResult(false, "No se ha podido recoger.");
        }
    }

    private void Set(IReadOnlyList<AdminGift> pending, IReadOnlyList<AdminGift> collected)
    {
        // Encima del juego también (2026-09-24): con la app minimizada, la bandeja no la ve nadie. Uno por regalo, y solo
        // la primera vez que se ve en esta sesión; al arrancar avisa de los que ya estaban esperando.
        foreach (var gift in pending.Where(gift => _told.Add(gift.Id)))
        {
            _notifier.Say(ToastKind.Gift, $"Regalo de {gift.From}", $"{gift.Say()}. Recógelo en PermaLocke, en el regalo de arriba.");
        }

        Pending = pending;
        Collected = collected;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
