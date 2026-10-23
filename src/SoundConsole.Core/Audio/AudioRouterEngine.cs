using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SoundConsole.Core.Models;
using SoundConsole.Core.Settings;

namespace SoundConsole.Core.Audio;

/// <summary>
/// Captures one WASAPI input and fans it out to one or more render endpoints.
/// One ring buffer is shared by all outputs; each output performs its own drift correction.
/// </summary>
public sealed class AudioRouterEngine : IDisposable
{
    private readonly AudioDeviceService _devices = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly MeterCell _inputMeter = new();
    private readonly object _sinkGate = new();
    private readonly object _faultGate = new();
    private List<OutputSink> _sinks = [];
    private List<ActiveOutputInfo> _activeOutputs = [];
    private WasapiCapture? _capture;
    private MMDevice? _captureDevice;
    private SharedAudioBuffer? _buffer;
    private WaveFormat? _captureFormat;
    private float[] _converted = [];
    private int _channelPair;
    private int _inputGainBits = BitConverter.SingleToInt32Bits(1f);
    private int _targetFrames;
    private int _bufferMilliseconds;
    private int _stateValue;
    private volatile bool _intentionalStop;
    private Exception? _captureFault;
    private Exception? _sinkFault;
    private int _failing;
    private long _captureFrames;
    private long _lastCaptureTick;
    private int _startRate;
    private int _startChannels;

    public RouterState State => (RouterState)Volatile.Read(ref _stateValue);

    public string? InputName { get; private set; }

    public int InputSampleRate => _captureFormat?.SampleRate ?? 0;

    public int InputChannels => _captureFormat?.Channels ?? 0;

    public int BufferMilliseconds => _bufferMilliseconds;

    public IReadOnlyList<ActiveOutputInfo> ActiveOutputs
    {
        get
        {
            lock (_sinkGate)
            {
                return _activeOutputs.ToArray();
            }
        }
    }

    public long CaptureFrames => Interlocked.Read(ref _captureFrames);

    public long LastCaptureAgeMs => _lastCaptureTick == 0
        ? -1
        : Environment.TickCount64 - Volatile.Read(ref _lastCaptureTick);

    public event EventHandler<RouterState>? StateChanged;
    public event EventHandler<RouteStartedEventArgs>? Started;
    public event EventHandler<RouterFaultEventArgs>? Faulted;

    public async Task StartAsync(RouteRequest request, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is RouterState.Running or RouterState.Starting or RouterState.Stopping)
            {
                throw new InvalidOperationException("音频路由已经在运行。");
            }

            if (request.Outputs.Count == 0)
            {
                throw new InvalidOperationException("请至少选择一个输出设备。");
            }

            if (request.Outputs.Count > 8)
            {
                throw new InvalidOperationException("一次最多选择 8 个输出设备。");
            }

            _captureFault = null;
            _sinkFault = null;
            _intentionalStop = false;
            Interlocked.Exchange(ref _failing, 0);
            SetState(RouterState.Starting);
            AppLog.Info($"开始路由：{request.InputDeviceName} 声道对 {request.InputChannelPair + 1}, " +
                        $"{request.BufferMilliseconds}ms, 独占={request.PreferExclusive}, " +
                        $"输出={string.Join(" / ", request.Outputs.Select(o => o.DeviceName))}");

            try
            {
                await OpenAndStartAsync(request, cancellationToken).ConfigureAwait(false);
                SetState(RouterState.Running);
                var args = new RouteStartedEventArgs(
                    ActiveOutputs,
                    InputChannels,
                    InputSampleRate,
                    _channelPair);
                Started?.Invoke(this, args);
                AppLog.Info($"路由已启动：输入 {InputName} {InputSampleRate}Hz/{InputChannels}ch, " +
                            $"输出 {string.Join(", ", ActiveOutputs.Select(o => $"{o.DeviceName}({o.SampleRate}Hz,{(o.Exclusive ? "独占" : "共享")})"))}");
            }
            catch (Exception ex)
            {
                AppLog.Error("启动音频路由失败。", ex);
                CleanupResources();
                SetState(RouterState.Stopped);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (State == RouterState.Stopped)
            {
                return;
            }

            SetState(RouterState.Stopping);
            AppLog.Info("正在停止音频路由。");
            CleanupResources();
            SetState(RouterState.Stopped);
            AppLog.Info("音频路由已停止。");
        }
        finally
        {
            _gate.Release();
        }
    }

    public RouterSnapshot SnapshotMeters()
    {
        var input = _inputMeter.Drain();
        OutputSink[] sinks;
        lock (_sinkGate)
        {
            sinks = _sinks.ToArray();
        }

        var outputs = new Dictionary<string, MeterReading>(StringComparer.OrdinalIgnoreCase);
        long dropouts = 0;
        foreach (var sink in sinks)
        {
            try
            {
                var (meter, sinkDropouts) = sink.DrainMeter();
                outputs[sink.DeviceId] = meter;
                dropouts += sinkDropouts;
            }
            catch
            {
                // Sink was torn down between snapshot and read.
            }
        }

        return new RouterSnapshot
        {
            Input = input,
            Outputs = outputs,
            Dropouts = dropouts
        };
    }

    public void SetInputGainDb(double db)
    {
        var linear = (float)Math.Pow(10, Math.Clamp(db, -60, 24) / 20.0);
        Volatile.Write(ref _inputGainBits, BitConverter.SingleToInt32Bits(linear));
    }

    public void SetOutputVolume(string deviceId, float linear)
    {
        foreach (var sink in SnapshotSinks())
        {
            if (string.Equals(sink.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                sink.SetVolume(linear);
            }
        }
    }

    public void SetOutputMuted(string deviceId, bool muted)
    {
        foreach (var sink in SnapshotSinks())
        {
            if (string.Equals(sink.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                sink.SetMuted(muted);
            }
        }
    }

    public string GetDiagnosticText()
    {
        var sinks = SnapshotSinks();
        var parts = new List<string>
        {
            $"state={State}",
            $"input={InputName ?? "-"}/{InputSampleRate}Hz/{InputChannels}ch",
            $"captureFrames={CaptureFrames}",
            $"captureAge={LastCaptureAgeMs}ms",
            $"buffer={_bufferMilliseconds}ms"
        };
        foreach (var sink in sinks)
        {
            parts.Add($"{sink.DeviceName}: rate={sink.SampleRate}, lastRead={sink.Provider.LastReadFrames}, " +
                      $"rendered={sink.Provider.RenderedFrames}, dropouts={sink.Provider.Dropouts}");
        }

        return string.Join(" | ", parts);
    }

    public void Dispose()
    {
        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore shutdown errors.
        }

        _gate.Dispose();
    }

    private async Task OpenAndStartAsync(RouteRequest request, CancellationToken cancellationToken)
    {
        var inputDevice = _devices.TryOpenDevice(request.InputDeviceId, AudioFlow.Capture)
            ?? throw new InvalidOperationException($"找不到输入设备“{request.InputDeviceName}”，请刷新设备列表。");
        _captureDevice = inputDevice;
        InputName = request.InputDeviceName;

        // Event-driven capture hands us ~10 ms packets instead of the 50 ms sleep loop.
        _capture = new WasapiCapture(
            inputDevice,
            useEventSync: true,
            audioBufferMillisecondsLength: Math.Clamp(request.BufferMilliseconds, 20, 100));
        _captureFormat = _capture.WaveFormat;
        _channelPair = request.InputChannelPair;
        _startRate = _captureFormat.SampleRate;
        _startChannels = _captureFormat.Channels;
        _bufferMilliseconds = Math.Clamp(request.BufferMilliseconds, 20, 300);
        _targetFrames = Math.Max(64, (int)(_startRate * (_bufferMilliseconds / 1000.0)));
        SetInputGainDb(request.InputGainDb);
        _captureFrames = 0;
        _lastCaptureTick = 0;

        if (_startChannels <= 0)
        {
            throw new InvalidOperationException("输入设备没有可用的录音通道。");
        }

        if (request.InputChannelPair * 2 >= _startChannels && _startChannels > 1)
        {
            throw new InvalidOperationException($"输入设备只有 {_startChannels} 个通道，无法使用所选声道。");
        }

        _buffer = new SharedAudioBuffer(_startRate, Math.Max(_startRate * 2, _targetFrames * 8));
        _converted = [];
        _inputMeter.Drain();

        var sinks = new List<OutputSink>();
        try
        {
            foreach (var output in request.Outputs)
            {
                try
                {
                    var sink = OutputSink.Open(
                        _devices,
                        output,
                        _buffer,
                        _startRate,
                        _targetFrames,
                        latencyMs: 20,
                        preferExclusive: request.PreferExclusive);
                    sink.UnexpectedStop += OnSinkStopped;
                    sinks.Add(sink);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        AudioError.Describe(ex, $"打开输出设备“{output.DeviceName}”"),
                        ex);
                }
            }

            lock (_sinkGate)
            {
                _sinks = sinks;
                _activeOutputs = sinks.Select(sink => sink.ToInfo()).ToList();
            }

            _capture.DataAvailable += OnCaptureData;
            _capture.RecordingStopped += OnCaptureStopped;
            _capture.StartRecording();

            var prefillTarget = _targetFrames + (int)(_startRate * 0.015);
            var wait = Stopwatch.StartNew();
            while (_buffer.WritePosition < prefillTarget)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_captureFault is not null)
                {
                    throw new InvalidOperationException(
                        AudioError.Describe(_captureFault, "启动输入设备"),
                        _captureFault);
                }

                if (wait.ElapsedMilliseconds > 2500)
                {
                    throw new TimeoutException("输入设备没有送回音频数据，请检查 USB 连接和效果器设置。");
                }

                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }

            foreach (var sink in sinks)
            {
                sink.Play();
            }

            // Give the WASAPI threads a moment to fail fast (device busy, exclusive conflict, ...).
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            if (_sinkFault is not null)
            {
                throw new InvalidOperationException(AudioError.Describe(_sinkFault, "启动输出设备"), _sinkFault);
            }

            var stopped = sinks.FirstOrDefault(sink => !sink.IsPlaying);
            if (stopped is not null)
            {
                throw new InvalidOperationException($"输出设备“{stopped.DeviceName}”未能开始播放，可能被其他程序占用。");
            }

            // A fresh WASAPI output may report one startup xrun while its callback loop spins up.
            // Only count dropouts that happen after the route is audibly stable.
            foreach (var sink in sinks)
            {
                sink.ResetDropouts();
            }
        }
        catch
        {
            foreach (var sink in sinks)
            {
                sink.UnexpectedStop -= OnSinkStopped;
            }

            throw;
        }
    }

    private void OnCaptureData(object? sender, WaveInEventArgs e)
    {
        try
        {
            var format = _captureFormat;
            var buffer = _buffer;
            if (format is null || buffer is null || e.BytesRecorded <= 0)
            {
                return;
            }

            var frames = PcmBlockReader.ReadStereo(e.Buffer, e.BytesRecorded, format, _channelPair, ref _converted);
            if (frames <= 0)
            {
                return;
            }

            var gain = BitConverter.Int32BitsToSingle(Volatile.Read(ref _inputGainBits));
            buffer.Write(
                new ReadOnlySpan<float>(_converted, 0, frames * 2),
                frames,
                gain,
                _inputMeter);
            Interlocked.Add(ref _captureFrames, frames);
            Volatile.Write(ref _lastCaptureTick, Environment.TickCount64);
        }
        catch (Exception ex)
        {
            Interlocked.CompareExchange(ref _captureFault, ex, null);
            AppLog.Error("处理输入音频数据失败。", ex);
            ScheduleFault("处理输入音频时出错。", ex);
        }
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (_intentionalStop)
        {
            return;
        }

        if (e.Exception is not null)
        {
            Interlocked.CompareExchange(ref _captureFault, e.Exception, null);
        }

        if (State == RouterState.Running)
        {
            var reason = e.Exception is null
                ? "输入设备已停止工作。"
                : AudioError.Describe(e.Exception, "输入设备");
            ScheduleFault(reason, e.Exception);
        }
    }

    private void OnSinkStopped(object? sender, Exception exception)
    {
        if (_intentionalStop)
        {
            return;
        }

        var name = sender is OutputSink sink ? sink.DeviceName : "输出设备";
        Interlocked.CompareExchange(ref _sinkFault, exception, null);
        if (State == RouterState.Running)
        {
            ScheduleFault(AudioError.Describe(exception, $"输出设备“{name}”"), exception);
        }
    }

    private void ScheduleFault(string message, Exception? exception)
    {
        if (Interlocked.CompareExchange(ref _failing, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (State is not (RouterState.Running or RouterState.Starting))
                    {
                        return;
                    }

                    SetState(RouterState.Faulted);
                    AppLog.Error($"路由异常中断：{message}", exception);
                    CleanupResources();
                    Faulted?.Invoke(this, new RouterFaultEventArgs(message, exception));
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("处理音频路由异常时再次失败。", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _failing, 0);
            }
        });
    }

    private void CleanupResources()
    {
        _intentionalStop = true;
        var capture = Interlocked.Exchange(ref _capture, null);
        if (capture is not null)
        {
            capture.DataAvailable -= OnCaptureData;
            capture.RecordingStopped -= OnCaptureStopped;
            try
            {
                capture.StopRecording();
            }
            catch
            {
                // The endpoint may already be gone.
            }

            try
            {
                capture.Dispose();
            }
            catch
            {
                // The endpoint may already be gone.
            }
        }

        var captureDevice = Interlocked.Exchange(ref _captureDevice, null);
        try
        {
            captureDevice?.Dispose();
        }
        catch
        {
            // Ignore teardown failures.
        }

        OutputSink[] sinks;
        lock (_sinkGate)
        {
            sinks = _sinks.ToArray();
            _sinks = [];
            _activeOutputs = [];
        }

        foreach (var sink in sinks)
        {
            sink.UnexpectedStop -= OnSinkStopped;
            try
            {
                sink.StopForShutdown();
            }
            catch
            {
                // Continue with the remaining outputs.
            }
        }

        foreach (var sink in sinks)
        {
            try
            {
                sink.Dispose();
            }
            catch
            {
                // Continue with the remaining outputs.
            }
        }

        _buffer = null;
        _captureFormat = null;
        _converted = [];
        _captureFrames = 0;
        _lastCaptureTick = 0;
        _intentionalStop = false;
    }

    private OutputSink[] SnapshotSinks()
    {
        lock (_sinkGate)
        {
            return _sinks.ToArray();
        }
    }

    private void SetState(RouterState state)
    {
        Volatile.Write(ref _stateValue, (int)state);
        StateChanged?.Invoke(this, state);
    }
}
