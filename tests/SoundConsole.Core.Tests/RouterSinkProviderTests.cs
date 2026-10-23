using SoundConsole.Core.Audio;

namespace SoundConsole.Core.Tests;

public sealed class RouterSinkProviderTests
{
    [Fact]
    public void Read_ProducesAudioWithoutDropoutsWhenCaptureKeepsPace()
    {
        var buffer = new SharedAudioBuffer(48000, 32768);
        var provider = CreatePrimedProvider(buffer);
        var destination = new float[240 * 2];

        Pump(provider, buffer, destination, blocks: 40);
        var (meter, dropouts) = provider.DrainMeter();

        Assert.Equal(0, dropouts);
        Assert.True(meter.Left > 0.4f, $"Expected audible output, got {meter.Left}.");
        Assert.True(meter.Right > 0.4f, $"Expected audible output, got {meter.Right}.");
        Assert.Equal(40 * 240L, provider.RenderedFrames);
    }

    [Fact]
    public void Read_MuteRampsOutputToSilence()
    {
        var buffer = new SharedAudioBuffer(48000, 32768);
        var provider = CreatePrimedProvider(buffer);
        var destination = new float[240 * 2];
        Pump(provider, buffer, destination, blocks: 10);
        provider.DrainMeter();

        provider.SetMuted(true);
        Pump(provider, buffer, destination, blocks: 10);
        provider.DrainMeter();
        Pump(provider, buffer, destination, blocks: 20);

        // Discard the fading tail, then verify that the settled output is silent.
        provider.DrainMeter();
        Pump(provider, buffer, destination, blocks: 10);
        var (meter, dropouts) = provider.DrainMeter();

        Assert.Equal(0, dropouts);
        Assert.True(meter.Left < 0.0001f);
        Assert.True(meter.Right < 0.0001f);
    }

    [Fact]
    public void Read_VolumeIsAppliedToOutputMeter()
    {
        var buffer = new SharedAudioBuffer(48000, 32768);
        var provider = CreatePrimedProvider(buffer);
        provider.SetVolume(0.25f);
        var destination = new float[240 * 2];

        Pump(provider, buffer, destination, blocks: 40);
        var (meter, dropouts) = provider.DrainMeter();

        Assert.Equal(0, dropouts);
        Assert.InRange(meter.Left, 0.1f, 0.14f);
    }

    [Fact]
    public void WaveFormat_IsStereoFloatAtOutputRate()
    {
        var buffer = new SharedAudioBuffer(48000, 32768);
        var provider = new RouterSinkProvider(buffer, outputSampleRate: 96000, targetFrames: 960);

        Assert.Equal(96000, provider.WaveFormat.SampleRate);
        Assert.Equal(2, provider.WaveFormat.Channels);
        Assert.Equal(NAudio.Wave.WaveFormatEncoding.IeeeFloat, provider.WaveFormat.Encoding);
    }

    [Fact]
    public void Read_OverflowSkipsAheadInsteadOfBuildingLatency()
    {
        var buffer = new SharedAudioBuffer(48000, 32768);
        WriteConstant(buffer, frames: 4800, value: 0.5f);
        var provider = new RouterSinkProvider(buffer, 48000, targetFrames: 960);
        provider.SetVolume(1f);
        var destination = new float[240 * 2];
        provider.Read(destination, 0, destination.Length);

        // Feed far beyond the allowed latency window without the output consuming it.
        WriteConstant(buffer, frames: 60000, value: 0.5f);
        provider.Read(destination, 0, destination.Length);

        Assert.True(provider.Dropouts >= 1);
        Assert.True(provider.LastOverflowUnixMs > 0);
    }

    private static RouterSinkProvider CreatePrimedProvider(SharedAudioBuffer buffer)
    {
        WriteConstant(buffer, frames: 4800, value: 0.5f);
        var provider = new RouterSinkProvider(buffer, 48000, targetFrames: 960);
        provider.SetVolume(1f);
        return provider;
    }

    private static void Pump(
        RouterSinkProvider provider,
        SharedAudioBuffer buffer,
        float[] destination,
        int blocks)
    {
        for (var i = 0; i < blocks; i++)
        {
            var count = provider.Read(destination, 0, destination.Length);
            Assert.Equal(destination.Length, count);
            WriteConstant(buffer, frames: destination.Length / 2, value: 0.5f);
        }
    }

    private static void WriteConstant(SharedAudioBuffer buffer, int frames, float value)
    {
        var samples = new float[frames * 2];
        Array.Fill(samples, value);
        buffer.Write(samples, frames, gain: 1f, new MeterCell());
    }
}
