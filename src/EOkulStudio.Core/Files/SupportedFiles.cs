namespace EOkulStudio.Core.Files;

public enum FileKind
{
    /// <summary>İşlenebilen fotoğraf.</summary>
    Photo,

    /// <summary>Görsel dosyası ama desteklenmiyor; listede nedeniyle gösterilir.</summary>
    UnsupportedImage,

    /// <summary>Görsel olmayan veya sistem dosyası; listelenmez.</summary>
    Ignored,
}

public static class SupportedFiles
{
    // OrdinalIgnoreCase: Türkçe kültürde ".TIF".ToLower() ".tıf" olur; kültüre bağlı karşılaştırma yapılmaz.
    private static readonly HashSet<string> Photos = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp",
    };

    private static readonly HashSet<string> Unsupported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".heic", ".heif", ".avif", ".gif", ".jfif", ".jp2", ".psd", ".svg",
        ".dng", ".cr2", ".cr3", ".nef", ".arw", ".orf", ".rw2", ".raf",
    };

    private static readonly HashSet<string> SystemFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "thumbs.db", "desktop.ini",
    };

    public static IReadOnlyCollection<string> PhotoExtensions => Photos;

    public static FileKind Classify(string path)
    {
        var name = Path.GetFileName(path);
        if (name.Length == 0 || name.StartsWith('.') || name.StartsWith("~$", StringComparison.Ordinal)
            || SystemFiles.Contains(name))
        {
            return FileKind.Ignored;
        }

        var extension = Path.GetExtension(name);
        if (Photos.Contains(extension)) return FileKind.Photo;
        return Unsupported.Contains(extension) ? FileKind.UnsupportedImage : FileKind.Ignored;
    }
}
