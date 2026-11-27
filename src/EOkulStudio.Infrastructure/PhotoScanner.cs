using EOkulStudio.Core.Files;
using EOkulStudio.Core.Results;

namespace EOkulStudio.Infrastructure;

public sealed record RejectedFile(string Path, IssueCode Reason);

/// <param name="Photos">İşlenecek fotoğraflar (tam yol, sıralı, tekrarsız).</param>
/// <param name="Rejected">Listede nedeniyle gösterilecek dosya ve klasörler.</param>
/// <param name="UnreadableFolders">İzin veya disk hatası yüzünden okunamayan klasörler.</param>
public sealed record ScanResult(
    IReadOnlyList<string> Photos,
    IReadOnlyList<RejectedFile> Rejected,
    IReadOnlyList<string> UnreadableFolders);

/// <summary>Seçilen klasör ve dosyalardan işlenecek fotoğrafları bulur. Hiçbir şey yazmaz.</summary>
public sealed class PhotoScanner
{
    private static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;

    public ScanResult Scan(IEnumerable<string> inputs, bool includeSubfolders, CancellationToken cancellationToken = default)
    {
        var photos = new SortedSet<string>(Paths);
        var rejected = new SortedDictionary<string, IssueCode>(Paths);
        var unreadable = new SortedSet<string>(Paths);

        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));

            if (Directory.Exists(path))
            {
                if (OutputLocation.IsInOutputTree(path)) rejected[path] = IssueCode.InsideOutputFolder;
                else ScanFolder(path, includeSubfolders, photos, rejected, unreadable, cancellationToken);
            }
            else if (File.Exists(path))
            {
                // Tek tek seçilen dosya: desteklenmeyen her tür nedeniyle gösterilir.
                if (OutputLocation.IsFileInOutputTree(path)) rejected[path] = IssueCode.InsideOutputFolder;
                else if (SupportedFiles.Classify(path) == FileKind.Photo) photos.Add(path);
                else rejected[path] = IssueCode.UnsupportedFormat;
            }
            else
            {
                rejected[path] = IssueCode.FileNotFound;
            }
        }

        return new ScanResult(
            photos.ToList(),
            rejected.Select(pair => new RejectedFile(pair.Key, pair.Value)).ToList(),
            unreadable.ToList());
    }

    private static void ScanFolder(
        string root,
        bool includeSubfolders,
        ISet<string> photos,
        IDictionary<string, IssueCode> rejected,
        ISet<string> unreadable,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Pop();
            List<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(folder)
                    .EnumerateFileSystemInfos("*", new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.System,
                        IgnoreInaccessible = false,
                    })
                    .ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                unreadable.Add(folder);
                continue;
            }

            foreach (var entry in entries)
            {
                if (entry is DirectoryInfo directory)
                {
                    // Bağlantı klasörleri (junction/symlink) döngüye yol açabildiği için izlenmez.
                    if (includeSubfolders
                        && !OutputLocation.IsOutputFolderName(directory.Name)
                        && directory.LinkTarget is null)
                    {
                        pending.Push(directory.FullName);
                    }
                    continue;
                }

                switch (SupportedFiles.Classify(entry.Name))
                {
                    case FileKind.Photo:
                        photos.Add(entry.FullName);
                        break;
                    case FileKind.UnsupportedImage:
                        rejected[entry.FullName] = IssueCode.UnsupportedFormat;
                        break;
                }
            }
        }
    }
}
