namespace EOkulStudio.Infrastructure;

internal static class FolderLinks
{
    /// <summary>Klasör bir bağlantı (junction/symlink) mı? Bulut eşitleme klasörleri bağlantı sayılmaz.</summary>
    public static bool IsLink(DirectoryInfo directory)
    {
        // Hedef yalnızca yeniden ayrıştırma noktalarında okunur: diğerlerinde klasörü ayrıca açmaya gerek yok.
        if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) == 0) return false;
        try
        {
            return directory.LinkTarget is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true; // Nereye gittiği bilinmiyorsa izlenmez.
        }
    }
}
