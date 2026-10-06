using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace PermaLocke.App.Services;

/// <summary>Whether the emulator is really running.</summary>
/// <remarks>
/// <para>
/// Counting what <see cref="Process.GetProcessesByName(string)"/> returns is not enough, and the
/// difference is not theoretical. A process that has already terminated <b>stays in the table</b>
/// while anyone still holds a handle to it — zero threads, zero handles, no window, nothing
/// listening on its ports. Every <see cref="Process"/> object <b>is</b> one of those handles, so a
/// caller that asks on a timer and never disposes keeps alive the very emulator it just watched
/// close, and from then on answers «it is running» for the rest of the session.
/// </para>
/// <para>
/// Measured on 2026-09-20 on the player's machine: four of those ghosts in the table — three
/// PermaLocke and one azahar, all with 0 threads and 0 handles, and nobody on the RPC port. With
/// them, PermaLocke minimised itself into the edge tab on startup and MODO COMBATE refused to work,
/// both of them convinced that a game was open. Neither could be terminated: there was nothing left
/// to terminate.
/// </para>
/// <para>
/// The answer is three-valued on purpose. «I cannot tell» is not «closed»: the two callers want
/// opposite things when the answer is missing — the edge tab must not hide the application on a
/// guess, and MODO COMBATE must not move a mod out from under an emulator that may be running.
/// Collapsing that into a bool would silently pick one of the two.
/// </para>
/// </remarks>
public static class EmulatorProcess
{
    private static readonly string[] Names = ["azahar", "citra"];

    /// <summary>
    /// <see langword="true"/> when an emulator process is alive, <see langword="false"/> when none
    /// is, and <see langword="null"/> when it cannot be told.
    /// </summary>
    public static bool? IsRunning()
    {
        // Una ventana no sobrevive a su proceso: si la del juego sigue ahí, está abierto y no hace falta listar nada.
        if (GameWindow.KnownAlive())
        {
            return true;
        }

        var liveness = new List<bool?>();
        var processes = Candidates();

        try
        {
            foreach (var process in processes)
            {
                liveness.Add(Liveness(process));
            }
        }
        finally
        {
            // The handle is the whole point: without this, asking keeps the answer true.
            foreach (var process in processes) process.Dispose();
        }

        return Decide(liveness);
    }

    /// <summary>
    /// Every azahar or citra process, from one look at the process table. The caller disposes them.
    /// </summary>
    /// <remarks>
    /// One look and not one per name: <see cref="Process.GetProcessesByName(string)"/> lists the whole table every time
    /// (about 6 ms of CPU with 390 processes, measured 2026-10-03), so asking for the two names cost two of them.
    /// </remarks>
    internal static Process[] Candidates()
    {
        var all = Process.GetProcesses();
        var found = new List<Process>();

        foreach (var process in all)
        {
            if (Array.Exists(Names, name => string.Equals(name, process.ProcessName, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(process);
            }
            else
            {
                process.Dispose();
            }
        }

        return [.. found];
    }

    /// <summary>Whether one process is alive, or null when it will not say.</summary>
    /// <remarks>
    /// <para>
    /// Two questions, because the first one does not answer for the case this exists for. Measured
    /// on the four ghosts of 2026-09-20: <see cref="Process.HasExited"/> <b>throws</b>
    /// <see cref="Win32Exception"/> «Acceso denegado» on every one of them — a terminated process
    /// cannot be opened, so the very state worth detecting is the state that refuses to answer.
    /// </para>
    /// <para>
    /// The thread count does answer, and without opening anything: it comes from the same snapshot
    /// that listed the process. Measured the same day — the four ghosts read <b>0 threads</b>, while
    /// three live processes read 233, 30 and 84. A process with no threads has terminated, and a
    /// live one always has at least one, so this settles it both ways: a ghost stops blocking, and
    /// an emulator that merely runs with more privileges than us still counts as running.
    /// </para>
    /// </remarks>
    private static bool? Liveness(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch (InvalidOperationException)
        {
            // It went away while being looked at. That is an answer, and it is «not this one».
            return false;
        }
        catch (Win32Exception)
        {
            // Not allowed to open it. Ask the other question instead of giving up.
        }

        try
        {
            return process.Threads.Count > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            // Neither question could be answered. Saying «closed» here would let a caller move
            // files under a live emulator, so it is not an answer.
            return null;
        }
    }

    /// <summary>
    /// The verdict for a set of candidates. Pure, so the decision can be tested without processes.
    /// </summary>
    /// <remarks>
    /// One live process settles it. Otherwise a candidate nobody could read leaves the whole answer
    /// unknown, because the one it hides is exactly the dangerous one. Only when every candidate
    /// answered, and every one of them had exited, is the emulator closed.
    /// </remarks>
    internal static bool? Decide(IReadOnlyList<bool?> liveness)
    {
        var unknown = false;

        foreach (var live in liveness)
        {
            if (live is true) return true;
            if (live is null) unknown = true;
        }

        return unknown ? null : false;
    }
}
