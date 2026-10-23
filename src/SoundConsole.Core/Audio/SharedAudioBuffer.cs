namespace SoundConsole.Core.Audio;

/// <summary>
/// A fixed-size stereo ring buffer written by the capture thread and read by every output.
/// Each output owns its own read position; interpolation performs drift-tolerant rate conversion.
/// </summary>
internal sealed class SharedAudioBuffer
{
    private readonly object _gate = new();
    private readonly float[] _samples;
    private readonly int _capacityFrames;
    private readonly int _frameMask;
    private long _writeFrame;
    private float[] _scratchLeft = [];
    private float[] _scratchRight = [];

    public SharedAudioBuffer(int sampleRate, int capacityFrames)
    {
        SampleRate = sampleRate;
        var capacity = 1024;
        while (capacity < capacityFrames)
        {
            capacity <<= 1;
        }

        _capacityFrames = capacity;
        _frameMask = capacity - 1;
        _samples = new float[capacity * 2];
    }

    public int SampleRate { get; }

    public int CapacityFrames => _capacityFrames;

    public long WritePosition => Volatile.Read(ref _writeFrame);

    public long Write(ReadOnlySpan<float> stereo, int frames, float gain, MeterCell meter)
    {
        if (frames <= 0)
        {
            return WritePosition;
        }

        var skippedFrames = 0;
        if (frames > _capacityFrames)
        {
            skippedFrames = frames - _capacityFrames;
            stereo = stereo[(skippedFrames * 2)..];
            frames = _capacityFrames;
        }

        float peakLeft = 0, peakRight = 0;
        lock (_gate)
        {
            var start = _writeFrame + skippedFrames;
            for (var i = 0; i < frames; i++)
            {
                var left = stereo[i * 2] * gain;
                var right = stereo[i * 2 + 1] * gain;
                if (!float.IsFinite(left)) left = 0;
                if (!float.IsFinite(right)) right = 0;

                // Keep pathological feedback from producing infinities further down the chain.
                left = Math.Clamp(left, -4f, 4f);
                right = Math.Clamp(right, -4f, 4f);

                var absLeft = Math.Abs(left);
                var absRight = Math.Abs(right);
                if (absLeft > peakLeft) peakLeft = absLeft;
                if (absRight > peakRight) peakRight = absRight;

                var slot = (int)((start + i) & _frameMask) * 2;
                _samples[slot] = left;
                _samples[slot + 1] = right;
            }

            _writeFrame = start + frames;
            Volatile.Write(ref _writeFrame, _writeFrame);
        }

        meter.Report(peakLeft, peakRight);
        return WritePosition;
    }

    /// <summary>
    /// Reads <paramref name="frames"/> stereo frames using cubic interpolation.
    /// Returns false if the requested position has fallen behind the ring (overrun).
    /// </summary>
    public bool ReadStereo(double readFrame, double ratio, int frames, float[] destination)
    {
        if (frames <= 0)
        {
            return true;
        }

        lock (_gate)
        {
            var write = _writeFrame;
            var oldest = write - _capacityFrames;
            var start = (long)Math.Floor(readFrame) - 1;
            if (start < oldest)
            {
                return false;
            }

            var step = Math.Max(0.000001, ratio);
            var sourceLength = (int)Math.Ceiling(frames * step) + 5;
            if (_scratchLeft.Length < sourceLength)
            {
                _scratchLeft = new float[sourceLength];
                _scratchRight = new float[sourceLength];
            }

            for (var i = 0; i < sourceLength; i++)
            {
                var index = start + i;
                if (index < oldest || index >= write)
                {
                    _scratchLeft[i] = 0;
                    _scratchRight[i] = 0;
                }
                else
                {
                    var slot = (int)(index & _frameMask) * 2;
                    _scratchLeft[i] = _samples[slot];
                    _scratchRight[i] = _samples[slot + 1];
                }
            }

            for (var i = 0; i < frames; i++)
            {
                var position = readFrame + i * step - start;
                if (position < 1 || position >= sourceLength - 2)
                {
                    destination[i * 2] = 0;
                    destination[i * 2 + 1] = 0;
                    continue;
                }

                var index = (int)position;
                var t = (float)(position - index);
                destination[i * 2] = Cubic(
                    _scratchLeft[index - 1], _scratchLeft[index],
                    _scratchLeft[index + 1], _scratchLeft[index + 2], t);
                destination[i * 2 + 1] = Cubic(
                    _scratchRight[index - 1], _scratchRight[index],
                    _scratchRight[index + 1], _scratchRight[index + 2], t);
            }

            return true;
        }
    }

    private static float Cubic(float p0, float p1, float p2, float p3, float t)
    {
        var a = -0.5f * p0 + 1.5f * p1 - 1.5f * p2 + 0.5f * p3;
        var b = p0 - 2.5f * p1 + 2f * p2 - 0.5f * p3;
        var c = -0.5f * p0 + 0.5f * p2;
        return ((a * t + b) * t + c) * t + p1;
    }
}
