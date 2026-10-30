using Blippy.Core;

namespace Blippy.Providers;

/// <summary>One way of finding out how charged devices are. Each runs off the UI thread.</summary>
internal interface IBatteryProvider
{
    string Name { get; }

    Task<IReadOnlyList<BatteryReading>> ReadAsync(CancellationToken cancel);
}
