using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WinKeyCastr.Input;
using WinKeyCastr.Native;
using WinKeyCastr.Overlay;
using WinKeyCastr.Settings;

namespace WinKeyCastr.Visualizers;

/// <summary>
/// KeyCastr's mouse visualizer: a ring drawn around the pointer while a button is held, following
/// drags and fading out on release. It uses the Default visualizer's bezel colour, as the original does.
/// </summary>
public sealed class MouseClickVisualizer : IDisposable
{
    private const double Radius = 22;
    private const double LineWidth = 2;

    private readonly AppSettings _settings;
    private OverlayWindow? _window;
    private Ellipse? _circle;

    public MouseClickVisualizer(AppSettings settings)
    {
        _settings = settings;
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled && _window is null)
            CreateWindow();
        else if (!enabled && _window is not null)
        {
            _window.Close();
            _window = null;
            _circle = null;
        }
    }

    private void CreateWindow()
    {
        _circle = new Ellipse
        {
            Width = 2 * Radius - 2 * LineWidth,
            Height = 2 * Radius - 2 * LineWidth,
            StrokeThickness = LineWidth,
            Margin = new Thickness(LineWidth),
            Opacity = 0,
        };
        _window = new OverlayWindow(acrylic: false, clickThrough: true)
        {
            Width = 2 * Radius,
            Height = 2 * Radius,
            Left = -1000,
            Top = -1000,
            Content = _circle,
        };
        _window.Show();
    }

    public void Update(MouseEventInfo e)
    {
        if (_window is null || _circle is null)
            return;

        switch (e.Kind)
        {
            case MouseEventKind.Down:
                _circle.Stroke = new SolidColorBrush(_settings.Default.BezelColor);
                _circle.BeginAnimation(UIElement.OpacityProperty, null);
                _circle.Opacity = 1;
                MoveTo(e.X, e.Y);
                break;

            case MouseEventKind.Dragged:
                MoveTo(e.X, e.Y);
                break;

            case MouseEventKind.Up:
                if (_circle.Opacity > 0)
                {
                    // Core Animation's default implicit duration.
                    var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.25));
                    _circle.BeginAnimation(UIElement.OpacityProperty, fade);
                }
                break;
        }
    }

    /// <summary>Centre the ring on a physical-pixel cursor position.</summary>
    private void MoveTo(int x, int y)
    {
        var hwnd = new WindowInteropHelper(_window!).Handle;
        double scale = ScreenInfo.ScaleAtPhysical(x, y);
        int size = (int)Math.Round(2 * Radius * scale);
        Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, x - size / 2, y - size / 2, size, size, Win32.SWP_NOACTIVATE);
    }

    public void Dispose() => SetEnabled(false);
}
