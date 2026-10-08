using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WinKeyCastr.Localization;
using WinKeyCastr.Native;

namespace WinKeyCastr.UI;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0);
        VersionText.Text = string.Format(Loc.T("About_Version"), version.ToString(3));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)!.CompositionTarget.BackgroundColor = Colors.Transparent;
        Background = Brushes.Transparent;
        Win32.ExtendFrameIntoWholeClientArea(hwnd);
        Win32.SetDwmInt(hwnd, Win32.DWMWA_SYSTEMBACKDROP_TYPE, Win32.DWMSBT_TRANSIENTWINDOW);
    }
}
