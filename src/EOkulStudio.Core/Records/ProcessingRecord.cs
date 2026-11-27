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

    [JsonIgnore]
    public bool HasOutput => Status is PhotoStatus.Success or PhotoStatus.NeedsReview;
}

public sealed record ProcessingRecord
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public IReadOnlyList<RecordEntry> Entries { get; init; } = [];

    public RecordEntry? Find(string sourceName) =>
        Entries.FirstOrDefault(e => string.Equals(e.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));

    public ProcessingRecord With(RecordEntry entry) => this with
    {
        Entries = Entries
            .Where(e => !string.Equals(e.SourceName, entry.SourceName, StringComparison.OrdinalIgnoreCase))
            .Append(entry)
            .OrderBy(e => e.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToList(),
    };

    public IReadOnlyDictionary<string, string> OutputNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Entries) names[entry.SourceName] = entry.OutputName;
        return names;
    }
}
