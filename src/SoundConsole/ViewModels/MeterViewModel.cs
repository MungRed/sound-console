using SoundConsole.Core.Models;
using SoundConsole.Infrastructure;

namespace SoundConsole.ViewModels;

/// <summary>View-side ballistics for a peak meter.</summary>
public sealed class MeterViewModel : ObservableObject
{
    private static readonly double MinDb = -60;
    private static readonly double MaxDb = 0;
    private double _leftLevel;
    private double _rightLevel;
    private double _leftPeak;
    private double _rightPeak;
    private string _leftText = "−∞";
    private string _rightText = "−∞";
    private bool _clipped;
    private int _leftPeakHold;
    private int _rightPeakHold;
    private int _clipHold;

    public double LeftLevel
    {
        get => _leftLevel;
        private set => SetProperty(ref _leftLevel, Math.Round(value, 4));
    }

    public double RightLevel
    {
        get => _rightLevel;
        private set => SetProperty(ref _rightLevel, Math.Round(value, 4));
    }

    public double LeftPeak
    {
        get => _leftPeak;
        private set => SetProperty(ref _leftPeak, Math.Round(value, 4));
    }

    public double RightPeak
    {
        get => _rightPeak;
        private set => SetProperty(ref _rightPeak, Math.Round(value, 4));
    }

    public string LeftText
    {
        get => _leftText;
        private set => SetProperty(ref _leftText, value);
    }

    public string RightText
    {
        get => _rightText;
        private set => SetProperty(ref _rightText, value);
    }

    public bool Clipped
    {
        get => _clipped;
        private set => SetProperty(ref _clipped, value);
    }

    public void Update(MeterReading reading, bool active)
    {
        if (!active)
        {
            LeftLevel = _leftLevel * 0.72;
            RightLevel = _rightLevel * 0.72;
            LeftPeak = _leftPeak * 0.72;
            RightPeak = _rightPeak * 0.72;
            LeftText = "−∞";
            RightText = "−∞";
            if (_leftPeak < 0.002) LeftPeak = 0;
            if (_rightPeak < 0.002) RightPeak = 0;
            Clipped = false;
            return;
        }

        var targetLeft = Normalize(reading.Left);
        var targetRight = Normalize(reading.Right);

        LeftLevel = targetLeft > _leftLevel ? targetLeft : Math.Max(targetLeft, _leftLevel * 0.82);
        RightLevel = targetRight > _rightLevel ? targetRight : Math.Max(targetRight, _rightLevel * 0.82);

        if (LeftLevel >= _leftPeak)
        {
            LeftPeak = LeftLevel;
            _leftPeakHold = 14;
        }
        else if (_leftPeakHold > 0)
        {
            _leftPeakHold--;
        }
        else
        {
            LeftPeak = Math.Max(LeftLevel, _leftPeak * 0.92);
        }

        if (RightLevel >= _rightPeak)
        {
            RightPeak = RightLevel;
            _rightPeakHold = 14;
        }
        else if (_rightPeakHold > 0)
        {
            _rightPeakHold--;
        }
        else
        {
            RightPeak = Math.Max(RightLevel, _rightPeak * 0.92);
        }

        LeftText = FormatDb(reading.Left);
        RightText = FormatDb(reading.Right);
        if (reading.Clipped)
        {
            _clipHold = 32;
        }
        else if (_clipHold > 0)
        {
            _clipHold--;
        }

        Clipped = _clipHold > 0;
    }

    public void Reset()
    {
        LeftLevel = 0;
        RightLevel = 0;
        LeftPeak = 0;
        RightPeak = 0;
        LeftText = "−∞";
        RightText = "−∞";
        Clipped = false;
        _leftPeakHold = 0;
        _rightPeakHold = 0;
        _clipHold = 0;
    }

    private static double Normalize(float peak)
    {
        if (peak <= 0.000001f)
        {
            return 0;
        }

        var db = 20 * Math.Log10(peak);
        return Math.Clamp((db - MinDb) / (MaxDb - MinDb), 0, 1);
    }

    private static string FormatDb(float peak)
    {
        if (peak <= 0.000001f)
        {
            return "−∞";
        }

        var db = 20 * Math.Log10(peak);
        return db <= MinDb ? "< −60" : $"{db:0.0}";
    }
}
