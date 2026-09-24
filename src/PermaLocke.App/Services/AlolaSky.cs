using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PermaLocke.GameLink;
using PermaLocke.GameLink.Clock;

namespace PermaLocke.App.Services;

/// <summary>
/// The hour in the player's Alola, for the sky behind the application.
/// </summary>
/// <remarks>
/// <para>
/// Presentation only: it decides nothing and records nothing. The hour comes from <see cref="AlolaClock"/>,
/// which reads Azahar's clock settings and nothing of the game, and is looked at again every
/// <see cref="Every"/> — the sky moves by the minute, not by the frame.
/// </para>
/// <para>
/// The settings are those of the emulator that is <b>running</b>, found by Azahar's own rule: a <c>user</c>
/// folder next to its executable makes it portable, otherwise it is <c>%APPDATA%\Azahar</c>. The player
/// opens a different Azahar from the one PermaLocke ships (§95), so asking the bundled one would be
/// reading somebody else's clock. With no emulator open, the one PermaLocke would use.
/// </para>
/// </remarks>
public sealed partial class AlolaSky : ObservableObject
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(20);

    private readonly AzaharInstallation _installation;
    private readonly DispatcherTimer _timer = new() { Interval = Every };
    private string? _fallbackConfig;

    public AlolaSky(AzaharInstallation installation)
    {
        _installation = installation;
        _timer.Tick += (_, _) => Update();
    }

    /// <summary>The game's hour as a fraction, 0 to 24.</summary>
    [ObservableProperty]
    private double _hour;

    /// <summary>False when Azahar keeps a fixed clock and the game's hour cannot be known.</summary>
    [ObservableProperty]
    private bool _isKnown;

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private string _periodText = string.Empty;

    /// <summary>
    /// A time of day to show instead of the real one, for looking at the sky at any hour without waiting for
    /// it (<c>--hora-alola</c>). Never set in normal use.
    /// </summary>
    public TimeOnly? Rehearsal { get; set; }

    public void Start()
    {
        Update();
        _timer.Start();
    }

    private void Update()
    {
        DateTime? game = Rehearsal is { } forced
            ? DateTime.Today + forced.ToTimeSpan()
            : AlolaClock.GameTime(DateTime.Now, AzaharClockSettings.Read(ConfigPath()));

        IsKnown = game is not null;

        if (game is not { } time)
        {
            TimeText = string.Empty;
            PeriodText = string.Empty;
            return;
        }

        Hour = time.TimeOfDay.TotalHours;
        TimeText = time.ToString("HH:mm");
        PeriodText = AlolaClock.PeriodOf(TimeOnly.FromDateTime(time)) switch
        {
            AlolaPeriod.Morning => "Mañana",
            AlolaPeriod.Day => "Día",
            AlolaPeriod.Evening => "Atardecer",
            _ => "Noche"
        };
    }

    private string ConfigPath()
    {
        foreach (var name in new[] { "azahar", "citra" })
        {
            var processes = Process.GetProcessesByName(name);
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        if (Path.GetDirectoryName(process.MainModule?.FileName) is { } folder)
                        {
                            var portable = Path.Combine(folder, "user");
                            var user = Directory.Exists(portable)
                                ? portable
                                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Azahar");

                            return Path.Combine(user, "config", "qt-config.ini");
                        }
                    }
                    catch (Win32Exception)
                    {
                        // Un proceso que no deja ver su ruta no dice dónde está su configuración: se prueba el siguiente.
                    }
                    catch (InvalidOperationException)
                    {
                        // Se cerró mientras se miraba.
                    }
                }
            }
            finally
            {
                foreach (var process in processes) process.Dispose();
            }
        }

        // Una vez: localizar escribe en el log, y esto se pregunta cada veinte segundos.
        return _fallbackConfig ??= Path.Combine(_installation.Locate(AppContext.BaseDirectory).UserDirectory, "config", "qt-config.ini");
    }
}
