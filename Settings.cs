using System;
using System.IO;
using System.Text.Json;

namespace Notepad;

/// <summary>
/// Persisted user preferences, stored as JSON under the platform's app-data
/// directory (~/.config/SimpleNotepad/settings.json on Linux).
/// </summary>
public class Settings
{
    public string Theme { get; set; } = "System";   // System | Light | Dark
    public bool WordWrap { get; set; }
    public bool StatusBar { get; set; } = true;
    public int Zoom { get; set; } = 100;
    public string? FontFamily { get; set; }
    public double FontSize { get; set; } = 14;

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleNotepad");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static Settings? _current;
    public static Settings Current => _current ??= Load();

    private static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* fall back to defaults on any read/parse error */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort; ignore write failures */ }
    }
}
