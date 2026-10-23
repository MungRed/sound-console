using NAudio.CoreAudioApi;
using NAudio.Wave;
using SoundConsole.Core.Models;
using SoundConsole.Core.Settings;

namespace SoundConsole.Core.Audio;

/// <summary>One opened render endpoint plus its router sample provider.</summary>
internal sealed class OutputSink : IDisposable
{
    private readonly MMDevice _device;
    private readonly WasapiOut _output;
    private readonly bool _exclusive;
    private readonly string _deviceId;
    private readonly string _deviceName;
    private readonly int _sampleRate;
    private volatile bool _stopping;
    private bool _disposed;

    private OutputSink(
        string deviceId,
        string deviceName,
        MMDevice device,
        WasapiOut output,
        RouterSinkProvider provider,
        bool exclusive,
        string? note)
    {
        _deviceId = deviceId;
        _deviceName = deviceName;
        _device = device;
        _output = output;
        Provider = provider;
        _exclusive = exclusive;
        Note = note;
        _sampleRate = output.OutputWaveFormat.SampleRate;
        _output.PlaybackStopped += OnPlaybackStopped;
    }

    public RouterSinkProvider Provider { get; }

    public string? Note { get; }

    public string DeviceId => _deviceId;

    public string DeviceName => _deviceName;

    public bool Exclusive => _exclusive;

    public int SampleRate => _sampleRate;

    public event EventHandler<Exception>? UnexpectedStop;

    public static OutputSink Open(
        AudioDeviceService devices,
        RouteOutputRequest request,
        SharedAudioBuffer buffer,
        int inputSampleRate,
        int targetFrames,
        int latencyMs,
        bool preferExclusive)
    {
        var device = devices.TryOpenDevice(request.DeviceId, AudioFlow.Render)
            ?? throw new InvalidOperationException($"找不到输出设备“{request.DeviceName}”，它可能已断开。");

        try
        {
            if (preferExclusive)
            {
                foreach (var rate in GetExclusiveRates(inputSampleRate))
                {
                    try
                    {
                        var format = WaveFormat.CreateIeeeFloatWaveFormat(rate, 2);
                        return Create(
                            device,
                            request,
                            buffer,
                            targetFrames,
                            latencyMs,
                            format,
                            exclusive: true,
                            note: null);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Info($"{request.DeviceName}: 独占 {rate}Hz 不可用：{ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}");
                    }
                }
            }

            int mixRate;
            using (var client = device.AudioClient)
            {
                mixRate = client.MixFormat.SampleRate;
            }

            var sharedFormat = WaveFormat.CreateIeeeFloatWaveFormat(NormalizeSharedRate(mixRate), 2);
            var note = preferExclusive ? "此设备不支持独占模式，已改用共享模式。" : null;
            return Create(
                device,
                request,
                buffer,
                targetFrames,
                latencyMs,
                sharedFormat,
                exclusive: false,
                note: note);
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    private static OutputSink Create(
        MMDevice device,
        RouteOutputRequest request,
        SharedAudioBuffer buffer,
        int targetFrames,
        int latencyMs,
        WaveFormat format,
        bool exclusive,
        string? note)
    {
        WasapiOut? output = null;
        try
        {
            var provider = new RouterSinkProvider(buffer, format.SampleRate, targetFrames);
            output = new WasapiOut(
                device,
                exclusive ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared,
                useEventSync: true,
                latencyMs);
            output.Init(provider);

            var sink = new OutputSink(
                request.DeviceId,
                request.DeviceName,
                device,
                output,
                provider,
                exclusive,
                note);
            sink.Provider.SetVolume(request.Volume);
            sink.Provider.SetMuted(request.Muted);
            return sink;
        }
        catch
        {
            output?.Dispose();
            throw;
        }
    }

    private static IEnumerable<int> GetExclusiveRates(int inputSampleRate)
    {
        return new[] { inputSampleRate, 48000, 44100, 96000 }
            .Where(rate => rate >= 8000 && rate <= 192000)
            .Distinct();
    }

    public void Play()
    {
        if (_stopping || _disposed)
        {
            return;
        }

        _output.Play();
    }

    public void SetVolume(float linear) => Provider.SetVolume(linear);

    public void SetMuted(bool muted) => Provider.SetMuted(muted);

    public void ResetDropouts() => Provider.ResetDropouts();

    public bool IsPlaying => _output.PlaybackState == PlaybackState.Playing;

    public (MeterReading Meter, long Dropouts) DrainMeter() => Provider.DrainMeter();

    public ActiveOutputInfo ToInfo() => new()
    {
        DeviceId = _deviceId,
        DeviceName = _deviceName,
        SampleRate = SampleRate,
        Exclusive = Exclusive,
        Note = Note
    };

    public void StopForShutdown()
    {
        if (_stopping || _disposed)
        {
            return;
        }

        _stopping = true;
        try
        {
            Provider.BeginFadeOut();
            Thread.Sleep(30);
            _output.Stop();
        }
        catch
        {
            // Stopping an already-invalidated endpoint can throw.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping = true;
        _output.PlaybackStopped -= OnPlaybackStopped;
        try
        {
            _output.Dispose();
        }
        catch
        {
            // Ignore teardown failures.
        }

        try
        {
            _device.Dispose();
        }
        catch
        {
            // Ignore teardown failures.
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_stopping || _disposed || e.Exception is null)
        {
            return;
        }

        UnexpectedStop?.Invoke(this, e.Exception);
    }

    private static int NormalizeSharedRate(int mixRate)
    {
        if (mixRate <= 48000) return mixRate;
        if (mixRate <= 96000) return 96000;
        return 96000;
    }
}
