using SoundConsole.Core.Audio;

namespace SoundConsole.Core.Tests;

public sealed class SharedAudioBufferTests
{
    [Fact]
    public void ReadStereo_ReturnsConstantAtIntegerPositions()
    {
        var buffer = CreateBuffer();
        var meter = new MeterCell();
        WriteConstant(buffer, frameCount: 1000, value: 0.25f, meter);
        var destination = new float[200 * 2];

        var read = buffer.ReadStereo(readFrame: 0, ratio: 1.0, frames: 200, destination);

        Assert.True(read);
        for (var i = 0; i < destination.Length; i++)
        {
            Assert.Equal(0.25f, destination[i], 4);
        }
    }

    [Fact]
    public void ReadStereo_ResamplesWhenOutputClockDiffers()
    {
        var buffer = CreateBuffer();
        WriteConstant(buffer, frameCount: 1000, value: 0.5f, new MeterCell());
        var destination = new float[100 * 2];

        var read = buffer.ReadStereo(readFrame: 100, ratio: 0.5, frames: 100, destination);

        Assert.True(read);
        Assert.All(destination, sample => Assert.Equal(0.5f, sample, 4));
    }

    [Fact]
    public void Write_AppliesGainAndReportsClipping()
    {
        var buffer = CreateBuffer();
        var meter = new MeterCell();
        float[] frame = [0.6f, -0.6f];

        buffer.Write(frame, frames: 1, gain: 2f, meter);
        var reading = meter.Drain();

        Assert.Equal(1.2f, reading.Left, 3);
        Assert.Equal(1.2f, reading.Right, 3);
        Assert.True(reading.Clipped);
    }

    [Fact]
    public void ReadStereo_ReturnsFalseAfterReaderFallsBehindCapacity()
    {
        var buffer = CreateBuffer(capacityFrames: 1024);
        WriteRamp(buffer, frameCount: 1500);
        var destination = new float[10 * 2];

        var read = buffer.ReadStereo(readFrame: 0, ratio: 1.0, frames: 10, destination);

        Assert.False(read);
    }

    [Fact]
    public void ReadStereo_ReadsAcrossRingWrap()
    {
        var buffer = CreateBuffer(capacityFrames: 1024);
        WriteRamp(buffer, frameCount: 1500);
        var destination = new float[20 * 2];

        var read = buffer.ReadStereo(readFrame: 600, ratio: 1.0, frames: 20, destination);

        Assert.True(read);
        Assert.Equal(600 / 1500f, destination[0], 4);
        Assert.Equal(600 / 1500f, destination[1], 4);
        Assert.Equal(601 / 1500f, destination[2], 4);
        Assert.Equal(619 / 1500f, destination[^2], 4);
        Assert.Equal(619 / 1500f, destination[^1], 4);
    }

    [Fact]
    public void Write_TruncatesOversizedBlockToNewestFrames()
    {
        var buffer = CreateBuffer(capacityFrames: 128);
        var data = new float[256 * 2];
        for (var i = 0; i < 256; i++)
        {
            data[i * 2] = i / 256f;
            data[i * 2 + 1] = i / 256f;
        }

        buffer.Write(data, frames: 256, gain: 1f, new MeterCell());
        var destination = new float[16 * 2];
        var read = buffer.ReadStereo(readFrame: 129, ratio: 1.0, frames: 16, destination);

        Assert.True(read);
        Assert.Equal(129 / 256f, destination[0], 4);
    }

    [Fact]
    public void Capacity_IsRoundedUpToPowerOfTwo()
    {
        var buffer = new SharedAudioBuffer(48000, 1000);

        Assert.Equal(1024, buffer.CapacityFrames);
        Assert.Equal(48000, buffer.SampleRate);
    }

    private static SharedAudioBuffer CreateBuffer(int capacityFrames = 4096) =>
        new(sampleRate: 48000, capacityFrames: capacityFrames);

    private static void WriteConstant(SharedAudioBuffer buffer, int frameCount, float value, MeterCell meter)
    {
        var data = new float[frameCount * 2];
        Array.Fill(data, value);
        buffer.Write(data, frameCount, gain: 1f, meter);
    }

    private static void WriteRamp(SharedAudioBuffer buffer, int frameCount)
    {
        var data = new float[frameCount * 2];
        for (var i = 0; i < frameCount; i++)
        {
            data[i * 2] = i / (float)frameCount;
            data[i * 2 + 1] = i / (float)frameCount;
        }

        buffer.Write(data, frameCount, gain: 1f, new MeterCell());
    }
}
