using System.Text.Json;
using EOkulStudio.Core.Settings;

namespace EOkulStudio.Infrastructure;

/// <summary>Ayarları JSON dosyasında saklar. Bozuk dosya programı engellemez: varsayılanlara dönülür.</summary>
public sealed class SettingsStore(string filePath)
{
    public string FilePath { get; } = filePath;

    /// <summary>Son yüklemede bozuk dosya yüzünden varsayılanlar kullanıldıysa true.</summary>
    public bool LastLoadFailed { get; private set; }

    public AppSettings Load()
    {
        LastLoadFailed = false;
        if (!File.Exists(FilePath)) return new AppSettings();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllBytes(FilePath), JsonFiles.Options);
            if (settings is not null) return settings.Normalized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
        }

        LastLoadFailed = true;
        TryKeepCopy();
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        SafeFileWriter.Write(
            FilePath,
            JsonSerializer.SerializeToUtf8Bytes(settings.Normalized(), JsonFiles.Options),
            overwrite: true);
    }

    private void TryKeepCopy()
    {
        try
        {
            File.Copy(FilePath, FilePath + ".bozuk", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
