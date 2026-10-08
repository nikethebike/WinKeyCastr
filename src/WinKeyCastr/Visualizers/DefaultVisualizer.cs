using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinKeyCastr.Input;
using WinKeyCastr.Overlay;
using WinKeyCastr.Settings;

namespace WinKeyCastr.Visualizers;

/// <summary>
/// KeyCastr's "Default" visualizer: a stack of rounded bezels in the bottom-left corner. Keystrokes
/// typed in quick succession share a bezel; commands, mouse clicks and pauses longer than the line
/// break delay start a new one. The newest bezel sits at the bottom, older ones are pushed up and
/// fade out after the linger time.
/// </summary>
public sealed class DefaultVisualizer : IVisualizer
{
    internal const double BezelPadding = 10;   // kKCDefaultBezelPadding: gap between bezels and to the screen edge
    internal const double BezelBorder = 6;     // kKCBezelBorder: inset of the text inside a bezel
    internal const double BezelCornerRadius = 16;

    private readonly VisualizerContext _context;
    private readonly List<Bezel> _bezels = [];   // oldest first
    private readonly DispatcherTimer _lineBreakTimer;
    private Bezel? _currentBezel;
    private Point _anchor;                        // bottom-left corner of the stack
    private bool _visible = true;
    private bool _dragging;

    public DefaultVisualizer(VisualizerContext context)
    {
        _context = context;
        _lineBreakTimer = new DispatcherTimer();
        _lineBreakTimer.Tick += (_, _) => AbandonCurrentBezel();
        _anchor = RestoreAnchor();
    }

    public string Name => "Default";

    private DefaultVisualizerSettings Settings => _context.Settings.Default;

    public void Show()
    {
        _visible = true;
        foreach (var bezel in _bezels)
            bezel.Show();
    }

    public void Hide()
    {
        _visible = false;
        foreach (var bezel in _bezels)
            bezel.Hide();
    }

    public void NoteKeyDown(KeyStroke keystroke)
    {
        if (!keystroke.IsCommand && Settings.DisplayMode == DefaultDisplayMode.CommandKeysOnly)
            return;
        if (!keystroke.IsModified && Settings.DisplayMode == DefaultDisplayMode.AllModifiedKeys)
            return;

        _lineBreakTimer.Stop();
        if (keystroke.IsCommand)
            AbandonCurrentBezel();
        AppendString(_context.Transformer.Transform(keystroke));
    }

    public void NoteFlagsChanged(ModifierFlags flags)
    {
        // KeyCastr does not display bare modifier presses in this visualizer.
    }

    public void NoteMouse(MouseEventInfo mouseEvent)
    {
        if (mouseEvent.Kind != MouseEventKind.Down)
            return;
        AbandonCurrentBezel();
        AppendString(_context.Transformer.Transform(mouseEvent));
    }

    private void AbandonCurrentBezel()
    {
        _lineBreakTimer.Stop();
        _currentBezel = null;
    }

    private void AppendString(string text)
    {
        if (_currentBezel is null || !_bezels.Contains(_currentBezel))
        {
            var workArea = ScreenInfo.WorkAreaAt(_anchor);
            double maxWidth = Math.Max(100, workArea.Right - _anchor.X - BezelPadding);
            var bezel = new Bezel(_context.Settings, maxWidth, text);
            bezel.SizeChanged += (_, _) => LayoutBezels();
            bezel.FadedOut += () => RemoveBezel(bezel);
            bezel.DragStarted += OnDragStarted;
            bezel.Dragged += OnDragged;
            bezel.DragEnded += OnDragEnded;
            if (_dragging)
                bezel.PauseFade();

            _bezels.Add(bezel);
            _currentBezel = bezel;
            bezel.Left = _anchor.X;
            bezel.Top = _anchor.Y;   // positioned properly once measured
            LayoutBezels();
            if (_visible)
                bezel.Show();
        }
        else
        {
            _currentBezel.Append(text);
        }

        _lineBreakTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.01, Settings.KeystrokeDelay));
        _lineBreakTimer.Start();
    }

    /// <summary>Stack the bezels upwards from the anchor, newest at the bottom.</summary>
    private void LayoutBezels()
    {
        double bottom = _anchor.Y;
        for (int i = _bezels.Count - 1; i >= 0; i--)
        {
            var bezel = _bezels[i];
            double height = bezel.ActualHeight > 0 ? bezel.ActualHeight : bezel.EstimatedHeight;
            bezel.Left = _anchor.X;
            bezel.Top = bottom - height;
            bottom = bezel.Top - BezelPadding;
        }
    }

    private void RemoveBezel(Bezel bezel)
    {
        _bezels.Remove(bezel);
        if (_currentBezel == bezel)
            _currentBezel = null;
        bezel.Close();
        LayoutBezels();
    }

    // ---- Dragging: moving any bezel moves the whole stack ------------------------------

    private void OnDragStarted()
    {
        _dragging = true;
        foreach (var bezel in _bezels)
            bezel.PauseFade();
    }

    private void OnDragged(Vector delta)
    {
        _anchor += delta;
        LayoutBezels();
    }

    private void OnDragEnded()
    {
        _dragging = false;
        _anchor = ClampAnchor(_anchor);
        LayoutBezels();
        Settings.Position.X = _anchor.X;
        Settings.Position.Y = _anchor.Y;
        _context.SaveSettings();
        foreach (var bezel in _bezels.ToArray())
            bezel.ResumeFade();
    }

    private Point RestoreAnchor()
    {
        var position = Settings.Position;
        if (position.X is double x && position.Y is double y)
            return ClampAnchor(new Point(x, y));
        return DefaultAnchor();
    }

    private static Point DefaultAnchor()
    {
        var workArea = ScreenInfo.PrimaryWorkArea;
        return new Point(workArea.Left + BezelPadding, workArea.Bottom - BezelPadding);
    }

    private static Point ClampAnchor(Point anchor)
    {
        var bounds = ScreenInfo.VirtualScreen;
        bounds.Inflate(-BezelPadding, -BezelPadding);
        return bounds.Contains(anchor) ? anchor : DefaultAnchor();
    }

    public void Dispose()
    {
        _lineBreakTimer.Stop();
        foreach (var bezel in _bezels.ToArray())
            bezel.Close();
        _bezels.Clear();
        _currentBezel = null;
    }
}

/// <summary>One rounded bezel of text (KCDefaultVisualizerBezelView).</summary>
internal sealed class Bezel : OverlayWindow
{
    private readonly AppSettings _settings;
    private readonly TextBlock _text;
    private readonly DispatcherTimer _fadeDelayTimer;
    private DispatcherTimer? _fadeTimer;
    private double _fadeProgress;
    private DateTime _fadeTickTime;
    private bool _fadePaused;
    private bool _fadePending;

    public event Action? FadedOut;

    public Bezel(AppSettings settings, double maxWidth, string text) : base(settings.Acrylic)
    {
        _settings = settings;
        var s = settings.Default;

        _text = new TextBlock
        {
            Text = text,
            FontFamily = SystemFonts.MessageFontFamily,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = Math.Max(1, maxWidth - DefaultVisualizer.BezelBorder * 2),
        };
        ApplyTextAttributes();

        Content = new Border
        {
            CornerRadius = new CornerRadius(DefaultVisualizer.BezelCornerRadius),
            Background = new SolidColorBrush(s.BezelColor),
            Padding = new Thickness(DefaultVisualizer.BezelBorder),
            Child = _text,
        };
        SizeToContent = SizeToContent.WidthAndHeight;
        BackdropCornerRadius = DefaultVisualizer.BezelCornerRadius;

        _fadeDelayTimer = new DispatcherTimer();
        _fadeDelayTimer.Tick += (_, _) => BeginFadeOut();
        ScheduleFadeOut();
    }

    /// <summary>Height before the first layout pass: one line of text plus the border.</summary>
    public double EstimatedHeight => _settings.Default.FontSize * 1.33 + DefaultVisualizer.BezelBorder * 2;

    public void Append(string text)
    {
        ScheduleFadeOut();
        _text.Text += text;
        ApplyTextAttributes();
    }

    private void ApplyTextAttributes()
    {
        var s = _settings.Default;
        _text.FontSize = Math.Max(1, s.FontSize);
        _text.Foreground = new SolidColorBrush(s.TextColor);
    }

    private void ScheduleFadeOut()
    {
        // Any new text restarts the linger time and cancels a fade already in progress.
        _fadeTimer?.Stop();
        _fadeTimer = null;
        _fadeProgress = 0;
        _fadePending = false;
        FadeOpacity = 1;

        double delay = _settings.Default.FadeDelay;
        if (delay <= 0)
            delay = 2;
        _fadeDelayTimer.Stop();
        _fadeDelayTimer.Interval = TimeSpan.FromSeconds(delay);
        _fadeDelayTimer.Start();
    }

    private void BeginFadeOut()
    {
        _fadeDelayTimer.Stop();
        if (_fadePaused)
        {
            _fadePending = true;
            return;
        }

        double duration = _settings.Default.FadeDuration;
        if (duration < 0.01)
        {
            FadedOut?.Invoke();
            return;
        }

        _fadeTickTime = DateTime.UtcNow;
        _fadeTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _fadeTimer.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            _fadeProgress += (now - _fadeTickTime).TotalSeconds / duration;
            _fadeTickTime = now;
            if (_fadeProgress >= 1)
            {
                _fadeTimer?.Stop();
                _fadeTimer = null;
                FadedOut?.Invoke();
                return;
            }
            FadeOpacity = 1 - _fadeProgress;
        };
        _fadeTimer.Start();
    }

    public void PauseFade()
    {
        _fadePaused = true;
        if (_fadeTimer is not null)
        {
            _fadeTimer.Stop();
            _fadePending = true;
        }
    }

    public void ResumeFade()
    {
        _fadePaused = false;
        if (!_fadePending)
            return;
        _fadePending = false;
        if (_fadeTimer is not null)
        {
            _fadeTickTime = DateTime.UtcNow;
            _fadeTimer.Start();
        }
        else
        {
            BeginFadeOut();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _fadeDelayTimer.Stop();
        _fadeTimer?.Stop();
        base.OnClosed(e);
    }
}
