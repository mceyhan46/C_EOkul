using System.Security.Cryptography;
using System.Text.Json;

namespace EOkulStudio.Imaging;

/// <param name="Path">Model dosyasının tam yolu.</param>
/// <param name="Id">Dosya adı ve özetinin başı; işlem kaydına yazılır.</param>
public sealed record LoadedModel(string Path, byte[] Content, string Id);

/// <summary>Program klasöründeki "models" klasörü. Her model, models.json'daki SHA-256 ile doğrulanır.</summary>
public sealed class ModelStore(string directory)
{
    public const string ListFile = "models.json";
    public const string MattingFile = "modnet_photographic.onnx";
    public const string FaceFile = "face_detection_yunet_2023mar.onnx";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private sealed record Entry(string File, string Sha256);

    public static ModelStore InDirectory(string baseDirectory) => new(System.IO.Path.Combine(baseDirectory, "models"));

    public LoadedModel Load(string fileName)
    {
        var listPath = System.IO.Path.Combine(directory, ListFile);
        var path = System.IO.Path.Combine(directory, fileName);
        if (!File.Exists(listPath) || !File.Exists(path))
            throw new FileNotFoundException($"Model dosyası bulunamadı: {fileName}", path);

        var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllBytes(listPath), Options) ?? [];
        var expected = entries.FirstOrDefault(e => string.Equals(e.File, fileName, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidDataException($"Model listede yok: {fileName}");

        var content = File.ReadAllBytes(path);
        var actual = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!string.Equals(actual, expected.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Model dosyası bozuk veya değiştirilmiş: {fileName}");

        return new LoadedModel(path, content, System.IO.Path.GetFileNameWithoutExtension(fileName) + ":" + actual[..8]);
    }
}
