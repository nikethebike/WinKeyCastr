using System.Runtime.InteropServices;
using Windows.UI.Composition;
using WinKeyCastr.Native;
using WinRT;

namespace WinKeyCastr.Overlay;

/// <summary>
/// Owns the Windows.UI.Composition compositor used to draw acrylic behind overlay windows.
/// Must be initialised on the UI thread before any overlay is shown.
/// </summary>
internal static class CompositionHost
{
    [ComImport, Guid("29E691FA-4567-4DCA-B319-D0F207EB6807"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICompositorDesktopInterop
    {
        [PreserveSig]
        int CreateDesktopWindowTarget(IntPtr hwndTarget, [MarshalAs(UnmanagedType.Bool)] bool isTopmost, out IntPtr result);
    }

    private static IntPtr _dispatcherQueueController;
    private static Compositor? _compositor;

    public static bool IsAvailable => _compositor is not null;

    public static Compositor Compositor => _compositor ?? throw new InvalidOperationException("Composition is not initialised.");

    public static void Initialize()
    {
        if (_compositor is not null)
            return;
        try
        {
            var options = new Win32.DispatcherQueueOptions
            {
                dwSize = Marshal.SizeOf<Win32.DispatcherQueueOptions>(),
                threadType = 2,     // DQTYPE_THREAD_CURRENT
                apartmentType = 2,  // DQTAT_COM_STA
            };
            Marshal.ThrowExceptionForHR(Win32.CreateDispatcherQueueController(options, out _dispatcherQueueController));
            _compositor = new Compositor();
        }
        catch
        {
            _compositor = null; // Acrylic simply becomes unavailable.
        }
    }

    public static Windows.UI.Composition.Desktop.DesktopWindowTarget CreateTarget(IntPtr hwnd)
    {
        var interop = (ICompositorDesktopInterop)Marshal.GetObjectForIUnknown(((IWinRTObject)Compositor).NativeObject.ThisPtr);
        Marshal.ThrowExceptionForHR(interop.CreateDesktopWindowTarget(hwnd, false, out var target));
        try
        {
            return MarshalInterface<Windows.UI.Composition.Desktop.DesktopWindowTarget>.FromAbi(target);
        }
        finally
        {
            Marshal.Release(target);
        }
    }
}
