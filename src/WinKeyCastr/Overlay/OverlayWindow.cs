using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinKeyCastr.Native;

namespace WinKeyCastr.Overlay;

/// <summary>
/// A transparent, always-on-top window that never takes focus — the equivalent of KeyCastr's
/// borderless NSWindow at NSScreenSaverWindowLevel. With acrylic enabled it is paired with a
/// <see cref="BackdropWindow"/> that blurs the desktop beneath it.
/// </summary>
public class OverlayWindow : Window
{
    private readonly BackdropWindow? _backdrop;
    private readonly bool _clickThrough;
    private double _fadeOpacity = 1;

    private DispatcherTimer? _dragTimer;
    private Win32.POINT _dragLastCursor;

    /// <summary>Raised while the user drags the window: the movement in WPF units since the last call.</summary>
    public event Action<Vector>? Dragged;

    public event Action? DragStarted;
    public event Action? DragEnded;

    public OverlayWindow(bool acrylic, bool clickThrough = false)
    {
        _clickThrough = clickThrough;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        UseLayoutRounding = true;

        if (acrylic && CompositionHost.IsAvailable)
            _backdrop = new BackdropWindow();

        LocationChanged += (_, _) => SyncBackdrop();
        SizeChanged += (_, _) => SyncBackdrop();
        IsVisibleChanged += (_, _) => SyncBackdropVisibility();
    }

    public bool HasBackdrop => _backdrop is not null;

    /// <summary>Corner radius of the blurred area, in WPF units.</summary>
    public double BackdropCornerRadius
    {
        get => _backdrop?.CornerRadius ?? 0;
        set
        {
            if (_backdrop is not null)
                _backdrop.CornerRadius = value;
        }
    }

    /// <summary>Opacity applied to both the window and its blur, for fade-outs.</summary>
    public double FadeOpacity
    {
        get => _fadeOpacity;
        set
        {
            _fadeOpacity = value;
            Opacity = value;
            if (_backdrop is not null)
                _backdrop.BackdropOpacity = value;
        }
    }

    public bool IsDragging => _dragTimer is not null;

    public new void Show()
    {
        if (_backdrop is not null && !_backdrop.IsVisible)
        {
            _backdrop.Left = Left;
            _backdrop.Top = Top;
            _backdrop.Width = Math.Max(1, ActualWidth);
            _backdrop.Height = Math.Max(1, ActualHeight);
            _backdrop.Show();
            if (Owner is null)
                Owner = _backdrop; // owned windows always stay above their owner
        }
        base.Show();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        if (_clickThrough)
            exStyle |= Win32.WS_EX_TRANSPARENT | Win32.WS_EX_LAYERED;
        Win32.AddExStyle(hwnd, exStyle);
        HwndSource.FromHwnd(hwnd)!.AddHook(WndProc);
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

    private void SyncBackdrop()
    {
        if (_backdrop is null || !_backdrop.IsVisible)
            return;
        _backdrop.Left = Left;
        _backdrop.Top = Top;
        _backdrop.Width = Math.Max(1, ActualWidth);
        _backdrop.Height = Math.Max(1, ActualHeight);
    }

    private void SyncBackdropVisibility()
    {
        if (_backdrop is null)
            return;
        if (IsVisible)
        {
            if (!_backdrop.IsVisible)
                _backdrop.Show();
            SyncBackdrop();
        }
        else if (_backdrop.IsVisible)
        {
            _backdrop.Hide();
        }
    }

    // ---- Dragging ----------------------------------------------------------------------
    // The window never becomes active, so mouse capture is unreliable. Instead, once the left
    // button goes down on the window we poll the cursor until the button is released.

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_clickThrough || _dragTimer is not null)
            return;
        Win32.GetCursorPos(out _dragLastCursor);
        _dragTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(8) };
        _dragTimer.Tick += OnDragTick;
        _dragTimer.Start();
        DragStarted?.Invoke();
        e.Handled = true;
    }

    private void OnDragTick(object? sender, EventArgs e)
    {
        const int VK_LBUTTON = 0x01;
        Win32.GetCursorPos(out var cursor);
        if (cursor.X != _dragLastCursor.X || cursor.Y != _dragLastCursor.Y)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var delta = new Vector((cursor.X - _dragLastCursor.X) / dpi.DpiScaleX, (cursor.Y - _dragLastCursor.Y) / dpi.DpiScaleY);
            _dragLastCursor = cursor;
            Dragged?.Invoke(delta);
        }

        if ((Win32.GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)
        {
            _dragTimer!.Stop();
            _dragTimer = null;
            DragEnded?.Invoke();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _dragTimer?.Stop();
        _dragTimer = null;
        base.OnClosed(e);
        _backdrop?.Close();
    }
}
