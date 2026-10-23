using SoundConsole.Core.Audio;

namespace SoundConsole.Core.Tests;

public sealed class MeterCellTests
{
    [Fact]
    public void Drain_ReturnsPeaksAndResetsState()
    {
        var meter = new MeterCell();
        meter.Report(-0.25f, 0.75f);

        var reading = meter.Drain();

        Assert.Equal(0.25f, reading.Left, 4);
        Assert.Equal(0.75f, reading.Right, 4);
        Assert.False(reading.Clipped);

        var cleared = meter.Drain();
        Assert.Equal(0, cleared.Left);
        Assert.Equal(0, cleared.Right);
    }

    [Fact]
    public void Report_LatchesClippingUntilDrained()
    {
        var meter = new MeterCell();
        meter.Report(1.2f, 0.1f);

        var clipped = meter.Drain();
        var next = meter.Drain();

        Assert.True(clipped.Clipped);
        Assert.False(next.Clipped);
    }

    [Fact]
    public void Report_IgnoresNonFiniteSamples()
    {
        var meter = new MeterCell();
        meter.Report(float.NaN, float.PositiveInfinity);

        var reading = meter.Drain();

        Assert.Equal(0, reading.Left);
        Assert.Equal(0, reading.Right);
        Assert.False(reading.Clipped);
    }

    [Fact]
    public void Report_KeepsTheHighestPeakInTheWindow()
    {
        var meter = new MeterCell();
        meter.Report(0.4f, 0.2f);
        meter.Report(0.1f, 0.8f);

        var reading = meter.Drain();

        Assert.Equal(0.4f, reading.Left, 4);
        Assert.Equal(0.8f, reading.Right, 4);
    }
}
