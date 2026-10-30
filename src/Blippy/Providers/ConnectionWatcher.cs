using HidSharp;
using Windows.Devices.Enumeration;
using Windows.Gaming.Input;

namespace Blippy.Providers;

/// <summary>
/// Raises <see cref="Changed"/> when a device connects or disconnects: any HID device
/// (USB or Bluetooth), a game controller, or a paired Bluetooth device's connection.
/// Fires from background threads and often several times for one plug-in; callers debounce.
/// </summary>
internal sealed class ConnectionWatcher : IDisposable
{
    // Bluetooth Classic and Bluetooth LE association endpoints.
    private const string BluetoothAqs =
        "(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR " +
        "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";

    private readonly DeviceWatcher? bluetooth;
    private bool bluetoothReady;

    public event Action<string>? Changed;

    public ConnectionWatcher()
    {
        DeviceList.Local.Changed += OnHidChanged;
        Try("game controllers", () =>
        {
            RawGameController.RawGameControllerAdded += OnControllerAdded;
            RawGameController.RawGameControllerRemoved += OnControllerRemoved;
        });
        Try("Bluetooth", () =>
        {
            var watcher = DeviceInformation.CreateWatcher(
                BluetoothAqs, ["System.Devices.Aep.IsConnected"], DeviceInformationKind.AssociationEndpoint);
            // The first burst of Added events is the existing list, not a change.
            watcher.EnumerationCompleted += (_, _) => bluetoothReady = true;
            watcher.Added += (_, d) => Raise($"Bluetooth device added: {d.Name}", bluetoothReady);
            watcher.Removed += (_, _) => Raise("Bluetooth device removed", bluetoothReady);
            watcher.Updated += (_, u) =>
            {
                if (u.Properties.ContainsKey("System.Devices.Aep.IsConnected"))
                    Raise("Bluetooth connection changed", bluetoothReady);
            };
            watcher.Start();
            return watcher;
        }, out bluetooth);
    }

    private void OnHidChanged(object? sender, DeviceListChangedEventArgs e) => Raise("HID devices changed");
    private void OnControllerAdded(object? sender, RawGameController c) => Raise($"Controller connected: {c.DisplayName}");
    private void OnControllerRemoved(object? sender, RawGameController c) => Raise($"Controller disconnected: {c.DisplayName}");

    private void Raise(string why, bool ready = true)
    {
        if (ready) Changed?.Invoke(why);
    }

    private static void Try(string what, Action start)
    {
        try
        {
            start();
        }
        catch (Exception ex)
        {
            Log.Error($"Watching {what}", ex);
        }
    }

    private static void Try<T>(string what, Func<T> start, out T? result) where T : class
    {
        result = null;
        try
        {
            result = start();
        }
        catch (Exception ex)
        {
            Log.Error($"Watching {what}", ex);
        }
    }

    public void Dispose()
    {
        DeviceList.Local.Changed -= OnHidChanged;
        Try("game controllers", () =>
        {
            RawGameController.RawGameControllerAdded -= OnControllerAdded;
            RawGameController.RawGameControllerRemoved -= OnControllerRemoved;
        });
        Try("Bluetooth", () =>
        {
            if (bluetooth?.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) bluetooth.Stop();
        });
    }
}
