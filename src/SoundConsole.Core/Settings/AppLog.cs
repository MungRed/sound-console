namespace SoundConsole.Core.Settings;

public static class AppLog
{
    private static readonly object Gate = new();
    private const long MaxLogBytes = 2 * 1024 * 1024;

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                AppPaths.EnsureCreated();
                var path = Path.Combine(AppPaths.LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
                if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                {
                    var archive = Path.Combine(
                        AppPaths.LogDirectory,
                        $"app-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                    File.Move(path, archive, overwrite: true);
                }

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                if (exception is not null)
                {
                    line += Environment.NewLine + exception;
                }

                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never take the audio path down.
        }
    }
}
