using NAudio.Wave;

namespace SoundConsole.Core.Audio;

/// <summary>
/// Stereo sample provider for one WASAPI output. It performs clock-drift tolerant
/// interpolation from the shared capture ring and an output-side gain ramp.
/// </summary>
internal sealed class RouterSinkProvider : ISampleProvider
{
    private const double CorrectionLimit = 0.004;
    private readonly SharedAudioBuffer _buffer;
    private readonly double _baseRatio;
    private readonly int _targetFrames;
    private readonly MeterCell _meter = new();
    private readonly object _readGate = new();
    private float[] _stereo = new float[8192];
    private double _readFrame;
    private bool _primed;
    private float _currentGain;
    private int _volumeBits = BitConverter.SingleToInt32Bits(0.5f);
    private int _muted;
    private int _fadeOut;
    private long _dropouts;
    private long _renderedFrames;
    private int _lastReadFrames;
    private long _lastOverflow;

    public RouterSinkProvider(SharedAudioBuffer buffer, int outputSampleRate, int targetFrames)
    {
        _buffer = buffer;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(outputSampleRate, 2);
        _baseRatio = buffer.SampleRate / (double)outputSampleRate;
        _targetFrames = Math.Max(64, targetFrames);
    }

    public WaveFormat WaveFormat { get; }

    public long Dropouts => Interlocked.Read(ref _dropouts);

    public long RenderedFrames => Interlocked.Read(ref _renderedFrames);

    public int LastReadFrames => Volatile.Read(ref _lastReadFrames);

    public long LastOverflowUnixMs => Interlocked.Read(ref _lastOverflow);

    public void SetVolume(float linear) =>
        Volatile.Write(ref _volumeBits, BitConverter.SingleToInt32Bits(Math.Clamp(linear, 0f, 2f)));

    public void SetMuted(bool muted) => Volatile.Write(ref _muted, muted ? 1 : 0);

    public void BeginFadeOut() => Volatile.Write(ref _fadeOut, 1);

    public void ResetDropouts() => Interlocked.Exchange(ref _dropouts, 0);

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / 2;
        if (frames <= 0)
        {
            if (count > 0)
            {
                buffer[offset] = 0;
            }

            return count;
        }

        if (_stereo.Length < frames * 2)
        {
            _stereo = new float[frames * 2];
        }

        Volatile.Write(ref _lastReadFrames, frames);
        var rendered = 0;
        var volume = Volatile.Read(ref _fadeOut) != 0
            ? 0f
            : BitConverter.Int32BitsToSingle(Volatile.Read(ref _volumeBits));
        if (Volatile.Read(ref _muted) != 0)
        {
            volume = 0f;
        }

        if (!_primed)
        {
            TryPrime();
        }

        if (_primed)
        {
            var write = _buffer.WritePosition;
            var pending = write - _readFrame;
            var needed = frames * _baseRatio + 3;
            if (pending >= needed)
            {
                var safeTarget = Math.Max(_targetFrames, 64);
                var error = pending - safeTarget;
                var correction = Math.Clamp(error / safeTarget * 0.06, -CorrectionLimit, CorrectionLimit);
                var ratio = _baseRatio * (1 + correction);

                // A long stall can leave seconds of old audio queued. Skip ahead rather than
                // playing an increasingly delayed signal.
                var maxPending = safeTarget + Math.Max(safeTarget * 8, _buffer.SampleRate);
                if (pending > maxPending)
                {
                    _readFrame = write - safeTarget;
                    Interlocked.Exchange(ref _lastOverflow, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    Interlocked.Increment(ref _dropouts);
                }

                if (_buffer.ReadStereo(_readFrame, ratio, frames, _stereo))
                {
                    _readFrame += frames * ratio;
                    rendered = frames;
                }
                else
                {
                    _primed = false;
                    Interlocked.Increment(ref _dropouts);
                }
            }
            else
            {
                _primed = false;
                Interlocked.Increment(ref _dropouts);
            }
        }

        if (rendered == 0)
        {
            Array.Clear(buffer, offset, frames * 2);
            _meter.Report(0, 0);
            if (Volatile.Read(ref _fadeOut) != 0)
            {
                _currentGain = 0;
            }

            return count;
        }

        var peakLeft = 0f;
        var peakRight = 0f;
        var gain = _currentGain;
        var destination = offset;
        for (var i = 0; i < frames; i++)
        {
            gain += (volume - gain) * 0.0025f;
            if (Math.Abs(volume - gain) < 0.00002f)
            {
                gain = volume;
            }

            var left = Math.Clamp(_stereo[i * 2] * gain, -1f, 1f);
            var right = Math.Clamp(_stereo[i * 2 + 1] * gain, -1f, 1f);
            var absLeft = Math.Abs(left);
            var absRight = Math.Abs(right);
            if (absLeft > peakLeft) peakLeft = absLeft;
            if (absRight > peakRight) peakRight = absRight;
            buffer[destination++] = left;
            buffer[destination++] = right;
        }

        _currentGain = gain;
        _meter.Report(peakLeft, peakRight);
        Interlocked.Add(ref _renderedFrames, frames);
        return count;
    }

    public (Models.MeterReading Meter, long Dropouts) DrainMeter() =>
        (_meter.Drain(), Interlocked.Read(ref _dropouts));

    private void TryPrime()
    {
        lock (_readGate)
        {
            if (_primed)
            {
                return;
            }

            var write = _buffer.WritePosition;
            if (write >= _targetFrames + 64)
            {
                _readFrame = write - _targetFrames;
                _primed = true;
            }
        }
    }
}
