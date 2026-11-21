using System;
using System.IO;
using System.Text.Json;

namespace MeuPlayer;

internal sealed class PlayerPreferences
{
    public bool Floating { get; set; }
    public bool Vertical { get; set; }
    public bool Behind { get; set; }
    public bool DockBehind { get; set; }
    public int WindowBehaviorVersion { get; set; }
    public bool MediaVisible { get; set; } = true;
    public bool BatteryVisible { get; set; } = true;
    public bool DockRight { get; set; }
    public bool ShowTrackLyrics { get; set; }
    public bool HideInFullScreen { get; set; } = true;
    public int Theme { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeuPlayer");
    private static readonly string FilePath = Path.Combine(Folder, "preferencias.json");

    internal static PlayerPreferences Load()
    {
        try
        {
            if (new FileInfo(FilePath).Length <= 4096)
                return Upgrade(JsonSerializer.Deserialize<PlayerPreferences>(File.ReadAllText(FilePath)) ?? new());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return Upgrade(new());
    }

    internal static PlayerPreferences Upgrade(PlayerPreferences preferences)
    {
        if (preferences.WindowBehaviorVersion < 1)
        {
            preferences.HideInFullScreen = true;
            preferences.WindowBehaviorVersion = 1;
        }
        return preferences;
    }

    internal void Save()
    {
        string temporary = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(temporary, JsonSerializer.Serialize(this));
            File.Move(temporary, FilePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}




