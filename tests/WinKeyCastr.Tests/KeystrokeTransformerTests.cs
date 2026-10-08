using WinKeyCastr.Input;
using Xunit;
using static WinKeyCastr.Input.ModifierFlags;

namespace WinKeyCastr.Tests;

/// <summary>
/// Ported from KeyCastr's KCKeystrokeConversionTests, with ⌘ mapped to the Windows key (⊞).
/// Modifiers print in Control-Option-Shift-Command order.
/// </summary>
public class KeystrokeTransformerTests
{
    private readonly KeystrokeTransformer _transformer = new();

    private static KeyStroke Key(int vk, ModifierFlags mods, string keyCap, string characters = "") => new()
    {
        VirtualKey = vk,
        ScanCode = 0,
        IsExtended = false,
        Modifiers = mods,
        IsRepeat = false,
        KeyCap = keyCap,
        Characters = characters,
    };

    // ---- Numbers ---------------------------------------------------------------------

    [Fact] public void CtrlNumber() => Assert.Equal("⌃7", _transformer.Transform(Key('7', Control, "7")));
    [Fact] public void ShiftNumber() => Assert.Equal("⇧7", _transformer.Transform(Key('7', Shift, "7", "&")));
    [Fact] public void CtrlShiftNumber() => Assert.Equal("⌃⇧7", _transformer.Transform(Key('7', Control | Shift, "7")));
    [Fact] public void WinNumber() => Assert.Equal("⊞7", _transformer.Transform(Key('7', Command, "7")));
    [Fact] public void WinShiftNumber() => Assert.Equal("⇧⊞7", _transformer.Transform(Key('7', Command | Shift, "7")));
    [Fact] public void WinAltNumber() => Assert.Equal("⌥⊞7", _transformer.Transform(Key('7', Command | Option, "7")));
    [Fact] public void ShiftAltNumber() => Assert.Equal("⌥⇧7", _transformer.Transform(Key('7', Shift | Option, "7")));
    [Fact] public void WinAltShiftNumber() => Assert.Equal("⌥⇧⊞7", _transformer.Transform(Key('7', Command | Option | Shift, "7")));

    // ---- Letters ---------------------------------------------------------------------

    [Fact] public void PlainLetterIsLowercase() => Assert.Equal("a", _transformer.Transform(Key('A', None, "a", "a")));
    [Fact] public void ShiftLetterShowsShiftGlyph() => Assert.Equal("⇧A", _transformer.Transform(Key('A', Shift, "a", "A")));
    [Fact] public void CtrlLetterIsUppercase() => Assert.Equal("⌃A", _transformer.Transform(Key('A', Control, "a")));
    [Fact] public void CtrlShiftLetter() => Assert.Equal("⌃⇧A", _transformer.Transform(Key('A', Control | Shift, "a")));
    [Fact] public void CtrlShiftWinLetter() => Assert.Equal("⌃⇧⊞A", _transformer.Transform(Key('A', Control | Shift | Command, "a")));
    [Fact] public void CtrlAltLetter() => Assert.Equal("⌃⌥A", _transformer.Transform(Key('A', Control | Option, "a")));
    [Fact] public void AltLetter() => Assert.Equal("⌥F4", _transformer.Transform(Key(0x73, Option, "")));

    [Fact]
    public void CyrillicKeyCapIsUppercasedForCommands() =>
        Assert.Equal("⌃С", _transformer.Transform(Key('C', Control, "с")));

    // ---- Special keys ----------------------------------------------------------------

    [Fact] public void Tab() => Assert.Equal("⇥", _transformer.Transform(Key(0x09, None, "")));
    [Fact] public void ShiftTabIsLeftTab() => Assert.Equal("⇤", _transformer.Transform(Key(0x09, Shift, "")));
    [Fact] public void Backspace() => Assert.Equal("⌫", _transformer.Transform(Key(0x08, None, "")));
    [Fact] public void Return() => Assert.Equal("↩", _transformer.Transform(Key(0x0D, None, "")));
    [Fact] public void Escape() => Assert.Equal("⎋", _transformer.Transform(Key(0x1B, None, "")));
    [Fact] public void FunctionKey() => Assert.Equal("F12", _transformer.Transform(Key(0x7B, None, "")));
    [Fact] public void CtrlShiftF24() => Assert.Equal("⌃⇧F24", _transformer.Transform(Key(0x87, Control | Shift, "")));

    [Fact]
    public void ShiftArrowKeepsModifiersWithAndWithoutApplyModifiers()
    {
        var up = Key(0x26, Option | Shift | AltGr, "");
        _transformer.DisplayModifiedCharacters = false;
        Assert.Equal("⌥⇧⇡", _transformer.Transform(up));
        _transformer.DisplayModifiedCharacters = true;
        Assert.Equal("⌥⇧⇡", _transformer.Transform(up));
    }

    // ---- AltGr plays the Mac Option key ----------------------------------------------

    [Fact]
    public void AltGrShowsKeyCapByDefaultAndCharacterWithApplyModifiers()
    {
        var euro = Key('E', Option | AltGr, "e", "€");
        _transformer.DisplayModifiedCharacters = false;
        Assert.Equal("⌥E", _transformer.Transform(euro));
        _transformer.DisplayModifiedCharacters = true;
        Assert.Equal("€", _transformer.Transform(euro));
    }

    [Fact]
    public void AltGrIsNotACommandButAltIs()
    {
        Assert.False((Option | AltGr).IsCommand());
        Assert.True(Option.IsCommand());
        Assert.True(Command.IsCommand());
        Assert.False(Shift.IsCommand());
    }

    [Fact]
    public void ApplyModifiersShowsShiftedCharacter()
    {
        _transformer.DisplayModifiedCharacters = true;
        Assert.Equal("&", _transformer.Transform(Key('7', Shift, "7", "&")));
    }

    // ---- Mouse and styles ------------------------------------------------------------

    [Fact]
    public void MouseClickWithModifiers()
    {
        var click = new MouseEventInfo { Kind = MouseEventKind.Down, Button = MouseButton.Left, X = 0, Y = 0, Modifiers = Command | Shift };
        Assert.Equal("⇧⊞" + KeystrokeTransformer.MouseGlyph, _transformer.Transform(click));
    }

    [Fact]
    public void NamesStyle()
    {
        _transformer.Style = ModifierStyle.Names;
        Assert.Equal("Ctrl+Shift+K", _transformer.Transform(Key('K', Control | Shift, "k")));
        Assert.Equal("Shift+A", _transformer.Transform(Key('A', Shift, "a")));
        Assert.Equal("Alt+F4", _transformer.Transform(Key(0x73, Option, "")));
    }

    // ---- Key caps for the Minimal visualizer -----------------------------------------

    [Fact] public void KeyCapPlainLetter() => Assert.Equal("a", _transformer.KeyCap(Key('A', None, "a")));
    [Fact] public void KeyCapCommandLetter() => Assert.Equal("A", _transformer.KeyCap(Key('A', Command, "a")));
    [Fact] public void KeyCapSpecialKeyIgnoresModifiers() => Assert.Equal("⇡", _transformer.KeyCap(Key(0x26, Option | Shift, "")));
    [Fact] public void KeyCapExcludesModifierGlyphs() => Assert.Equal("7", _transformer.KeyCap(Key('7', Command, "7")));
}
