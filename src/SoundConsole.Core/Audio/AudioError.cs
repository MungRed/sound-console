using System.Runtime.InteropServices;

namespace SoundConsole.Core.Audio;

public static class AudioError
{
    public static string Describe(Exception exception, string action)
    {
        var code = (uint)exception.HResult;
        var reason = code switch
        {
            0x88890004 => "音频设备已断开或重新连接。",
            0x88890008 => "设备不支持当前音频格式。",
            0x8889000A => "设备被其他程序独占或暂时不可用。",
            0x88890017 => "设备不允许独占模式。",
            0x80070005 => "没有访问该音频设备的权限。",
            0x8007001F => "设备驱动程序报告了一个错误。",
            _ => string.Empty
        };

        if (exception is COMException && string.IsNullOrEmpty(reason))
        {
            reason = "Windows 音频接口返回错误。";
        }

        if (exception.Message.Contains("Can't find a supported format", StringComparison.OrdinalIgnoreCase))
        {
            reason = "设备不支持所选输出格式。";
        }

        var detail = string.IsNullOrWhiteSpace(reason)
            ? exception.Message
            : $"{reason}（{exception.Message}）";
        return $"{action}失败：{detail}";
    }
}
