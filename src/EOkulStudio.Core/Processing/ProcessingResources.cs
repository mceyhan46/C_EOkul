namespace EOkulStudio.Core.Processing;

/// <param name="Workers">Aynı anda işlenen fotoğraf sayısı.</param>
/// <param name="ModelThreads">Her model çalıştırmasının kullandığı iş parçacığı sayısı.</param>
public readonly record struct ProcessingResources(int Workers, int ModelThreads)
{
    public const int MaxWorkers = 4;

    /// <summary>Çekirdekleri paralel fotoğraflarla model iş parçacıkları arasında paylaştırır.</summary>
    /// <remarks>4 mantıksal çekirdekte ölçüm: 2 paralel fotoğraf, 1 fotoğrafa göre yaklaşık %25 daha hızlı, 200 MB daha çok bellek.</remarks>
    public static ProcessingResources For(int processorCount, int? requestedWorkers = null)
    {
        var cores = Math.Max(1, processorCount);
        var workers = requestedWorkers is { } requested
            ? Math.Clamp(requested, 1, 16)
            : Math.Clamp(cores / 2, 1, MaxWorkers);
        return new ProcessingResources(workers, Math.Max(1, cores / workers));
    }
}
