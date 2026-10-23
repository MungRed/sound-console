using System.Windows.Input;
using SoundConsole.Core.Models;
using SoundConsole.Infrastructure;
using SoundConsole.ViewModels;

namespace SoundConsole.ViewModels;

public sealed record BufferOption(int Milliseconds, string Label, string Description)
{
    public override string ToString() => Label;
}

public sealed class OutputDeviceViewModel : ObservableObject
{
    private bool _selected;
    private double _volumeDb = -6;
    private bool _muted;
    private bool _isActive;

    public OutputDeviceViewModel(AudioEndpointInfo device)
    {
        Device = device;
        ToggleMuteCommand = new RelayCommand(() => Muted = !Muted);
    }

    public AudioEndpointInfo Device { get; }

    public string Id => Device.Id;
    public string Name => Device.DisplayName;
    public string RawName => Device.RawName;
    public string Kind => Device.Kind;
    public string Tag => Device.Tag;
    public string Detail => Device.FormatSummary;
    public string DetailLine => $"{Kind} · {Detail}";
    public bool IsDefault => Device.IsDefault;
    public bool IsAmpero => Device.IsAmpero;
    public bool IsVirtual => Device.IsVirtual;

    public MeterViewModel Meter { get; } = new();

    public ICommand ToggleMuteCommand { get; }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(StatusText));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public double VolumeDb
    {
        get => _volumeDb;
        set
        {
            var clamped = Math.Clamp(Math.Round(value, 1), -60, 0);
            if (SetProperty(ref _volumeDb, clamped))
            {
                OnPropertyChanged(nameof(VolumeText));
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            if (SetProperty(ref _muted, value))
            {
                OnPropertyChanged(nameof(VolumeText));
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string VolumeText => Muted ? "静音" : $"{VolumeDb:0.0} dB";

    public string StatusText => IsActive ? "输出中" : Selected ? "待命" : string.Empty;

    public double LinearVolume => Muted ? 0 : Math.Pow(10, VolumeDb / 20.0);

    public event EventHandler? SelectionChanged;
    public event EventHandler? SettingsChanged;

    public void ApplyMeter(MeterReading reading, bool active) => Meter.Update(reading, active);
}
