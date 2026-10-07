using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.App.ViewModels;

/// <summary>
/// The role menu of the run creation and of the role change (§220): the ordinary roles as cards, and ONE card for MONOTYPE
/// that opens a second menu with its types, each with an icon, before the role is settled.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue has one role per type (<c>monotype_agua</c>, <c>monotype_fuego</c>...), because that is what the rest of the
/// program needs: an id with its own points and its own type. The menu is only the way to reach them, so a player does not
/// face twelve cards of which eight are the same role: <see cref="SelectedRoleId"/> is empty while MONOTYPE is ticked
/// and no type has been chosen, which is what keeps the button of the window off.
/// </para>
/// <para>
/// The icons are the player's own sprites, read from their ROM like everywhere else, so there is nothing to ship; without
/// ROM the type is the coloured plate and its name.
/// </para>
/// </remarks>
public sealed partial class RoleMenuViewModel : ObservableObject
{
    private readonly RoleChoiceViewModel? _monotype;
    private readonly PokemonSpriteService? _sprites;

    public RoleMenuViewModel(IReadOnlyList<Role> roles, PokemonSpriteService? sprites = null)
    {
        _sprites = sprites;

        foreach (var role in roles.Where(r => !r.IsMonotype))
        {
            Cards.Add(new RoleChoiceViewModel(role, OnCardChosen));
        }

        var typed = roles.Where(r => r.IsMonotype).ToList();

        if (typed.Count > 0)
        {
            // Los números de la tarjeta son los de los roles de tipo (iguales entre ellos); el texto, el de la tarjeta.
            var first = typed[0];
            var group = first with
            {
                Id = string.Empty,
                Name = "MONOTYPE",
                Summary = "Un solo tipo a tu elección.",
                Description = "Juegas con un único tipo, que eliges en el menú de abajo. Solo puedes tener Pokémon de ese tipo "
                              + "(solo o con un segundo tipo); el gacha y el wonder trade solo te dan de ese tipo, y el gacha "
                              + "no se deja usar mientras tengas uno que no lo sea (lo sueltas). Tu guardería te da huevos de tu tipo con cada prueba.",
                MonoType = null,
                IconSpecies = null
            };

            _monotype = new RoleChoiceViewModel(group, OnCardChosen, isGroup: true);
            Cards.Add(_monotype);

            foreach (var role in typed)
            {
                Types.Add(new TypeChoiceViewModel(role, OnTypeChosen));
            }
        }

        _ = LoadSpritesAsync();
    }

    /// <summary>The cards: every role that is not of a type, and MONOTYPE as one more.</summary>
    public ObservableCollection<RoleChoiceViewModel> Cards { get; } = [];

    /// <summary>The types MONOTYPE can be, one per role of the catalogue that has one.</summary>
    public ObservableCollection<TypeChoiceViewModel> Types { get; } = [];

    /// <summary>The role that is settled, or empty when there is none yet (nothing ticked, or MONOTYPE without a type).</summary>
    [ObservableProperty]
    private string _selectedRoleId = string.Empty;

    /// <summary>True while MONOTYPE is ticked: the second menu is on screen.</summary>
    [ObservableProperty]
    private bool _showTypes;

    /// <summary>Raised whenever <see cref="SelectedRoleId"/> changes, so the window's own button can follow it.</summary>
    public event EventHandler? Changed;

    /// <summary>Ticks what a role id stands for: its card, and its type when it has one. For the role a run already has.</summary>
    public void Select(string? roleId)
    {
        var type = Types.FirstOrDefault(t => string.Equals(t.Role.Id, roleId, StringComparison.OrdinalIgnoreCase));

        if (type is not null && _monotype is not null)
        {
            _monotype.IsSelected = true;
            type.IsSelected = true;
            return;
        }

        var card = Cards.FirstOrDefault(c => !c.IsGroup && string.Equals(c.Role.Id, roleId, StringComparison.OrdinalIgnoreCase));

        if (card is not null)
        {
            card.IsSelected = true;
        }
    }

    private void OnCardChosen(RoleChoiceViewModel chosen)
    {
        foreach (var other in Cards.Where(c => !ReferenceEquals(c, chosen)))
        {
            other.Clear();
        }

        ShowTypes = chosen.IsGroup;

        if (chosen.IsGroup)
        {
            // Sin tipo elegido no hay rol: el botón de la ventana se queda apagado hasta que se elija uno.
            SelectedRoleId = Types.FirstOrDefault(t => t.IsSelected)?.Role.Id ?? string.Empty;
        }
        else
        {
            foreach (var type in Types)
            {
                type.Clear();
            }

            SelectedRoleId = chosen.Role.Id;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTypeChosen(TypeChoiceViewModel chosen)
    {
        foreach (var other in Types.Where(t => !ReferenceEquals(t, chosen)))
        {
            other.Clear();
        }

        SelectedRoleId = chosen.Role.Id;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The icons of the types, once the sprites are ready. Without them the menu works the same.</summary>
    private async Task LoadSpritesAsync()
    {
        if (_sprites is null || Types.Count == 0)
        {
            return;
        }

        try
        {
            await _sprites.PrepareAsync();

            foreach (var type in Types)
            {
                if (type.Role.IconSpecies is { } species)
                {
                    type.Sprite = _sprites.Get(species);
                }
            }
        }
        catch (Exception)
        {
            // Los sprites son decoración: sin ellos el tipo sigue siendo su placa y su nombre.
        }
    }
}

/// <summary>One type of the MONOTYPE menu: its plate, its icon and the role behind it.</summary>
public sealed partial class TypeChoiceViewModel(Role role, Action<TypeChoiceViewModel> chosen) : ObservableObject
{
    public Role Role { get; } = role;

    public string Name => Role.MonoTypeName.ToUpperInvariant();

    /// <summary>The type's own colour, a step darker so the white letters read on the light ones.</summary>
    public Color Colour { get; } = TypeBadges.Colour(role.MonoType ?? -1);

    [ObservableProperty]
    private BitmapSource? _sprite;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            chosen(this);
        }
    }

    public void Clear() => IsSelected = false;
}
