using System.Windows;
using System.Windows.Threading;
using Blippy.Tray;
using Blippy.Ui;

namespace Blippy;

public partial class App : Application
{
    private Mutex? singleInstance;
    private BatteryMonitor? monitor;
    private TrayController? tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args is ["--preview", var folder])
        {
            base.OnStartup(e);
            Shutdown(Preview.Run(this, folder));
            return;
        }

        singleInstance = new Mutex(initiallyOwned: true, @"Local\Blippy.SingleInstance", out var first);
        if (!first)
        {
            // Already running; the tray icon is the way in.
            singleInstance.Dispose();
            singleInstance = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
        Theme.Apply(Resources);
        Theme.WatchForChanges(() => Theme.Apply(Resources));

        var settings = new SettingsStore();
        settings.SyncStartWithWindows();
        monitor = new BatteryMonitor(settings, Dispatcher);
        var flyout = new FlyoutWindow(settings, monitor);
        tray = new TrayController(settings, monitor, flyout);
        monitor.Start();
        Log.Info("Blippy started");
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Keep the tray alive; a glitch in one redraw should not quit the app.
        Log.Error("Unhandled", e.Exception);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        monitor?.Dispose();
        if (singleInstance is not null)
        {
            singleInstance.ReleaseMutex();
            singleInstance.Dispose();
        }
        base.OnExit(e);
    }
}
