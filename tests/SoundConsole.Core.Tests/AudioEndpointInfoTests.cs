using SoundConsole.Core.Models;

namespace SoundConsole.Core.Tests;

public sealed class AudioEndpointInfoTests
{
    [Fact]
    public void FormatSummary_UsesReadableSampleRateAndChannels()
    {
        var info = Endpoint("Ampero II Stomp", AudioFlow.Capture, 44100, 8, isAmpero: true);

        Assert.Equal("44.1 kHz · 8 声道", info.FormatSummary);
        Assert.Equal("线路 · 44.1 kHz · 8 声道", info.DetailSummary);
    }

    [Fact]
    public void WholeKilohertzSampleRate_OmitsDecimal()
    {
        var info = Endpoint("Speakers", AudioFlow.Render, 48000, 2);

        Assert.Equal("48 kHz · 2 声道", info.FormatSummary);
    }

    [Fact]
    public void AmperoEndpoint_GetsEffectProcessorTag()
    {
        var info = Endpoint("线路 (3- Ampero II Stomp)", AudioFlow.Capture, 44100, 8, isAmpero: true);

        Assert.Equal("吉他效果器", info.Tag);
    }

    [Fact]
    public void DacEndpoint_GetsUsbDacTag()
    {
        var info = Endpoint("扬声器 (TOPPING USB DAC)", AudioFlow.Render, 384000, 2);

        Assert.Equal("USB DAC", info.Tag);
    }

    [Fact]
    public void DefaultBadge_OnlyAppearsForDefaultEndpoint()
    {
        var defaultEndpoint = Endpoint("Speakers", AudioFlow.Render, 48000, 2, isDefault: true);
        var otherEndpoint = Endpoint("Headphones", AudioFlow.Render, 48000, 2);

        Assert.Equal("系统默认", defaultEndpoint.DefaultBadge);
        Assert.Empty(otherEndpoint.DefaultBadge);
    }

    private static AudioEndpointInfo Endpoint(
        string rawName,
        AudioFlow flow,
        int sampleRate,
        int channels,
        bool isDefault = false,
        bool isAmpero = false) =>
        new(
            Id: "{test}",
            RawName: rawName,
            DisplayName: "Test endpoint",
            Kind: flow == AudioFlow.Capture ? "线路" : "扬声器",
            Flow: flow,
            Channels: channels,
            SampleRate: sampleRate,
            BitsPerSample: 32,
            IsDefault: isDefault,
            IsAmpero: isAmpero,
            IsVirtual: false);
}
