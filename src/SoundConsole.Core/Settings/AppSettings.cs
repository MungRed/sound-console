using System.Text.Encodings.Web;
using System.Text.Json;

namespace SoundConsole.Core.Settings;

public sealed class OutputPreference
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public double VolumeDb { get; set; } = -6;
    public bool Muted { get; set; }
}

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public string? InputDeviceId { get; set; }
    public string? InputDeviceName { get; set; }
    public int InputChannelPair { get; set; }
    public double InputGainDb { get; set; }
    public int BufferMilliseconds { get; set; } = 40;
    public bool PreferExclusive { get; set; }
    public List<OutputPreference> Outputs { get; set; } = [];

    public OutputPreference GetOutput(string id, string name)
    {
        var existing = Outputs.FirstOrDefault(item =>
            string.Equals(item.DeviceId, id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        existing = Outputs.FirstOrDefault(item =>
            string.IsNullOrEmpty(item.DeviceId) &&
            string.Equals(item.DeviceName, name, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null)
        {
            existing.DeviceId = id;
            return existing;
        }

        var created = new OutputPreference
        {
            DeviceId = id,
            DeviceName = name,
            Enabled = false,
            VolumeDb = -6
        };
        Outputs.Add(created);
        return created;
    }

    public void Normalize()
    {
        InputGainDb = Math.Clamp(InputGainDb, -48, 24);
        InputChannelPair = Math.Max(0, InputChannelPair);
        BufferMilliseconds = Math.Clamp(BufferMilliseconds, 20, 300);
        Outputs ??= [];
        foreach (var output in Outputs)
        {
            output.VolumeDb = Math.Clamp(output.VolumeDb, -60, 6);
        }
    }
}

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();

    public AppSettings Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(AppPaths.SettingsFile))
                {
                    return new AppSettings();
                }

                var json = File.ReadAllText(AppPaths.SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                settings.Normalize();
                return settings;
            }
            catch (Exception ex)
            {
                AppLog.Warn($"读取设置失败，使用默认设置：{ex.Message}");
                TryBackupBrokenSettings();
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            try
            {
                settings.Normalize();
                AppPaths.EnsureCreated();
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                var temp = AppPaths.SettingsFile + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, AppPaths.SettingsFile, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLog.Warn($"保存设置失败：{ex.Message}");
            }
        }
    }

    private static void TryBackupBrokenSettings()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                File.Copy(AppPaths.SettingsFile, AppPaths.BackupSettingsFile, overwrite: true);
            }
        }
        catch
        {
            // Ignore backup failures.
        }
    }
}
