using SoundConsole.Core.Models;

namespace SoundConsole.Core.Audio;

/// <summary>Single-block peak meter with atomic updates from audio threads.</summary>
internal sealed class MeterCell
{
    private int _leftBits;
    private int _rightBits;
    private int _clipped;

    public void Report(float left, float right)
    {
        ReportPeak(ref _leftBits, Math.Abs(left));
        ReportPeak(ref _rightBits, Math.Abs(right));
        var finiteClipping =
            (float.IsFinite(left) && Math.Abs(left) >= 1f) ||
            (float.IsFinite(right) && Math.Abs(right) >= 1f);
        if (finiteClipping)
        {
            Interlocked.Exchange(ref _clipped, 1);
        }
    }

    public MeterReading Drain()
    {
        var left = BitConverter.Int32BitsToSingle(Interlocked.Exchange(ref _leftBits, 0));
        var right = BitConverter.Int32BitsToSingle(Interlocked.Exchange(ref _rightBits, 0));
        var clipped = Interlocked.Exchange(ref _clipped, 0) != 0;
        if (!float.IsFinite(left)) left = 0;
        if (!float.IsFinite(right)) right = 0;
        return new MeterReading(left, right, clipped);
    }

    private static void ReportPeak(ref int target, float value)
    {
        if (!float.IsFinite(value) || value <= 0)
        {
            return;
        }

        while (true)
        {
            var oldBits = Volatile.Read(ref target);
            var oldValue = BitConverter.Int32BitsToSingle(oldBits);
            if (value <= oldValue)
            {
                return;
            }

            var newBits = BitConverter.SingleToInt32Bits(value);
            if (Interlocked.CompareExchange(ref target, newBits, oldBits) == oldBits)
            {
                return;
            }
        }
    }
}
