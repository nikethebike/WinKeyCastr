using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinKeyCastr.UI;

/// <summary>
/// A colour swatch that opens a picker with opacity, standing in for AppKit's NSColorWell
/// (KeyCastr's bezel and text colours carry alpha, so the stock Windows colour dialog won't do).
/// </summary>
public sealed class ColorWell : Button
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(ColorWell),
        new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColorWell)d).UpdateSwatch()));

    private readonly Rectangle _swatch;
    private readonly Popup _popup;
    private readonly ColorPicker _picker;

    public ColorWell()
    {
        Width = 50;
        Height = 28;
        MinWidth = 0;
        MinHeight = 0;
        Padding = new Thickness(4);
        HorizontalAlignment = HorizontalAlignment.Left;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        _swatch = new Rectangle { RadiusX = 3, RadiusY = 3, Stroke = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)) };
        Content = new Grid
        {
            Children =
            {
                new Rectangle { RadiusX = 3, RadiusY = 3, Fill = CheckerBrush() },
                _swatch,
            },
        };

        _picker = new ColorPicker();
        _picker.ColorChanged += c => Color = c;
        _popup = new Popup
        {
            PlacementTarget = this,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 4, 0, 0),
                Child = _picker,
            },
        };
        ((Border)_popup.Child).SetResourceReference(Border.BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
        ((Border)_popup.Child).SetResourceReference(Border.BorderBrushProperty, "SurfaceStrokeColorFlyoutBrush");

        Click += (_, _) =>
        {
            _picker.Color = Color;
            _popup.IsOpen = true;
        };
        UpdateSwatch();
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private void UpdateSwatch() => _swatch.Fill = new SolidColorBrush(Color);

    internal static DrawingBrush CheckerBrush()
    {
        var light = new SolidColorBrush(Color.FromRgb(240, 240, 240));
        var dark = new SolidColorBrush(Color.FromRgb(190, 190, 190));
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 4, 4))));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(4, 4, 4, 4))));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 8, 8),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }
}

/// <summary>Hue/saturation/value picker with an opacity strip and a hex field.</summary>
internal sealed class ColorPicker : StackPanel
{
    private const double AreaWidth = 220, AreaHeight = 150, StripHeight = 14;

    private readonly Rectangle _hueLayer;
    private readonly Ellipse _areaThumb;
    private readonly Canvas _areaCanvas;
    private readonly Border _hueThumb;
    private readonly Border _alphaThumb;
    private readonly Rectangle _alphaGradient;
    private readonly TextBox _hex;
    private readonly Rectangle _preview;

    private double _h, _s, _v, _a = 1;
    private bool _updating;

    public event Action<Color>? ColorChanged;

    public ColorPicker()
    {
        Width = AreaWidth;

        // Saturation / value area
        _hueLayer = new Rectangle { Width = AreaWidth, Height = AreaHeight };
        _areaThumb = new Ellipse { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
        _areaCanvas = new Canvas { Width = AreaWidth, Height = AreaHeight, Background = Brushes.Transparent, ClipToBounds = true, Cursor = Cursors.Cross };
        _areaCanvas.Children.Add(_hueLayer);
        _areaCanvas.Children.Add(new Rectangle
        {
            Width = AreaWidth, Height = AreaHeight,
            Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0),
        });
        _areaCanvas.Children.Add(new Rectangle
        {
            Width = AreaWidth, Height = AreaHeight,
            Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90),
        });
        _areaCanvas.Children.Add(_areaThumb);
        HookDrag(_areaCanvas, p =>
        {
            _s = Math.Clamp(p.X / AreaWidth, 0, 1);
            _v = 1 - Math.Clamp(p.Y / AreaHeight, 0, 1);
            Commit();
        });
        Children.Add(new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = _areaCanvas });

        // Hue strip
        var hueStops = new GradientStopCollection();
        for (int i = 0; i <= 6; i++)
            hueStops.Add(new GradientStop(FromHsv(i * 60 % 360, 1, 1, 1), i / 6.0));
        _hueThumb = StripThumb();
        Children.Add(Strip(new Rectangle { Fill = new LinearGradientBrush(hueStops, 0) }, _hueThumb, x =>
        {
            _h = Math.Clamp(x, 0, 1) * 359.999;
            Commit();
        }));

        // Opacity strip
        _alphaGradient = new Rectangle();
        _alphaThumb = StripThumb();
        var alphaGrid = new Grid { Children = { new Rectangle { Fill = ColorWell.CheckerBrush() }, _alphaGradient } };
        Children.Add(Strip(alphaGrid, _alphaThumb, x =>
        {
            _a = Math.Clamp(x, 0, 1);
            Commit();
        }));

        // Hex field and preview
        _hex = new TextBox { Width = 120, VerticalContentAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Consolas") };
        _hex.LostFocus += (_, _) => ParseHex();
        _hex.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                ParseHex();
        };
        _preview = new Rectangle { Width = 40, Height = 28, RadiusX = 4, RadiusY = 4 };
        var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        bottom.Children.Add(new TextBlock { Text = "#", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), Opacity = 0.7 });
        bottom.Children.Add(_hex);
        var previewHost = new Grid { Children = { new Rectangle { RadiusX = 4, RadiusY = 4, Fill = ColorWell.CheckerBrush() }, _preview } };
        DockPanel.SetDock(previewHost, Dock.Right);
        bottom.Children.Add(previewHost);
        Children.Add(bottom);
    }

    public Color Color
    {
        get => FromHsv(_h, _s, _v, _a);
        set
        {
            ToHsv(value, out _h, out _s, out _v);
            _a = value.A / 255.0;
            Refresh();
        }
    }

    private void Commit()
    {
        Refresh();
        ColorChanged?.Invoke(Color);
    }

    private void Refresh()
    {
        if (_updating)
            return;
        _updating = true;
        var color = Color;
        _hueLayer.Fill = new SolidColorBrush(FromHsv(_h, 1, 1, 1));
        Canvas.SetLeft(_areaThumb, _s * AreaWidth - 6);
        Canvas.SetTop(_areaThumb, (1 - _v) * AreaHeight - 6);
        _areaThumb.Stroke = _v > 0.5 && _s < 0.5 ? Brushes.Black : Brushes.White;
        _hueThumb.Margin = new Thickness(_h / 360 * (AreaWidth - 6), 0, 0, 0);
        _alphaThumb.Margin = new Thickness(_a * (AreaWidth - 6), 0, 0, 0);
        var opaque = Color.FromRgb(color.R, color.G, color.B);
        _alphaGradient.Fill = new LinearGradientBrush(Color.FromArgb(0, color.R, color.G, color.B), opaque, 0);
        _preview.Fill = new SolidColorBrush(color);
        if (!_hex.IsKeyboardFocused)
            _hex.Text = $"{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        _updating = false;
    }

    private void ParseHex()
    {
        var text = _hex.Text.Trim().TrimStart('#');
        if (text.Length == 6)
            text = "FF" + text;
        if (text.Length == 8 && uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            Color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            ColorChanged?.Invoke(Color);
        }
        else
        {
            Refresh();
        }
    }

    private static Border StripThumb() => new()
    {
        Width = 6,
        Height = StripHeight + 4,
        CornerRadius = new CornerRadius(3),
        BorderBrush = Brushes.Black,
        BorderThickness = new Thickness(1),
        Background = Brushes.White,
        HorizontalAlignment = HorizontalAlignment.Left,
        IsHitTestVisible = false,
    };

    private static Grid Strip(FrameworkElement fill, Border thumb, Action<double> onPick)
    {
        var track = new Border
        {
            Height = StripHeight,
            CornerRadius = new CornerRadius(StripHeight / 2),
            ClipToBounds = true,
            Margin = new Thickness(3, 0, 3, 0),
            Child = fill,
        };
        var grid = new Grid { Height = StripHeight + 4, Margin = new Thickness(0, 10, 0, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        grid.Children.Add(track);
        grid.Children.Add(thumb);
        HookDrag(grid, p => onPick((p.X - 3) / (AreaWidth - 6)));
        return grid;
    }

    private static void HookDrag(FrameworkElement element, Action<Point> onPoint)
    {
        element.MouseLeftButtonDown += (_, e) =>
        {
            element.CaptureMouse();
            onPoint(e.GetPosition(element));
            e.Handled = true;
        };
        element.MouseMove += (_, e) =>
        {
            if (element.IsMouseCaptured)
                onPoint(e.GetPosition(element));
        };
        element.MouseLeftButtonUp += (_, _) => element.ReleaseMouseCapture();
    }

    private static Color FromHsv(double h, double s, double v, double a)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromArgb((byte)Math.Round(a * 255), (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    private static void ToHsv(Color color, out double h, out double s, out double v)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        h = delta == 0 ? 0
            : max == r ? 60 * ((g - b) / delta % 6)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);
        if (h < 0)
            h += 360;
        s = max == 0 ? 0 : delta / max;
        v = max;
    }
}
