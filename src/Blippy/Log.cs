using System.IO;

namespace Blippy;

/// <summary>A small rolling log in %LOCALAPPDATA%\Blippy, for figuring out why a device does not show up.</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blippy", "blippy.log");

    public static void Info(string message) => Write("info", message);

    public static void Error(string what, Exception ex) => Write("error", $"{what}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 512 * 1024) File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Logging is best effort; never let it take the app down.
        }
    }
}
