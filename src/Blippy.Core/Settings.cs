using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blippy.Core;

public enum TrayStyle
{
    /// <summary>The battery percentage as a number.</summary>
    Number,
    /// <summary>The device's icon with a fill that drains with the battery.</summary>
    Glyph,
}

/// <summary>Everything the person can choose. Stored as JSON in %APPDATA%\Blippy\settings.json.</summary>
public sealed record Settings
{
    public bool LightbarEnabled { get; init; }
    public string LightbarColor { get; init; } = "#6E64E8";
    public bool IconPerDevice { get; init; } = true;
    public TrayStyle TrayStyle { get; init; } = TrayStyle.Number;
    public int RefreshSeconds { get; init; } = 30;
    public bool LowBatteryAlerts { get; init; } = true;
    public bool StartWithWindows { get; init; }

    public static readonly int[] RefreshChoices = [15, 30, 60, 120];

    public Rgb LightbarRgb => Rgb.TryParse(LightbarColor, out var c) ? c : new Rgb(0x6E, 0x64, 0xE8);

    /// <summary>Clamps anything out of range a hand-edited file might contain.</summary>
    public Settings Sanitized() => this with
    {
        LightbarColor = LightbarRgb.ToHex(),
        RefreshSeconds = Math.Clamp(RefreshSeconds, 5, 3600),
    };

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>A missing, empty or unreadable file means the defaults.</summary>
    public static Settings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Settings();
        try
        {
            return (JsonSerializer.Deserialize<Settings>(json, Json) ?? new Settings()).Sanitized();
        }
        catch (JsonException)
        {
            return new Settings();
        }
    }

    public static Settings Load(string path)
    {
        try
        {
            return File.Exists(path) ? FromJson(File.ReadAllText(path)) : new Settings();
        }
        catch (IOException)
        {
            return new Settings();
        }
    }

    /// <summary>Writes a sibling file and moves it over, so a crash never leaves half a file.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, ToJson());
        File.Move(tmp, path, overwrite: true);
    }
}
