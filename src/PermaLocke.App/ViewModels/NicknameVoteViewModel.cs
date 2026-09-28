using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PermaLocke.Core.Services;

namespace PermaLocke.App.ViewModels;

/// <summary>The four moments of a voted nickname, each one small window over the emulator.</summary>
public enum NicknameStage
{
    /// <summary>To the catcher: do the others name it?</summary>
    Ask,

    /// <summary>To the others: write a name.</summary>
    Propose,

    /// <summary>To the others: pick one of the names.</summary>
    Vote,

    /// <summary>To everyone: the name that won.</summary>
    Result
}

/// <summary>One name to vote for.</summary>
public sealed partial class NicknameOption(string name, IRelayCommand pick) : ObservableObject
{
    public string Name { get; } = name;

    public IRelayCommand Pick { get; } = pick;

    [ObservableProperty]
    private bool _isChosen;

    /// <summary>How many voted it so far, read from the server every second.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count))]
    private int _votes;

    public string Count => Votes == 1 ? "1 VOTO" : $"{Votes} VOTOS";
}

/// <summary>
/// The voted nickname window (2026-09-28, players' list item 9, as the organiser described it): small, in a corner of the
/// emulator, a 15-second bar, and gone when the bar runs out. What the answers do is <see cref="Services.NicknameVoteService"/>'s.
/// </summary>
public sealed partial class NicknameVoteViewModel : ObservableObject
{
    public NicknameVoteViewModel(NicknameStage stage, string pokemon, BitmapSource? sprite, DateTimeOffset ends,
        string owner = "", IReadOnlyList<string>? options = null, string result = "", DateTimeOffset? starts = null)
    {
        Stage = stage;
        Pokemon = pokemon.ToUpperInvariant();
        Sprite = sprite;
        Ends = ends;
        Total = starts is { } from ? Math.Max(1, (ends - from).TotalSeconds) : stage == NicknameStage.Result ? 8 : 15;
        Owner = owner;
        Result = result;
        Options = [.. (options ?? []).Select(name => new NicknameOption(name, new RelayCommand(() => Choose(name))))];

        (Caption, Heading) = stage switch
        {
            NicknameStage.Ask => ("MOTE", "¿Quieres que los otros pongan el mote a este Pokémon?"),
            NicknameStage.Propose => ("MOTE", $"{owner} quiere ponerle un mote a"),
            NicknameStage.Vote => ("VOTACIÓN", $"Vota el mote de {owner} para"),
            _ => ("¡MOTE ELEGIDO!", owner.Length == 0 ? "Tus amigos han elegido el mote de" : $"Así se llama el Pokémon de {owner}:")
        };
    }

    public NicknameStage Stage { get; }

    public bool IsAsk => Stage == NicknameStage.Ask;

    public bool IsPropose => Stage == NicknameStage.Propose;

    public bool IsVote => Stage == NicknameStage.Vote;

    public bool IsResult => Stage == NicknameStage.Result;

    public string Caption { get; }

    public string Heading { get; }

    public string Pokemon { get; }

    public string Owner { get; }

    public string Result { get; }

    public BitmapSource? Sprite { get; }

    public DateTimeOffset Ends { get; }

    private double Total { get; }

    public IReadOnlyList<NicknameOption> Options { get; }

    public bool HasNoOptions => Options.Count == 0;

    public int MaxLength => RenameService.MaxLength;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _proposal = string.Empty;

    [ObservableProperty]
    private string _problem = string.Empty;

    /// <summary>What is left of the bar, 1 to 0.</summary>
    [ObservableProperty]
    private double _left = 1;

    [ObservableProperty]
    private string _seconds = string.Empty;

    /// <summary>The last seconds, when the bar turns red.</summary>
    [ObservableProperty]
    private bool _hurry;

    /// <summary>After sending or voting: the window thanks and waits for the bar.</summary>
    [ObservableProperty]
    private string _done = string.Empty;

    /// <summary>The player's answer: true/false to Ask, the name to Propose or Vote. Raised once per answer.</summary>
    public event EventHandler<object>? Answered;

    /// <summary>The bar ran out.</summary>
    public event EventHandler? Expired;

    private bool _expired;

    /// <summary>The live count of each option.</summary>
    public void SetCounts(IReadOnlyDictionary<string, int> counts)
    {
        foreach (var option in Options) option.Votes = counts.GetValueOrDefault(option.Name.ToUpperInvariant());
    }

    public void Tick(DateTimeOffset now)
    {
        var left = (Ends - now).TotalSeconds;
        Left = Math.Clamp(left / Total, 0, 1);
        Seconds = $"{Math.Max(0, (int)Math.Ceiling(left))} s";
        Hurry = left <= 5;

        if (left <= 0 && !_expired)
        {
            _expired = true;
            Expired?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Yes() => Answered?.Invoke(this, true);

    [RelayCommand]
    private void No() => Answered?.Invoke(this, false);

    /// <summary>Characters used, out of what the game takes.</summary>
    public string Counter => $"{Proposal.Trim().Length}/{RenameService.MaxLength}";

    /// <summary>As the player types: what the game would not take, before sending.</summary>
    partial void OnProposalChanged(string value)
    {
        Problem = RenameService.Problem(value) ?? string.Empty;
        OnPropertyChanged(nameof(Counter));
    }

    private bool CanSend() => Proposal.Trim().Length > 0 && Done.Length == 0 && RenameService.Problem(Proposal) is null;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private void Send()
    {
        if (RenameService.Problem(Proposal) is { } problem)
        {
            Problem = problem;
            return;
        }

        Problem = string.Empty;
        Done = "¡ENVIADO! Luego se vota.";
        SendCommand.NotifyCanExecuteChanged();
        Answered?.Invoke(this, Proposal.Trim());
    }

    /// <summary>True once voted: the vote cannot be changed (the organiser asked), and the server refuses it too.</summary>
    [ObservableProperty]
    private bool _hasVoted;

    private void Choose(string name)
    {
        if (HasVoted) return;

        HasVoted = true;
        foreach (var option in Options) option.IsChosen = option.Name == name;
        Done = "¡VOTO GUARDADO!";
        Answered?.Invoke(this, name);
    }
}
