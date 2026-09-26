using PermaLocke.App.Views;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Core.Services;

namespace PermaLocke.App.Services;

/// <summary>
/// A Pokémon as its TCG card: the same card in the ÁLBUM (§186) and flying into it when one is caught (§190).
/// </summary>
/// <remarks>
/// Out of <c>AlbumViewModel</c>, where it was born, so a card made at the moment of a capture cannot come out different
/// from the one the album shows later: same types, same figures, same rarity.
/// </remarks>
public sealed class TcgCardFactory(PokemonSpriteService sprites, ITypeLookup types, IMoveCatalog moves,
    IStatForecast forecast, GachaService gacha, ISpeciesStatsCatalog species)
{
    private Dictionary<int, int>? _stages;

    /// <summary>One Pokémon as its card.</summary>
    /// <param name="fallen">Dead in the run: the card comes out burnt.</param>
    /// <param name="pulledTier">The gacha tier it came out of, when it came out of the gacha; its species' tier otherwise.</param>
    public TcgCard Make(BoxedPokemon pokemon, bool fallen = false, int? pulledTier = null)
    {
        var fromGacha = pulledTier is not null;
        var sprite = RoomSprite.From(pokemon.IsEgg ? sprites.GetEgg() : sprites.Get(pokemon.Species, pokemon.Form, pokemon.IsShiny));

        if (pokemon.IsEgg)
        {
            return new TcgCard("Huevo", "Huevo", pokemon.Species, string.Empty, 0, 0, [], [], string.Empty, string.Empty,
                -1, -1, string.Empty, string.Empty, 0, -1, fromGacha, false, true, fallen, sprite,
                [], [], [], pokemon.Pid);
        }

        var pair = types.GetTypes(pokemon.Species, pokemon.Form);
        IReadOnlyList<int> kinds = pair.IsDual ? [pair.First, pair.Second] : [pair.First];

        // Las mismas cifras que el visor: en caja, las del mundo instalado; en el equipo, las que guarda el juego.
        var stats = (pokemon.IsInParty ? null : forecast.With(pokemon, pokemon.Evs)) ?? pokemon.Stats;

        var up = -1;
        var down = -1;
        for (var stat = 0; stat < 6; stat++)
        {
            var effect = forecast.NatureEffect(pokemon, stat);
            if (effect > 0) up = stat;
            if (effect < 0) down = stat;
        }

        var cardMoves = new List<TcgMove>();
        var ids = pokemon.MoveIds ?? [];
        for (var slot = 0; slot < 4; slot++)
        {
            var id = slot < ids.Count ? ids[slot] : 0;
            if (id != 0 && moves.Describe(id) is { } sheet)
            {
                cardMoves.Add(new TcgMove(sheet.Name, sheet.Type, sheet.Power, sheet.Accuracy, sheet.PP, sheet.CategoryName));
            }
            else if (id != 0 && slot < pokemon.Moves.Count && pokemon.Moves[slot].Length > 0)
            {
                cardMoves.Add(new TcgMove(pokemon.Moves[slot], -1, 0, 0, 0, string.Empty));
            }
        }

        var name = string.IsNullOrWhiteSpace(pokemon.Nickname) ? pokemon.SpeciesName : pokemon.Nickname;

        return new TcgCard(
            name,
            pokemon.SpeciesName,
            pokemon.Species,
            StageOf(pokemon.Species),
            pokemon.Level,
            stats.Count > 0 ? stats[0] : 0,
            kinds,
            cardMoves,
            pokemon.AbilityName,
            pokemon.NatureName,
            up,
            down,
            pokemon.HeldItemName,
            pokemon.MetLocationName,
            pokemon.MetLevel,
            pulledTier ?? gacha.TierIndexOf(pokemon.Species),
            fromGacha,
            pokemon.IsShiny,
            false,
            fallen,
            sprite,
            stats,
            pokemon.Ivs,
            pokemon.Evs,
            pokemon.Pid);
    }

    /// <summary>«BÁSICO», «FASE 1» or «FASE 2»: the rung of its family it stands on, from the cartridge's families.</summary>
    private string StageOf(int id)
    {
        if (_stages is null)
        {
            var stages = new Dictionary<int, int>();
            foreach (var line in species.Lines)
            {
                for (var stage = 0; stage < line.Stages.Count; stage++)
                {
                    foreach (var member in line.Stages[stage])
                    {
                        stages.TryAdd(member, stage);
                    }
                }
            }

            _stages = stages;
        }

        return _stages.GetValueOrDefault(id) switch
        {
            0 => "Básico",
            1 => "Fase 1",
            _ => "Fase 2"
        };
    }
}
