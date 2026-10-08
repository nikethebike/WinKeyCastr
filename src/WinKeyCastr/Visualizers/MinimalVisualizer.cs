using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WinKeyCastr.Input;
using WinKeyCastr.Overlay;
using WinKeyCastr.Settings;

namespace WinKeyCastr.Visualizers;

/// <summary>
/// KeyCastr's "Minimal" visualizer: one bezel that shows exactly what is held down right now —
/// modifiers, the key cap and the mouse button — and disappears when everything is released.
/// </summary>
public sealed class MinimalVisualizer : IVisualizer
{
    // Largest font size, as a fraction of the bezel height, that still fits inside the bezel.
    private const double MaxFontToBezelRatio = 0.85;

    private static readonly ModifierFlags[] ModifierOrder =
        [ModifierFlags.Control, ModifierFlags.Option, ModifierFlags.Shift, ModifierFlags.Command];

    private readonly VisualizerContext _context;
    private readonly OverlayWindow _window;
    private readonly Border _bezel;
    private readonly StackPanel _slots;

    private ModifierFlags _flags;
    private string? _characters;
    private bool _mouse;
    private bool _visible = true;
    private double _anchorX;
    private double _top;
    private double _lastFontSize;
    private double _lastBezelSize;
    private bool _adjustingCoupledSizes;

    public MinimalVisualizer(VisualizerContext context)
    {
        _context = context;
        _window = new OverlayWindow(context.Settings.Acrylic) { SizeToContent = SizeToContent.WidthAndHeight };
        _slots = new StackPanel { Orientation = Orientation.Horizontal };
        _bezel = new Border { Child = _slots };
        _window.Content = _bezel;

        _lastFontSize = Settings.FontSize;
        _lastBezelSize = Settings.BezelSize;
        EnforceFontBezelCoupling();
        RestorePosition();

        _window.SizeChanged += (_, _) => PlaceWindow();
        _window.Dragged += delta =>
        {
            _anchorX += delta.X;
            _top += delta.Y;
            PlaceWindow();
        };
        _window.DragEnded += SavePosition;
        Settings.PropertyChanged += OnSettingsChanged;
        Rebuild();
    }

    public string Name => "Minimal";

    private MinimalVisualizerSettings Settings => _context.Settings.Minimal;

    public void Show()
    {
        _visible = true;
        UpdateVisibility();
    }

    public void Hide()
    {
        _visible = false;
        _window.Hide();
    }

    public void NoteKeyDown(KeyStroke keystroke)
    {
        if (keystroke.IsRepeat)
            return;
        _characters = ShouldDisplay(keystroke) ? _context.Transformer.KeyCap(keystroke) : null;
        Rebuild();
    }

    public void NoteKeyUp(KeyStroke? keystroke)
    {
        _characters = null;
        Rebuild();
    }

    public void NoteFlagsChanged(ModifierFlags flags)
    {
        _flags = flags;
        Rebuild();
    }

    public void NoteMouse(MouseEventInfo mouseEvent)
    {
        if (mouseEvent.Kind == MouseEventKind.Down)
            _mouse = true;
        else if (mouseEvent.Kind == MouseEventKind.Up)
            _mouse = false;
        else
            return;
        Rebuild();
    }

    private bool ShouldDisplay(KeyStroke keystroke)
    {
        bool isSpecial = KeystrokeTransformer.SpecialKeys.ContainsKey(keystroke.VirtualKey);
        if (!(isSpecial ? Settings.SpecialKeys : Settings.AllKeys))
            return false;
        if (keystroke.IsModified && !Settings.ModifierKeys)
            return false;
        return true;
    }

    private void Rebuild()
    {
        var s = Settings;
        double bezelSize = Math.Max(10, s.BezelSize);
        var flags = s.ModifierKeys ? _flags & ModifierFlags.DeviceIndependent : ModifierFlags.None;

        _slots.Children.Clear();
        foreach (var modifier in ModifierOrder)
        {
            if ((flags & modifier) != 0)
                _slots.Children.Add(Slot(_context.Transformer.ModifierLabel(modifier), bezelSize, bezelSize));
        }

        if (!string.IsNullOrWhiteSpace(_characters))
        {
            var label = CreateLabel(_characters);
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double width = Math.Max(bezelSize, Math.Ceiling(label.DesiredSize.Width) + Math.Round(bezelSize * 0.3));
            _slots.Children.Add(Slot(label, width, bezelSize));
        }

        if (_mouse)
            _slots.Children.Add(Slot(KeystrokeTransformer.MouseGlyph, bezelSize, bezelSize));

        _bezel.Background = new SolidColorBrush(s.BezelColor);
        _bezel.CornerRadius = new CornerRadius(s.BorderRadius);
        _window.BackdropCornerRadius = s.BorderRadius;
        UpdateVisibility();
    }

    private FrameworkElement Slot(string text, double width, double height) => Slot(CreateLabel(text), width, height);

    private static FrameworkElement Slot(TextBlock label, double width, double height) => new Border
    {
        Width = width,
        Height = height,
        // Shrink labels that are wider than their slot (e.g. "Shift" in the Names style).
        Child = new Viewbox { StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(width * 0.05, 0, width * 0.05, 0), Child = label },
    };

    private TextBlock CreateLabel(string text)
    {
        var s = Settings;
        return new TextBlock
        {
            Text = text,
            FontFamily = SystemFonts.MessageFontFamily,
            FontWeight = FontWeights.Bold,
            FontSize = Math.Max(1, s.FontSize),
            Foreground = new SolidColorBrush(s.TextColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect { Color = s.TextShadowColor, BlurRadius = 2, ShadowDepth = 2.83, Direction = 315, Opacity = s.TextShadowColor.A / 255.0 },
        };
    }

    private void UpdateVisibility()
    {
        bool hasContent = _slots.Children.Count > 0;
        if (_visible && hasContent)
        {
            if (!_window.IsVisible)
                _window.Show();
            PlaceWindow();
        }
        else if (_window.IsVisible)
        {
            _window.Hide();
        }
    }

    /// <summary>Keep the anchored edge fixed as the bezel grows and shrinks.</summary>
    private void PlaceWindow()
    {
        double width = _window.ActualWidth;
        _window.Left = Settings.AnchorRight ? _anchorX - width : _anchorX;
        _window.Top = _top;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_adjustingCoupledSizes)
            return;
        if (e.PropertyName == nameof(MinimalVisualizerSettings.AnchorRight))
        {
            // Re-anchor on the other edge without moving the bezel.
            _anchorX = Settings.AnchorRight ? _window.Left + _window.ActualWidth : _window.Left;
            SavePosition();
        }
        EnforceFontBezelCoupling();
        Rebuild();
    }

    /// <summary>
    /// Keeps the font within <see cref="MaxFontToBezelRatio"/> of the bezel. Whichever slider the
    /// user did not move yields: a larger font grows the bezel, a smaller bezel shrinks the font.
    /// </summary>
    private void EnforceFontBezelCoupling()
    {
        var s = Settings;
        double font = s.FontSize, bezel = s.BezelSize;
        bool fontChanged = font != _lastFontSize;
        bool bezelChanged = bezel != _lastBezelSize;

        if (font > MaxFontToBezelRatio * bezel)
        {
            _adjustingCoupledSizes = true;
            if (fontChanged && !bezelChanged)
            {
                bezel = Math.Ceiling(font / MaxFontToBezelRatio);
                s.BezelSize = bezel;
            }
            else
            {
                font = Math.Floor(MaxFontToBezelRatio * bezel);
                s.FontSize = font;
            }
            _adjustingCoupledSizes = false;
        }
        _lastFontSize = font;
        _lastBezelSize = bezel;
    }

    private void RestorePosition()
    {
        var position = Settings.Position;
        var bounds = ScreenInfo.VirtualScreen;
        if (position.X is double x && position.Y is double y && bounds.Contains(new Point(x, y)))
        {
            _anchorX = x;
            _top = y;
        }
        else
        {
            var workArea = ScreenInfo.PrimaryWorkArea;
            double bezelSize = Settings.BezelSize;
            _anchorX = workArea.Left + bezelSize;
            _top = workArea.Bottom - bezelSize - bezelSize;
        }
    }

    private void SavePosition()
    {
        Settings.Position.X = _anchorX;
        Settings.Position.Y = _top;
        _context.SaveSettings();
    }

    public void Dispose()
    {
        Settings.PropertyChanged -= OnSettingsChanged;
        _window.Close();
    }
}
