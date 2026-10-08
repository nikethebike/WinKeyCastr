using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using WinKeyCastr.Input;
using WinKeyCastr.Localization;
using WinKeyCastr.Settings;
using WinKeyCastr.UI;
using WinKeyCastr.Visualizers;

namespace WinKeyCastr;

/// <summary>
/// Wires input to the visualizers and owns the notification-area icon and windows
/// (KeyCastr's KCAppController).
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly AppSettings _settings;
    private readonly InputHook _hook;
    private readonly MouseClickVisualizer _mouseVisualizer;
    private readonly DispatcherTimer _saveTimer;
    private readonly VisualizerContext _context;
    private TrayIcon? _tray;
    private IVisualizer? _visualizer;
    private PreferencesWindow? _preferences;
    private AboutWindow? _about;
    private Action<KeyStroke>? _keyRecorder;

    public AppController(AppSettings settings)
    {
        Current = this;
        _settings = settings;
        Transformer = new KeystrokeTransformer();
        _context = new VisualizerContext { Settings = settings, Transformer = Transformer, SaveSettings = settings.Save };
        _mouseVisualizer = new MouseClickVisualizer(settings);

        _hook = new InputHook(Dispatcher.CurrentDispatcher);
        _hook.KeyDown += OnKeyDown;
        _hook.KeyUp += OnKeyUp;
        _hook.FlagsChanged += OnFlagsChanged;
        _hook.Mouse += OnMouse;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _settings.Save();
        };
    }

    public static AppController? Current { get; private set; }

    public KeystrokeTransformer Transformer { get; }

    public bool IsCapturing { get; private set; }

    public bool IsQuitting { get; private set; }

    public void Start()
    {
        Loc.Instance.SetLanguage(_settings.Language);
        ApplyTransformerSettings();
        SetVisualizer(_settings.SelectedVisualizer);
        _mouseVisualizer.SetEnabled(_settings.MouseDisplayOption != MouseDisplayOption.None);

        _tray = new TrayIcon(this);
        _settings.PropertyChanged += OnSettingsChanged;
        _settings.Default.PropertyChanged += OnDefaultSettingsChanged;
        _settings.Changed += (_, _) =>
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        };

        if (!_hook.Start())
        {
            MessageBox.Show(Loc.T("HookError"), "KeyCastr", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        SetCapturing(_hook.IsInstalled);
        ApplyIconPlacement();

        if (_settings.ShowPreferencesAtLaunch)
            ShowPreferences();
        else if (!_settings.DisplayIcon.HasFlag(IconPlacement.NotificationArea))
            ShowPreferences(minimized: true); // the taskbar button is the only way back in
    }

    // ---- Casting -----------------------------------------------------------------------

    public void ToggleCapturing() => SetCapturing(!IsCapturing);

    private void SetCapturing(bool capture)
    {
        if (capture && !_hook.IsInstalled)
            return;
        IsCapturing = capture;
        if (!capture)
            _visualizer?.NoteFlagsChanged(ModifierFlags.None); // avoid stuck modifiers
        _tray?.Update(capture, HotkeyFormatter.Format(_settings.ToggleShortcut, Transformer));
        _preferences?.UpdateIcon(capture);
    }

    // ---- Input -------------------------------------------------------------------------

    /// <summary>While recording a shortcut, key-downs go to the recorder instead of the visualizer.</summary>
    public void BeginKeyRecording(Action<KeyStroke> recorder) => _keyRecorder = recorder;

    public void EndKeyRecording() => _keyRecorder = null;

    private void OnKeyDown(KeyStroke keystroke)
    {
        if (_keyRecorder is { } recorder)
        {
            recorder(keystroke);
            return;
        }

        var shortcut = _settings.ToggleShortcut;
        if (!shortcut.IsEmpty && keystroke.VirtualKey == shortcut.VirtualKey
            && (keystroke.Modifiers & ModifierFlags.DeviceIndependent) == shortcut.Modifiers)
        {
            if (!keystroke.IsRepeat)
                ToggleCapturing();
            return;
        }

        if (IsCapturing)
            _visualizer?.NoteKeyDown(keystroke);
    }

    private void OnKeyUp(KeyStroke keystroke) => _visualizer?.NoteKeyUp(keystroke);

#if DEBUG
    internal void SimulateKeyDown(KeyStroke keystroke) => OnKeyDown(keystroke);
    internal void SimulateKeyUp(KeyStroke keystroke) => OnKeyUp(keystroke);
    internal void SimulateFlags(ModifierFlags flags) => OnFlagsChanged(flags);
    internal void SelectPreferencesPane(string pane) => _preferences?.SelectPane(pane);
    internal void OpenTrayMenu() => _tray?.ShowMenu(staysOpen: true);
    internal void OpenFirstColorWell()
    {
        static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var d in Descendants(child))
                    yield return d;
            }
        }
        if (_preferences is not null && Descendants(_preferences).OfType<ColorWell>().FirstOrDefault(w => w.IsVisible) is { } well)
            well.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }
    internal void SimulateMouse(MouseEventKind kind) =>
        OnMouse(new MouseEventInfo { Kind = kind, Button = MouseButton.Left, X = 900, Y = 500, Modifiers = ModifierFlags.None });
#endif

    private void OnFlagsChanged(ModifierFlags flags)
    {
        if (IsCapturing)
            _visualizer?.NoteFlagsChanged(flags);
    }

    private void OnMouse(MouseEventInfo mouseEvent)
    {
        // Mouse-up always gets through so a click that started while casting can't get stuck.
        bool isUp = mouseEvent.Kind == MouseEventKind.Up;
        if (!IsCapturing && !isUp)
            return;

        var option = _settings.MouseDisplayOption;
        if (option is MouseDisplayOption.WithMousePointer or MouseDisplayOption.WithPointerAndVisualizer || isUp)
            _mouseVisualizer.Update(mouseEvent);
        if (option >= MouseDisplayOption.WithCurrentVisualizer || isUp)
            _visualizer?.NoteMouse(mouseEvent);
    }

    // ---- Settings ----------------------------------------------------------------------

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSettings.SelectedVisualizer):
                SetVisualizer(_settings.SelectedVisualizer);
                break;
            case nameof(AppSettings.Acrylic):
                SetVisualizer(_settings.SelectedVisualizer, force: true);
                break;
            case nameof(AppSettings.MouseDisplayOption):
                _mouseVisualizer.SetEnabled(_settings.MouseDisplayOption != MouseDisplayOption.None);
                break;
            case nameof(AppSettings.DisplayIcon):
                ApplyIconPlacement();
                break;
            case nameof(AppSettings.ModifierStyle):
                ApplyTransformerSettings();
                SetCapturing(IsCapturing);
                break;
            case nameof(AppSettings.ToggleShortcut):
                SetCapturing(IsCapturing);
                break;
            case nameof(AppSettings.Language):
                Loc.Instance.SetLanguage(_settings.Language);
                SetCapturing(IsCapturing); // refresh the tray menu text
                break;
        }
    }

    private void OnDefaultSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DefaultVisualizerSettings.ApplyModifiers))
            ApplyTransformerSettings();
    }

    private void ApplyTransformerSettings()
    {
        Transformer.DisplayModifiedCharacters = _settings.Default.ApplyModifiers;
        Transformer.Style = _settings.ModifierStyle;
    }

    private void SetVisualizer(string name, bool force = false)
    {
        if (!force && _visualizer?.Name == name)
            return;
        _visualizer?.Dispose();
        _visualizer = VisualizerRegistry.Create(name, _context);
        _visualizer.Show();
    }

    // ---- Windows -----------------------------------------------------------------------

    private bool ShowsInTaskbar => _settings.DisplayIcon.HasFlag(IconPlacement.Taskbar);

    private void ApplyIconPlacement()
    {
        if (_tray is not null)
            _tray.Visible = _settings.DisplayIcon.HasFlag(IconPlacement.NotificationArea);
        if (_preferences is not null)
            _preferences.ShowInTaskbar = ShowsInTaskbar;
    }

    public void ShowPreferences() => ShowPreferences(minimized: false);

    private void ShowPreferences(bool minimized)
    {
        if (_preferences is null)
        {
            _preferences = new PreferencesWindow(this, _settings) { ShowInTaskbar = ShowsInTaskbar };
        }
        if (minimized)
        {
            _preferences.WindowState = WindowState.Minimized;
            _preferences.Show();
            return;
        }
        if (_preferences.WindowState == WindowState.Minimized)
            _preferences.WindowState = WindowState.Normal;
        _preferences.Show();
        _preferences.Activate();
    }

    public void HidePreferences()
    {
        if (_preferences is null)
            return;
        // With a taskbar button (KeyCastr's Dock icon) the window stays reachable from the taskbar.
        if (ShowsInTaskbar)
            _preferences.WindowState = WindowState.Minimized;
        else
            _preferences.Hide();
    }

    public void ShowAbout()
    {
        if (_about is null)
        {
            _about = new AboutWindow();
            _about.Closed += (_, _) => _about = null;
        }
        _about.Show();
        _about.Activate();
    }

    public void Quit()
    {
        IsQuitting = true;
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _hook.Dispose();
        _saveTimer.Stop();
        _settings.Save();
        _visualizer?.Dispose();
        _visualizer = null;
        _mouseVisualizer.Dispose();
        _tray?.Dispose();
        _tray = null;
        _preferences?.Close();
        _about?.Close();
    }
}
