using EOkulStudio.Core.Files;

namespace EOkulStudio.Infrastructure;

/// <summary>E_Okul klasörüne yazılamadığında atılır.</summary>
public sealed class OutputNotWritableException(string directory, Exception? inner = null)
    : IOException($"Çıktı klasörüne yazılamıyor: {directory}", inner)
{
    public string Directory { get; } = directory;
}

/// <summary>Programın kaynak klasörlere yaptığı tek yazma noktası.</summary>
public static class OutputWriter
{
    /// <returns>Yazılan dosyanın tam yolu.</returns>
    public static string WritePhoto(
        string sourcePath,
        string outputName,
        ReadOnlySpan<byte> jpeg,
        bool overwrite,
        Func<string, bool>? verify = null)
    {
        if (Path.GetFileName(outputName) != outputName
            || !outputName.EndsWith(OutputNaming.Extension, StringComparison.OrdinalIgnoreCase)
            || outputName.StartsWith('.'))
        {
            throw new ArgumentException($"Geçersiz çıktı adı: {outputName}", nameof(outputName));
        }

        var source = Path.GetFullPath(sourcePath);
        if (OutputLocation.IsFileInOutputTree(source))
            throw new ArgumentException("Kaynak bir E_Okul klasörünün içinde.", nameof(sourcePath));

        var directory = OutputLocation.DirectoryFor(source);
        var created = !Directory.Exists(directory);
        try
        {
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, outputName);
            SafeFileWriter.Write(target, jpeg, overwrite, verify);
            return target;
        }
        catch (Exception ex)
        {
            // Klasör silinmeden önce değerlendirilir: aksi halde her disk hatası "yazılamıyor" sayılır.
            var unwritable = ex is UnauthorizedAccessException || (ex is IOException && !Directory.Exists(directory));
            // E_Okul yalnızca içine yazılan bir çıktı varsa kalır.
            if (created) TryRemoveEmpty(directory);
            if (unwritable)
                throw new OutputNotWritableException(directory, ex);
            throw;
        }
    }

    private static void TryRemoveEmpty(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
