using System.Text.Json;
using EOkulStudio.Core.Files;
using EOkulStudio.Core.Records;

namespace EOkulStudio.Infrastructure;

/// <param name="WasCorrupt">Kayıt dosyası okunamadı; kopyası saklandı ve boş kayıtla devam ediliyor.</param>
public sealed record LoadedRecord(ProcessingRecord Record, bool WasCorrupt);

/// <summary>E_Okul klasöründeki gizli işlem kaydı.</summary>
public sealed class ProcessingRecordStore
{
    public const string CorruptCopyName = ".eokul_islem.bozuk.json";

    private readonly Lock _gate = new();
    private readonly string _directory;

    /// <param name="outputDirectory">E_Okul klasörünün yolu.</param>
    public ProcessingRecordStore(string outputDirectory)
    {
        if (!OutputLocation.IsOutputFolderName(Path.GetFileName(Path.TrimEndingDirectorySeparator(outputDirectory))))
            throw new ArgumentException("İşlem kaydı yalnızca E_Okul klasöründe tutulur.", nameof(outputDirectory));
        _directory = outputDirectory;
    }

    public string FilePath => Path.Combine(_directory, OutputLocation.RecordFileName);

    public LoadedRecord Load()
    {
        lock (_gate) return LoadUnlocked();
    }

    /// <summary>Tek bir fotoğrafın kaydını ekler veya yeniler.</summary>
    /// <returns>
    /// Yazılmadıysa false: E_Okul yalnızca çıktı varsa oluşturulur; klasör yokken başarısız fotoğraf kaydedilmez.
    /// </returns>
    public bool Upsert(RecordEntry entry)
    {
        lock (_gate)
        {
            if (!entry.HasOutput && !Directory.Exists(_directory)) return false;
            Save(LoadUnlocked().Record.With(entry));
            return true;
        }
    }

    private LoadedRecord LoadUnlocked()
    {
        if (!File.Exists(FilePath)) return new LoadedRecord(new ProcessingRecord(), false);
        try
        {
            var record = JsonSerializer.Deserialize<ProcessingRecord>(File.ReadAllBytes(FilePath), JsonFiles.Options);
            if (record is { Version: ProcessingRecord.CurrentVersion, Entries: not null }
                && record.Entries.All(e => e is { SourceName: not null, SourceSha256: not null, OutputName: not null, Issues: not null }))
            {
                return new LoadedRecord(record, false);
            }
        }
        catch (JsonException)
        {
        }

        var copy = Path.Combine(_directory, CorruptCopyName);
        // Windows gizli bir dosyanın üzerine kopyalamaya izin vermez.
        File.Delete(copy);
        File.Copy(FilePath, copy);
        return new LoadedRecord(new ProcessingRecord(), true);
    }

    private void Save(ProcessingRecord record)
    {
        Directory.CreateDirectory(_directory);
        SafeFileWriter.Write(FilePath, JsonSerializer.SerializeToUtf8Bytes(record, JsonFiles.Options), overwrite: true);
        if (OperatingSystem.IsWindows())
            File.SetAttributes(FilePath, File.GetAttributes(FilePath) | FileAttributes.Hidden);
    }
}
