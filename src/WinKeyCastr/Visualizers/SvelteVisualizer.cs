using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WinKeyCastr.Input;
using WinKeyCastr.Overlay;
using WinKeyCastr.Settings;

namespace WinKeyCastr.Visualizers;

/// <summary>
/// KeyCastr's "Svelte" visualizer: a small fixed panel with the last few keystrokes on top and a
/// row of modifier indicators (⇧ ⌃ ⌥ ⌘) underneath that light up while held.
/// </summary>
public sealed class SvelteVisualizer : IVisualizer
{
    private const double PanelWidth = 200;
    private const double PanelHeight = 100;
    private const double ModifierRowHeight = 30;
    private const double CornerRadius = 16;
    private const int MaxDisplayedCharacters = 6;

    private static readonly ModifierFlags[] ModifierOrder =
        [ModifierFlags.Shift, ModifierFlags.Control, ModifierFlags.Option, ModifierFlags.Command];

    private readonly VisualizerContext _context;
    private readonly OverlayWindow _window;
    private readonly TextBlock _displayedText;
    private readonly TextBlock[] _modifierLabels = new TextBlock[4];
    private string? _displayed;
    private ModifierFlags _flags;

    public SvelteVisualizer(VisualizerContext context)
    {
        _context = context;
        _window = new OverlayWindow(context.Settings.Acrylic)
        {
            Width = PanelWidth,
            Height = PanelHeight,
            BackdropCornerRadius = CornerRadius,
        };

        var root = new Grid();
        root.Children.Add(new System.Windows.Shapes.Path
        {
            Data = BuildBackground(),
            // KeyCastr's fixed black at 85 %. Over acrylic that would hide the blur almost entirely,
            // so the tint is lighter there, as with Windows' own acrylic surfaces.
            Fill = new SolidColorBrush(context.Settings.Acrylic && _window.HasBackdrop
                ? Color.FromArgb(110, 0, 0, 0)
                : Color.FromArgb(217, 0, 0, 0)),
        });

        _displayedText = new TextBlock
        {
            Foreground = Brushes.White,
            FontFamily = SystemFonts.MessageFontFamily,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, ModifierRowHeight),
            // Keeps the text readable over the lighter acrylic tint.
            Effect = _window.HasBackdrop
                ? new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1.5, Direction = 315, Opacity = 0.6 }
                : null,
        };
        root.Children.Add(_displayedText);

        var row = new UniformGrid { Rows = 1, Columns = 4, Height = ModifierRowHeight, VerticalAlignment = VerticalAlignment.Bottom };
        for (int i = 0; i < 4; i++)
        {
            _modifierLabels[i] = new TextBlock
            {
                FontFamily = SystemFonts.MessageFontFamily,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 2, ShadowDepth = 2.83, Direction = 315, Opacity = 1 },
            };
            row.Children.Add(_modifierLabels[i]);
        }
        root.Children.Add(row);
        _window.Content = root;

        RestorePosition();
        _window.DragEnded += SavePosition;
        _window.Dragged += delta =>
        {
            _window.Left += delta.X;
            _window.Top += delta.Y;
        };
        Redraw();
    }

    public string Name => "Svelte";

    public void Show() => _window.Show();

    public void Hide() => _window.Hide();

    public void NoteKeyDown(KeyStroke keystroke)
    {
        if (!_context.Settings.Svelte.DisplayAll && !keystroke.IsCommand)
            return;
        Append(_context.Transformer.Transform(keystroke));
    }

    public void NoteFlagsChanged(ModifierFlags flags)
    {
        _displayed = null;
        _flags = flags;
        Redraw();
    }

    public void NoteMouse(MouseEventInfo mouseEvent)
    {
        if (mouseEvent.Kind == MouseEventKind.Down)
            Append(_context.Transformer.Transform(mouseEvent));
        else if (mouseEvent.Kind == MouseEventKind.Up)
            NoteFlagsChanged(mouseEvent.Modifiers);
    }

    private void Append(string text)
    {
        var combined = (_displayed ?? "") + text;
        var elements = StringInfo.GetTextElementEnumerator(combined);
        var parts = new List<string>();
        while (elements.MoveNext())
            parts.Add(elements.GetTextElement());
        _displayed = string.Concat(parts.Skip(Math.Max(0, parts.Count - MaxDisplayedCharacters)));
        Redraw();
    }

    private void Redraw()
    {
        for (int i = 0; i < 4; i++)
        {
            var label = _modifierLabels[i];
            label.Text = _context.Transformer.ModifierLabel(ModifierOrder[i]);
            label.Foreground = (_flags & ModifierOrder[i]) != 0
                ? Brushes.White
                : new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
        }

        _displayedText.Text = _displayed ?? "";
        if (_displayed is null)
            return;

        // Start at 48pt and shrink until the text fits, like the original.
        double fontSize = 48;
        var typeface = new Typeface(_displayedText.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        double pixelsPerDip = VisualTreeHelper.GetDpi(_window).PixelsPerDip;
        while (fontSize > 6)
        {
            var formatted = new FormattedText(_displayed, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                typeface, fontSize, Brushes.White, pixelsPerDip);
            if (formatted.WidthIncludingTrailingWhitespace <= PanelWidth - 10)
                break;
            fontSize -= 1;
        }
        _displayedText.FontSize = fontSize;
    }

    /// <summary>The rounded panel with hair-line gaps separating the four modifier cells.</summary>
    private static Geometry BuildBackground()
    {
        double quarter = Math.Floor(PanelWidth / 4);
        double rowTop = PanelHeight - ModifierRowHeight;
        var gaps = new GeometryGroup();
        gaps.Children.Add(new RectangleGeometry(new Rect(0, rowTop - 1, PanelWidth, 1)));
        for (int i = 1; i <= 3; i++)
            gaps.Children.Add(new RectangleGeometry(new Rect(quarter * i, rowTop, 1, ModifierRowHeight)));
        var panel = new RectangleGeometry(new Rect(0, 0, PanelWidth, PanelHeight), CornerRadius, CornerRadius);
        var geometry = new CombinedGeometry(GeometryCombineMode.Exclude, panel, gaps);
        geometry.Freeze();
        return geometry;
    }

    private void RestorePosition()
    {
        var position = _context.Settings.Svelte.Position;
        var workArea = ScreenInfo.PrimaryWorkArea;
        var bounds = ScreenInfo.VirtualScreen;
        if (position.X is double x && position.Y is double y && bounds.Contains(new Point(x, y)))
        {
            _window.Left = x;
            _window.Top = y;
        }
        else
        {
            _window.Left = workArea.Left + 10;
            _window.Top = workArea.Bottom - 10 - PanelHeight;
        }
    }

    private void SavePosition()
    {
        _context.Settings.Svelte.Position.X = _window.Left;
        _context.Settings.Svelte.Position.Y = _window.Top;
        _context.SaveSettings();
    }

    public void Dispose() => _window.Close();
}
