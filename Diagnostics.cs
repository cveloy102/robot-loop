namespace RobotLoop;

internal static class Diagnostics
{
    private static readonly object gate = new();
    internal static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RobotLoop", "diagnostics.log");
    internal static void Write(string message)
    {
        // Called only outside input callbacks. Never log mouse positions or keyboard input.
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 131072)
                    File.Move(LogPath, LogPath + ".previous", true);
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
