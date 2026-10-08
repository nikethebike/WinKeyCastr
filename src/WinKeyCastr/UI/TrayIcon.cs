using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using WinKeyCastr.Localization;
using WinKeyCastr.Native;
using Forms = System.Windows.Forms;

namespace WinKeyCastr.UI;

/// <summary>
/// The notification-area icon and its menu — KeyCastr's status item. The menu matches the original:
/// About KeyCastr, Preferences…, Start/Stop Casting (with the toggle shortcut), Quit KeyCastr.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly AppController _controller;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Window _menuHost;
    private readonly ContextMenu _menu;
    private readonly MenuItem _castingItem;
    private Icon? _activeIcon;
    private Icon? _inactiveIcon;
    private bool _capturing;

    public TrayIcon(AppController controller)
    {
        _controller = controller;

        _castingItem = new MenuItem();
        _castingItem.Click += (_, _) => _controller.ToggleCapturing();
        var about = new MenuItem();
        BindHeader(about, "Menu_About");
        about.Click += (_, _) => _controller.ShowAbout();
        var preferences = new MenuItem();
        BindHeader(preferences, "Menu_Preferences");
        preferences.Click += (_, _) => _controller.ShowPreferences();
        var quit = new MenuItem();
        BindHeader(quit, "Menu_Quit");
        quit.Click += (_, _) => _controller.Quit();

        _menu = new ContextMenu { Items = { about, preferences, _castingItem, quit } };
        _menu.Closed += (_, _) => _menuHost!.Hide();

        // A context menu only dismisses properly when its owner is the foreground window, so the
        // menu is hosted by an invisible window that is activated just before it opens.
        var anchor = new Border { ContextMenu = _menu };
        _menuHost = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            Width = 1,
            Height = 1,
            Left = -10000,
            Top = -10000,
            ThemeMode = ThemeMode.System,
            Content = anchor,
        };
        _menu.PlacementTarget = anchor;

        _notifyIcon = new Forms.NotifyIcon { Text = "KeyCastr" };
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button is Forms.MouseButtons.Left or Forms.MouseButtons.Right)
                ShowMenu();
        };

        LoadIcons();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public bool Visible
    {
        get => _notifyIcon.Visible;
        set => _notifyIcon.Visible = value;
    }

    private static void BindHeader(MenuItem item, string key) =>
        item.SetBinding(HeaderedItemsControl.HeaderProperty,
            new System.Windows.Data.Binding($"[{key}]") { Source = Loc.Instance });

    public void Update(bool capturing, string shortcut)
    {
        _capturing = capturing;
        _castingItem.Header = Loc.T(capturing ? "Menu_Stop" : "Menu_Start");
        _castingItem.InputGestureText = shortcut;
        _notifyIcon.Icon = capturing ? _activeIcon : _inactiveIcon;
        _notifyIcon.Text = Loc.T(capturing ? "Tray_Casting" : "Tray_NotCasting");
    }

    internal void ShowMenu(bool staysOpen = false)
    {
        _menu.StaysOpen = staysOpen;
        _menuHost.Show();
        var hwnd = new WindowInteropHelper(_menuHost).Handle;
        Win32.SetForegroundWindow(hwnd);
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _menu.IsOpen = true;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            LoadIcons();
            _notifyIcon.Icon = _capturing ? _activeIcon : _inactiveIcon;
        }
    }

    /// <summary>
    /// KeyCastr's status item is a key cap, here with the Windows logo instead of ⌘: filled while casting, hollow otherwise.
    /// It is drawn here so it stays crisp at any scale and readable on dark and light taskbars.
    /// </summary>
    private void LoadIcons()
    {
        bool lightTaskbar = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 0) is int value && value != 0;
        var ink = lightTaskbar ? Color.Black : Color.White;
        var paper = lightTaskbar ? Color.White : Color.Black;

        _activeIcon?.Dispose();
        _inactiveIcon?.Dispose();
        _activeIcon = CreateIcon(filled: true, ink, paper);
        _inactiveIcon = CreateIcon(filled: false, ink, paper);
    }

    private static Icon CreateIcon(bool filled, Color ink, Color paper)
    {
        int size = Forms.SystemInformation.SmallIconSize.Width;
        using var canvas = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(canvas))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float stroke = Math.Max(1f, size / 14f);
            float inset = stroke / 2 + size * 0.04f;
            var rect = new RectangleF(inset, inset + size * 0.06f, size - 2 * inset, size - 2 * inset - size * 0.12f);
            float radius = size * 0.22f;
            using var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, radius, radius, 180, 90);
            path.AddArc(rect.Right - radius, rect.Top, radius, radius, 270, 90);
            path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();

            Color glyphColor;
            if (filled)
            {
                using var fill = new SolidBrush(ink);
                g.FillPath(fill, path);
                glyphColor = paper;
            }
            else
            {
                using var pen = new Pen(Color.FromArgb(200, ink), stroke);
                g.DrawPath(pen, path);
                glyphColor = Color.FromArgb(200, ink);
            }

            // The Windows logo (four squares) in place of KeyCastr's ⌘, snapped to whole pixels so it
            // stays crisp at 16 px.
            int gap = Math.Max(1, (int)Math.Round(size / 16.0));
            int square = Math.Max(2, (int)((rect.Height * 0.62f - gap) / 2));
            int logo = square * 2 + gap;
            int left = (int)Math.Round(rect.X + (rect.Width - logo) / 2);
            int top = (int)Math.Round(rect.Y + (rect.Height - logo) / 2);
            using var glyphBrush = new SolidBrush(glyphColor);
            g.SmoothingMode = SmoothingMode.None;
            foreach (int dx in (ReadOnlySpan<int>)[0, square + gap])
            {
                foreach (int dy in (ReadOnlySpan<int>)[0, square + gap])
                    g.FillRectangle(glyphBrush, left + dx, top + dy, square, square);
            }
        }

        var handle = canvas.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _activeIcon?.Dispose();
        _inactiveIcon?.Dispose();
        _menuHost.Close();
    }
}
