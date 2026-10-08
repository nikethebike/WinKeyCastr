using System.Runtime.InteropServices;
using System.Windows.Threading;
using static WinKeyCastr.Native.Win32;

namespace WinKeyCastr.Input;

/// <summary>
/// Global low-level keyboard and mouse hooks (the Windows counterpart of KeyCastr's CGEventTap).
/// The hooks live on a dedicated thread so that UI work can never delay input system-wide;
/// events are raised on the UI dispatcher.
/// </summary>
public sealed class InputHook : IDisposable
{
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_CAPITAL = 0x14;
    private const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    private const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
    private const int VK_PACKET = 0xE7;

    // ToUnicodeEx flag: do not change the keyboard state (keeps dead keys working for the user).
    private const uint TOUNICODE_NO_STATE_CHANGE = 0x4;
    private const uint MAPVK_VK_TO_CHAR = 2;

    private static readonly byte[] EmptyKeyState = new byte[256];

    private readonly Dispatcher _dispatcher;
    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;
    private readonly bool[] _down = new bool[256];

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;

    private bool _fakeControlDown;  // the synthetic LCtrl that AltGr sends
    private bool _capsLockOn;
    private ModifierFlags _lastFlags;
    private int _buttonsDown;

    private MouseEventInfo? _pendingDrag;
    private int _dragDispatchQueued;

    public event Action<KeyStroke>? KeyDown;
    public event Action<KeyStroke>? KeyUp;
    public event Action<ModifierFlags>? FlagsChanged;
    public event Action<MouseEventInfo>? Mouse;

    public InputHook(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
        _capsLockOn = (GetKeyState(VK_CAPITAL) & 1) != 0;
    }

    public bool IsInstalled => _keyboardHook != IntPtr.Zero;

    public bool Start()
    {
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "KeyCastr input hook" };
        _thread.Start();
        ready.Wait();
        return IsInstalled;
    }

    public void Dispose()
    {
        if (_thread is null)
            return;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _thread = null;
    }

    private void Run(ManualResetEventSlim ready)
    {
        _threadId = GetCurrentThreadId();
        var module = GetModuleHandle(null);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        ready.Set();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _keyboardHook = _mouseHook = IntPtr.Zero;
    }

    // ---- Keyboard ----------------------------------------------------------------------

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = (int)wParam;
            bool isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool isUp = message is WM_KEYUP or WM_SYSKEYUP;
            if (isDown || isUp)
            {
                try
                {
                    ProcessKey(Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam), isDown);
                }
                catch
                {
                    // Never let an exception escape into the hook chain.
                }
            }
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void ProcessKey(KBDLLHOOKSTRUCT info, bool isDown)
    {
        int vk = (int)info.vkCode & 0xFF;

        // AltGr is delivered as a fake LCtrl (scan code 0x21D) followed by RAlt.
        if (vk == VK_LCONTROL && (info.scanCode & 0x200) != 0)
        {
            _fakeControlDown = isDown;
            RaiseFlagsIfChanged();
            return;
        }

        if (vk == VK_PACKET)
            return; // Unicode text injected by SendInput — not a physical key.

        if (IsModifierKey(vk))
        {
            _down[vk] = isDown;
            RaiseFlagsIfChanged();
            return;
        }

        if (vk == VK_CAPITAL)
        {
            // On the Mac Caps Lock is a modifier flag and never displayed; keep it that way.
            if (isDown && !_down[vk])
                _capsLockOn = !_capsLockOn;
            _down[vk] = isDown;
            return;
        }

        ResyncModifiers();

        bool isRepeat = isDown && _down[vk];
        _down[vk] = isDown;

        var flags = CurrentFlags();
        var layout = ForegroundKeyboardLayout();
        var keystroke = new KeyStroke
        {
            VirtualKey = vk,
            ScanCode = (int)info.scanCode,
            IsExtended = (info.flags & LLKHF_EXTENDED) != 0,
            Modifiers = flags,
            IsRepeat = isRepeat,
            KeyCap = TranslateKeyCap(vk, info.scanCode, layout),
            Characters = TranslateCharacters(vk, info.scanCode, layout, flags, _capsLockOn),
        };

        var handler = isDown ? KeyDown : KeyUp;
        if (handler is not null)
            _dispatcher.BeginInvoke(handler, keystroke);
    }

    private static bool IsModifierKey(int vk) => vk is VK_SHIFT or VK_CONTROL or VK_MENU
        or VK_LSHIFT or VK_RSHIFT or VK_LCONTROL or VK_RCONTROL or VK_LMENU or VK_RMENU or VK_LWIN or VK_RWIN;

    private ModifierFlags CurrentFlags()
    {
        var flags = ModifierFlags.None;
        if (_down[VK_LSHIFT] || _down[VK_RSHIFT] || _down[VK_SHIFT])
            flags |= ModifierFlags.Shift;
        if (_down[VK_LCONTROL] || _down[VK_RCONTROL] || _down[VK_CONTROL])
            flags |= ModifierFlags.Control;
        if (_down[VK_LMENU] || _down[VK_RMENU] || _down[VK_MENU])
            flags |= ModifierFlags.Option;
        if (_down[VK_LWIN] || _down[VK_RWIN])
            flags |= ModifierFlags.Command;
        if (_down[VK_RMENU] && _fakeControlDown)
            flags |= ModifierFlags.AltGr;
        return flags;
    }

    /// <summary>
    /// Key-ups can be lost (Win+L, UAC prompts, elevated windows). Before reporting a key,
    /// drop any modifier we think is held but the system says is not.
    /// </summary>
    private void ResyncModifiers()
    {
        bool changed = false;
        foreach (int vk in (ReadOnlySpan<int>)[VK_LSHIFT, VK_RSHIFT, VK_LCONTROL, VK_RCONTROL, VK_LMENU, VK_RMENU, VK_LWIN, VK_RWIN])
        {
            if (_down[vk] && (GetAsyncKeyState(vk) & 0x8000) == 0)
            {
                _down[vk] = false;
                changed = true;
            }
        }
        if (_fakeControlDown && !_down[VK_RMENU])
        {
            _fakeControlDown = false;
            changed = true;
        }
        if (changed)
            RaiseFlagsIfChanged();
    }

    private void RaiseFlagsIfChanged()
    {
        var flags = CurrentFlags();
        if (flags == _lastFlags)
            return;
        _lastFlags = flags;
        if (FlagsChanged is { } handler)
            _dispatcher.BeginInvoke(handler, flags);
    }

    private static IntPtr ForegroundKeyboardLayout()
    {
        var thread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        var layout = GetKeyboardLayout(thread);
        return layout != IntPtr.Zero ? layout : GetKeyboardLayout(0);
    }

    internal static string TranslateKeyCap(int vk, uint scanCode, IntPtr layout)
    {
        var text = ToUnicode(vk, scanCode, EmptyKeyState, layout);
        if (text.Length > 0)
            return text;

        uint mapped = MapVirtualKeyEx((uint)vk, MAPVK_VK_TO_CHAR, layout) & 0x7FFF;
        return mapped >= 0x20 ? char.ToLowerInvariant((char)mapped).ToString() : "";
    }

    internal static string TranslateCharacters(int vk, uint scanCode, IntPtr layout, ModifierFlags flags, bool capsLock)
    {
        var state = new byte[256];
        if (flags.Has(ModifierFlags.Shift))
            state[VK_SHIFT] = state[VK_LSHIFT] = 0x80;
        if (capsLock)
            state[VK_CAPITAL] = 0x01;
        if (flags.Has(ModifierFlags.AltGr))
        {
            state[VK_CONTROL] = state[VK_LCONTROL] = 0x80;
            state[VK_MENU] = state[VK_RMENU] = 0x80;
        }
        else if (flags.Has(ModifierFlags.Control))
        {
            state[VK_CONTROL] = state[VK_LCONTROL] = 0x80;
        }
        return ToUnicode(vk, scanCode, state, layout);
    }

    private static string ToUnicode(int vk, uint scanCode, byte[] state, IntPtr layout)
    {
        var buffer = new char[8];
        int count = ToUnicodeEx((uint)vk, scanCode, state, buffer, buffer.Length, TOUNICODE_NO_STATE_CHANGE, layout);
        if (count < 0)
            count = 1; // dead key: the buffer holds its spacing form
        if (count == 0 || char.IsControl(buffer[0]))
            return "";
        return new string(buffer, 0, count);
    }

    // ---- Mouse -------------------------------------------------------------------------

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                ProcessMouse((int)wParam, Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam));
            }
            catch
            {
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void ProcessMouse(int message, MSLLHOOKSTRUCT info)
    {
        MouseEventKind kind;
        MouseButton button = MouseButton.Left;
        switch (message)
        {
            case WM_LBUTTONDOWN: kind = MouseEventKind.Down; break;
            case WM_LBUTTONUP: kind = MouseEventKind.Up; break;
            case WM_RBUTTONDOWN: kind = MouseEventKind.Down; button = MouseButton.Right; break;
            case WM_RBUTTONUP: kind = MouseEventKind.Up; button = MouseButton.Right; break;
            case WM_MBUTTONDOWN: kind = MouseEventKind.Down; button = MouseButton.Middle; break;
            case WM_MBUTTONUP: kind = MouseEventKind.Up; button = MouseButton.Middle; break;
            case WM_XBUTTONDOWN:
            case WM_XBUTTONUP:
                kind = message == WM_XBUTTONDOWN ? MouseEventKind.Down : MouseEventKind.Up;
                button = (info.mouseData >> 16) == 1 ? MouseButton.X1 : MouseButton.X2;
                break;
            case WM_MOUSEMOVE:
                if (_buttonsDown == 0)
                    return;
                kind = MouseEventKind.Dragged;
                break;
            default:
                return;
        }

        if (kind == MouseEventKind.Down)
            _buttonsDown++;
        else if (kind == MouseEventKind.Up)
            _buttonsDown = Math.Max(0, _buttonsDown - 1);

        var e = new MouseEventInfo { Kind = kind, Button = button, X = info.pt.X, Y = info.pt.Y, Modifiers = CurrentFlags() };

        if (kind == MouseEventKind.Dragged)
        {
            // Drags arrive at the mouse polling rate; deliver only the latest one per UI frame.
            Volatile.Write(ref _pendingDrag, e);
            if (Interlocked.Exchange(ref _dragDispatchQueued, 1) == 0)
            {
                _dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                {
                    Interlocked.Exchange(ref _dragDispatchQueued, 0);
                    if (Interlocked.Exchange(ref _pendingDrag, null) is { } drag)
                        Mouse?.Invoke(drag);
                });
            }
            return;
        }

        if (Mouse is { } handler)
            _dispatcher.BeginInvoke(handler, e);
    }
}
