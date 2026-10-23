using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using SoundConsole.Core.Audio;
using SoundConsole.Core.Models;
using SoundConsole.Core.Settings;
using SoundConsole.Infrastructure;

namespace SoundConsole.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly Dispatcher _dispatcher;
    private readonly AudioDeviceService _deviceService = new();
    private readonly SettingsService _settingsService = new();
    private readonly AudioRouterEngine _engine = new();
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _meterTimer;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _errorTimer;
    private readonly DispatcherTimer _deviceCheckTimer;
    private readonly DispatcherTimer _noteTimer;
    private readonly Stopwatch _session = new();
    private readonly HashSet<string> _activeOutputIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncRelayCommand _toggleRouteCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private bool _initialized;
    private bool _isRefreshing = true;
    private bool _shuttingDown;
    private int _deviceCheckBusy;
    private int _dropouts;
    private string _lastDeviceSignature = string.Empty;
    private string _statusNote = string.Empty;
    private string _errorMessage = string.Empty;
    private RouterState _state = RouterState.Stopped;
    private AudioEndpointInfo? _selectedInput;
    private InputChannelOption? _selectedChannel;
    private double _inputGainDb;
    private BufferOption _selectedBuffer;
    private bool _preferExclusive;
    private string _sessionText = "00:00:00";

    public MainViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _settings = _settingsService.Load();
        _inputGainDb = Math.Clamp(Math.Round(_settings.InputGainDb * 2) / 2, -24, 12);
        _preferExclusive = _settings.PreferExclusive;

        BufferOptions =
        [
            new BufferOption(20, "20 ms", "软件侧最低延迟，建议独占 + 有线输出"),
            new BufferOption(30, "30 ms", "低延迟，建议独占 + 有线输出"),
            new BufferOption(40, "40 ms", "推荐，延迟与稳定性平衡"),
            new BufferOption(60, "60 ms", "更稳定，适合无线或虚拟设备"),
            new BufferOption(100, "100 ms", "高容错，优先保证不断音"),
            new BufferOption(160, "160 ms", "极高容错，延迟较明显")
        ];
        _selectedBuffer = BufferOptions.FirstOrDefault(option => option.Milliseconds == _settings.BufferMilliseconds)
            ?? BufferOptions.First(option => option.Milliseconds == 40);

        InputDevices = [];
        InputChannels = [];
        OutputDevices = [];

        _toggleRouteCommand = new AsyncRelayCommand(ToggleRouteAsync, () => !IsBusy);
        _refreshCommand = new AsyncRelayCommand(() => RefreshDevicesAsync(initial: false), () => CanEditRouting && !IsRefreshing);
        ToggleRouteCommand = _toggleRouteCommand;
        RefreshCommand = _refreshCommand;
        DismissErrorCommand = new RelayCommand(DismissError);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        OpenLogsCommand = new RelayCommand(OpenLogs);

        _meterTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _meterTimer.Tick += (_, _) => UpdateMeters();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateSessionText();

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveNow();
        };

        _errorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(14)
        };
        _errorTimer.Tick += (_, _) =>
        {
            _errorTimer.Stop();
            DismissError();
        };

        _deviceCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _deviceCheckTimer.Tick += async (_, _) => await CheckDeviceChangesAsync();

        _noteTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(7)
        };
        _noteTimer.Tick += (_, _) =>
        {
            _noteTimer.Stop();
            if (State != RouterState.Running)
            {
                StatusNote = string.Empty;
            }
        };

        _engine.StateChanged += OnEngineStateChanged;
        _engine.Started += OnEngineStarted;
        _engine.Faulted += OnEngineFaulted;

        VersionText = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";
    }

    public ObservableCollection<AudioEndpointInfo> InputDevices { get; }
    public ObservableCollection<InputChannelOption> InputChannels { get; }
    public ObservableCollection<BufferOption> BufferOptions { get; }
    public ObservableCollection<OutputDeviceViewModel> OutputDevices { get; }

    public MeterViewModel InputMeter { get; } = new();

    public AsyncRelayCommand ToggleRouteCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand DismissErrorCommand { get; }
    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand OpenLogsCommand { get; }

    public string VersionText { get; }

    public AudioEndpointInfo? SelectedInput
    {
        get => _selectedInput;
        set
        {
            if (!SetProperty(ref _selectedInput, value))
            {
                return;
            }

            RebuildChannels(_selectedChannel?.PairIndex ?? 0);
            NotifyInputInfo();
            ScheduleSave();
        }
    }

    public InputChannelOption? SelectedChannel
    {
        get => _selectedChannel;
        set
        {
            if (SetProperty(ref _selectedChannel, value))
            {
                OnPropertyChanged(nameof(InputChannelHint));
                ScheduleSave();
            }
        }
    }

    public double InputGainDb
    {
        get => _inputGainDb;
        set
        {
            var clamped = Math.Clamp(Math.Round(value * 2) / 2, -24, 12);
            if (SetProperty(ref _inputGainDb, clamped))
            {
                OnPropertyChanged(nameof(InputGainText));
                _engine.SetInputGainDb(clamped);
                ScheduleSave();
            }
        }
    }

    public string InputGainText => $"{InputGainDb:+0.0;-0.0;0.0} dB";

    public BufferOption SelectedBuffer
    {
        get => _selectedBuffer;
        set
        {
            if (SetProperty(ref _selectedBuffer, value))
            {
                OnPropertyChanged(nameof(BufferSummary));
                OnPropertyChanged(nameof(SelectedBufferMsText));
                OnPropertyChanged(nameof(LatencyEstimateText));
                OnPropertyChanged(nameof(LatencyHint));
                OnPropertyChanged(nameof(StatusDetail));
                ScheduleSave();
            }
        }
    }

    public string BufferSummary => $"{SelectedBuffer.Milliseconds} ms 安全缓冲";

    public string SelectedBufferMsText => $"{SelectedBuffer.Milliseconds} ms";

    public string LatencyEstimateText =>
        $"软件侧约 {AudioLatencyPolicy.EstimateSoftwareLatencyMilliseconds(SelectedBuffer.Milliseconds)} ms";

    public string LatencyHint
    {
        get
        {
            var softwareMs = AudioLatencyPolicy.EstimateSoftwareLatencyMilliseconds(SelectedBuffer.Milliseconds);
            var outputMs = AudioLatencyPolicy.GetOutputBufferMilliseconds(SelectedBuffer.Milliseconds);
            return $"软件侧预计约 {softwareMs} ms：" +
                   $"应用缓冲 {SelectedBuffer.Milliseconds} ms + 输出设备缓冲约 {outputMs} ms + 采集周期约 {AudioLatencyPolicy.CapturePeriodMilliseconds} ms。\n" +
                   "实际总延迟还需加上 USB 传输、效果器处理和声卡/耳机自身缓冲。\n" +
                   "低延迟组合：20 ms + 独占输出 + 有线 44.1/48 kHz 设备。";
        }
    }

    public string InputFormatText => SelectedInput is null
        ? "—"
        : $"{SelectedInput.SampleRate / 1000.0:0.#} kHz · {SelectedInput.Channels}ch";

    public bool PreferExclusive
    {
        get => _preferExclusive;
        set
        {
            if (SetProperty(ref _preferExclusive, value))
            {
                OnPropertyChanged(nameof(ModeText));
                ScheduleSave();
            }
        }
    }

    public string ModeText => PreferExclusive ? "独占输出" : "共享输出";

    public RouterState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanEditRouting));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusDetail));
            OnPropertyChanged(nameof(StartButtonText));
            OnPropertyChanged(nameof(IsStopped));
            _toggleRouteCommand.RaiseCanExecuteChanged();
            _refreshCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsRunning => State == RouterState.Running;
    public bool IsStopped => State is RouterState.Stopped or RouterState.Faulted;
    public bool IsBusy => State is RouterState.Starting or RouterState.Stopping;
    public bool CanEditRouting => State is RouterState.Stopped or RouterState.Faulted;

    public string StartButtonText => State switch
    {
        RouterState.Starting => "正在连接…",
        RouterState.Running => "停止路由",
        RouterState.Stopping => "正在停止…",
        _ => "开始路由"
    };

    public string StatusText => State switch
    {
        RouterState.Starting => "连接中",
        RouterState.Running => "路由中",
        RouterState.Stopping => "停止中",
        RouterState.Faulted => "已中断",
        _ => "就绪"
    };

    public string StatusDetail
    {
        get
        {
            if (State == RouterState.Running)
            {
                return $"{SelectedInput?.DisplayName ?? "输入"} → {SelectedOutputCount} 个输出 · {BufferSummary}";
            }

            if (State == RouterState.Starting) return "正在打开音频设备并预缓冲…";
            if (State == RouterState.Stopping) return "正在安全关闭音频流…";
            if (State == RouterState.Faulted) return "音频链路已中断，请检查设备后重试";
            if (IsRefreshing) return "正在扫描音频设备…";
            if (SelectedOutputCount == 0) return "选择至少一个输出设备后开始";
            return $"{SelectedOutputCount} 个输出已就绪 · {BufferSummary}";
        }
    }

    public string StatusNote
    {
        get => _statusNote;
        private set
        {
            if (SetProperty(ref _statusNote, value))
            {
                OnPropertyChanged(nameof(HasStatusNote));
            }
        }
    }

    public bool HasStatusNote => !string.IsNullOrWhiteSpace(StatusNote);

    public string InputMeta => SelectedInput is null
        ? "未检测到输入设备"
        : $"{SelectedInput.Kind} · {SelectedInput.FormatSummary}" +
          (SelectedInput.IsDefault ? " · 系统默认" : string.Empty);

    public string InputTag => SelectedInput?.Tag ?? "未连接";

    public bool HasInputDevices => InputDevices.Count > 0;
    public bool HasOutputDevices => OutputDevices.Count > 0;
    public bool ShowChannelSelector => SelectedInput is { Channels: > 2 };
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (SetProperty(ref _isRefreshing, value))
            {
                OnPropertyChanged(nameof(StatusDetail));
                OnPropertyChanged(nameof(IsLoading));
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLoading => _isRefreshing && !_initialized;

    public string InputChannelHint => SelectedChannel is null
        ? "选择输入声道"
        : $"{SelectedChannel.Hint}。Ampero 8×8 USB 音频通常使用声道 1 + 2。";

    public string InputTip => SelectedInput?.IsAmpero == true
        ? "Ampero 是 8×8 USB 音频接口。若 1+2 没有声音，可尝试 3+4；请先在效果器上确认 USB 音频输出路由。"
        : "使用共享采集时，请关闭可能独占麦克风的 ASIO 程序，否则输入可能不可用。";

    public int SelectedOutputCount => OutputDevices.Count(device => device.Selected);

    public string SelectedOutputsSummary => SelectedOutputCount switch
    {
        0 => "未选择输出",
        1 => OutputDevices.First(device => device.Selected).Name,
        _ => $"{OutputDevices.First(device => device.Selected).Name} 等 {SelectedOutputCount} 个设备"
    };

    public string OutputCountText => SelectedOutputCount == 0 ? "未选择" : $"{SelectedOutputCount} 路输出";

    public string OutputHelpText => SelectedOutputCount == 0
        ? "勾选耳机或音箱作为监听输出。可以同时启用多个设备。"
        : "多个输出会同时播放，可分别调节音量；开始后仍可实时调节。";

    public bool HasAmperoOutputSelected => OutputDevices.Any(device => device.Selected && device.IsAmpero);

    public string DropoutText => _dropouts > 0 ? $"缓冲丢帧 {_dropouts} 次" : "缓冲稳定";
    public bool HasDropouts => _dropouts > 0;

    public string SessionText
    {
        get => _sessionText;
        private set => SetProperty(ref _sessionText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await RefreshDevicesAsync(initial: true);
        _initialized = true;
        OnPropertyChanged(nameof(IsLoading));
        _meterTimer.Start();
        _clockTimer.Start();
        _deviceCheckTimer.Start();
        SaveNow();
    }

    public async Task ShutdownAsync()
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        _meterTimer.Stop();
        _clockTimer.Stop();
        _saveTimer.Stop();
        _errorTimer.Stop();
        _deviceCheckTimer.Stop();
        _noteTimer.Stop();
        SaveNow();
        try
        {
            await Task.Run(() => _engine.StopAsync());
        }
        catch (Exception ex)
        {
            AppLog.Warn($"退出时停止音频路由失败：{ex.Message}");
        }

        _engine.Dispose();
    }

    private async Task ToggleRouteAsync()
    {
        if (IsBusy || _shuttingDown)
        {
            return;
        }

        DismissError();
        StatusNote = string.Empty;

        if (State == RouterState.Running)
        {
            try
            {
                await Task.Run(() => _engine.StopAsync());
            }
            catch (Exception ex)
            {
                AppLog.Error("停止音频路由失败。", ex);
                ShowError("停止音频路由失败：" + ex.Message);
            }

            return;
        }

        if (SelectedInput is null)
        {
            ShowError("没有可用的输入设备。请连接 Ampero 后点击“刷新设备”。");
            return;
        }

        var selectedOutputs = OutputDevices.Where(device => device.Selected).ToList();
        if (selectedOutputs.Count == 0)
        {
            ShowError("请至少选择一个输出设备（耳机、音箱或 DAC）。");
            return;
        }

        var request = new RouteRequest
        {
            InputDeviceId = SelectedInput.Id,
            InputDeviceName = SelectedInput.DisplayName,
            InputChannelPair = SelectedChannel?.PairIndex ?? 0,
            InputGainDb = InputGainDb,
            BufferMilliseconds = SelectedBuffer.Milliseconds,
            PreferExclusive = PreferExclusive,
            Outputs = selectedOutputs.Select(device => new RouteOutputRequest
            {
                DeviceId = device.Id,
                DeviceName = device.Name,
                Volume = (float)device.LinearVolume,
                Muted = device.Muted
            }).ToList()
        };

        try
        {
            await Task.Run(() => _engine.StartAsync(request));
        }
        catch (Exception ex)
        {
            AppLog.Error("无法启动音频路由。", ex);
            ShowError(ex.Message);
        }
    }

    private async Task RefreshDevicesAsync(bool initial)
    {
        if (_isRefreshing && !initial && IsRefreshing)
        {
            return;
        }

        if (!CanEditRouting && !initial)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            var (inputs, outputs) = await Task.Run(() =>
                (_deviceService.Enumerate(AudioFlow.Capture), _deviceService.Enumerate(AudioFlow.Render)));
            ApplyDevices(inputs, outputs, initial);
        }
        catch (Exception ex)
        {
            AppLog.Error("刷新音频设备失败。", ex);
            if (!initial)
            {
                ShowError("刷新音频设备失败：" + ex.Message);
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ApplyDevices(IReadOnlyList<AudioEndpointInfo> inputs, IReadOnlyList<AudioEndpointInfo> outputs, bool initial)
    {
        var previousInputId = SelectedInput?.Id;
        var previousPair = SelectedChannel?.PairIndex ?? 0;
        var previousOutputState = OutputDevices.ToDictionary(
            device => device.Id,
            device => (device.Selected, device.VolumeDb, device.Muted),
            StringComparer.OrdinalIgnoreCase);

        InputDevices.Clear();
        foreach (var input in inputs)
        {
            InputDevices.Add(input);
        }

        var inputId = previousInputId ?? _settings.InputDeviceId;
        var selectedInput = InputDevices.FirstOrDefault(device =>
                string.Equals(device.Id, inputId, StringComparison.OrdinalIgnoreCase))
            ?? InputDevices.FirstOrDefault(device =>
                string.Equals(device.DisplayName, _settings.InputDeviceName, StringComparison.CurrentCultureIgnoreCase))
            ?? InputDevices.FirstOrDefault(device => device.IsAmpero)
            ?? InputDevices.FirstOrDefault();

        var pair = previousInputId is not null ? previousPair : _settings.InputChannelPair;
        SetInput(selectedInput, pair);

        var savedPreferences = _settings.Outputs;
        var outputModels = new List<OutputDeviceViewModel>();
        foreach (var output in outputs)
        {
            var pref = savedPreferences.FirstOrDefault(item =>
                    string.Equals(item.DeviceId, output.Id, StringComparison.OrdinalIgnoreCase))
                ?? savedPreferences.FirstOrDefault(item =>
                    string.IsNullOrEmpty(item.DeviceId) &&
                    string.Equals(item.DeviceName, output.DisplayName, StringComparison.CurrentCultureIgnoreCase));

            var model = new OutputDeviceViewModel(output);
            if (previousOutputState.TryGetValue(output.Id, out var live))
            {
                model.Selected = live.Selected;
                model.VolumeDb = live.VolumeDb;
                model.Muted = live.Muted;
            }
            else if (pref is not null)
            {
                model.Selected = pref.Enabled;
                model.VolumeDb = pref.VolumeDb;
                model.Muted = pref.Muted;
            }

            model.SelectionChanged += OnOutputSelectionChanged;
            model.SettingsChanged += OnOutputSettingsChanged;
            outputModels.Add(model);
        }

        OutputDevices.Clear();
        foreach (var model in outputModels)
        {
            OutputDevices.Add(model);
        }

        var hasAnySavedPreference = savedPreferences.Count > 0 &&
            outputModels.Any(model => savedPreferences.Any(pref =>
                string.Equals(pref.DeviceId, model.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pref.DeviceName, model.Name, StringComparison.CurrentCultureIgnoreCase)));

        if (!hasAnySavedPreference && OutputDevices.All(device => !device.Selected))
        {
            var defaultOutput = OutputDevices.FirstOrDefault(device => device.IsDefault && !device.IsAmpero)
                ?? OutputDevices.FirstOrDefault(device => !device.IsAmpero)
                ?? OutputDevices.FirstOrDefault();
            if (defaultOutput is not null)
            {
                defaultOutput.Selected = true;
            }
        }

        _lastDeviceSignature = BuildDeviceSignature(inputs, outputs);
        OnPropertyChanged(nameof(HasInputDevices));
        OnPropertyChanged(nameof(HasOutputDevices));
        OnPropertyChanged(nameof(SelectedOutputCount));
        OnPropertyChanged(nameof(SelectedOutputsSummary));
        OnPropertyChanged(nameof(OutputCountText));
        OnPropertyChanged(nameof(OutputHelpText));
        OnPropertyChanged(nameof(HasAmperoOutputSelected));
        OnPropertyChanged(nameof(StatusDetail));
        OnPropertyChanged(nameof(InputChannelHint));

        if (selectedInput is null)
        {
            _noteTimer.Stop();
            StatusNote = "没有找到输入设备。请连接 Ampero 并刷新。";
        }
        else if (!initial && _initialized)
        {
            StatusNote = "设备列表已更新。";
            _noteTimer.Stop();
            _noteTimer.Start();
        }
    }

    private void SetInput(AudioEndpointInfo? input, int pairIndex)
    {
        _selectedInput = input;
        OnPropertyChanged(nameof(SelectedInput));
        RebuildChannels(pairIndex);
        NotifyInputInfo();
    }

    private void RebuildChannels(int desiredPair)
    {
        InputChannels.Clear();
        if (SelectedInput is null)
        {
            SelectedChannel = null;
            return;
        }

        foreach (var option in InputChannelOption.ForChannelCount(SelectedInput.Channels))
        {
            InputChannels.Add(option);
        }

        var channel = InputChannels.FirstOrDefault(option => option.PairIndex == desiredPair)
            ?? InputChannels.FirstOrDefault();
        _selectedChannel = channel;
        OnPropertyChanged(nameof(SelectedChannel));
    }

    private void NotifyInputInfo()
    {
        OnPropertyChanged(nameof(InputMeta));
        OnPropertyChanged(nameof(InputTag));
        OnPropertyChanged(nameof(InputFormatText));
        OnPropertyChanged(nameof(ShowChannelSelector));
        OnPropertyChanged(nameof(InputChannelHint));
        OnPropertyChanged(nameof(InputTip));
        OnPropertyChanged(nameof(StatusDetail));
        OnPropertyChanged(nameof(SelectedInput));
    }

    private void OnOutputSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SelectedOutputCount));
        OnPropertyChanged(nameof(SelectedOutputsSummary));
        OnPropertyChanged(nameof(OutputCountText));
        OnPropertyChanged(nameof(OutputHelpText));
        OnPropertyChanged(nameof(HasAmperoOutputSelected));
        OnPropertyChanged(nameof(StatusDetail));
        ScheduleSave();
    }

    private void OnOutputSettingsChanged(object? sender, EventArgs e)
    {
        if (sender is not OutputDeviceViewModel device)
        {
            return;
        }

        if (IsRunning)
        {
            if (device.Selected)
            {
                _engine.SetOutputVolume(device.Id, (float)device.LinearVolume);
                _engine.SetOutputMuted(device.Id, device.Muted);
            }
        }

        ScheduleSave();
    }

    private void UpdateMeters()
    {
        if (State == RouterState.Running)
        {
            RouterSnapshot snapshot;
            try
            {
                snapshot = _engine.SnapshotMeters();
            }
            catch
            {
                return;
            }

            InputMeter.Update(snapshot.Input, true);
            _activeOutputIds.Clear();
            foreach (var active in _engine.ActiveOutputs)
            {
                _activeOutputIds.Add(active.DeviceId);
            }

            foreach (var device in OutputDevices)
            {
                var active = _activeOutputIds.Contains(device.Id);
                device.IsActive = active;
                if (active && snapshot.Outputs.TryGetValue(device.Id, out var reading))
                {
                    device.ApplyMeter(reading, true);
                }
                else
                {
                    device.ApplyMeter(MeterReading.Silence, false);
                }
            }

            if (_dropouts != (int)snapshot.Dropouts)
            {
                _dropouts = (int)snapshot.Dropouts;
                OnPropertyChanged(nameof(DropoutText));
                OnPropertyChanged(nameof(HasDropouts));
            }
        }
        else
        {
            InputMeter.Update(MeterReading.Silence, false);
            foreach (var device in OutputDevices)
            {
                device.IsActive = false;
                device.ApplyMeter(MeterReading.Silence, false);
            }

            if (_dropouts != 0)
            {
                _dropouts = 0;
                OnPropertyChanged(nameof(DropoutText));
                OnPropertyChanged(nameof(HasDropouts));
            }
        }
    }

    private void OnEngineStateChanged(object? sender, RouterState state)
    {
        if (_shuttingDown)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            State = state;
            if (state == RouterState.Running)
            {
                _session.Restart();
                UpdateSessionText();
            }
            else if (state is RouterState.Stopped or RouterState.Faulted)
            {
                _session.Stop();
                _activeOutputIds.Clear();
                foreach (var device in OutputDevices)
                {
                    device.IsActive = false;
                }

                if (state == RouterState.Stopped)
                {
                    _noteTimer.Stop();
                    StatusNote = string.Empty;
                }

                UpdateSessionText();
            }
        });
    }

    private void OnEngineStarted(object? sender, RouteStartedEventArgs e)
    {
        if (_shuttingDown)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            var notes = e.Outputs
                .Where(output => !string.IsNullOrWhiteSpace(output.Note))
                .Select(output => $"{output.DeviceName}：{output.Note}")
                .ToList();
            _noteTimer.Stop();
            StatusNote = notes.Count > 0 ? string.Join("  ", notes) : string.Empty;
            OnPropertyChanged(nameof(StatusDetail));
            OnPropertyChanged(nameof(SessionText));
        });
    }

    private void OnEngineFaulted(object? sender, RouterFaultEventArgs e)
    {
        if (_shuttingDown)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            State = RouterState.Faulted;
            ShowError(e.Message);
            _deviceCheckTimer.Stop();
            _deviceCheckTimer.Start();
        });
    }

    private void UpdateSessionText()
    {
        SessionText = State == RouterState.Running
            ? _session.Elapsed.ToString(@"hh\:mm\:ss")
            : "00:00:00";
    }

    private async Task CheckDeviceChangesAsync()
    {
        if (_shuttingDown || !CanEditRouting || IsRefreshing ||
            Interlocked.Exchange(ref _deviceCheckBusy, 1) != 0)
        {
            return;
        }

        try
        {
            var (inputs, outputs) = await Task.Run(() =>
                (_deviceService.Enumerate(AudioFlow.Capture), _deviceService.Enumerate(AudioFlow.Render)));
            var signature = BuildDeviceSignature(inputs, outputs);
            if (!string.Equals(signature, _lastDeviceSignature, StringComparison.Ordinal))
            {
                ApplyDevices(inputs, outputs, initial: false);
            }
        }
        catch
        {
            // Background device polling is best-effort.
        }
        finally
        {
            Interlocked.Exchange(ref _deviceCheckBusy, 0);
        }
    }

    private static string BuildDeviceSignature(
        IEnumerable<AudioEndpointInfo> inputs,
        IEnumerable<AudioEndpointInfo> outputs)
    {
        var parts = inputs.Select(device => $"i:{device.Id}:{device.Channels}:{device.SampleRate}")
            .Concat(outputs.Select(device => $"o:{device.Id}:{device.Channels}:{device.SampleRate}"))
            .OrderBy(part => part, StringComparer.OrdinalIgnoreCase);
        return string.Join("|", parts);
    }

    private void ScheduleSave()
    {
        if (!_initialized || _shuttingDown)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        if (!_initialized)
        {
            return;
        }

        try
        {
            _settings.InputDeviceId = SelectedInput?.Id;
            _settings.InputDeviceName = SelectedInput?.DisplayName;
            _settings.InputChannelPair = SelectedChannel?.PairIndex ?? 0;
            _settings.InputGainDb = InputGainDb;
            _settings.BufferMilliseconds = SelectedBuffer.Milliseconds;
            _settings.PreferExclusive = PreferExclusive;

            foreach (var device in OutputDevices)
            {
                var pref = _settings.GetOutput(device.Id, device.Name);
                pref.DeviceName = device.Name;
                pref.Enabled = device.Selected;
                pref.VolumeDb = device.VolumeDb;
                pref.Muted = device.Muted;
            }

            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"保存用户设置失败：{ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        _errorTimer.Stop();
        _errorTimer.Start();
        AppLog.Warn(message);
    }

    private void DismissError()
    {
        _errorTimer.Stop();
        ErrorMessage = string.Empty;
    }

    private static void OpenDataFolder()
    {
        try
        {
            AppPaths.OpenDataDirectory();
        }
        catch (Exception ex)
        {
            AppLog.Warn($"打开数据目录失败：{ex.Message}");
        }
    }

    private static void OpenLogs()
    {
        try
        {
            AppPaths.EnsureCreated();
            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"打开日志目录失败：{ex.Message}");
        }
    }
}
