using System.Runtime.InteropServices;

namespace PermaLocke.App.Services;

/// <summary>
/// Tells when the player carries on (2026-10-09): a key or a button of the controller that goes down while the emulator's
/// window has the focus. What the animation of an item uses to leave at once instead of keeping the game waiting.
/// </summary>
/// <remarks>
/// <para>
/// It looks at the state of the keyboard (<c>GetAsyncKeyState</c>) and of the controllers (XInput) and nothing else: no hook,
/// no key is read or kept, only whether any <b>new</b> one went down. Keys that were already held when it began do not count
/// until they are let go and pressed again, so the A that picked the item up, or a direction held while walking, does not
/// cut the animation: the next press does.
/// </para>
/// <para>
/// Risks, said straight. A key of the keyboard that the game does not use (a hotkey of the emulator, Alt to switch window)
/// counts as the player carrying on, and so does a stick that crosses its threshold by drift: the animation goes out a
/// second early, which is harmless. A controller that XInput does not see (a DualShock through SDL without a wrapper) is
/// not noticed, and then only the keyboard cuts it.
/// </para>
/// </remarks>
public sealed class GameInputWatch
{
    private const int Keys = 256;
    private const int PadSlots = 32;

    private readonly InputEdges _edges = new(Keys + PadSlots);
    private readonly bool[] _now = new bool[Keys + PadSlots];
    private readonly Func<bool> _focused;

    private bool _padMissing;

    public GameInputWatch()
        : this(() => GetForegroundWindow() is var front && front != IntPtr.Zero && front == GameWindow.Handle())
    {
    }

    /// <param name="focused">Whether the game's window has the focus: only then does the player's input count.</param>
    public GameInputWatch(Func<bool> focused)
    {
        _focused = focused;
    }

    /// <summary>Takes what is held now as the starting point: only what goes down after this counts.</summary>
    public void Begin()
    {
        Sample();
        _edges.Reset(_now);
    }

    /// <summary>True when something went down since the last time it was asked (or since <see cref="Begin"/>).</summary>
    public bool Advanced()
    {
        Sample();
        return _edges.Rose(_now);
    }

    private void Sample()
    {
        Array.Clear(_now);
        if (!_focused()) return;

        // 0x01 to 0x06 are the mouse buttons: the mouse is not the game.
        for (var key = 0x08; key < Keys; key++)
        {
            _now[key] = (GetAsyncKeyState(key) & 0x8000) != 0;
        }

        if (_padMissing) return;

        try
        {
            for (var pad = 0; pad < 4; pad++)
            {
                if (XInputGetState((uint)pad, out var state) != 0) continue;

                var g = state.Gamepad;
                for (var bit = 0; bit < 16; bit++) _now[Keys + bit] |= (g.Buttons & (1 << bit)) != 0;

                _now[Keys + 16] |= g.LeftTrigger > 128;
                _now[Keys + 17] |= g.RightTrigger > 128;
                _now[Keys + 18] |= g.ThumbLX < -16000;
                _now[Keys + 19] |= g.ThumbLX > 16000;
                _now[Keys + 20] |= g.ThumbLY < -16000;
                _now[Keys + 21] |= g.ThumbLY > 16000;
                _now[Keys + 22] |= g.ThumbRX < -16000;
                _now[Keys + 23] |= g.ThumbRX > 16000;
                _now[Keys + 24] |= g.ThumbRY < -16000;
                _now[Keys + 25] |= g.ThumbRY > 16000;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _padMissing = true;
        }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint index, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }
}

/// <summary>
/// Which of a set of keys went from up to down since the last look. A key that was down and stays down is not a new press.
/// </summary>
public sealed class InputEdges(int size)
{
    private readonly bool[] _before = new bool[size];

    /// <summary>Takes a state as the one nothing has changed from.</summary>
    public void Reset(ReadOnlySpan<bool> now) => now[..Math.Min(now.Length, _before.Length)].CopyTo(_before);

    /// <summary>True when any of them is down now and was not before; remembers this state for the next look.</summary>
    public bool Rose(ReadOnlySpan<bool> now)
    {
        var rose = false;
        for (var i = 0; i < _before.Length && i < now.Length; i++)
        {
            rose |= now[i] && !_before[i];
            _before[i] = now[i];
        }

        return rose;
    }
}
