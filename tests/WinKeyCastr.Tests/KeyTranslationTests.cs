using System.Runtime.InteropServices;
using WinKeyCastr.Input;
using Xunit;

namespace WinKeyCastr.Tests;

/// <summary>
/// Exercises the real ToUnicodeEx path the hook uses, against keyboard layouts installed on this machine.
/// Tests for a layout that is not installed pass vacuously.
/// </summary>
public class KeyTranslationTests
{
    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(int count, IntPtr[]? list);

    private static IntPtr? InstalledLayout(int languageId)
    {
        int count = GetKeyboardLayoutList(0, null);
        var layouts = new IntPtr[count];
        GetKeyboardLayoutList(count, layouts);
        foreach (var layout in layouts)
        {
            if ((layout.ToInt64() & 0xFFFF) == languageId)
                return layout;
        }
        return null;
    }

    [Theory]
    [InlineData('A', "a")]
    [InlineData('Z', "z")]
    [InlineData('7', "7")]
    [InlineData(0xBD, "-")]  // VK_OEM_MINUS
    public void EnglishKeyCaps(int vk, string expected)
    {
        if (InstalledLayout(0x0409) is not { } us)
            return;
        Assert.Equal(expected, InputHook.TranslateKeyCap(vk, 0, us));
    }

    [Theory]
    [InlineData('A', "ф")]
    [InlineData('C', "с")]
    [InlineData(0xDB, "х")]  // VK_OEM_4
    public void RussianKeyCaps(int vk, string expected)
    {
        if (InstalledLayout(0x0419) is not { } ru)
            return;
        Assert.Equal(expected, InputHook.TranslateKeyCap(vk, 0, ru));
    }

    [Fact]
    public void ShiftAndCapsLockProduceUppercase()
    {
        if (InstalledLayout(0x0409) is not { } us)
            return;
        Assert.Equal("A", InputHook.TranslateCharacters('A', 0, us, ModifierFlags.Shift, capsLock: false));
        Assert.Equal("A", InputHook.TranslateCharacters('A', 0, us, ModifierFlags.None, capsLock: true));
        Assert.Equal("&", InputHook.TranslateCharacters('7', 0, us, ModifierFlags.Shift, capsLock: false));
    }

    [Fact]
    public void NonPrintingKeysTranslateToNothing()
    {
        if (InstalledLayout(0x0409) is not { } us)
            return;
        Assert.Equal("", InputHook.TranslateKeyCap(0x87 /* F24 */, 0, us));
        Assert.Equal("", InputHook.TranslateCharacters('A', 0, us, ModifierFlags.Control, capsLock: false));
    }

    [Fact]
    public void RepeatedTranslationIsStable()
    {
        // Guards against the buffer-marshalling bug that corrupted the heap.
        if (InstalledLayout(0x0419) is not { } ru)
            return;
        for (int i = 0; i < 10_000; i++)
            Assert.Equal("ф", InputHook.TranslateKeyCap('A', 0, ru));
    }
}
