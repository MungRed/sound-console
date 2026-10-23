using SoundConsole.Core.Settings;

namespace SoundConsole.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Normalize_ClampsUserFacingRanges()
    {
        var settings = new AppSettings
        {
            InputGainDb = 99,
            InputChannelPair = -3,
            BufferMilliseconds = 1,
            Outputs =
            [
                new OutputPreference { VolumeDb = 12 },
                new OutputPreference { VolumeDb = -100 }
            ]
        };

        settings.Normalize();

        Assert.Equal(24, settings.InputGainDb);
        Assert.Equal(0, settings.InputChannelPair);
        Assert.Equal(20, settings.BufferMilliseconds);
        Assert.Equal(6, settings.Outputs[0].VolumeDb);
        Assert.Equal(-60, settings.Outputs[1].VolumeDb);
    }

    [Fact]
    public void GetOutput_ReturnsExistingPreferenceById()
    {
        var existing = new OutputPreference { DeviceId = "device-a", DeviceName = "Old name" };
        var settings = new AppSettings { Outputs = [existing] };

        var result = settings.GetOutput("device-a", "New name");

        Assert.Same(existing, result);
    }

    [Fact]
    public void GetOutput_MatchesByNameAndRepairsMissingId()
    {
        var existing = new OutputPreference { DeviceId = "", DeviceName = "TOPPING USB DAC" };
        var settings = new AppSettings { Outputs = [existing] };

        var result = settings.GetOutput("new-id", "TOPPING USB DAC");

        Assert.Same(existing, result);
        Assert.Equal("new-id", existing.DeviceId);
    }

    [Fact]
    public void GetOutput_CreatesDisabledPreferenceWithSafeDefaultVolume()
    {
        var settings = new AppSettings();

        var result = settings.GetOutput("new-id", "Headphones");

        Assert.False(result.Enabled);
        Assert.False(result.Muted);
        Assert.Equal(-6, result.VolumeDb);
        Assert.Single(settings.Outputs);
    }
}
