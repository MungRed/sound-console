namespace SoundConsole.Core.Models;

/// <summary>A selectable pair of capture channels.</summary>
public sealed record InputChannelOption(int PairIndex, string Label, string Hint)
{
    public int FirstChannel => PairIndex * 2 + 1;
    public int SecondChannel => PairIndex * 2 + 2;

    public override string ToString() => Label;

    public static IReadOnlyList<InputChannelOption> ForChannelCount(int channels)
    {
        if (channels <= 1)
        {
            return [new InputChannelOption(0, "单声道", "仅一个输入通道")];
        }

        if (channels == 2)
        {
            return [new InputChannelOption(0, "声道 1 + 2", "立体声")];
        }

        var options = new List<InputChannelOption>();
        for (var start = 0; start + 1 < channels; start += 2)
        {
            var hint = start == 0 ? "USB 主输出 / 前两个通道" : $"USB 通道 {start + 1}–{start + 2}";
            options.Add(new InputChannelOption(start / 2, $"声道 {start + 1} + {start + 2}", hint));
        }

        if (channels % 2 == 1)
        {
            options.Add(new InputChannelOption(channels / 2, $"声道 {channels}（单声道）", "最后一个通道"));
        }

        return options;
    }
}
