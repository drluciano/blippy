using System.Windows.Threading;
using Blippy.Core;
using Blippy.Providers;

namespace Blippy;

/// <summary>
/// Asks every provider for battery levels on a timer, right away on request, and soon
/// after any device connects or disconnects; merges what they find and hands the list to
/// the UI thread.
/// </summary>
internal sealed class BatteryMonitor : IDisposable
{
    private readonly SettingsStore settings;
    private readonly Dispatcher ui;
    private readonly DualSenseProvider dualSense = new();
    private readonly IBatteryProvider[] providers;
    private readonly CancellationTokenSource stop = new();
    private readonly ConnectionWatcher? connections;
    private readonly Timer soon;
    private readonly Timer settled;
    private TaskCompletionSource wake = NewWake();
    private int repaintPending;

    // Plugging something in fires a burst of events; wait for it to pass, then check.
    // A newly connected pad can take a few seconds to start reporting, so check once more.
    private static readonly TimeSpan SoonAfterChange = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan SettledAfterChange = TimeSpan.FromSeconds(4);

    public BatteryMonitor(SettingsStore settings, Dispatcher ui)
    {
        this.settings = settings;
        this.ui = ui;
        IBatteryProvider?[] all = [dualSense, Safe(() => new GamingInputProvider()), new WindowsBluetoothProvider()];
        providers = all.OfType<IBatteryProvider>().ToArray();
        settings.Changed += _ => RefreshNow();

        soon = new Timer(_ => RefreshAfterChange());
        settled = new Timer(_ => RefreshAfterChange());
        connections = Safe(() => new ConnectionWatcher());
        if (connections is not null) connections.Changed += OnConnectionChanged;
    }

    private void OnConnectionChanged(string why)
    {
        Log.Info(why);
        soon.Change(SoonAfterChange, Timeout.InfiniteTimeSpan);
        settled.Change(SettledAfterChange, Timeout.InfiniteTimeSpan);
    }

    private void RefreshAfterChange()
    {
        // A pad that was plugged in or out may have reset its lightbar to the default blue.
        Interlocked.Exchange(ref repaintPending, 1);
        RefreshNow();
    }

    public IReadOnlyList<BatteryReading> Latest { get; private set; } = [];

    public bool HasRead { get; private set; }

    /// <summary>Raised on the UI thread after every round.</summary>
    public event Action<IReadOnlyList<BatteryReading>>? Updated;

    public void Start() => _ = Task.Run(LoopAsync);

    public void RefreshNow() => wake.TrySetResult();

    private async Task LoopAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            var round = await PollOnceAsync();
            HasRead = true;
            Latest = round;
            _ = ui.BeginInvoke(() => Updated?.Invoke(round));

            var delay = TimeSpan.FromSeconds(settings.Current.RefreshSeconds);
            try
            {
                await Task.WhenAny(Task.Delay(delay, stop.Token), wake.Task);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            wake = NewWake();
        }
    }

    private async Task<IReadOnlyList<BatteryReading>> PollOnceAsync()
    {
        var all = new List<BatteryReading>();
        foreach (var provider in providers)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                all.AddRange(await provider.ReadAsync(timeout.Token));
            }
            catch (Exception ex) when (!stop.IsCancellationRequested)
            {
                // One broken source should never hide the others.
                Log.Error($"Reading {provider.Name}", ex);
            }
        }
        var merged = ReadingMerger.Merge(all);
        try
        {
            if (Interlocked.Exchange(ref repaintPending, 0) == 1) dualSense.ForceRepaint();
            dualSense.ApplyLightbar(settings.Current, merged);
        }
        catch (Exception ex)
        {
            Log.Error("Applying lightbar", ex);
        }
        return merged;
    }

    private static TaskCompletionSource NewWake() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static T? Safe<T>(Func<T> create) where T : class
    {
        try
        {
            return create();
        }
        catch (Exception ex)
        {
            Log.Error("Starting a battery source", ex);
            return null;
        }
    }

    public void Dispose()
    {
        connections?.Dispose();
        soon.Dispose();
        settled.Dispose();
        stop.Cancel();
        stop.Dispose();
    }
}
