using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Records;
using EOkulStudio.Core.Results;
using EOkulStudio.Core.Settings;
using EOkulStudio.Core.Files;

namespace EOkulStudio.Infrastructure.Tests;

public sealed class ProcessingRecordStoreTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly ProcessingRecordStore _store;

    public ProcessingRecordStoreTests() => _store = new ProcessingRecordStore(_temp.Combine("E_Okul"));

    public void Dispose() => _temp.Dispose();

    private static RecordEntry Entry(string name, PhotoStatus status = PhotoStatus.Success) => new()
    {
        SourceName = name,
        SourceSha256 = "abc",
        OutputName = name,
        Status = status,
        Issues = status == PhotoStatus.Success ? [] : [IssueCode.Blurry],
        Crop = new RotatedCrop(1.25, 2.5, 133, 171, -3.75),
        ManuallyAdjusted = true,
        ModelId = "modnet",
        SettingsVersion = "1",
        UpdatedAt = new DateTimeOffset(2026, 11, 27, 10, 30, 0, TimeSpan.FromHours(3)),
    };

    private void Corrupt(string content)
    {
        Directory.CreateDirectory(_temp.Combine("E_Okul"));
        File.Delete(_store.FilePath);
        File.WriteAllText(_store.FilePath, content);
        if (OperatingSystem.IsWindows()) File.SetAttributes(_store.FilePath, FileAttributes.Hidden);
    }

    [Fact]
    public void Only_an_E_Okul_folder_can_hold_the_record()
    {
        var act = () => new ProcessingRecordStore(_temp.Path);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reading_a_missing_record_creates_nothing()
    {
        var loaded = _store.Load();

        loaded.WasCorrupt.Should().BeFalse();
        loaded.Record.Entries.Should().BeEmpty();
        Directory.Exists(_temp.Combine("E_Okul")).Should().BeFalse();
    }

    [Fact]
    public void Failed_photo_alone_does_not_create_E_Okul()
    {
        _store.Upsert(Entry("a.jpg", PhotoStatus.Failed)).Should().BeFalse();

        Directory.Exists(_temp.Combine("E_Okul")).Should().BeFalse();
    }

    [Fact]
    public void Entry_survives_a_round_trip()
    {
        var entry = Entry("Şule.jpg", PhotoStatus.NeedsReview);

        _store.Upsert(entry).Should().BeTrue();

        _store.Load().Record.Entries.Should().ContainSingle().Which.Should().BeEquivalentTo(entry);
        File.ReadAllText(_store.FilePath).Should().Contain("Şule.jpg").And.Contain("NeedsReview");
        Path.GetFileName(_store.FilePath).Should().Be(OutputLocation.RecordFileName);
    }

    [Fact]
    public void Record_can_be_updated_repeatedly()
    {
        _store.Upsert(Entry("a.jpg"));
        _store.Upsert(Entry("b.jpg"));
        _store.Upsert(Entry("A.JPG", PhotoStatus.Failed)).Should().BeTrue();

        var record = _store.Load().Record;
        record.Entries.Should().HaveCount(2);
        record.Find("a.jpg")!.Status.Should().Be(PhotoStatus.Failed);
        Directory.GetFiles(_temp.Combine("E_Okul")).Should().ContainSingle();
        if (OperatingSystem.IsWindows())
            File.GetAttributes(_store.FilePath).Should().HaveFlag(FileAttributes.Hidden);
    }

    [Fact]
    public void Ownership_of_an_earlier_output_is_saved()
    {
        _store.Upsert(Entry("a.jpg"));
        _store.Upsert(Entry("a.jpg", PhotoStatus.Failed) with { SourceSha256 = "yeni" });

        _store.Load().Record.Find("a.jpg")!.OutputSha256.Should().Be("abc");
    }

    [Theory]
    [InlineData("{ bozuk")]
    [InlineData("null")]
    [InlineData("""{ "Version": 99, "Entries": [] }""")]
    [InlineData("""{ "Version": 1, "Entries": [ { } ] }""")]
    [InlineData("""{ "Version": 1, "Entries": null }""")]
    public void Unreadable_record_is_kept_aside(string content)
    {
        Corrupt(content);

        var loaded = _store.Load();

        loaded.WasCorrupt.Should().BeTrue();
        loaded.Record.Entries.Should().BeEmpty();
        File.ReadAllText(_temp.Combine("E_Okul", ProcessingRecordStore.CorruptCopyName)).Should().Be(content);
    }

    [Fact]
    public void Record_can_break_twice_and_still_recover()
    {
        Corrupt("birinci");
        _store.Load();
        Corrupt("ikinci");
        _store.Load().WasCorrupt.Should().BeTrue();

        _store.Upsert(Entry("a.jpg"));

        _store.Load().WasCorrupt.Should().BeFalse();
        File.ReadAllText(_temp.Combine("E_Okul", ProcessingRecordStore.CorruptCopyName)).Should().Be("ikinci");
    }
}

public sealed class SettingsStoreTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly SettingsStore _store;

    public SettingsStoreTests() => _store = new SettingsStore(_temp.Combine("veri", "settings.json"));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Missing_file_gives_defaults()
    {
        _store.Load().Should().BeEquivalentTo(new AppSettings());
        _store.LastLoadFailed.Should().BeFalse();
    }

    [Fact]
    public void Settings_survive_a_round_trip()
    {
        var settings = new AppSettings
        {
            AutoStart = false,
            ExistingOutput = ExistingOutputPolicy.AddSuffix,
            JpegQuality = 90,
            Crop = new CropRules { TopGap = 0.05 },
            RecentFolders = [@"D:\Öğrenciler"],
            Window = new WindowPlacement(1, 2, 1000, 700, true),
        };

        _store.Save(settings);

        _store.Load().Should().BeEquivalentTo(settings);
        File.ReadAllText(_store.FilePath).Should().Contain("Öğrenciler").And.Contain("AddSuffix");
    }

    [Fact]
    public void Missing_and_out_of_range_values_are_completed()
    {
        _temp.File("veri/settings.json", """{ "JpegQuality": 5, "Crop": { "TopGap": 0.06 } }""");

        var settings = _store.Load();

        settings.JpegQuality.Should().Be(70);
        settings.MaxFileSizeKb.Should().Be(150);
        settings.Crop.TopGap.Should().Be(0.06);
        settings.Crop.BottomGap.Should().Be(0.27);
        _store.LastLoadFailed.Should().BeFalse();
    }

    [Fact]
    public void Broken_file_gives_defaults_and_is_kept_aside()
    {
        _temp.File("veri/settings.json", "{ bozuk");

        _store.Load().Should().BeEquivalentTo(new AppSettings());

        _store.LastLoadFailed.Should().BeTrue();
        File.ReadAllText(_store.FilePath + ".bozuk").Should().Be("{ bozuk");
    }
}

public sealed class SourceFilesTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Hash_is_sha256_of_the_content()
    {
        var path = _temp.Combine("a.jpg");
        await File.WriteAllBytesAsync(path, "abc"u8.ToArray(), TestContext.Current.CancellationToken);

        var hash = await SourceFiles.Sha256Async(path, TestContext.Current.CancellationToken);

        hash.Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Sources_are_opened_read_only()
    {
        using var stream = SourceFiles.OpenRead(_temp.File("a.jpg"));

        stream.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void Program_data_is_kept_under_the_local_profile()
    {
        var paths = AppPaths.Default();

        Path.GetFileName(paths.DataDirectory).Should().Be("EOkulFotoStudio");
        paths.SettingsFile.Should().StartWith(paths.DataDirectory);
        paths.LogDirectory.Should().StartWith(paths.DataDirectory);
    }
}
