namespace SoundConsole.Core.Settings;

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SoundConsole");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string BackupSettingsFile => Path.Combine(DataDirectory, "settings.backup.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    public static void OpenDataDirectory()
    {
        EnsureCreated();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = DataDirectory,
            UseShellExecute = true
        });
    }
}
