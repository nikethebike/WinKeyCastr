using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinKeyCastr.Localization;
using WinKeyCastr.Native;
using WinKeyCastr.Settings;

namespace WinKeyCastr.UI;

public partial class PreferencesWindow : Window
{
    private readonly AppController _controller;
    private string _pane = "General";

    public PreferencesWindow(AppController controller, AppSettings settings)
    {
        _controller = controller;
        InitializeComponent();
        DataContext = settings;
        UpdateIcon(controller.IsCapturing);
        UpdateTitle();
        Loc.Instance.PropertyChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => UpdateTitle();

    private void UpdateTitle() => Title = Loc.T("Pane_" + _pane);

    public void UpdateIcon(bool capturing) =>
        Icon = new BitmapImage(new Uri(capturing
            ? "pack://application:,,,/Assets/KeyCastr-512.png"
            : "pack://application:,,,/Assets/KeyCastrInactive-512.png"));

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Acrylic behind the whole window while it is active (Windows shows a solid fallback when inactive).
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)!.CompositionTarget.BackgroundColor = Colors.Transparent;
        Background = Brushes.Transparent;
        Win32.ExtendFrameIntoWholeClientArea(hwnd);
        Win32.SetDwmInt(hwnd, Win32.DWMWA_SYSTEMBACKDROP_TYPE, Win32.DWMSBT_TRANSIENTWINDOW);
    }

    public void SelectPane(string pane) => (pane == "Display" ? DisplayTab : GeneralTab).IsChecked = true;

    private void OnPaneChecked(object sender, RoutedEventArgs e)
    {
        if (GeneralPane is null || DisplayPane is null)
            return;
        var pane = (string)((RadioButton)sender).Tag;
        GeneralPane.Visibility = pane == "General" ? Visibility.Visible : Visibility.Collapsed;
        DisplayPane.Visibility = pane == "Display" ? Visibility.Visible : Visibility.Collapsed;
        _pane = pane;
        UpdateTitle();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_controller.IsQuitting)
        {
            Loc.Instance.PropertyChanged -= OnLanguageChanged;
            return;
        }
        // Like a Mac preferences window, closing only hides it.
        e.Cancel = true;
        _controller.HidePreferences();
    }
}
