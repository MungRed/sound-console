namespace SoundConsole.Core.Audio;

/// <summary>
/// Latency choices shared by the engine and the UI. These values describe software-side
/// buffering only; USB transport, the instrument processor, and the output device add more.
/// </summary>
public static class AudioLatencyPolicy
{
    /// <summary>Observed WASAPI capture callback period on the tested devices.</summary>
    public const int CapturePeriodMilliseconds = 10;

    /// <summary>
    /// The render buffer can be shorter when the user has already chosen a small safety
    /// buffer. Larger safety buffers keep the more conservative 20 ms render buffer.
    /// </summary>
    public static int GetOutputBufferMilliseconds(int safetyBufferMilliseconds) =>
        safetyBufferMilliseconds <= 30 ? 10 : 20;

    /// <summary>
    /// Approximate software-side latency: capture period + application ring target +
    /// requested WASAPI render buffer. Hardware and driver latency are excluded.
    /// </summary>
    public static int EstimateSoftwareLatencyMilliseconds(int safetyBufferMilliseconds) =>
        CapturePeriodMilliseconds +
        safetyBufferMilliseconds +
        GetOutputBufferMilliseconds(safetyBufferMilliseconds);
}
