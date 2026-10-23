using NAudio.CoreAudioApi;
using SoundConsole.Core.Models;

namespace SoundConsole.Core.Audio;

/// <summary>Enumerates active WASAPI endpoints and opens devices by endpoint id.</summary>
public sealed class AudioDeviceService
{
    private static readonly string[] VirtualMarkers =
    [
        "virtual", "steam streaming", "vb-audio", "voicemeeter", "cable output", "cable input", "nvidia virtual"
    ];

    public IReadOnlyList<AudioEndpointInfo> Enumerate(AudioFlow flow)
    {
        var result = new List<AudioEndpointInfo>();
        using var enumerator = new MMDeviceEnumerator();
        var defaultIds = GetDefaultIds(enumerator);
        var dataFlow = flow == AudioFlow.Capture ? DataFlow.Capture : DataFlow.Render;

        foreach (var device in enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active))
        {
            try
            {
                var mix = device.AudioClient.MixFormat;
                if (mix.Channels <= 0 || mix.SampleRate <= 0)
                {
                    continue;
                }

                var raw = string.IsNullOrWhiteSpace(device.FriendlyName) ? "未命名音频设备" : device.FriendlyName.Trim();
                var (display, kind) = ParseFriendlyName(raw);
                var isVirtual = VirtualMarkers.Any(marker => raw.Contains(marker, StringComparison.OrdinalIgnoreCase));
                var isAmpero = raw.Contains("Ampero", StringComparison.OrdinalIgnoreCase);
                result.Add(new AudioEndpointInfo(
                    device.ID,
                    raw,
                    display,
                    kind,
                    flow,
                    mix.Channels,
                    mix.SampleRate,
                    mix.BitsPerSample,
                    defaultIds.Contains(device.ID),
                    isAmpero,
                    isVirtual));
            }
            catch
            {
                // An endpoint can disappear between enumeration and inspection.
            }
            finally
            {
                device.Dispose();
            }
        }

        return result
            .OrderBy(dev => flow == AudioFlow.Capture && dev.IsAmpero ? 0 : dev.IsAmpero ? 2 : 1)
            .ThenByDescending(dev => dev.IsDefault)
            .ThenBy(dev => dev.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(dev => dev.RawName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Opens an active endpoint. The caller owns the returned COM device.</summary>
    public MMDevice? TryOpenDevice(string deviceId, AudioFlow expectedFlow)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        MMDevice? device = null;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            device = enumerator.GetDevice(deviceId);
            if (device.State != DeviceState.Active || device.DataFlow != ToDataFlow(expectedFlow))
            {
                device.Dispose();
                return null;
            }

            return device;
        }
        catch
        {
            device?.Dispose();
            return null;
        }
    }

    public AudioEndpointInfo? Find(string deviceId, AudioFlow flow) =>
        Enumerate(flow).FirstOrDefault(dev => dev.Id == deviceId);

    private static HashSet<string> GetDefaultIds(MMDeviceEnumerator enumerator)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var flow in new[] { DataFlow.Render, DataFlow.Capture })
        {
            foreach (var role in new[] { Role.Multimedia, Role.Communications })
            {
                try
                {
                    using var device = enumerator.GetDefaultAudioEndpoint(flow, role);
                    if (!string.IsNullOrEmpty(device.ID))
                    {
                        ids.Add(device.ID);
                    }
                }
                catch
                {
                    // No default endpoint for this role.
                }
            }
        }

        return ids;
    }

    private static DataFlow ToDataFlow(AudioFlow flow) =>
        flow == AudioFlow.Capture ? DataFlow.Capture : DataFlow.Render;

    private static (string DisplayName, string Kind) ParseFriendlyName(string raw)
    {
        var close = raw.LastIndexOf(')');
        var open = close > 0 ? raw.LastIndexOf('(', close) : -1;
        if (open <= 0 || close != raw.Length - 1)
        {
            return (raw, "音频设备");
        }

        var kind = raw[..open].Trim();
        var inner = raw[(open + 1)..close].Trim();

        // Windows prefixes re-connected USB endpoints with an index: "3- Ampero II Stomp".
        var dash = inner.IndexOf('-');
        if (dash > 0 && dash < 5)
        {
            var prefix = inner[..dash].Trim();
            if (prefix.Length > 0 && prefix.All(char.IsDigit))
            {
                inner = inner[(dash + 1)..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(inner))
        {
            inner = raw;
        }

        return (inner, string.IsNullOrWhiteSpace(kind) ? "音频设备" : kind);
    }
}
