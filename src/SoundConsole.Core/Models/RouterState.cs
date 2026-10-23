namespace SoundConsole.Core.Models;

public enum RouterState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted
}

public sealed class RouterFaultEventArgs : EventArgs
{
    public RouterFaultEventArgs(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }

    public string Message { get; }
    public Exception? Exception { get; }
}

public sealed class RouteStartedEventArgs : EventArgs
{
    public RouteStartedEventArgs(IReadOnlyList<ActiveOutputInfo> outputs, int inputChannels, int inputSampleRate, int channelPair)
    {
        Outputs = outputs;
        InputChannels = inputChannels;
        InputSampleRate = inputSampleRate;
        ChannelPair = channelPair;
    }

    public IReadOnlyList<ActiveOutputInfo> Outputs { get; }
    public int InputChannels { get; }
    public int InputSampleRate { get; }
    public int ChannelPair { get; }
}
