using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Files;
using EOkulStudio.Core.Settings;

namespace EOkulStudio.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void Defaults_are_already_valid()
    {
        var settings = new AppSettings();

        settings.Normalized().Should().BeEquivalentTo(settings);
        new CropRules().Normalized().Should().Be(new CropRules());
        settings.ExistingOutput.Should().Be(ExistingOutputPolicy.Skip);
    }

    [Fact]
    public void Out_of_range_values_are_corrected()
    {
        var settings = new AppSettings
        {
            JpegQuality = 10,
            MaxFileSizeKb = 1,
            MinFileSizeKb = 999,
            ExistingOutput = (ExistingOutputPolicy)99,
            MattingModel = " ",
            Crop = null!,
            RecentFolders = null!,
        }.Normalized();

        settings.JpegQuality.Should().Be(70);
        settings.MaxFileSizeKb.Should().Be(5);
        settings.MinFileSizeKb.Should().Be(5);
        settings.ExistingOutput.Should().Be(ExistingOutputPolicy.Skip);
        settings.MattingModel.Should().Be(AppSettings.DefaultMattingModel);
        settings.Crop.Should().Be(new CropRules());
        settings.RecentFolders.Should().BeEmpty();
    }

    [Fact]
    public void Crop_rules_are_clamped()
    {
        var rules = new CropRules
        {
            TopGap = 5,
            BottomGap = -1,
            LeftGap = double.NaN,
            MaxTiltDegrees = double.PositiveInfinity,
            TargetWidth = 1,
            EyeLineMin = 0.6,
            EyeLineMax = 0.2,
        }.Normalized();

        rules.TopGap.Should().Be(0.25);
        rules.BottomGap.Should().Be(0.05);
        rules.LeftGap.Should().Be(0.12);
        rules.MaxTiltDegrees.Should().Be(20);
        rules.TargetWidth.Should().Be(40);
        rules.EyeLineMax.Should().Be(0.6);
    }

    [Fact]
    public void Recent_folders_are_unique_newest_first_and_limited()
    {
        var settings = new AppSettings();
        for (var i = 0; i < 12; i++) settings = settings.WithRecentFolder($"klasor{i}");
        settings = settings.WithRecentFolder("KLASOR5");

        settings.RecentFolders.Should().HaveCount(AppSettings.MaxRecentFolders);
        settings.RecentFolders[0].Should().Be("KLASOR5");
        settings.RecentFolders.Count(f => f.Equals("klasor5", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        settings.RecentFolders[1].Should().Be("klasor11");
    }
}
