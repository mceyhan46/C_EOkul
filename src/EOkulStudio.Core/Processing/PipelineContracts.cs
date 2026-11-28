using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Results;
using EOkulStudio.Core.Settings;

namespace EOkulStudio.Core.Processing;

/// <summary>Bir fotoğrafın çıktısını etkileyen ayarlar.</summary>
public sealed record PipelineOptions(CropRules Crop, int JpegQuality, int MinFileSizeKb, int MaxFileSizeKb)
{
    /// <summary>İşleme algoritması çıktıyı değiştirecek biçimde güncellenince artırılır.</summary>
    public const int AlgorithmVersion = 1;

    public static PipelineOptions From(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalized();
        return new PipelineOptions(
            normalized.Crop, normalized.JpegQuality, normalized.MinFileSizeKb, normalized.MaxFileSizeKb);
    }

    /// <summary>Alt sınır; tam sınırda kalmasın diye küçük bir pay eklenir.</summary>
    [JsonIgnore]
    public int MinBytes => MinFileSizeKb > 0 ? MinFileSizeKb * 1024 + 200 : 0;

    [JsonIgnore]
    public int MaxBytes => MaxFileSizeKb * 1024;

    /// <summary>İşlem kaydına yazılan kısa sürüm: algoritma numarası ve ayarların özeti.</summary>
    [JsonIgnore]
    public string Version =>
        AlgorithmVersion + ":" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)))[..8];
}

/// <param name="Jpeg">Hazır çıktı; üretilemediyse null.</param>
/// <param name="Crop">Kullanılan kırpma (yön düzeltmesi uygulanmış kaynak koordinatında).</param>
public sealed record PipelineResult(byte[]? Jpeg, IReadOnlyList<IssueCode> Issues, RotatedCrop? Crop)
{
    public static PipelineResult Failed(IssueCode reason) => new(null, [reason], null);
}

public enum PipelineStage
{
    Reading,
    FindingFace,
    RemovingBackground,
    Rendering,
}

/// <summary>Tek bir fotoğrafı bellekte işler; diske dokunmaz. Aynı anda birçok iş parçacığından çağrılabilir.</summary>
public interface IPhotoPipeline
{
    /// <summary>Arka plan modelinin kimliği (işlem kaydına yazılır).</summary>
    string ModelId { get; }

    /// <param name="source">Kaynak dosyanın içeriği.</param>
    /// <param name="sourceName">Yalnızca günlük ve inceleme için.</param>
    PipelineResult Process(
        byte[] source,
        string sourceName,
        PipelineOptions options,
        IProgress<PipelineStage>? progress = null,
        CancellationToken cancellationToken = default);
}
