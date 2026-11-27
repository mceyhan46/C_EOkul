namespace EOkulStudio.Core.Files;

/// <summary>Her fotoğrafın çıktısı, bulunduğu klasörün içindeki E_Okul klasörüne yazılır.</summary>
public static class OutputLocation
{
    public const string FolderName = "E_Okul";
    public const string RecordFileName = ".eokul_islem.json";

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static bool IsOutputFolderName(string name) =>
        string.Equals(name, FolderName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Verilen klasör yolu bir E_Okul klasörünün kendisi veya içi mi?</summary>
    public static bool IsInOutputTree(string directoryPath) =>
        directoryPath.Split(Separators, StringSplitOptions.RemoveEmptyEntries).Any(IsOutputFolderName);

    /// <summary>Dosya bir E_Okul klasörünün (herhangi bir derinlikte) içinde mi?</summary>
    public static bool IsFileInOutputTree(string filePath) =>
        IsInOutputTree(Path.GetDirectoryName(filePath) ?? string.Empty);

    public static string DirectoryFor(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrEmpty(directory))
            throw new ArgumentException("Kaynak yolu klasör içermiyor.", nameof(sourcePath));
        return Path.Combine(directory, FolderName);
    }
}
