namespace WinKeyCastr.Input;

/// <summary>
/// Modifier state, named after the macOS modifiers KeyCastr was built around:
/// Control = Ctrl, Option = Alt, Command = Win. AltGr is reported as Option + AltGr,
/// because on Windows it plays the role of the Mac Option key (typing extra characters).
/// </summary>
[Flags]
public enum ModifierFlags
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Option = 1 << 2,
    Command = 1 << 3,
    AltGr = 1 << 4,

    DeviceIndependent = Shift | Control | Option | Command,
}

public static class ModifierFlagsExtensions
{
    /// <summary>
    /// KeyCastr treats ⌃ and ⌘ chords as "commands". On Windows, Alt chords (Alt+Tab, Alt+F4)
    /// are commands too, while AltGr is used for typing and is not.
    /// </summary>
    public static bool IsCommand(this ModifierFlags flags) =>
        (flags & (ModifierFlags.Control | ModifierFlags.Command)) != 0
        || ((flags & ModifierFlags.Option) != 0 && (flags & ModifierFlags.AltGr) == 0);

    public static bool IsModified(this ModifierFlags flags) => (flags & ModifierFlags.DeviceIndependent) != 0;

    public static bool Has(this ModifierFlags flags, ModifierFlags flag) => (flags & flag) != 0;
}

/// <summary>A single key press or release, captured with the layout that was active at the time.</summary>
public sealed class KeyStroke
{
    public required int VirtualKey { get; init; }
    public required int ScanCode { get; init; }
    public required bool IsExtended { get; init; }
    public required ModifierFlags Modifiers { get; init; }
    public required bool IsRepeat { get; init; }

    /// <summary>The character printed on the key cap in the current layout (no modifiers applied).</summary>
    public required string KeyCap { get; init; }

    /// <summary>The characters the key actually produces with Shift/AltGr/Caps Lock applied.</summary>
    public required string Characters { get; init; }

    public bool IsCommand => Modifiers.IsCommand();
    public bool IsModified => Modifiers.IsModified();
}

public enum MouseEventKind { Down, Up, Dragged }

public enum MouseButton { Left, Right, Middle, X1, X2 }

public sealed class MouseEventInfo
{
    public required MouseEventKind Kind { get; init; }
    public required MouseButton Button { get; init; }

    /// <summary>Cursor position in physical screen pixels.</summary>
    public required int X { get; init; }
    public required int Y { get; init; }

    public required ModifierFlags Modifiers { get; init; }

    public bool IsCommand => Modifiers.IsCommand();
}
