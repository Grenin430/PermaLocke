using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The LUDÓPATA wheel.
/// </summary>
/// <remarks>
/// <para>
/// Like the gacha, and for the same reason: the spin is <b>decided, applied and recorded before a
/// single frame plays</b>. The animation shows a result that already exists in the save and in the
/// history; it never stands in for a pending computation, and closing the window mid-spin cannot
/// change what happened.
/// </para>
/// <para>
/// The wheel only turns when a milestone has paid for it. That number is not kept here: it is the
/// achievements minus the history, asked for fresh every time this screen opens.
/// </para>
/// </remarks>
public sealed partial class RouletteViewModel : SectionViewModel
{
    private readonly RouletteService _roulette;
    private readonly IRunContext _runContext;
    private readonly PokemonSpriteService _sprites;
    private readonly ILogger<RouletteViewModel> _logger;

    /// <summary>How long the card naming the result stays over the wheel.</summary>
    /// <remarks>
    /// It sits on top of the wheel, so leaving it there hides the thing the player came to look
    /// at. Eight seconds is enough to read three lines twice and short enough not to have to
    /// dismiss it. What it covered — the lines saying what actually changed — stays on screen: the
    /// card is the announcement, not the record.
    /// </remarks>
    private static readonly TimeSpan ResultShown = TimeSpan.FromSeconds(8);

    /// <summary>Cancels the countdown of the previous result when a new spin starts.</summary>
    private CancellationTokenSource? _hidingResult;

    /// <summary>
    /// Takes the result card away on its own after <see cref="ResultShown"/>.
    /// </summary>
    /// <remarks>
    /// Started rather than awaited, so the command finishes and the buttons come back at once
    /// instead of eight seconds later. It runs on the UI thread throughout — the continuation of a
    /// <c>Task.Delay</c> started here comes back to it — and a cancelled countdown is the normal
    /// way this ends, not a failure.
    /// </remarks>
    private void HideResultLater()
    {
        _hidingResult?.Cancel();
        _hidingResult?.Dispose();
        _hidingResult = new CancellationTokenSource();

        var token = _hidingResult.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ResultShown, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    HasResult = false;
                }
            });
        });
    }

    public RouletteViewModel(RouletteService roulette, IRunContext runContext,
        PokemonSpriteService sprites, ILogger<RouletteViewModel> logger)
        : base("RULETA", "Lo que la ruleta diga, va")
    {
        _roulette = roulette;
        _runContext = runContext;
        _sprites = sprites;
        _logger = logger;
    }

    /// <summary>
    /// The picture that stands for a face, out of the player's own cartridge.
    /// </summary>
    /// <remarks>
    /// Null when the face names no picture, or names an item whose icon nobody has measured. That
    /// is on purpose and it is why <c>ItemIconIndex</c> refuses to guess: a wedge with no drawing
    /// is obvious, a wedge with the wrong drawing is not. What keeps the shipped file honest is a
    /// test, not this method.
    /// </remarks>
    private System.Windows.Media.Imaging.BitmapSource? Sprite(RouletteFace face) =>
        face.SpeciesIcon > 0 ? _sprites.Get(face.SpeciesIcon)
        : face.ItemIcon > 0 ? _sprites.GetItem(face.ItemIcon)
        : null;

    /// <summary>
    /// Where the last spin left the wheel, so the next one starts from there instead of jumping. Nothing but looks
    /// hangs on it: the wedge that wins is decided by the service, and the wheel is only brought round to it.
    /// </summary>
    private double _restAngle;

    /// <summary>
    /// The spin the wheel is playing, with the moment it was confirmed. The wheel draws itself from it; it stays after
    /// the spin so the six faces and the winner remain on the wheel until the next one.
    /// </summary>
    [ObservableProperty]
    private RoulettePlay? _currentPlay;

    /// <summary>The sixteen faces for the prize board on the wall, in the catalogue's order.</summary>
    [ObservableProperty]
    private IReadOnlyList<Views.RouletteBoardItem> _board = [];

    /// <summary>What came out, line by line: who died, what changed, what arrived.</summary>
    public ObservableCollection<string> Lines { get; } = [];

    [ObservableProperty]
    private int _owed;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isSpinning;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _resultName = string.Empty;

    [ObservableProperty]
    private string _resultDetail = string.Empty;

    /// <summary>True when the face that won was one of the good ones, so the card can say so.</summary>
    [ObservableProperty]
    private bool _resultIsGood;

    public bool CanSpin => Owed > 0 && !IsSpinning;

    partial void OnOwedChanged(int value) => SpinCommand.NotifyCanExecuteChanged();

    partial void OnIsSpinningChanged(bool value) => SpinCommand.NotifyCanExecuteChanged();

    public override string IconKey => "IconWheel";

    public override GameNeed Needs => GameNeed.Closed;

    public override async Task ActivateAsync()
    {
        await _sprites.PrepareAsync();

        Board = [.. _roulette.Faces.Select(face => new Views.RouletteBoardItem(face.Id, face.Label, face.Figure, face.Good))];

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_runContext.Current is not { } run)
        {
            Owed = 0;
            Status = "No hay ninguna run abierta.";
            return;
        }

        try
        {
            var earned = await _roulette.EarnedAsync(run);
            var spun = await _roulette.SpunAsync(run.Id);
            Owed = Math.Max(0, earned - spun);

            Status = Owed > 0
                ? $"Debes {Owed} tirada{(Owed == 1 ? string.Empty : "s")}. "
                  + "Guarda y cierra Azahar para girar."
                : $"No debes ninguna tirada. Llevas {spun} de {earned}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al contar las tiradas de la ruleta");
            Status = "No se han podido contar las tiradas.";
        }
    }
    [RelayCommand(CanExecute = nameof(CanSpin))]
    private async Task SpinAsync()
    {
        if (_runContext.Current is not { } run)
        {
            return;
        }

        IsSpinning = true;

        // La cuenta atrás del resultado anterior se cancela aquí: si no, la de la tirada pasada
        // vencería a mitad de esta y borraría una tarjeta que se acaba de poner.
        _hidingResult?.Cancel();
        HasResult = false;
        Lines.Clear();

        try
        {
            var owedBefore = Owed;

            // Se decide, se escribe y se registra ANTES de animar nada. Lo que gire después es
            // presentación de algo que ya ha pasado.
            var result = await _roulette.SpinAsync(run);

            if (!result.Succeeded || result.Wheel is not { } wheel)
            {
                Status = result.Message;
                await RefreshAsync();
                return;
            }

            // Mientras gira, la línea de abajo no puede seguir diciendo lo que se debía antes de girar.
            Status = "La ruleta está girando…";

            // El espectáculo va aparte. Lo de debajo ya ha pasado -- está en la partida y en el
            // historial -- así que una animación rota puede costar la animación y nada más. Costó
            // dos tiradas aprenderlo: una excepción en el primer desvelado abortaba el método
            // entero y el jugador se quedaba sin saber qué le había tocado.
            try
            {
                await PlayAsync(wheel, Math.Max(owedBefore, result.Owed + 1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la animación de la ruleta; el resultado sí es válido");
            }

            ResultName = wheel.Winner.Name;
            ResultDetail = wheel.Winner.Detail;
            ResultIsGood = wheel.Winner.Good;

            foreach (var line in result.Lines)
            {
                Lines.Add(line);
            }

            HasResult = true;
            HideResultLater();
            Owed = result.Owed;
            Status = result.Owed > 0
                ? $"Te quedan {result.Owed} tirada{(result.Owed == 1 ? string.Empty : "s")}."
                : "No debes ninguna tirada más.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la tirada de la ruleta");
            Status = "Ha fallado la tirada.";
        }
        finally
        {
            IsSpinning = false;
        }
    }

    /// <summary>
    /// The show: a chip goes into the slot, the lever comes down, the six question marks turn over one by one, and
    /// then the wheel spins onto the winner.
    /// </summary>
    /// <remarks>
    /// Revealing before spinning is the point of it — the player finds out what is on the wheel, and only then what it
    /// lands on. The six can be six good ones or six bad ones; nothing here arranges them, they are the six the seed
    /// drew. The wheel draws all of it from <see cref="CurrentPlay"/>; this only waits until it has landed.
    /// </remarks>
    private async Task PlayAsync(RouletteWheel wheel, int owedBefore)
    {
        var play = new RoulettePlay(
            [.. wheel.Faces.Select(face => new RoulettePlayWedge(face.Label, face.Figure, face.Good, Sprite(face), face.Id))],
            wheel.WinningIndex,
            Views.WheelEnding.For(wheel.Seed, wheel.Number),
            _restAngle,
            unchecked((int)(wheel.Seed ^ (ulong)wheel.Number)),
            owedBefore,
            System.Diagnostics.Stopwatch.GetTimestamp());

        CurrentPlay = play;

        // La próxima tirada sale de donde para esta, y no de cero: si no, la rueda daría un salto al empezar.
        _restAngle = Views.RouletteTimeline.RestingAngle(wheel.WinningIndex);

        var left = Views.RouletteTimeline.Landed - play.Elapsed;
        if (left > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(left));
        }
    }

    /// <summary>The wheel could not draw; the spin is written and recorded all the same.</summary>
    public void AnimationFailed(Exception ex) =>
        _logger.LogError(ex, "Falló el dibujo de la ruleta; la tirada sí es válida");
}
