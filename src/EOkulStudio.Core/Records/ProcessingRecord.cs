using System.Text.Json.Serialization;
using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Results;

namespace EOkulStudio.Core.Records;

/// <summary>Bir fotoğrafın işlem kaydı. Dosya adları yol içermez: klasör taşınsa da kayıt geçerli kalır.</summary>
public sealed record RecordEntry
{
    public required string SourceName { get; init; }
    public required string SourceSha256 { get; init; }

    /// <summary>E_Okul içindeki dosya adı; çıktı üretilmediyse de ayrılan ad.</summary>
    public required string OutputName { get; init; }

    public required PhotoStatus Status { get; init; }
    public IReadOnlyList<IssueCode> Issues { get; init; } = [];
    public RotatedCrop? Crop { get; init; }
    public bool ManuallyAdjusted { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string SettingsVersion { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Son işlem başarısız olduğunda, E_Okul'da duran eski çıktının üretildiği kaynağın özeti.
    /// </summary>
    public string? PreviousOutputSha256 { get; init; }

    [JsonIgnore]
    public bool HasOutput => Status is PhotoStatus.Success or PhotoStatus.NeedsReview;

    /// <summary>Programın bu adla yazdığı dosyanın kaynak özeti; hiç yazmadıysa null.</summary>
    [JsonIgnore]
    public string? OutputSha256 => HasOutput ? SourceSha256 : PreviousOutputSha256;
}

public sealed record ProcessingRecord
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public IReadOnlyList<RecordEntry> Entries { get; init; } = [];

    public RecordEntry? Find(string sourceName) =>
        Entries.FirstOrDefault(e => string.Equals(e.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Kaydı ekler veya yeniler. Yeni sonuç başarısızsa, daha önce yazılmış çıktının programa ait olduğu unutulmaz.
    /// </summary>
    public ProcessingRecord With(RecordEntry entry)
    {
        var previous = Find(entry.SourceName);
        if (!entry.HasOutput && previous is not null
            && string.Equals(previous.OutputName, entry.OutputName, StringComparison.OrdinalIgnoreCase))
        {
            entry = entry with { PreviousOutputSha256 = previous.OutputSha256 };
        }

        return this with
        {
            Entries = Entries
                .Where(e => !string.Equals(e.SourceName, entry.SourceName, StringComparison.OrdinalIgnoreCase))
                .Append(entry)
                .OrderBy(e => e.SourceName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    public IReadOnlyDictionary<string, string> OutputNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Entries) names[entry.SourceName] = entry.OutputName;
        return names;
    }
}
