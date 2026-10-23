using SoundConsole.Core.Audio;
using SoundConsole.Core.Models;

namespace SoundConsole.Core.Tests;

public sealed class AudioErrorTests
{
    [Fact]
    public void Describe_MapsKnownWindowsAudioError()
    {
        var exception = new System.Runtime.InteropServices.COMException(
            "The endpoint was invalidated.",
            unchecked((int)0x88890004));

        var message = AudioError.Describe(exception, "输出设备");

        Assert.StartsWith("输出设备失败：", message);
        Assert.Contains("已断开", message);
    }

    [Fact]
    public void MeterReading_CalculatesDbAndSignalPresence()
    {
        var reading = new MeterReading(0.5f, 0f, false);

        Assert.Equal(-6.02, reading.LeftDb, 1);
        Assert.Equal(-120, reading.RightDb);
        Assert.True(reading.HasSignal);
    }
}
