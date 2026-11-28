using System.Diagnostics;
using System.Globalization;
using EOkulStudio.Core.Results;
using EOkulStudio.Infrastructure;

namespace EOkulStudio.Cli;

/// <summary>Sonuçları geldikleri anda yazar (çağrılar BatchRunner tarafından sıralanır).</summary>
internal sealed class ConsoleProgress(CultureInfo culture) : IProgress<BatchProgress>
{
    private const int MemoryEvery = 100;

    public void Report(BatchProgress value)
    {
        var item = value.Item;
        var status = item.Status switch
        {
            PhotoStatus.Success => "Başarılı",
            PhotoStatus.NeedsReview => "Kontrol gerekli",
            PhotoStatus.Failed => "Başarısız",
            _ => "Atlandı",
        };
        Console.WriteLine(string.Create(culture,
            $"[{value.Done}/{value.Total}] {status,-15} {item.SourcePath} ({item.Elapsed.TotalSeconds:0.00} sn)"));
        foreach (var issue in item.Issues)
            Console.WriteLine($"      - {issue}: {IssueCatalog.Get(issue).Message}");

        if (value.Done % MemoryEvery == 0)
        {
            using var process = Process.GetCurrentProcess();
            Console.WriteLine($"Bellek ({value.Done}): {process.WorkingSet64 / (1024 * 1024)} MB");
        }
    }
}
