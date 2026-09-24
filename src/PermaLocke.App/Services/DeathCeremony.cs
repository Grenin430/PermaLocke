using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Views;

namespace PermaLocke.App.Services;

/// <summary>A Pokémon that has just died, as the ceremony needs it.</summary>
/// <param name="Name">Its nickname if it has one, its species otherwise.</param>
/// <param name="Penalty">What its death actually cost, which is zero for a role that loses nothing.</param>
public sealed record DeathNotice(string Name, int Species, int Penalty, int Form = 0);

/// <summary>One death of the ceremony, already resolved to what is drawn.</summary>
/// <param name="Flash">The sprite as a white silhouette, for the hit it takes before it faints.</param>
public sealed record DeathCard(string Title, string Points, BitmapSource? Sprite, BitmapSource? Flash,
    bool IsWipe);

/// <summary>
/// The «X HA MUERTO» over the game, one death after another.
/// </summary>
/// <remarks>
/// <para>
/// A notice in a corner was all a death got, and in a Nuzlocke it is the most dramatic thing that
/// happens. The game freezes on the frame and goes grey, the bars close in, the Pokémon takes a hit
/// and sinks out of sight the way a fainted Pokémon does in the games, and its name is typed across
/// the bottom.
/// </para>
/// <para>
/// <b>One card per death, in the order they happened</b>, at the player's request. Deaths are
/// usually read when a battle ends, all at once, and a single «three Pokémon have died» would lose
/// exactly what the screen is for. The scene opens once, the deaths go through it one after the
/// other, and a team wipe is the last card. A death that arrives while the scene is closing opens
/// it again.
/// </para>
/// <para>
/// Nothing here decides or records anything. It is told about deaths that are already written, and
/// a failure in it costs the animation and nothing else.
/// </para>
/// </remarks>
public sealed class DeathCeremony(IUiDispatcher ui, PokemonSpriteService sprites, KillcamRecorder killcam,
    ILogger<DeathCeremony> logger)
{
    /// <summary>Cards waiting their turn. Only touched on the UI thread.</summary>
    private readonly Queue<DeathCard> _waiting = new();

    private bool _playing;
    private DeathWindow? _window;

    /// <summary>Plays one death. Safe to call from any thread.</summary>
    /// <summary>Off in CONFIGURACIÓN: the death is recorded all the same, only the scene is not shown.</summary>
    public bool Enabled { get; set; } = true;

    public void Mourn(DeathNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);

        if (!Enabled)
        {
            return;
        }

        try
        {
            var sprite = sprites.Get(notice.Species, notice.Form);

            Enqueue(new DeathCard(
                $"{notice.Name.ToUpperInvariant()} HA MUERTO",
                Points(notice.Penalty),
                sprite,
                sprite is null ? null : White(sprite),
                IsWipe: false));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo preparar la animación de la muerte de {Pokemon}", notice.Name);
        }
    }

    /// <summary>The last card of a wipe, after the deaths that made it.</summary>
    public void TeamFell(int penalty)
    {
        if (Enabled)
        {
            Enqueue(new DeathCard("EQUIPO CAÍDO", Points(penalty), null, null, IsWipe: true));
        }
    }

    private static string Points(int penalty) => penalty > 0 ? $"−{penalty} PUNTOS" : string.Empty;

    private void Enqueue(DeathCard card)
    {
        _ = ui.InvokeAsync(async () =>
        {
            _waiting.Enqueue(card);

            // Una sola escena a la vez: si ya hay una en marcha, esta tarjeta espera su turno en la
            // cola y la recoge el mismo bucle sin volver a abrir.
            if (!_playing)
            {
                await PlayAllAsync();
            }
        });
    }

    private async Task PlayAllAsync()
    {
        _playing = true;

        // La escena tapa el juego y la killcam copia la pantalla: mientras esté, no se graba nada.
        killcam.CoverBegins();

        try
        {
            while (_waiting.Count > 0)
            {
                _window ??= new DeathWindow();
                await _window.OpenAsync();

                while (_waiting.Count > 0)
                {
                    await _window.PlayAsync(_waiting.Dequeue());
                }

                await _window.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la animación de muerte; las muertes sí están registradas");
            _waiting.Clear();
        }
        finally
        {
            _playing = false;
            _window?.Hide();
            killcam.CoverEnds();
        }
    }

    /// <summary>
    /// The sprite as a flat white shape, transparency kept: the flash of a Pokémon taking a hit.
    /// </summary>
    private static BitmapSource White(BitmapSource source)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var width = bgra.PixelWidth;
        var height = bgra.PixelHeight;
        var pixels = new byte[width * height * 4];

        bgra.CopyPixels(pixels, width * 4, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
            pixels[i + 1] = 255;
            pixels[i + 2] = 255;
        }

        var white = BitmapSource.Create(width, height, bgra.DpiX, bgra.DpiY, PixelFormats.Bgra32, null,
            pixels, width * 4);

        white.Freeze();
        return white;
    }
}
