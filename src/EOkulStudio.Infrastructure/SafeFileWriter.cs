namespace EOkulStudio.Infrastructure;

/// <summary>Güvenli yazma: geçici dosya → doğrula → atomik yer değiştir. Yarım dosya kalmaz.</summary>
public static class SafeFileWriter
{
    /// <summary>Tarayıcı noktayla başlayan dosyaları yok sayar; artık kalan geçici dosya fotoğraf sanılmaz.</summary>
    public const string TemporaryPrefix = ".eokul_tmp_";

    /// <param name="verify">Geçici dosya yolunu alır; false dönerse hedef değiştirilmez.</param>
    /// <param name="overwrite">False ise var olan hedefte IOException atılır.</param>
    public static void Write(
        string targetPath,
        ReadOnlySpan<byte> content,
        bool overwrite,
        Func<string, bool>? verify = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath))!;
        var temporary = Path.Combine(directory, TemporaryPrefix + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            if (!File.ReadAllBytes(temporary).AsSpan().SequenceEqual(content))
                throw new IOException("Yazılan dosya doğrulanamadı: içerik farklı.");
            if (verify is not null && !verify(temporary))
                throw new InvalidDataException("Yazılan dosya doğrulamayı geçemedi.");

            File.Move(temporary, targetPath, overwrite);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Asıl hatanın üstünü örtme.
        }
    }
}
