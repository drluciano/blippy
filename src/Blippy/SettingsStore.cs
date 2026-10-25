using System.IO;
using Blippy.Core;
using Microsoft.Win32;

namespace Blippy;

/// <summary>Holds the current <see cref="Settings"/>, saves every change and keeps "Start with Windows" in step.</summary>
internal sealed class SettingsStore
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blippy", "settings.json");

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "Blippy";

    private readonly bool persist;

    public SettingsStore()
    {
        Current = Settings.Load(FilePath);
        persist = true;
    }

    /// <summary>Settings that live in memory only (the preview renderer).</summary>
    public SettingsStore(Settings initial)
    {
        Current = initial;
    }

    public Settings Current { get; private set; }

    public event Action<Settings>? Changed;

    public void Update(Func<Settings, Settings> change)
    {
        var next = change(Current).Sanitized();
        if (next == Current) return;
        var startupChanged = next.StartWithWindows != Current.StartWithWindows;
        Current = next;
        if (!persist)
        {
            Changed?.Invoke(next);
            return;
        }
        try
        {
            next.Save(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Saving settings", ex);
        }
        if (startupChanged) ApplyStartWithWindows(next.StartWithWindows);
        Changed?.Invoke(next);
    }

    /// <summary>Re-points the startup entry at this exe in case the app was moved since it was turned on.</summary>
    public void SyncStartWithWindows()
    {
        if (Current.StartWithWindows) ApplyStartWithWindows(true);
    }

    private static void ApplyStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && Environment.ProcessPath is { } exe) key.SetValue(RunValue, $"\"{exe}\"");
            else key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Log.Error("Updating Start with Windows", ex);
        }
    }
}
