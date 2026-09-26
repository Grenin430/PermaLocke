using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.GameLink;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer.Output;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.App.Services;

/// <summary>Where the game is, from the launcher's point of view.</summary>
public enum EmulatorState
{
    /// <summary>Something the game needs is missing: no emulator, or no ROM.</summary>
    Unavailable,

    /// <summary>Closed and ready to open.</summary>
    Ready,

    /// <summary>Asked to open; the window is on its way.</summary>
    Starting,

    Running,

    /// <summary>Asked to close; waiting for the emulator to stop.</summary>
    Closing,

    /// <summary>Asked to close and still open after the wait. Only forcing it is left.</summary>
    WontClose,
}

public enum CheckLevel
{
    Ok,
    Warning,
    Blocking,
}

/// <param name="Title">What is being checked, in two or three words.</param>
/// <param name="Detail">What was found.</param>
public sealed record LaunchCheck(string Title, string Detail, CheckLevel Level);

/// <summary>Where the launcher's own choices live: <c>Config/lanzador.json</c>.</summary>
public sealed record LauncherSettings
{
    /// <summary>The <c>azahar.exe</c> the player picked by hand, or null to find one.</summary>
    public string? Azahar { get; init; }
}

/// <summary>
/// Opens the game, watches it and closes it, the way a game library does (§125).
/// </summary>
/// <remarks>
/// <para>
/// <b>It watches the process, not only what it started.</b> Azahar opened by hand is still the game,
/// and a launcher that only knew about its own child would say «listo para jugar» over a game that is
/// running. So the state comes from whether an <c>azahar</c> process exists, every second, and the
/// session starts at that process's own start time — which is also what makes restarting PermaLocke
/// mid-game carry on with the same session instead of opening a second one.
/// </para>
/// <para>
/// <b>Closing asks, and forcing is separate.</b> Closing sends the window its close message, which is
/// how Azahar shuts down cleanly: it stops the emulation and writes its settings. Its own «Would you
/// like to exit now?» is turned off before launching, because the launcher asks first. If the window
/// is still there after the wait, the only thing left is killing the process, and that is a different
/// button the player has to press, not something done behind their back.
/// </para>
/// <para>
/// Neither closing nor forcing saves the game. What is lost is whatever was not saved inside the game,
/// exactly as with closing Azahar by hand, and the question says so.
/// </para>
/// </remarks>
public sealed partial class EmulatorLauncher : ObservableObject
{
    private const string ProcessName = "azahar";
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HeartbeatEvery = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan StartWait = TimeSpan.FromSeconds(20);

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly AzaharInstallation _azahar;
    private readonly AppPaths _paths;
    private readonly AppSettings _settings;
    private readonly IRunContext _runContext;
    private readonly IPlaytimeStore _playtime;
    private readonly BattleModeService _battle;
    private readonly EmulatorCrashReport _crashes;
    private readonly Notifier _notifier;
    private readonly ILogger<EmulatorLauncher> _logger;
    private readonly DispatcherTimer _timer = new() { Interval = PollEvery };

    private DateTimeOffset? _sessionStart;
    private Guid? _sessionRun;
    private DateTimeOffset _lastHeartbeat = DateTimeOffset.MinValue;
    private DateTimeOffset _requestedAt;
    private RomInfo? _rom;
    private bool _romScanned;

    /// <summary>The running emulator, held open so its exit code survives it (§168).</summary>
    private ProcessExitWatch? _watch;

    /// <summary>Whether PermaLocke asked the emulator to go: then its end is not a crash.</summary>
    private bool _closeRequested;

    /// <summary>What to say instead of «listo para jugar» after a crash, until the next session starts.</summary>
    private string? _crashNotice;

    public EmulatorLauncher(AzaharInstallation azahar, AppPaths paths, IRunContext runContext,
        IPlaytimeStore playtime, BattleModeService battle, EmulatorCrashReport crashes, Notifier notifier,
        AppSettings settings, ILogger<EmulatorLauncher> logger)
    {
        _settings = settings;
        _crashes = crashes;
        _notifier = notifier;
        _azahar = azahar;
        _paths = paths;
        _runContext = runContext;
        _playtime = playtime;
        _battle = battle;
        _logger = logger;
        _timer.Tick += (_, _) => _ = PollAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsBusy))]
    private EmulatorState _state = EmulatorState.Ready;

    /// <summary>How long the running session has lasted, as a clock. Empty when closed.</summary>
    [ObservableProperty]
    private string _sessionClock = string.Empty;

    /// <summary>One line saying what is happening.</summary>
    [ObservableProperty]
    private string _statusText = "Listo para jugar";

    [ObservableProperty]
    private IReadOnlyList<LaunchCheck> _checks = [];

    public bool IsRunning => State is EmulatorState.Running or EmulatorState.Closing or EmulatorState.WontClose;

    /// <summary>When the running emulator process started, or null with the game closed.</summary>
    public DateTimeOffset? SessionStart => _sessionStart;

    public bool IsBusy => State is EmulatorState.Starting or EmulatorState.Closing;

    /// <summary>Raised when a session was written, so the play time on screen can be read again.</summary>
    public event EventHandler? PlaytimeChanged;

    private string SettingsPath => Path.Combine(_paths.Config, "lanzador.json");

    public void Start()
    {
        Refresh();
        _timer.Start();
        _ = PollAsync();
    }

    /// <summary>Looks at the emulator, the ROM and the world again.</summary>
    public void Refresh()
    {
        _romScanned = false;
        Checks = Evaluate();

        if (!IsRunning && !IsBusy)
        {
            SetIdle();
        }
    }

    /// <summary>The executable that would be started, and why.</summary>
    public EmulatorChoice Emulator()
    {
        var location = _azahar.Locate(AppContext.BaseDirectory);

        string[] candidates =
        [
            Path.Combine(_paths.Root, "Nuevo_azahar", "azahar.exe"),
            Path.Combine(_paths.Root, "Emulator", "azahar.exe"),
            Path.Combine(_paths.Root, "Azahar", "azahar.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Azahar", "azahar.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Azahar", "azahar.exe")
        ];

        return AzaharExecutable.Choose(location, LoadSettings().Azahar, candidates);
    }

    private RomInfo? Rom()
    {
        if (!_romScanned)
        {
            _rom = RomInspector.ScanFolder(_paths.Rom).FirstOrDefault(r => r.IsSupported);
            _romScanned = true;
        }

        return _rom;
    }

    public IReadOnlyList<LaunchCheck> Evaluate()
    {
        var checks = new List<LaunchCheck>();

        var emulator = Emulator();
        checks.Add(emulator.Executable is null
            ? new LaunchCheck("Emulador", emulator.Detail, CheckLevel.Blocking)
            : new LaunchCheck("Emulador", emulator.Executable, CheckLevel.Ok));

        var rom = SafeRom();
        checks.Add(rom is null
            ? new LaunchCheck("ROM", "Pon tu ROM de Ultra Luna en la carpeta ROM.", CheckLevel.Blocking)
            : new LaunchCheck("ROM", rom.FileName, CheckLevel.Ok));

        // Lo del PC va antes que el mundo y la run: JUGAR enseña el primer aviso, y un emulador que se va a caer
        // importa más que un mundo sin instalar.
        checks.AddRange(ComputerChecks(emulator.Executable));

        var location = _azahar.Locate(AppContext.BaseDirectory);
        var world = Directory.Exists(Path.Combine(AzaharInstallation.ModDirectory(location, LayeredFsMod.UltraMoonProgramId), "romfs"));

        checks.Add(_battle.State == BattleModeState.InBattle
            ? new LaunchCheck("Tu mundo", "Estás en modo combate. Devuelve tu mundo en COMBATES.", CheckLevel.Warning)
            : world
                ? new LaunchCheck("Tu mundo", _runContext.Current is { } run
                    ? $"Instalado · {run.SeedLabel}"
                    : "Instalado", CheckLevel.Ok)
                : new LaunchCheck("Tu mundo", "No has instalado tu mundo. Hazlo en RANDOMIZADOR.", CheckLevel.Warning));

        checks.Add(_runContext.Current is null
            ? new LaunchCheck("Tu run", "No tienes ninguna run. Créala en HOME.", CheckLevel.Warning)
            : new LaunchCheck("Tu run", _runContext.Current.Name, CheckLevel.Ok));

        return checks;
    }

    /// <summary>What this computer could get in the way of, said before playing and not after a crash (§168).</summary>
    /// <remarks>
    /// Each of these is something that works on the computer where PermaLocke is built and fails on another, silently
    /// or much later: the emulator's runtime, a folder that OneDrive locks while uploading, a folder nobody can write
    /// to, a full disk. None of them needs anybody to test anything: it is read off the computer.
    /// </remarks>
    private IEnumerable<LaunchCheck> ComputerChecks(string? emulator)
    {
        if (emulator is not null)
        {
            var runtime = VisualCppRuntime.Check(emulator);

            yield return runtime.Enough
                ? new LaunchCheck("Visual C++",
                    runtime.Local is { } local ? $"Incluido con el emulador ({local})" : $"El de Windows ({runtime.System})",
                    CheckLevel.Ok)
                : new LaunchCheck("Visual C++",
                    runtime.System is { } old
                        ? $"El de este PC es el {old} y el emulador necesita el {runtime.Needed}: puede cerrarse solo. " +
                          "Instala el Visual C++ 2015-2022 x64 más reciente de Microsoft."
                        : "Falta el Visual C++ de Microsoft y el emulador no arrancará. " +
                          "Instala el Visual C++ 2015-2022 x64 más reciente.",
                    CheckLevel.Warning);
        }

        if (!CanWrite(_paths.Root))
        {
            yield return new LaunchCheck("Carpeta",
                "No se puede escribir en la carpeta de PermaLocke. Muévela a una carpeta tuya, por ejemplo C:\\Juegos.",
                CheckLevel.Blocking);
        }
        else if (Machine.IsInOneDrive(_paths.Root))
        {
            yield return new LaunchCheck("Carpeta",
                "Está dentro de OneDrive, que bloquea los ficheros mientras los sube. Muévela fuera, por ejemplo a C:\\Juegos.",
                CheckLevel.Warning);
        }

        if (FreeGigabytes(_paths.Root) is { } free && free < 2)
        {
            yield return new LaunchCheck("Espacio",
                $"Quedan {free:F1} GB libres: instalar el mundo y guardar pueden fallar. Libera espacio.",
                CheckLevel.Warning);
        }
    }

    private static bool CanWrite(string folder)
    {
        try
        {
            var probe = Path.Combine(folder, $".permalocke-escritura-{Environment.ProcessId}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static double? FreeGigabytes(string folder)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))!).AvailableFreeSpace / (1024.0 * 1024 * 1024);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private RomInfo? SafeRom()
    {
        try
        {
            return Rom();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido mirar la carpeta ROM");
            return null;
        }
    }

    /// <summary>Opens the game, or brings its window forward when it is already open.</summary>
    /// <returns>Null when it opened; otherwise why not.</returns>
    public string? Launch()
    {
        using var running = FindProcess();
        if (running is not null)
        {
            BringToFront(running);
            return null;
        }

        Checks = Evaluate();

        if (Checks.FirstOrDefault(c => c.Level == CheckLevel.Blocking) is { } blocking)
        {
            return blocking.Detail;
        }

        var executable = Emulator().Executable!;
        var rom = SafeRom()!;
        var location = _azahar.Locate(AppContext.BaseDirectory);

        // Antes de abrir y con el emulador cerrado: los dos ajustes se escriben en su configuración, y
        // un Azahar abierto la reescribiría al salir.
        _azahar.EnsureRpcEnabled(location);
        _azahar.DisableCloseConfirmation(location);
        _azahar.DisableDiscordPresence(location);
        _azahar.SetFollower(location, Path.Combine(_paths.Root, "Emulator", "follower", AzaharInstallation.FollowerPluginName),
            _settings.Current.Follower);

        try
        {
            var start = new ProcessStartInfo(executable)
            {
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false
            };
            start.ArgumentList.Add(rom.Path);

            using var launched = Process.Start(start);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se ha podido abrir {Emulator}", executable);
            return "No se ha podido abrir el emulador.";
        }

        _logger.LogInformation("Juego abierto desde el lanzador: {Emulator} {Rom}", executable, rom.Path);
        _requestedAt = DateTimeOffset.Now;
        State = EmulatorState.Starting;
        StatusText = "Abriendo Ultra Luna…";
        return null;
    }

    /// <summary>Asks the emulator's window to close, the same as pressing its X.</summary>
    public void Close()
    {
        using var process = FindProcess();
        if (process is null)
        {
            SetIdle();
            return;
        }

        _logger.LogInformation("Pidiendo a Azahar que cierre su ventana (proceso {Id})", process.Id);
        _closeRequested = true;
        _requestedAt = DateTimeOffset.Now;
        State = EmulatorState.Closing;
        StatusText = "Cerrando el juego…";

        if (!process.CloseMainWindow())
        {
            _logger.LogWarning("Azahar no tiene ventana a la que pedirle que se cierre");
            State = EmulatorState.WontClose;
            StatusText = "El emulador no responde a cerrar.";
        }
    }

    /// <summary>Ends the emulator's process. Only ever from its own button.</summary>
    public void ForceClose()
    {
        _closeRequested = true;

        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            try
            {
                process.Kill();
                _logger.LogWarning("Azahar cerrado a la fuerza desde el lanzador (proceso {Id})", process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se ha podido forzar el cierre de Azahar");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>Remembers an <c>azahar.exe</c> picked by hand.</summary>
    /// <returns>Null when it will be used; otherwise why not.</returns>
    public string? UseEmulator(string executable)
    {
        if (AzaharExecutable.IsPortable(executable))
        {
            return "Ese Azahar no sirve: guarda la partida en otro sitio.";
        }

        Directory.CreateDirectory(_paths.Config);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new LauncherSettings { Azahar = executable }, Json));
        Refresh();
        return null;
    }

    private LauncherSettings LoadSettings()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsPath), Json) ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se ha podido leer {Path}", SettingsPath);
            return new();
        }
    }

    private bool _polling;

    private async Task PollAsync()
    {
        // El temporizador no espera a que acabe la vuelta anterior, y una escritura de sesiones lenta no puede
        // hacer que dos vueltas se pisen.
        if (_polling)
        {
            return;
        }

        _polling = true;

        try
        {
            using var process = FindProcess();
            var now = DateTimeOffset.Now;

            if (process is not null)
            {
                var started = StartOf(process) ?? _sessionStart ?? now;

                if (_sessionStart != started)
                {
                    _sessionStart = started;
                    _sessionRun = _runContext.Current?.Id;
                    _lastHeartbeat = DateTimeOffset.MinValue;
                    _crashNotice = null;
                    _logger.LogInformation("Sesión de juego desde {Start}", started);
                }

                // Se sujeta el proceso mientras vive: al desaparecer, su código de salida es lo único que distingue
                // cerrarlo de que se haya caído, y sin un asa abierta Windows lo olvida con él.
                if (_watch?.ProcessId != process.Id)
                {
                    _watch?.Dispose();
                    _watch = new ProcessExitWatch(process.Id);
                }

                SessionClock = Playtime.Clock(now - started);

                if (State == EmulatorState.Closing && now - _requestedAt > CloseWait)
                {
                    State = EmulatorState.WontClose;
                    StatusText = "Sigue abierto. Si Azahar se ha colgado, fuerza el cierre.";
                }
                else if (State is not (EmulatorState.Closing or EmulatorState.WontClose))
                {
                    State = EmulatorState.Running;
                    StatusText = "Jugando";
                }

                if (now - _lastHeartbeat >= HeartbeatEvery)
                {
                    _lastHeartbeat = now;
                    await RecordAsync(now);
                }

                return;
            }

            if (_sessionStart is not null)
            {
                var length = now - _sessionStart.Value;
                var exit = _watch?.ExitCode();

                await RecordAsync(now);
                _logger.LogInformation("Sesión de juego terminada: {Length} (código de salida {Code})",
                    Playtime.Say(length), exit is { } seen ? $"0x{seen:X8}" : "desconocido");
                _sessionStart = null;
                _sessionRun = null;
                SessionClock = string.Empty;
                Checks = Evaluate();

                if (!_closeRequested && exit is { } code && EmulatorCrashReport.IsCrash(code))
                {
                    await CrashedAsync(code, length);
                }

                _watch?.Dispose();
                _watch = null;
                _closeRequested = false;
            }

            if (State == EmulatorState.Starting && now - _requestedAt < StartWait)
            {
                return;
            }

            SetIdle();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo vigilando el emulador");
        }
        finally
        {
            _polling = false;
        }
    }

    private async Task RecordAsync(DateTimeOffset seen)
    {
        // La sesión es de la run que estaba cargada cuando empezó: si a mitad se cambia de run, las horas
        // no se pasan a la otra.
        if (_sessionStart is not { } start || _sessionRun is not { } run)
        {
            return;
        }

        var sessions = await _playtime.LoadAsync(run);
        await _playtime.SaveAsync(run, Playtime.Record(sessions, start, seen));
        PlaytimeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetIdle()
    {
        var blocking = Checks.FirstOrDefault(c => c.Level == CheckLevel.Blocking);
        State = blocking is null ? EmulatorState.Ready : EmulatorState.Unavailable;
        StatusText = blocking?.Detail ?? _crashNotice ?? "Listo para jugar";
    }

    /// <summary>
    /// Azahar went down with nobody asking it to: the report is written at once, while the logs still hold the
    /// minutes before, and the player is told where it is and what to do with it (§168).
    /// </summary>
    private async Task CrashedAsync(uint code, TimeSpan length)
    {
        var executable = Emulator().Executable ?? Path.Combine(_paths.Root, "Emulator", "azahar.exe");
        var zip = await Task.Run(() => _crashes.Write(code, length, executable));
        var meaning = EmulatorCrashReport.Meaning(code);

        _crashNotice = zip is null
            ? $"Azahar se ha cerrado solo ({meaning})."
            : $"Azahar se ha cerrado solo ({meaning}). Informe en Diagnosticos\\{Path.GetFileName(zip)}.";

        _notifier.Say(ToastKind.Warning, "Azahar se ha cerrado solo",
            zip is null
                ? $"{char.ToUpper(meaning![0])}{meaning[1..]}. No se ha podido guardar el informe."
                : $"Informe guardado en Diagnosticos\\{Path.GetFileName(zip)}. Pásaselo a quien te dio PermaLocke.");
    }

    private static Process? FindProcess()
    {
        Process? found = null;
        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            try
            {
                if (found is null && !process.HasExited) found = process;
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally
            {
                if (!ReferenceEquals(found, process)) process.Dispose();
            }
        }
        // Ownership transfers to the caller; disposing a Process does not stop the emulator.
        return found;
    }
    private static DateTimeOffset? StartOf(Process process)
    {
        try
        {
            return new DateTimeOffset(process.StartTime);
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    private static void BringToFront(Process process)
    {
        var window = process.MainWindowHandle;

        if (window != IntPtr.Zero)
        {
            ShowWindow(window, 9); // SW_RESTORE
            SetForegroundWindow(window);
        }
    }
}
