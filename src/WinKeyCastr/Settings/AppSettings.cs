using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using WinKeyCastr.Input;
using WinKeyCastr.Localization;

namespace WinKeyCastr.Settings;

/// <summary>A keyboard shortcut: a virtual key plus device-independent modifiers.</summary>
public readonly record struct Hotkey(int VirtualKey, ModifierFlags Modifiers)
{
    public bool IsEmpty => VirtualKey == 0;
}

public enum DefaultDisplayMode
{
    CommandKeysOnly,
    AllModifiedKeys,
    AllKeys,
}

[Flags]
public enum IconPlacement
{
    NotificationArea = 1,
    Taskbar = 2,
    Both = NotificationArea | Taskbar,
}

public enum MouseDisplayOption
{
    None,
    WithMousePointer,
    WithCurrentVisualizer,
    WithPointerAndVisualizer,
}

/// <summary>Screen position saved as a pair of nullable coordinates (null = use the default spot).</summary>
public sealed class SavedPosition
{
    public double? X { get; set; }
    public double? Y { get; set; }
}

/// <summary>
/// All preferences. Defaults mirror KeyCastr's registered user defaults.
/// Saved as JSON in %APPDATA%\WinKeyCastr\settings.json.
/// </summary>
public sealed class AppSettings : ObservableObject
{
    // General
    private IconPlacement _displayIcon = IconPlacement.Both;
    private Hotkey _toggleShortcut = new(0x4B /* K */, ModifierFlags.Control | ModifierFlags.Option | ModifierFlags.Command);
    private bool _showPreferencesAtLaunch = true;
    private ModifierStyle _modifierStyle = ModifierStyle.Symbols;
    private AppLanguage _language = AppLanguage.System;

    // Display
    private string _selectedVisualizer = "Default";
    private MouseDisplayOption _mouseDisplayOption = MouseDisplayOption.None;
    private bool _acrylic = true;

    public IconPlacement DisplayIcon { get => _displayIcon; set => Set(ref _displayIcon, value); }
    public Hotkey ToggleShortcut { get => _toggleShortcut; set => Set(ref _toggleShortcut, value); }
    public bool ShowPreferencesAtLaunch { get => _showPreferencesAtLaunch; set => Set(ref _showPreferencesAtLaunch, value); }
    public ModifierStyle ModifierStyle { get => _modifierStyle; set => Set(ref _modifierStyle, value); }
    public AppLanguage Language { get => _language; set => Set(ref _language, value); }
    public string SelectedVisualizer { get => _selectedVisualizer; set => Set(ref _selectedVisualizer, value); }
    public MouseDisplayOption MouseDisplayOption { get => _mouseDisplayOption; set => Set(ref _mouseDisplayOption, value); }

    /// <summary>Blur what is behind the bezels (Windows addition; KeyCastr bezels are flat).</summary>
    public bool Acrylic { get => _acrylic; set => Set(ref _acrylic, value); }

    public DefaultVisualizerSettings Default { get; set; } = new();
    public SvelteVisualizerSettings Svelte { get; set; } = new();
    public MinimalVisualizerSettings Minimal { get; set; } = new();

    // ---- Persistence -------------------------------------------------------------------

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinKeyCastr", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(), new ColorJsonConverter() },
    };

    /// <summary>Raised when this object or any nested visualizer settings change.</summary>
    public event EventHandler? Changed;

    public static AppSettings Load()
    {
        AppSettings? settings = null;
        try
        {
            if (File.Exists(FilePath))
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
        }
        catch
        {
            // A corrupt file falls back to defaults.
        }
        settings ??= new AppSettings();
        settings.Wire();
        return settings;
    }

    /// <summary>When set, <see cref="Save"/> does nothing (used by the debug demo mode).</summary>
    [JsonIgnore]
    public bool IsReadOnly { get; set; }

    public void Save()
    {
        if (IsReadOnly)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Preferences are best effort; never crash the caster over a write failure.
        }
    }

    private void Wire()
    {
        PropertyChangedEventHandler forward = (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        PropertyChanged += forward;
        Default.PropertyChanged += forward;
        Svelte.PropertyChanged += forward;
        Minimal.PropertyChanged += forward;
    }
}

public sealed class DefaultVisualizerSettings : ObservableObject
{
    private DefaultDisplayMode _displayMode = DefaultDisplayMode.CommandKeysOnly;
    private bool _applyModifiers;
    private double _fontSize = 16.0;
    private double _keystrokeDelay = 0.5;
    private double _fadeDelay = 2.0;
    private double _fadeDuration = 0.2;
    private Color _bezelColor = Color.FromArgb(204, 0, 0, 0);   // black, 80 %
    private Color _textColor = Colors.White;

    public DefaultDisplayMode DisplayMode { get => _displayMode; set => Set(ref _displayMode, value); }

    /// <summary>KeyCastr's "Apply Modifiers" (default_displayModifiedCharacters).</summary>
    public bool ApplyModifiers { get => _applyModifiers; set => Set(ref _applyModifiers, value); }

    public double FontSize { get => _fontSize; set => Set(ref _fontSize, value); }

    /// <summary>"Line Break Delay": seconds of inactivity before a new bezel starts.</summary>
    public double KeystrokeDelay { get => _keystrokeDelay; set => Set(ref _keystrokeDelay, value); }

    /// <summary>"Linger Time": seconds before a bezel starts fading.</summary>
    public double FadeDelay { get => _fadeDelay; set => Set(ref _fadeDelay, value); }

    public double FadeDuration { get => _fadeDuration; set => Set(ref _fadeDuration, value); }
    public Color BezelColor { get => _bezelColor; set => Set(ref _bezelColor, value); }
    public Color TextColor { get => _textColor; set => Set(ref _textColor, value); }

    /// <summary>Bottom-left corner of the bezel stack, in WPF screen units.</summary>
    public SavedPosition Position { get; set; } = new();
}

public sealed class SvelteVisualizerSettings : ObservableObject
{
    private bool _displayAll = true;

    public bool DisplayAll { get => _displayAll; set => Set(ref _displayAll, value); }

    /// <summary>Top-left corner of the window, in WPF screen units.</summary>
    public SavedPosition Position { get; set; } = new();
}

public sealed class MinimalVisualizerSettings : ObservableObject
{
    private bool _modifierKeys = true;
    private bool _specialKeys = true;
    private bool _allKeys = true;
    private bool _anchorRight;
    private double _fontSize = 80.0;
    private double _bezelSize = 100.0;
    private double _borderRadius = 10.0;
    private Color _bezelColor = Color.FromArgb(191, 0, 0, 0);       // black, 75 %
    private Color _textColor = Color.FromArgb(204, 255, 255, 255);  // white, 80 %
    private Color _textShadowColor = Colors.Black;

    public bool ModifierKeys { get => _modifierKeys; set => Set(ref _modifierKeys, value); }
    public bool SpecialKeys { get => _specialKeys; set => Set(ref _specialKeys, value); }
    public bool AllKeys { get => _allKeys; set => Set(ref _allKeys, value); }
    public bool AnchorRight { get => _anchorRight; set => Set(ref _anchorRight, value); }
    public double FontSize { get => _fontSize; set => Set(ref _fontSize, value); }
    public double BezelSize { get => _bezelSize; set => Set(ref _bezelSize, value); }
    public double BorderRadius { get => _borderRadius; set => Set(ref _borderRadius, value); }
    public Color BezelColor { get => _bezelColor; set => Set(ref _bezelColor, value); }
    public Color TextColor { get => _textColor; set => Set(ref _textColor, value); }
    public Color TextShadowColor { get => _textShadowColor; set => Set(ref _textShadowColor, value); }

    /// <summary>X of the anchored edge (left or right) and Y of the top edge, in WPF screen units.</summary>
    public SavedPosition Position { get; set; } = new();
}

internal sealed class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(reader.GetString()!);
        }
        catch
        {
            return Colors.Black;
        }
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) =>
        writer.WriteStringValue($"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
}
