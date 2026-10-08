using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinKeyCastr.Input;
using WinKeyCastr.Localization;
using WinKeyCastr.Native;
using WinKeyCastr.Settings;

namespace WinKeyCastr.UI;

internal static class HotkeyFormatter
{
    /// <summary>"⌃⌥⊞K" or "Ctrl+Alt+Win+K", depending on the transformer's style.</summary>
    public static string Format(Hotkey hotkey, KeystrokeTransformer transformer)
    {
        if (hotkey.IsEmpty)
            return "";
        var text = new StringBuilder();
        foreach (var modifier in (ReadOnlySpan<ModifierFlags>)[ModifierFlags.Control, ModifierFlags.Option, ModifierFlags.Shift, ModifierFlags.Command])
        {
            if ((hotkey.Modifiers & modifier) != 0)
                text.Append(transformer.Glyph(modifier));
        }
        text.Append(KeyName(hotkey.VirtualKey));
        return text.ToString();
    }

    public static string KeyName(int vk)
    {
        if (KeystrokeTransformer.SpecialKeys.TryGetValue(vk, out var special))
            return special.TrimEnd('​');
        if (vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
            return ((char)vk).ToString();
        uint mapped = Win32.MapVirtualKeyEx((uint)vk, 2, Win32.GetKeyboardLayout(0)) & 0x7FFF;
        return mapped >= 0x20 ? char.ToUpperInvariant((char)mapped).ToString() : $"#{vk}";
    }
}

/// <summary>
/// Click to record a new shortcut, like the ShortcutRecorder control KeyCastr uses.
/// Keys are read from the global hook, so Windows-key chords can be recorded too.
/// Esc cancels; the ✕ button clears the shortcut.
/// </summary>
public sealed class ShortcutRecorder : Border
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(Hotkey), typeof(ShortcutRecorder),
        new FrameworkPropertyMetadata(default(Hotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ShortcutRecorder)d).UpdateDisplay()));

    private readonly TextBlock _label;
    private readonly Button _clearButton;
    private bool _recording;

    public ShortcutRecorder()
    {
        CornerRadius = new CornerRadius(4);
        BorderThickness = new Thickness(1);
        Height = 32;
        MinWidth = 140;
        Cursor = Cursors.Hand;
        Focusable = true;
        SetResourceReference(BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        SetResourceReference(BackgroundProperty, "ControlFillColorDefaultBrush");

        _label = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 24, 0),
        };
        _clearButton = new Button
        {
            Content = "✕",
            FontSize = 10,
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Loc.T("Recorder_Clear"),
            Cursor = Cursors.Arrow,
        };
        _clearButton.Click += (_, e) =>
        {
            e.Handled = true;
            StopRecording();
            Hotkey = default;
        };

        var grid = new Grid();
        grid.Children.Add(_label);
        grid.Children.Add(_clearButton);
        Child = grid;

        MouseLeftButtonUp += (_, _) =>
        {
            if (_recording)
                StopRecording();
            else
                StartRecording();
        };
        LostKeyboardFocus += (_, _) => StopRecording();
        Unloaded += (_, _) => StopRecording();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
                StopRecording();
        };
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            _clearButton.ToolTip = Loc.T("Recorder_Clear");
            UpdateDisplay();
        };
        UpdateDisplay();
    }

    public Hotkey Hotkey
    {
        get => (Hotkey)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    private void StartRecording()
    {
        if (AppController.Current is not { } controller)
            return;
        _recording = true;
        Focus();
        controller.BeginKeyRecording(OnRecordedKey);
        UpdateDisplay();
    }

    private void StopRecording()
    {
        if (!_recording)
            return;
        _recording = false;
        AppController.Current?.EndKeyRecording();
        UpdateDisplay();
    }

    private void OnRecordedKey(KeyStroke keystroke)
    {
        const int VK_ESCAPE = 0x1B;
        var modifiers = keystroke.Modifiers & ModifierFlags.DeviceIndependent;
        if (keystroke.VirtualKey == VK_ESCAPE && modifiers == ModifierFlags.None)
        {
            StopRecording();
            return;
        }

        bool isFunctionKey = keystroke.VirtualKey is >= 0x70 and <= 0x87;
        if (modifiers == ModifierFlags.None && !isFunctionKey)
            return; // a shortcut needs a modifier, like the original recorder

        Hotkey = new Hotkey(keystroke.VirtualKey, modifiers);
        StopRecording();
    }

    private void UpdateDisplay()
    {
        var transformer = AppController.Current?.Transformer ?? new KeystrokeTransformer();
        if (_recording)
        {
            _label.Text = Loc.T("Recorder_Type");
            _label.Opacity = 0.7;
            SetResourceReference(BorderBrushProperty, "AccentFillColorDefaultBrush");
        }
        else
        {
            _label.Text = Hotkey.IsEmpty ? Loc.T("Recorder_Empty") : HotkeyFormatter.Format(Hotkey, transformer);
            _label.Opacity = Hotkey.IsEmpty ? 0.6 : 1;
            SetResourceReference(BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        }
        _clearButton.Visibility = !_recording && !Hotkey.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }
}
