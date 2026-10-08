using System.Windows;
using System.Windows.Threading;
using WinKeyCastr.Overlay;
using WinKeyCastr.Settings;

namespace WinKeyCastr;

public partial class App : Application
{
    private const string InstanceMutexName = @"Local\WinKeyCastr.SingleInstance";
    private const string ShowPreferencesEventName = @"Local\WinKeyCastr.ShowPreferences";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showPreferencesEvent;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Launching KeyCastr again (like clicking its Dock icon) brings up the preferences of the running copy.
        _instanceMutex = new Mutex(true, InstanceMutexName, out bool isFirstInstance);
        _showPreferencesEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowPreferencesEventName);
        if (!isFirstInstance)
        {
            _showPreferencesEvent.Set();
            Shutdown();
            return;
        }

        var listener = new Thread(() =>
        {
            while (_showPreferencesEvent.WaitOne())
                Dispatcher.BeginInvoke(() => _controller?.ShowPreferences());
        }) { IsBackground = true, Name = "KeyCastr instance listener" };
        listener.Start();

        CompositionHost.Initialize();
        var settings = AppSettings.Load();
#if DEBUG
        var demo = Demo.RequestedVisualizer(e.Args);
        if (demo is not null)
        {
            settings.IsReadOnly = true;
            settings.SelectedVisualizer = demo;
            settings.Default.DisplayMode = DefaultDisplayMode.AllKeys;
            settings.MouseDisplayOption = MouseDisplayOption.WithPointerAndVisualizer;
            settings.ShowPreferencesAtLaunch = e.Args.Contains("--prefs");
            if (e.Args.Contains("--ru"))
                settings.Language = WinKeyCastr.Localization.AppLanguage.Russian;
            else if (e.Args.Contains("--en"))
                settings.Language = WinKeyCastr.Localization.AppLanguage.English;
        }
#endif
        _controller = new AppController(settings);
        _controller.Start();
#if DEBUG
        if (demo is not null)
        {
            if (e.Args.Contains("--display"))
                _controller.SelectPreferencesPane("Display");
            if (e.Args.Contains("--picker"))
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, _controller.OpenFirstColorWell);
            if (e.Args.Contains("--traymenu"))
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, _controller.OpenTrayMenu);
            if (!e.Args.Contains("--idle"))
                Demo.Run(_controller, settings);
        }
#endif
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_controller is { IsQuitting: false })
            _controller.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
