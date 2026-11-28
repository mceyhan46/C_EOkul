using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using EOkulStudio.Core.Files;
using EOkulStudio.Core.Processing;
using EOkulStudio.Core.Results;
using EOkulStudio.Core.Settings;

namespace EOkulStudio.Infrastructure.Tests;

/// <summary>Sonucu kaynak dosyanın metnine göre belirlenen yapay boru hattı.</summary>
internal sealed class FakePipeline : IPhotoPipeline
{
    private int _running;

    public ConcurrentQueue<string> Processed { get; } = new();
    public int MaxConcurrency;
    public TimeSpan Delay { get; init; }
    public Action<string>? Before { get; set; }

    public string ModelId => "sahte";

    public PipelineResult Process(
        byte[] source, string sourceName, PipelineOptions options,
        IProgress<PipelineStage>? progress = null, CancellationToken cancellationToken = default)
    {
        Before?.Invoke(sourceName);
        var running = Interlocked.Increment(ref _running);
        InterlockedMax(ref MaxConcurrency, running);
        try
        {
            if (Delay > TimeSpan.Zero) Thread.Sleep(Delay);
            Processed.Enqueue(Path.GetFileName(sourceName));
            var text = Encoding.UTF8.GetString(source);
            if (text.StartsWith("patla", StringComparison.Ordinal)) throw new InvalidOperationException("patladı");
            if (text.StartsWith("yüzsüz", StringComparison.Ordinal)) return PipelineResult.Failed(IssueCode.NoFace);
            if (text.StartsWith("boş", StringComparison.Ordinal)) return new PipelineResult(null, [], null);
            var size = text.StartsWith("yanlış boyut", StringComparison.Ordinal) ? 10 : 133;
            IReadOnlyList<IssueCode> issues = text.StartsWith("bulanık", StringComparison.Ordinal)
                ? [IssueCode.Blurry]
                : [];
            return new PipelineResult(Jpeg(size, 171, source), issues, null);
        }
        finally
        {
            Interlocked.Decrement(ref _running);
        }
    }

    public static byte[] Jpeg(int width, int height, byte[] content) =>
    [
        0xFF, 0xD8, 0xFF, 0xC0, 0, 11, 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width,
        1, 1, 0x11, 0, .. SHA256.HashData(content), 0xFF, 0xD9,
    ];

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
            if (Interlocked.CompareExchange(ref target, value, current) == current) return;
    }
}

internal sealed class ListProgress : IProgress<BatchProgress>
{
    public List<BatchProgress> Reports { get; } = [];
    public Action<BatchProgress>? OnReport { get; init; }

    public void Report(BatchProgress value)
    {
        Reports.Add(value);
        OnReport?.Invoke(value);
    }
}

public sealed class BatchRunnerTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly FakePipeline _pipeline = new();
    private readonly ConcurrentQueue<(string Path, Exception Error)> _errors = new();

    public void Dispose() => _temp.Dispose();

    private static BatchOptions Options(
        ExistingOutputPolicy policy = ExistingOutputPolicy.Skip, bool resuming = false, int parallelism = 1) =>
        new(PipelineOptions.From(new AppSettings()), policy, resuming, parallelism);

    private Task<IReadOnlyList<BatchItem>> Run(
        IEnumerable<string> photos, BatchOptions? options = null, IProgress<BatchProgress>? progress = null) =>
        new BatchRunner(_pipeline, onError: (path, error) => _errors.Enqueue((path, error)))
            .RunAsync(photos, options ?? Options(), progress, TestContext.Current.CancellationToken);

    private ProcessingRecordStore Store(params string[] folder) =>
        new(_temp.Combine([.. folder, OutputLocation.FolderName]));

    private List<string> Sources() =>
        _temp.Snapshot().Where(e => !e.Contains(OutputLocation.FolderName)).ToList();

    private string[] Outputs(params string[] folder) =>
        Directory.GetFiles(_temp.Combine([.. folder, OutputLocation.FolderName]), "*.jpg")
            .Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal).ToArray()!;

    [Fact]
    public async Task Writes_outputs_into_each_folder_without_touching_the_sources()
    {
        var photos = new[]
        {
            _temp.File("9A/Ayşe Yılmaz.jpg", "ayşe"), _temp.File("9B/ışık.png", "bulanık"),
        };
        var before = Sources();
        var progress = new ListProgress();

        var results = await Run(photos, progress: progress);

        results.Select(r => r.SourcePath).Should().Equal(photos);
        results[0].Status.Should().Be(PhotoStatus.Success);
        results[0].Action.Should().Be(OutputAction.Write);
        results[0].OutputPath.Should().Be(_temp.Combine("9A", "E_Okul", "Ayşe Yılmaz.jpg"));
        results[1].Status.Should().Be(PhotoStatus.NeedsReview);
        Outputs("9B").Should().Equal("ışık.jpg");
        Sources().Should().Equal(before);
        progress.Reports.Select(r => r.Done).Should().Equal(1, 2);
        progress.Reports[0].Total.Should().Be(2);

        var entry = Store("9B").Load().Record.Find("ışık.png")!;
        entry.Status.Should().Be(PhotoStatus.NeedsReview);
        entry.Issues.Should().Equal(IssueCode.Blurry);
        entry.OutputName.Should().Be("ışık.jpg");
        entry.ModelId.Should().Be("sahte");
        entry.SettingsVersion.Should().Be(Options().Pipeline.Version);
        entry.SourceSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(photos[1]))));
        _errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Existing_outputs_follow_the_chosen_rule()
    {
        var photo = _temp.File("a.jpg", "bir");
        await Run([photo]);

        var skipped = await Run([photo]);
        skipped[0].Skipped.Should().BeTrue();
        skipped[0].Action.Should().Be(OutputAction.SkipExisting);
        _pipeline.Processed.Should().HaveCount(1);

        var overwritten = await Run([photo], Options(ExistingOutputPolicy.Overwrite));
        overwritten[0].Action.Should().Be(OutputAction.Write);
        Outputs().Should().Equal("a.jpg");

        var second = await Run([photo], Options(ExistingOutputPolicy.AddSuffix));
        second[0].OutputPath.Should().EndWith("a_2.jpg");
        var third = await Run([photo], Options(ExistingOutputPolicy.AddSuffix));
        third[0].OutputPath.Should().EndWith("a_3.jpg");
        Outputs().Should().Equal("a.jpg", "a_2.jpg", "a_3.jpg");
        Store().Load().Record.Find("a.jpg")!.OutputName.Should().Be("a_3.jpg");
    }

    [Fact]
    public async Task Changed_photo_is_processed_again_even_when_skipping()
    {
        var photo = _temp.File("a.jpg", "bir");
        await Run([photo]);
        var first = File.ReadAllBytes(_temp.Combine("E_Okul", "a.jpg"));
        _temp.File("a.jpg", "iki");

        var results = await Run([photo]);

        results[0].Action.Should().Be(OutputAction.Write);
        File.ReadAllBytes(_temp.Combine("E_Okul", "a.jpg")).SequenceEqual(first).Should().BeFalse();
        Outputs().Should().Equal("a.jpg");
    }

    [Fact]
    public async Task Resuming_skips_finished_photos_even_when_overwriting()
    {
        var photos = new[] { _temp.File("a.jpg", "a"), _temp.File("b.jpg", "b") };
        await Run([photos[0]]);

        var results = await Run(photos, Options(ExistingOutputPolicy.Overwrite, resuming: true));

        results[0].Action.Should().Be(OutputAction.SkipAlreadyProcessed);
        results[1].Status.Should().Be(PhotoStatus.Success);
        _pipeline.Processed.Should().Equal("a.jpg", "b.jpg");
    }

    [Fact]
    public async Task A_file_the_program_did_not_write_is_kept()
    {
        var photo = _temp.File("a.jpg", "a");
        var other = _temp.File("E_Okul/a.jpg", "elle konmuş");

        var results = await Run([photo]);

        results[0].Skipped.Should().BeTrue();
        File.ReadAllText(other).Should().Be("elle konmuş");
    }

    [Fact]
    public async Task Names_do_not_depend_on_which_photos_are_selected()
    {
        _temp.File("a.jpg", "1");
        var png = _temp.File("a.png", "2");

        var results = await Run([png]);

        results[0].OutputPath.Should().EndWith("a_2.jpg");
    }

    [Fact]
    public async Task One_failure_does_not_stop_the_others()
    {
        var photos = new[]
        {
            _temp.File("a.jpg", "yüzsüz"), _temp.File("b.jpg", "patla"), _temp.File("c.jpg", "boş"),
            _temp.File("d.jpg", "yanlış boyut"), _temp.Combine("yok.jpg"), _temp.File("E_Okul/eski.jpg", "x"),
            _temp.File("z.jpg", "iyi"),
        };

        var results = await Run(photos);

        results.Select(r => r.Issues.SingleOrDefault().ToString()).Take(6).Should().Equal(
            nameof(IssueCode.NoFace), nameof(IssueCode.ProcessingError), nameof(IssueCode.ProcessingError),
            nameof(IssueCode.ProcessingError), nameof(IssueCode.FileNotFound), nameof(IssueCode.InsideOutputFolder));
        results.Take(6).All(r => r.Status == PhotoStatus.Failed).Should().BeTrue();
        results[6].Status.Should().Be(PhotoStatus.Success);
        Directory.GetFileSystemEntries(_temp.Combine("E_Okul")).Select(p => Path.GetFileName(p))
            .Order(StringComparer.Ordinal).Should().Equal(".eokul_islem.json", "eski.jpg", "z.jpg");
        _errors.Select(e => Path.GetFileName(e.Path)).Order().Should().Equal("b.jpg", "d.jpg");

        // Çıktı klasörü henüz yokken başarısız olanlar da kayda girer.
        var record = Store().Load().Record;
        record.Entries.Select(e => e.SourceName).Should().Equal("a.jpg", "b.jpg", "c.jpg", "d.jpg", "z.jpg");
        record.Find("a.jpg")!.HasOutput.Should().BeFalse();
    }

    [Fact]
    public async Task A_folder_with_only_failures_gets_no_output_folder()
    {
        var photo = _temp.File("a.jpg", "yüzsüz");

        await Run([photo]);

        Directory.Exists(_temp.Combine("E_Okul")).Should().BeFalse();
    }

    [Fact]
    public async Task Unwritable_output_folder_is_reported()
    {
        var photo = _temp.File("a.jpg", "a");
        _temp.File("E_Okul", "klasör yerine dosya");

        var results = await Run([photo]);

        results[0].Issues.Should().Equal(IssueCode.OutputNotWritable);
        File.ReadAllText(_temp.Combine("E_Okul")).Should().Be("klasör yerine dosya");
    }

    [Fact]
    public async Task A_linked_output_folder_receives_nothing()
    {
        var photo = _temp.File("9A/a.jpg", "asıl");
        var link = _temp.Combine("9A", "E_Okul");
        Directory.CreateSymbolicLink(link, _temp.Combine("9A"));
        try
        {
            var results = await Run([photo], Options(ExistingOutputPolicy.Overwrite));

            results[0].Issues.Should().Equal(IssueCode.OutputNotWritable);
            Directory.GetFileSystemEntries(_temp.Combine("9A")).Should().HaveCount(2);
            File.ReadAllText(photo).Should().Be("asıl");
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public async Task Parallel_processing_stays_bounded_and_records_everything()
    {
        var pipeline = new FakePipeline { Delay = TimeSpan.FromMilliseconds(5) };
        var photos = Enumerable.Range(0, 60)
            .Select(i => _temp.File($"{i % 2}/foto{i:D2}.jpg", i % 7 == 0 ? "yüzsüz " + i : "iyi " + i))
            .ToList();
        var progress = new ListProgress();

        var results = await new BatchRunner(pipeline)
            .RunAsync(photos.Concat(photos), Options(parallelism: 4), progress, TestContext.Current.CancellationToken);

        results.Should().HaveCount(60);
        results.Select(r => r.SourcePath).Should().Equal(photos);
        pipeline.Processed.Should().HaveCount(60);
        pipeline.MaxConcurrency.Should().BeInRange(2, 4);
        progress.Reports.Select(r => r.Done).Should().Equal(Enumerable.Range(1, 60));
        (Store("0").Load().Record.Entries.Count + Store("1").Load().Record.Entries.Count).Should().Be(60);
        (Outputs("0").Length + Outputs("1").Length).Should().Be(results.Count(r => r.Status == PhotoStatus.Success));
    }

    [Fact]
    public async Task Cancellation_stops_without_half_written_files()
    {
        var photos = Enumerable.Range(0, 20).Select(i => _temp.File($"f{i:D2}.jpg", "iyi " + i)).ToList();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var progress = new ListProgress { OnReport = report => { if (report.Done == 3) cancel.Cancel(); } };

        var act = () => new BatchRunner(_pipeline).RunAsync(photos, Options(), progress, cancel.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Outputs().Should().HaveCount(3);
        Directory.GetFiles(_temp.Combine("E_Okul"), "*.tmp").Should().BeEmpty();
        Store().Load().Record.Entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task Cancellation_during_processing_writes_nothing()
    {
        var photo = _temp.File("a.jpg", "a");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _pipeline.Before = _ => cancel.Cancel();

        var act = () => new BatchRunner(_pipeline).RunAsync([photo], Options(), null, cancel.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.Exists(_temp.Combine("E_Okul")).Should().BeFalse();
    }

    [Fact]
    public async Task A_failing_progress_listener_ends_the_batch_with_its_error()
    {
        var photos = Enumerable.Range(0, 20).Select(i => _temp.File($"f{i:D2}.jpg", "iyi " + i)).ToList();
        var progress = new ListProgress { OnReport = _ => throw new NotSupportedException("dinleyici") };

        var act = () => Run(photos, Options(parallelism: 3), progress);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Corrupt_record_is_reported_and_processing_continues()
    {
        var photo = _temp.File("a.jpg", "a");
        _temp.File("E_Okul/.eokul_islem.json", "{ bozuk");

        var results = await Run([photo]);

        results[0].Status.Should().Be(PhotoStatus.Success);
        _errors.Should().ContainSingle();
        File.Exists(_temp.Combine("E_Okul", ProcessingRecordStore.CorruptCopyName)).Should().BeTrue();
    }

    [Fact]
    public async Task Empty_list_is_fine()
    {
        var results = await Run([]);

        results.Should().BeEmpty();
    }
}
