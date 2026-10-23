namespace SoundConsole.Core.Models;

/// <summary>An output device the user has chosen for the current route.</summary>
public sealed class RouteOutputRequest
{
    public required string DeviceId { get; init; }
    public required string DeviceName { get; init; }
    /// <summary>Linear gain, 0..1 (1 = 0 dB).</summary>
    public float Volume { get; init; } = 0.5f;
    public bool Muted { get; init; }
}

/// <summary>Everything required to start a route.</summary>
public sealed class RouteRequest
{
    public required string InputDeviceId { get; init; }
    public required string InputDeviceName { get; init; }
    public int InputChannelPair { get; init; }
    /// <summary>Input gain in dB.</summary>
    public double InputGainDb { get; init; }
    /// <summary>Extra safety buffer in milliseconds.</summary>
    public int BufferMilliseconds { get; init; } = 40;
    public bool PreferExclusive { get; init; }
    public required IReadOnlyList<RouteOutputRequest> Outputs { get; init; }
}

public sealed class ActiveOutputInfo
{
    public required string DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public required int SampleRate { get; init; }
    public required bool Exclusive { get; init; }
    public string? Note { get; init; }
}

public sealed record MeterReading(float Left, float Right, bool Clipped)
{
    public static readonly MeterReading Silence = new(0, 0, false);

    public double LeftDb => ToDb(Left);
    public double RightDb => ToDb(Right);
    public bool HasSignal => Left > 0.00001f || Right > 0.00001f;

    private static double ToDb(float peak) =>
        peak <= 1e-6f ? -120 : 20 * Math.Log10(peak);
}

public sealed class RouterSnapshot
{
    public required MeterReading Input { get; init; }
    public required IReadOnlyDictionary<string, MeterReading> Outputs { get; init; }
    public required long Dropouts { get; init; }
}
