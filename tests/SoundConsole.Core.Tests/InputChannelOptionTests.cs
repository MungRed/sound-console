using SoundConsole.Core.Models;

namespace SoundConsole.Core.Tests;

public sealed class InputChannelOptionTests
{
    [Fact]
    public void MonoDevice_HasSingleOption()
    {
        var options = InputChannelOption.ForChannelCount(1);

        var option = Assert.Single(options);
        Assert.Equal(0, option.PairIndex);
        Assert.Equal("单声道", option.Label);
    }

    [Fact]
    public void StereoDevice_HasOnePair()
    {
        var option = Assert.Single(InputChannelOption.ForChannelCount(2));

        Assert.Equal("声道 1 + 2", option.Label);
        Assert.Equal(1, option.FirstChannel);
        Assert.Equal(2, option.SecondChannel);
    }

    [Fact]
    public void EightChannelDevice_HasFourStereoSets()
    {
        var options = InputChannelOption.ForChannelCount(8);

        Assert.Equal(4, options.Count);
        Assert.Equal(["声道 1 + 2", "声道 3 + 4", "声道 5 + 6", "声道 7 + 8"], options.Select(option => option.Label));
        Assert.Equal(3, options[^1].PairIndex);
    }

    [Fact]
    public void OddChannelDevice_AddsMonoOptionForLastChannel()
    {
        var options = InputChannelOption.ForChannelCount(3);

        Assert.Equal(2, options.Count);
        Assert.Equal("声道 3（单声道）", options[^1].Label);
        Assert.Equal(1, options[^1].PairIndex);
    }

    [Fact]
    public void ToString_ReturnsLabel()
    {
        var option = new InputChannelOption(1, "声道 3 + 4", "USB 通道 3–4");

        Assert.Equal("声道 3 + 4", option.ToString());
    }
}
