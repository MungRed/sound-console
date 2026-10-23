using System.Runtime.InteropServices;
using NAudio.Wave;
using SoundConsole.Core.Audio;

namespace SoundConsole.Core.Tests;

public sealed class PcmBlockReaderTests
{
    [Fact]
    public void ReadStereo_Float32_SelectsRequestedChannelPair()
    {
        var samples = new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
        var bytes = ToBytes(samples);
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 4);
        var destination = Array.Empty<float>();

        var frames = PcmBlockReader.ReadStereo(bytes, bytes.Length, format, pairIndex: 1, ref destination);

        Assert.Equal(2, frames);
        Assert.Equal(0.3f, destination[0], 4);
        Assert.Equal(0.4f, destination[1], 4);
        Assert.Equal(0.7f, destination[2], 4);
        Assert.Equal(0.8f, destination[3], 4);
    }

    [Fact]
    public void ReadStereo_MonoPcm16_DuplicatesChannel()
    {
        short[] samples = [16384, -16384];
        var bytes = ToBytes(samples);
        var format = new WaveFormat(44100, 16, 1);
        var destination = Array.Empty<float>();

        var frames = PcmBlockReader.ReadStereo(bytes, bytes.Length, format, pairIndex: 0, ref destination);

        Assert.Equal(2, frames);
        Assert.Equal(0.5f, destination[0], 4);
        Assert.Equal(0.5f, destination[1], 4);
        Assert.Equal(-0.5f, destination[2], 4);
        Assert.Equal(-0.5f, destination[3], 4);
    }

    [Fact]
    public void ReadStereo_Pcm24_DecodesSignedSamples()
    {
        // Stereo frame: +0.5 on channel 1 and -0.5 on channel 2.
        byte[] bytes = [0x00, 0x00, 0x40, 0x00, 0x00, 0xC0];
        var format = new WaveFormat(48000, 24, 2);
        var destination = Array.Empty<float>();

        var frames = PcmBlockReader.ReadStereo(bytes, bytes.Length, format, pairIndex: 0, ref destination);

        Assert.Equal(1, frames);
        Assert.Equal(0.5f, destination[0], 3);
        Assert.Equal(-0.5f, destination[1], 3);
    }

    [Fact]
    public void ReadStereo_EmptyBlock_ReturnsZero()
    {
        var destination = Array.Empty<float>();
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        var frames = PcmBlockReader.ReadStereo([], 0, format, pairIndex: 0, ref destination);

        Assert.Equal(0, frames);
        Assert.Empty(destination);
    }

    private static byte[] ToBytes<T>(T[] values) where T : unmanaged
    {
        var bytes = new byte[values.Length * Marshal.SizeOf<T>()];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
