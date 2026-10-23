using System.Runtime.InteropServices;
using NAudio.Wave;

namespace SoundConsole.Core.Audio;

/// <summary>Converts a captured PCM/float block into interleaved stereo floats.</summary>
internal static class PcmBlockReader
{
    public static int ReadStereo(
        byte[] source,
        int byteCount,
        WaveFormat format,
        int pairIndex,
        ref float[] stereo)
    {
        var channels = format.Channels;
        if (channels <= 0 || byteCount <= 0)
        {
            return 0;
        }

        var first = Math.Clamp(pairIndex * 2, 0, channels - 1);
        var second = first + 1 < channels ? first + 1 : first;
        var bytesPerSample = format.BitsPerSample / 8;
        var blockAlign = channels * bytesPerSample;
        if (bytesPerSample <= 0 || blockAlign <= 0)
        {
            return 0;
        }

        var frames = byteCount / blockAlign;
        if (frames <= 0)
        {
            return 0;
        }

        var needed = frames * 2;
        if (stereo.Length < needed)
        {
            stereo = new float[needed];
        }

        if (format.Encoding == WaveFormatEncoding.IeeeFloat && bytesPerSample == 4)
        {
            var samples = MemoryMarshal.Cast<byte, float>(source.AsSpan(0, frames * blockAlign));
            for (var i = 0; i < frames; i++)
            {
                var offset = i * channels;
                stereo[i * 2] = samples[offset + first];
                stereo[i * 2 + 1] = samples[offset + second];
            }

            return frames;
        }

        if (format.Encoding == WaveFormatEncoding.Pcm && bytesPerSample == 2)
        {
            var samples = MemoryMarshal.Cast<byte, short>(source.AsSpan(0, frames * blockAlign));
            for (var i = 0; i < frames; i++)
            {
                var offset = i * channels;
                stereo[i * 2] = samples[offset + first] / 32768f;
                stereo[i * 2 + 1] = samples[offset + second] / 32768f;
            }

            return frames;
        }

        if (format.Encoding == WaveFormatEncoding.Pcm && bytesPerSample == 3)
        {
            for (var i = 0; i < frames; i++)
            {
                var offset = i * blockAlign;
                stereo[i * 2] = Read24(source, offset + first * 3) / 8388608f;
                stereo[i * 2 + 1] = Read24(source, offset + second * 3) / 8388608f;
            }

            return frames;
        }

        if (format.Encoding == WaveFormatEncoding.Pcm && bytesPerSample == 4)
        {
            var samples = MemoryMarshal.Cast<byte, int>(source.AsSpan(0, frames * blockAlign));
            for (var i = 0; i < frames; i++)
            {
                var offset = i * channels;
                stereo[i * 2] = (float)(samples[offset + first] / 2147483648.0);
                stereo[i * 2 + 1] = (float)(samples[offset + second] / 2147483648.0);
            }

            return frames;
        }

        if (format.Encoding == WaveFormatEncoding.Pcm && bytesPerSample == 1)
        {
            for (var i = 0; i < frames; i++)
            {
                var offset = i * channels;
                stereo[i * 2] = (source[offset + first] - 128) / 128f;
                stereo[i * 2 + 1] = (source[offset + second] - 128) / 128f;
            }

            return frames;
        }

        // Unknown shared-mode format: let NAudio's converter handle it.
        var stream = new RawSourceWaveStream(new MemoryStream(source, 0, frames * blockAlign), format);
        var provider = stream.ToSampleProvider();
        var temp = new float[frames * 2];
        var read = 0;
        while (read < temp.Length)
        {
            var count = provider.Read(temp, read, temp.Length - read);
            if (count <= 0)
            {
                break;
            }

            read += count;
        }

        var actualFrames = read / 2;
        for (var i = 0; i < actualFrames; i++)
        {
            stereo[i * 2] = temp[i * 2];
            stereo[i * 2 + 1] = temp[i * 2 + 1];
        }

        return actualFrames;
    }

    private static int Read24(byte[] data, int offset)
    {
        var value = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }
}
