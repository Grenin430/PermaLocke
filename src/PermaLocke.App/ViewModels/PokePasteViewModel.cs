using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Services;
using PermaLocke.GameLink;

namespace PermaLocke.App.ViewModels;

/// <summary>What can be exported. Today only the party, which is what a paste is for.</summary>
public sealed record PasteSourceViewModel(string Label, int Number, int Count);

/// <summary>
/// Exports the player's Pokémon as text for <c>pokepast.es</c>.
/// </summary>
/// <remarks>
/// <para>
/// It reads the save with a <b>second reader set to English</b>. The rest of PermaLocke resolves
/// names in Spanish because that is what the player reads, but the paste site keys off the English
/// ones: a paste full of «Bola Sombra» and «Miedosa» looks fine and imports as nothing.
/// </para>
/// <para>
/// Export only. Reading a paste back in would mean creating Pokémon from text, and where a
/// Pokémon came from is the one thing a Nuzlocke cannot be careless about.
/// </para>
/// </remarks>
public sealed partial class PokePasteViewModel : SectionViewModel
{
    private readonly SaveBoxReader _english;
    private readonly ILogger<PokePasteViewModel> _logger;

    private BoxSnapshot? _snapshot;

    public PokePasteViewModel(PlayerSave save, ILocationLookup locations,
        ILogger<PokePasteViewModel> logger) : base("POKE PASTE")
    {
        _logger = logger;

        _english = new SaveBoxReader(save, locations, "en", NullLogger<SaveBoxReader>.Instance);
    }

    /// <summary>
    /// What can be exported: the party, and nothing else.
    /// </summary>
    /// <remarks>
    /// The boxes used to be here too and are not any more. A paste is a <em>team</em> — six
    /// Pokémon somebody else can load and battle with — and a box of thirty is not a team; the PC
    /// is what the viewer is for.
    /// </remarks>
    public ObservableCollection<PasteSourceViewModel> Sources { get; } = [];

    [ObservableProperty]
    private PasteSourceViewModel? _selectedSource;

    [ObservableProperty]
    private string _paste = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public override Task ActivateAsync() => LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;

        try
        {
            _snapshot = await _english.ReadAsync();

            Sources.Clear();

            if (!_snapshot.Available)
            {
                Paste = string.Empty;
                Status = _snapshot.Problem ?? "No se ha podido leer la partida.";
                return;
            }

            foreach (var box in _snapshot.Boxes.Where(box => box.IsParty))
            {
                Sources.Add(new PasteSourceViewModel("Equipo", box.Number, box.Count));
            }

            SelectedSource = Sources.FirstOrDefault();

            Status = Sources.Count == 0
                ? "No hay equipo en la partida guardada."
                : _snapshot.Notice ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al preparar el Poke Paste");
            Status = "No se ha podido leer la partida. El detalle está en la carpeta Logs.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSourceChanged(PasteSourceViewModel? value) => Build(value);

    private void Build(PasteSourceViewModel? source)
    {
        if (source is null || _snapshot is null)
        {
            Paste = string.Empty;
            return;
        }

        var box = _snapshot.Boxes.FirstOrDefault(b => b.Number == source.Number);

        Paste = box is null
            ? string.Empty
            : PokePasteFormatter.Write(box.Pokemon.OrderBy(p => p.Slot));
    }

    /// <summary>
    /// Puts the paste on the clipboard.
    /// </summary>
    /// <remarks>
    /// The clipboard is owned by whatever else is running, so a copy can genuinely fail. It is
    /// reported rather than swallowed: silently not copying is worse than saying so, because the
    /// player pastes an old clipboard into the site and blames the site.
    /// </remarks>
    [RelayCommand]
    private void Copy()
    {
        if (string.IsNullOrWhiteSpace(Paste))
        {
            return;
        }

        try
        {
            Clipboard.SetText(Paste);
            Status = $"Copiado. Pégalo en pokepast.es ({Paste.Split('\n').Length} líneas).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo copiar al portapapeles");
            Status = "Windows no ha dejado copiar al portapapeles. Selecciona el texto y cópialo a mano.";
        }
    }
}
