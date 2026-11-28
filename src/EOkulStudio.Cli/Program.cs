using System.Diagnostics;
using System.Globalization;
using System.Text;
using EOkulStudio.Cli;
using EOkulStudio.Core.Processing;
using EOkulStudio.Core.Results;
using EOkulStudio.Imaging;
using EOkulStudio.Infrastructure;

const int SomeFailed = 1, BadUsage = 2, StartupError = 3, Cancelled = 130;

Console.OutputEncoding = new UTF8Encoding(false);
var turkish = CultureInfo.GetCultureInfo("tr-TR");

if (args is [] or ["--yardim"] or ["--help"])
{
    Console.WriteLine(CliArguments.Usage);
    return args is [] ? BadUsage : 0;
}

CliArguments arguments;
try
{
    arguments = CliArguments.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine(CliArguments.Usage);
    return BadUsage;
}

using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancel.Cancel();
};

try
{
    var scan = new PhotoScanner().Scan(arguments.Inputs, arguments.Settings.IncludeSubfolders, cancel.Token);
    foreach (var rejected in scan.Rejected)
        Console.WriteLine($"Alınmadı: {rejected.Path} — {IssueCatalog.Get(rejected.Reason).Message}");
    foreach (var folder in scan.UnreadableFolders)
        Console.WriteLine($"Okunamayan klasör: {folder}");
    Console.WriteLine($"İşlenecek fotoğraf: {scan.Photos.Count}");
    if (scan.Photos.Count == 0) return SomeFailed;

    var resources = ProcessingResources.For(Environment.ProcessorCount, arguments.Workers);
    PhotoPipeline pipeline;
    try
    {
        var inspector = arguments.ComparisonDirectory is { } directory
            ? new ComparisonWriter(Path.GetFullPath(directory))
            : null;
        pipeline = PhotoPipeline.Create(AppContext.BaseDirectory, resources.ModelThreads, inspector);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Başlatılamadı: {ex.Message}");
        return StartupError;
    }

    using (pipeline)
    {
        Console.WriteLine(
            $"{NativeRuntime.Describe()} | işlemci: {Environment.ProcessorCount} mantıksal çekirdek | " +
            $"aynı anda {resources.Workers} fotoğraf, model başına {resources.ModelThreads} iş parçacığı");

        var runner = new BatchRunner(pipeline, onError: (path, ex) =>
            Console.Error.WriteLine($"Hata: {path}: {ex.GetType().Name}: {ex.Message}"));
        var options = new BatchOptions(
            PipelineOptions.From(arguments.Settings), arguments.Settings.ExistingOutput,
            arguments.Resuming, resources.Workers);

        var clock = Stopwatch.StartNew();
        var results = await runner.RunAsync(scan.Photos, options, new ConsoleProgress(turkish), cancel.Token);
        clock.Stop();

        var processed = results.Where(r => !r.Skipped).ToList();
        using var process = Process.GetCurrentProcess();
        Console.WriteLine();
        Console.WriteLine(
            $"Başarılı: {results.Count(r => r.Status == PhotoStatus.Success)}  " +
            $"Kontrol gerekli: {results.Count(r => r.Status == PhotoStatus.NeedsReview)}  " +
            $"Başarısız: {results.Count(r => r.Status == PhotoStatus.Failed)}  " +
            $"Atlanan: {results.Count(r => r.Skipped)}");
        if (processed.Count > 0)
        {
            var times = processed.Select(r => r.Elapsed.TotalSeconds).Order().ToList();
            Console.WriteLine(string.Create(turkish,
                $"Süre: toplam {clock.Elapsed.TotalSeconds:0.0} sn; fotoğraf başına geçen süre ortalama {times.Average():0.00} sn, " +
                $"ortanca {times[times.Count / 2]:0.00} sn; toplam/adet {clock.Elapsed.TotalSeconds / processed.Count:0.00} sn"));
        }
        Console.WriteLine($"En yüksek bellek: {process.PeakWorkingSet64 / (1024 * 1024)} MB");
        return results.Any(r => r.Status == PhotoStatus.Failed) ? SomeFailed : 0;
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("İptal edildi. Tamamlanan çıktılar korunur; --devam ile sürdürebilirsiniz.");
    return Cancelled;
}
