namespace SoundConsole.Core.Models;

/// <summary>Audio endpoint data flow direction.</summary>
public enum AudioFlow
{
    Capture,
    Render
}

/// <summary>A snapshot of an active Windows audio endpoint.</summary>
public sealed record AudioEndpointInfo(
    string Id,
    string RawName,
    string DisplayName,
    string Kind,
    AudioFlow Flow,
    int Channels,
    int SampleRate,
    int BitsPerSample,
    bool IsDefault,
    bool IsAmpero,
    bool IsVirtual)
{
    public string FormatSummary => $"{FormatRate(SampleRate)} · {Channels} 声道";

    public string DetailSummary => $"{Kind} · {FormatSummary}";

    public string Tag
    {
        get
        {
            if (IsAmpero) return "吉他效果器";
            if (IsVirtual) return "虚拟设备";
            var name = RawName.ToUpperInvariant();
            if (name.Contains("TOPPING") || name.Contains("DAC")) return "USB DAC";
            if (name.Contains("INZONE") || name.Contains("HEADPHONE") || name.Contains("耳机")) return "耳机";
            if (name.Contains("HDMI") || name.Contains("数字输出")) return "数字输出";
            if (name.Contains("MIC") || name.Contains("麦克风")) return "麦克风";
            if (name.Contains("SPEAKER") || name.Contains("扬声器")) return "扬声器";
            return Flow == AudioFlow.Capture ? "输入设备" : "输出设备";
        }
    }

    public string DefaultBadge => IsDefault ? "系统默认" : string.Empty;

    private static string FormatRate(int rate) =>
        rate % 1000 == 0 ? $"{rate / 1000.0:0.#} kHz" : $"{rate / 1000.0:0.##} kHz";
}

public static class AudioFlowExtensions
{
    public static string ToChinese(this AudioFlow flow) =>
        flow == AudioFlow.Capture ? "输入" : "输出";
}
