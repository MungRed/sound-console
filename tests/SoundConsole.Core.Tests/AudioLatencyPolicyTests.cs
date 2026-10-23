using SoundConsole.Core.Audio;

namespace SoundConsole.Core.Tests;

public sealed class AudioLatencyPolicyTests
{
    [Theory]
    [InlineData(20, 10)]
    [InlineData(30, 10)]
    [InlineData(40, 20)]
    [InlineData(60, 20)]
    [InlineData(160, 20)]
    public void OutputBuffer_UsesLowLatencyValueForSmallSafetyBuffers(int safetyBuffer, int expectedOutputBuffer)
    {
        Assert.Equal(expectedOutputBuffer, AudioLatencyPolicy.GetOutputBufferMilliseconds(safetyBuffer));
    }

    [Theory]
    [InlineData(20, 40)]
    [InlineData(30, 50)]
    [InlineData(40, 70)]
    [InlineData(60, 90)]
    [InlineData(160, 190)]
    public void Estimate_AddsCaptureAndRenderBuffers(int safetyBuffer, int expectedSoftwareLatency)
    {
        Assert.Equal(expectedSoftwareLatency, AudioLatencyPolicy.EstimateSoftwareLatencyMilliseconds(safetyBuffer));
    }
}
