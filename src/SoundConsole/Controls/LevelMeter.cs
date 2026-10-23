using System.Windows;
using System.Windows.Media;

namespace SoundConsole.Controls;

/// <summary>A segmented peak meter rendered by WPF without extra dependencies.</summary>
public sealed class LevelMeter : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(double), typeof(LevelMeter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PeakProperty = DependencyProperty.Register(
        nameof(Peak), typeof(double), typeof(LevelMeter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SegmentGapProperty = DependencyProperty.Register(
        nameof(SegmentGap), typeof(double), typeof(LevelMeter),
        new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush TrackBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x20, 0x2D, 0x37)));
    private static readonly Brush LowBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x53, 0xE0, 0xA9)));
    private static readonly Brush MidBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE7, 0xC4, 0x68)));
    private static readonly Brush HighBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x76, 0x6E)));
    private static readonly Brush PeakBrush = Freeze(new SolidColorBrush(Color.FromArgb(235, 0xF4, 0xFA, 0xFC)));

    public LevelMeter()
    {
        SnapsToDevicePixels = true;
        Height = 10;
        MinWidth = 24;
    }

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public double Peak
    {
        get => (double)GetValue(PeakProperty);
        set => SetValue(PeakProperty, value);
    }

    public double SegmentGap
    {
        get => (double)GetValue(SegmentGapProperty);
        set => SetValue(SegmentGapProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var width = ActualWidth;
        var height = Math.Max(4, ActualHeight);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var gap = Math.Max(1.2, SegmentGap);
        var segmentCount = Math.Max(12, (int)((width + gap) / 5.2));
        var segmentWidth = Math.Max(1.2, (width - gap * (segmentCount - 1)) / segmentCount);
        var radius = Math.Min(1.4, height / 2);
        var level = Math.Clamp(Level, 0, 1);
        var peak = Math.Clamp(Peak, 0, 1);
        var litSegments = level <= 0 ? 0 : (int)Math.Ceiling(level * segmentCount);

        for (var i = 0; i < segmentCount; i++)
        {
            var x = i * (segmentWidth + gap);
            var ratio = (i + 1d) / segmentCount;
            var brush = i < litSegments
                ? ratio > 0.94 ? HighBrush : ratio > 0.76 ? MidBrush : LowBrush
                : TrackBrush;
            drawingContext.DrawRoundedRectangle(brush, null, new Rect(x, 0, segmentWidth, height), radius, radius);
        }

        if (peak > 0.01)
        {
            var peakX = Math.Clamp(peak * width - 1, 0, width - 2);
            drawingContext.DrawRectangle(PeakBrush, null, new Rect(peakX, 0, 2, height));
        }
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
