using System.Security.Cryptography;

namespace EOkulStudio.Infrastructure.Tests;

/// <summary>Türkçe karakterli ve boşluklu geçici klasör.</summary>
internal sealed class TempFolder : IDisposable
{
    private readonly string _base = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "eokul-test-" + Guid.NewGuid().ToString("N"));

    public TempFolder()
    {
        Path = System.IO.Path.Combine(_base, "Okul İşleri çğşöü");
        Directory.CreateDirectory(Path);
        // CI'da TEMP kısa (8.3) adla gelebilir; program tam yolu uzun adla döndürür.
        Path = System.IO.Path.GetFullPath(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string File(string relative, string content = "içerik")
    {
        var path = Combine(relative.Split('/'));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Tüm dosya ve klasörlerin durumu: kaynakların değişmediğini göstermek için.</summary>
    public IReadOnlyList<string> Snapshot() =>
        Directory.EnumerateFileSystemEntries(Path, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = 0,
        })
        .Order(StringComparer.Ordinal)
        .Select(entry => Directory.Exists(entry)
            ? entry
            : $"{entry}|{Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(entry)))}"
              + $"|{System.IO.File.GetLastWriteTimeUtc(entry):O}")
        .ToList();

    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
    }
}
