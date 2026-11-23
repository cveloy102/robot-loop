using System.Text.Json;

namespace RobotLoop;

internal sealed class Settings
{
    public Keys RecordKey { get; set; } = Keys.F6;
    public Keys PlayKey { get; set; } = Keys.F7;
    public decimal PauseSeconds { get; set; } = 3;
    public bool LightTheme { get; set; }
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RobotLoop", "settings.json");
    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
    }
    public static string KeyName(Keys key) => new KeysConverter().ConvertToString(key) ?? key.ToString();
}
