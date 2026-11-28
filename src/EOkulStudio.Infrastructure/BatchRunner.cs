using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Channels;
using EOkulStudio.Core.Files;
using EOkulStudio.Core.Imaging;
using EOkulStudio.Core.Processing;
using EOkulStudio.Core.Records;
using EOkulStudio.Core.Results;

namespace EOkulStudio.Infrastructure;

/// <param name="Resuming">Yarım kalan bir iş sürdürülüyorsa, tamamlanmış fotoğraflar atlanır.</param>
/// <param name="Parallelism">Aynı anda işlenen fotoğraf sayısı.</param>
public sealed record BatchOptions(
    PipelineOptions Pipeline,
    ExistingOutputPolicy ExistingOutput,
    bool Resuming = false,
    int Parallelism = 1);

/// <param name="Status">Sonuç; fotoğraf atlandıysa null.</param>
/// <param name="Action">Var olan çıktıya göre verilen karar; karar öncesinde hata olduysa null.</param>
/// <param name="OutputPath">Yazılan (veya atlandığında zaten var olan) çıktı; yoksa null.</param>
public sealed record BatchItem(
    string SourcePath,
    PhotoStatus? Status,
    IReadOnlyList<IssueCode> Issues,
    OutputAction? Action,
    string? OutputPath,
    TimeSpan Elapsed)
{
    public bool Skipped => Status is null;
}

public sealed record BatchProgress(int Done, int Total, BatchItem Item);

/// <summary>
/// Fotoğrafları sınırlı bir kuyruktan alıp işler, çıktıları ve işlem kaydını yazar. Arayüzden bağımsızdır.
/// Bir fotoğraftaki hata diğerlerini durdurmaz.
/// </summary>
/// <param name="onError">Beklenmeyen hataların günlüğe yazılması için.</param>
public sealed class BatchRunner(
    IPhotoPipeline pipeline,
    TimeProvider? timeProvider = null,
    Action<string, Exception>? onError = null)
{
    private static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    /// <returns>Verilen sırada sonuçlar (tekrar eden yollar bir kez).</returns>
    public async Task<IReadOnlyList<BatchItem>> RunAsync(
        IEnumerable<string> photos,
        BatchOptions options,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(options);

        var sources = photos.Select(Path.GetFullPath).Distinct(Paths).ToList();
        var results = new BatchItem[sources.Count];
        var byFolder = sources.ToLookup(p => Path.GetDirectoryName(p) ?? string.Empty, Paths);
        var folders = new ConcurrentDictionary<string, Lazy<Folder>>(Paths);
        var workers = Math.Clamp(options.Parallelism, 1, 16);
        var queue = Channel.CreateBounded<int>(new BoundedChannelOptions(workers * 2) { SingleWriter = true });
        var reporting = new Lock();
        var done = 0;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linked.Token;

        var producer = Task.Run(async () =>
        {
            for (var i = 0; i < sources.Count; i++) await queue.Writer.WriteAsync(i, token).ConfigureAwait(false);
            queue.Writer.Complete();
        }, token);

        var consumers = Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
        {
            await foreach (var index in queue.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                var source = sources[index];
                var directory = Path.GetDirectoryName(source) ?? string.Empty;
                var folder = folders.GetOrAdd(directory,
                    key => new Lazy<Folder>(() => OpenFolder(key, byFolder[key]))).Value;

                var item = ProcessOne(source, folder, options, token);
                results[index] = item;
                lock (reporting) progress?.Report(new BatchProgress(++done, sources.Count, item));
            }
        }, token)).ToList();

        var all = consumers.Append(producer).ToList();
        try
        {
            // Bir görev beklenmedik biçimde düşerse diğerleri kuyrukta takılı kalmasın.
            var pending = all.ToList();
            while (pending.Count > 0)
            {
                var finished = await Task.WhenAny(pending).ConfigureAwait(false);
                pending.Remove(finished);
                await finished.ConfigureAwait(false);
            }
        }
        catch
        {
            await linked.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(all).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }

        return results;
    }

    private sealed class Folder
    {
        public required string OutputDirectory { get; init; }
        public required ProcessingRecordStore Store { get; init; }

        /// <summary>İş başındaki kayıt: bir kaynağın kararı yalnızca kendi kaydına bağlıdır.</summary>
        public ProcessingRecord Record { get; init; } = new();

        public IReadOnlyDictionary<string, string> Names { get; init; } = new Dictionary<string, string>();

        /// <summary>E_Okul bir bağlantıysa (başka yeri gösteriyorsa) bu klasöre hiçbir şey yazılmaz.</summary>
        public bool Linked { get; init; }

        public Lock Gate { get; } = new();
        public HashSet<string> Claimed { get; } = new(Paths);

        /// <summary>E_Okul henüz yokken biriken başarısız kayıtlar; ilk çıktıyla birlikte yazılır.</summary>
        public List<RecordEntry> Unsaved { get; } = [];
    }

    private Folder OpenFolder(string directory, IEnumerable<string> batchFiles)
    {
        var outputDirectory = Path.Combine(directory, OutputLocation.FolderName);
        var store = new ProcessingRecordStore(outputDirectory);
        var linked = false;
        var record = new ProcessingRecord();
        try
        {
            linked = FolderLinks.IsLink(new DirectoryInfo(outputDirectory));
            if (!linked)
            {
                var loaded = store.Load();
                record = loaded.Record;
                if (loaded.WasCorrupt)
                    onError?.Invoke(store.FilePath, new InvalidDataException("İşlem kaydı bozuktu; kopyası saklandı."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            onError?.Invoke(store.FilePath, ex);
        }

        // Adlar klasördeki tüm fotoğraflara göre verilir: yalnız bir kısmı seçilse de aynı ad çıkar.
        var names = batchFiles.Select(p => Path.GetFileName(p)).ToHashSet(Paths);
        try
        {
            names.UnionWith(Directory.EnumerateFiles(directory)
                .Select(p => Path.GetFileName(p))
                .Where(n => SupportedFiles.Classify(n) == FileKind.Photo));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            onError?.Invoke(directory, ex);
        }

        return new Folder
        {
            OutputDirectory = outputDirectory,
            Store = store,
            Record = record,
            Linked = linked,
            Names = OutputNaming.Assign(names, record.OutputNames()),
        };
    }

    private BatchItem ProcessOne(string source, Folder folder, BatchOptions options, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        BatchItem Finish(PhotoStatus? status, IReadOnlyList<IssueCode> issues, OutputAction? action, string? output) =>
            new(source, status, issues, action, output, Stopwatch.GetElapsedTime(started));
        BatchItem Fail(IssueCode reason, OutputAction? action = null) =>
            Finish(PhotoStatus.Failed, [reason], action, null);

        if (OutputLocation.IsFileInOutputTree(source)) return Fail(IssueCode.InsideOutputFolder);

        byte[] bytes;
        try
        {
            using var stream = SourceFiles.OpenRead(source);
            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Fail(IssueCode.FileNotFound);
        }
        catch (Exception ex)
        {
            onError?.Invoke(source, ex);
            return Fail(IssueCode.CorruptFile);
        }

        if (folder.Linked) return Fail(IssueCode.OutputNotWritable);

        var sourceName = Path.GetFileName(source);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var outputName = folder.Names[sourceName];
        var outputPath = Path.Combine(folder.OutputDirectory, outputName);
        var exists = File.Exists(outputPath);
        var action = OutputDecision.Decide(
            exists, folder.Record.Find(sourceName), sha256, options.ExistingOutput, options.Resuming);
        if (action is OutputAction.SkipAlreadyProcessed or OutputAction.SkipExisting)
            return Finish(null, [], action, outputPath);

        PipelineResult result;
        try
        {
            result = pipeline.Process(bytes, source, options.Pipeline, cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            onError?.Invoke(source, ex);
            result = PipelineResult.Failed(IssueCode.ProcessingError);
        }

        var issues = result.Issues.Distinct().ToList();
        var status = StatusRules.Evaluate(issues);
        if (result.Jpeg is null && status != PhotoStatus.Failed)
        {
            issues.Add(IssueCode.ProcessingError);
            status = PhotoStatus.Failed;
        }

        // İptal istenmişse yeni dosya yazılmaz.
        cancellationToken.ThrowIfCancellationRequested();

        string? written = null;
        if (status != PhotoStatus.Failed)
        {
            if (action == OutputAction.WriteWithSuffix)
            {
                lock (folder.Gate)
                {
                    var natural = Path.GetFileNameWithoutExtension(sourceName) + OutputNaming.Extension;
                    var assigned = folder.Names.Values.ToHashSet(Paths);
                    outputName = OutputNaming.NextFree(natural, name =>
                        assigned.Contains(name) || folder.Claimed.Contains(name)
                        || Path.Exists(Path.Combine(folder.OutputDirectory, name)));
                    folder.Claimed.Add(outputName);
                }
            }

            var size = (options.Pipeline.Crop.TargetWidth, options.Pipeline.Crop.TargetHeight);
            try
            {
                written = OutputWriter.WritePhoto(
                    source, outputName, result.Jpeg, overwrite: action == OutputAction.Write && exists,
                    verify: path => JpegFile.ReadSize(File.ReadAllBytes(path)) == size);
            }
            catch (Exception ex)
            {
                onError?.Invoke(source, ex);
                issues = [ex is OutputNotWritableException ? IssueCode.OutputNotWritable : IssueCode.ProcessingError];
                status = PhotoStatus.Failed;
                outputName = folder.Names[sourceName];
            }
        }

        Save(folder, new RecordEntry
        {
            SourceName = sourceName,
            SourceSha256 = sha256,
            OutputName = outputName,
            Status = status,
            Issues = issues,
            Crop = result.Crop,
            ModelId = pipeline.ModelId,
            SettingsVersion = options.Pipeline.Version,
            UpdatedAt = _time.GetUtcNow(),
        });

        return Finish(status, issues, action, written);
    }

    private void Save(Folder folder, RecordEntry entry)
    {
        lock (folder.Gate)
        {
            try
            {
                if (!folder.Store.Upsert(entry))
                {
                    folder.Unsaved.Add(entry);
                    return;
                }
                foreach (var earlier in folder.Unsaved) folder.Store.Upsert(earlier);
                folder.Unsaved.Clear();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Çıktı yazıldıysa geçerlidir; kayıt yazılamaması yalnızca günlüğe düşer.
                onError?.Invoke(folder.Store.FilePath, ex);
            }
        }
    }
}
