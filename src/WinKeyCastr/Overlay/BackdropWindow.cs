using System.Numerics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using ContainerVisual = Windows.UI.Composition.ContainerVisual;
using WinKeyCastr.Native;

namespace WinKeyCastr.Overlay;

/// <summary>
/// A borderless window that shows nothing but a blur of whatever is behind it, clipped to a rounded
/// rectangle. Unlike the DWM system backdrop, the host backdrop brush keeps blurring while the window
/// is inactive, which is what overlays that never take focus need.
/// It sits directly beneath an <see cref="OverlayWindow"/>, which draws the tint and the text.
/// </summary>
internal sealed class BackdropWindow : Window
{
    private DesktopWindowTarget? _target;
    private ContainerVisual? _root;
    private CompositionRoundedRectangleGeometry? _clipGeometry;
    private float _cornerRadius;
    private float _opacity = 1;

    public BackdropWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        Background = Brushes.Transparent;
        SizeChanged += (_, _) => UpdateClip();
        DpiChanged += (_, _) => UpdateClip();
    }

    public double CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = (float)value;
            UpdateClip();
        }
    }

    public double BackdropOpacity
    {
        get => _opacity;
        set
        {
            _opacity = (float)value;
            if (_root is not null)
                _root.Opacity = _opacity;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd)!;
        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        source.AddHook(WndProc);

        Win32.AddExStyle(hwnd, Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
        Win32.ExtendFrameIntoWholeClientArea(hwnd);
        Win32.SetDwmInt(hwnd, Win32.DWMWA_USE_HOSTBACKDROPBRUSH, 1);
        Win32.SetDwmInt(hwnd, Win32.DWMWA_WINDOW_CORNER_PREFERENCE, Win32.DWMWCP_DONOTROUND);
        Win32.SetDwmInt(hwnd, Win32.DWMWA_SYSTEMBACKDROP_TYPE, Win32.DWMSBT_NONE);
        Win32.SetDwmInt(hwnd, Win32.DWMWA_BORDER_COLOR, Win32.DWMWA_COLOR_NONE);

        if (!CompositionHost.IsAvailable)
            return;

        var compositor = CompositionHost.Compositor;
        _target = CompositionHost.CreateTarget(hwnd);

        _root = compositor.CreateContainerVisual();
        _root.RelativeSizeAdjustment = Vector2.One;
        _root.Opacity = _opacity;

        var blur = compositor.CreateSpriteVisual();
        blur.RelativeSizeAdjustment = Vector2.One;
        blur.Brush = compositor.CreateHostBackdropBrush();
        _root.Children.InsertAtTop(blur);

        _clipGeometry = compositor.CreateRoundedRectangleGeometry();
        _root.Clip = compositor.CreateGeometricClip(_clipGeometry);
        _target.Root = _root;

        UpdateClip();
    }

    private void UpdateClip()
    {
        if (_clipGeometry is null)
            return;
        var dpi = VisualTreeHelper.GetDpi(this);
        _clipGeometry.Size = new Vector2((float)(ActualWidth * dpi.DpiScaleX), (float)(ActualHeight * dpi.DpiScaleY));
        _clipGeometry.CornerRadius = new Vector2((float)(_cornerRadius * dpi.DpiScaleX));
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(Win32.MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _target?.Dispose();
        _target = null;
        _root = null;
        _clipGeometry = null;
    }
}
