using System.Globalization;
using System.Text;

namespace WinKeyCastr.Input;

public enum ModifierStyle
{
    /// <summary>⌃ ⌥ ⇧ ⊞ — the KeyCastr look.</summary>
    Symbols,

    /// <summary>Ctrl+ Alt+ Shift+ Win+ — familiar to Windows audiences.</summary>
    Names,
}

/// <summary>
/// Turns keystrokes into display strings. A port of KeyCastr's KCEventTransformer:
/// modifiers are printed in Control-Option-Shift-Command order, Shift on a plain key is shown as
/// ⇧ plus the upper-case key cap unless "Apply Modifiers" is enabled, and so on.
/// </summary>
public sealed class KeystrokeTransformer
{
    public const string ControlGlyph = "⌃"; // ⌃
    public const string OptionGlyph = "⌥";  // ⌥
    public const string ShiftGlyph = "⇧";   // ⇧
    public const string WindowsGlyph = "⊞";  // ⊞ (in place of ⌘)
    public const string MouseGlyph = "\U0001F5B1️"; // 🖱️
    private const string LeftTabGlyph = "⇤"; // ⇤
    private const int VK_TAB = 0x09;

    /// <summary>Glyphs for keys that have no printable character, keyed by virtual-key code.</summary>
    public static readonly IReadOnlyDictionary<int, string> SpecialKeys = BuildSpecialKeys();

    public bool DisplayModifiedCharacters { get; set; }

    public ModifierStyle Style { get; set; } = ModifierStyle.Symbols;

    public string Transform(KeyStroke keystroke) => Transform(keystroke.Modifiers, keystroke);

    public string Transform(MouseEventInfo mouseEvent) => Transform(mouseEvent.Modifiers, null);

    private string Transform(ModifierFlags modifiers, KeyStroke? keystroke)
    {
        bool hasOption = modifiers.Has(ModifierFlags.Option);
        bool hasShift = modifiers.Has(ModifierFlags.Shift);
        bool isCommand = modifiers.IsCommand();
        bool altGr = modifiers.Has(ModifierFlags.AltGr);
        bool displayModified = DisplayModifiedCharacters;
        bool needsShiftGlyph = false;

        var response = new StringBuilder();

        if (modifiers.Has(ModifierFlags.Control))
            response.Append(Glyph(ModifierFlags.Control));

        if (hasOption && (isCommand || !displayModified))
            response.Append(Glyph(altGr ? ModifierFlags.AltGr : ModifierFlags.Option));

        if (hasShift)
        {
            if (isCommand || (hasOption && !displayModified))
                response.Append(Glyph(ModifierFlags.Shift));
            else
                needsShiftGlyph = !displayModified;
        }

        void AddShiftGlyphIfNeeded()
        {
            if (needsShiftGlyph)
            {
                response.Append(Glyph(ModifierFlags.Shift));
                needsShiftGlyph = false;
            }
        }

        if (modifiers.Has(ModifierFlags.Command))
        {
            AddShiftGlyphIfNeeded();
            response.Append(Glyph(ModifierFlags.Command));
        }

        if (keystroke is null)
        {
            AddShiftGlyphIfNeeded();
            response.Append(MouseGlyph);
            return response.ToString();
        }

        // A bare Shift-Tab is shown as a left tab.
        if (hasShift && !isCommand && !hasOption && keystroke.VirtualKey == VK_TAB)
        {
            response.Append(LeftTabGlyph);
            return response.ToString();
        }

        AddShiftGlyphIfNeeded();

        void AppendModifiers(bool append)
        {
            if (append && !isCommand)
            {
                if (hasOption)
                    response.Append(Glyph(altGr ? ModifierFlags.AltGr : ModifierFlags.Option));
                if (hasShift)
                    response.Append(Glyph(ModifierFlags.Shift));
            }
        }

        if (SpecialKeys.TryGetValue(keystroke.VirtualKey, out var special))
        {
            AppendModifiers(displayModified);
            response.Append(special);
            return response.ToString();
        }

        string key;
        if (displayModified && !isCommand)
        {
            if (keystroke.Characters.Length > 0)
            {
                key = keystroke.Characters;
            }
            else
            {
                AppendModifiers(displayModified);
                key = keystroke.KeyCap;
            }
        }
        else
        {
            key = keystroke.KeyCap;
        }

        // Commands, shifted keystrokes and Option combinations (when not applying modifiers) are upper-cased.
        if (isCommand || hasShift || (hasOption && !displayModified))
            key = key.ToUpper(CultureInfo.CurrentCulture);

        response.Append(key);
        return response.ToString();
    }

    /// <summary>The bare key cap (no modifier glyphs), as used by the Minimal visualizer.</summary>
    public string KeyCap(KeyStroke keystroke)
    {
        if (SpecialKeys.TryGetValue(keystroke.VirtualKey, out var special))
            return special;

        var keyCap = keystroke.KeyCap;
        var modifiers = keystroke.Modifiers;
        if (keystroke.IsCommand || modifiers.Has(ModifierFlags.Shift) || modifiers.Has(ModifierFlags.Option))
            keyCap = keyCap.ToUpper(CultureInfo.CurrentCulture);
        return keyCap;
    }

    public string Glyph(ModifierFlags modifier) => Style == ModifierStyle.Symbols
        ? modifier switch
        {
            ModifierFlags.Control => ControlGlyph,
            ModifierFlags.Option or ModifierFlags.AltGr => OptionGlyph,
            ModifierFlags.Shift => ShiftGlyph,
            ModifierFlags.Command => WindowsGlyph,
            _ => "",
        }
        : modifier switch
        {
            ModifierFlags.Control => "Ctrl+",
            ModifierFlags.Option => "Alt+",
            ModifierFlags.AltGr => "AltGr+",
            ModifierFlags.Shift => "Shift+",
            ModifierFlags.Command => "Win+",
            _ => "",
        };

    /// <summary>A modifier as a stand-alone label (Svelte and Minimal visualizers).</summary>
    public string ModifierLabel(ModifierFlags modifier) => Style == ModifierStyle.Symbols
        ? Glyph(modifier)
        : Glyph(modifier).TrimEnd('+');

    private static Dictionary<int, string> BuildSpecialKeys()
    {
        var keys = new Dictionary<int, string>
        {
            [0x26] = "⇡",          // up          ⇡
            [0x28] = "⇣",          // down        ⇣
            [0x27] = "⇢",          // right       ⇢
            [0x25] = "⇠",          // left        ⇠
            [0x09] = "⇥",          // tab         ⇥
            [0x1B] = "⎋",          // escape      ⎋
            [0x0C] = "⌧",          // clear       ⌧
            [0x08] = "⌫",          // backspace   ⌫
            [0x2E] = "⌦",          // delete      ⌦
            [0x2F] = "?⃝",         // help        ?⃝
            [0x24] = "↖",          // home        ↖
            [0x23] = "↘",          // end         ↘
            [0x21] = "⇞",          // page up     ⇞
            [0x22] = "⇟",          // page down   ⇟
            [0x0D] = "↩",          // return      ↩ (also numpad enter)
            [0x20] = "␣​",    // space       ␣
            [0x2D] = "⎀",          // insert      ⎀
            [0x2C] = "⎙",          // print scrn  ⎙
            [0x91] = "⇳",          // scroll lock ⇳
            [0x13] = "⎉",          // pause       ⎉
            [0x90] = "⇭",          // num lock    ⇭
            [0x5D] = "▤",          // menu key    ▤
            [0xAD] = "\U0001F507",      // mute        🔇
            [0xAE] = "\U0001F509",      // volume down 🔉
            [0xAF] = "\U0001F50A",      // volume up   🔊
            [0xB0] = "⏭",          // next track  ⏭
            [0xB1] = "⏮",          // prev track  ⏮
            [0xB2] = "⏹",          // stop        ⏹
            [0xB3] = "⏯",          // play/pause  ⏯
            [0xAA] = "\U0001F50D",      // search      🔍
            [0x15] = "かな",             // kana
            [0x19] = "漢字",             // kanji
            [0x1C] = "変換",             // convert
            [0x1D] = "無変換",            // non-convert
        };
        for (int i = 0; i < 24; i++)
            keys[0x70 + i] = "F" + (i + 1);
        return keys;
    }
}
