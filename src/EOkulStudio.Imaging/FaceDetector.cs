using System.Collections.Concurrent;
using EOkulStudio.Core.Cropping;
using OpenCvSharp;

namespace EOkulStudio.Imaging;

/// <param name="Box">Yüz kutusu.</param>
/// <param name="Points">Göz, göz, burun ucu, ağız köşesi, ağız köşesi.</param>
public sealed record DetectedFace(RectD Box, IReadOnlyList<PointD> Points, float Score);

/// <summary>
/// YuNet yüz bulucu. Giriş her zaman karedir; her kare boyutu için iş parçacıkları arasında paylaşılmayan
/// bulucular yeniden kullanılır.
/// </summary>
public sealed class FaceDetector(string modelPath) : IDisposable
{
    public const float MinScore = 0.3f;
    private const float NmsThreshold = 0.3f;
    private const int TopK = 100;

    private readonly ConcurrentDictionary<int, ConcurrentBag<FaceDetectorYN>> _pool = new();
    private bool _disposed;

    /// <summary>Görüntünün uzun kenarını en çok <paramref name="size"/> yapıp (büyütmeden) yüz arar.</summary>
    /// <returns>Görüntü koordinatında yüzler.</returns>
    public IReadOnlyList<DetectedFace> Detect(Mat image, int size)
    {
        ArgumentNullException.ThrowIfNull(image);
        var scale = Math.Min(1.0, (double)size / Math.Max(image.Width, image.Height));
        var width = Math.Clamp((int)Math.Round(image.Width * scale), 1, size);
        var height = Math.Clamp((int)Math.Round(image.Height * scale), 1, size);

        using var square = new Mat(size, size, MatType.CV_8UC3, Scalar.All(0));
        using (var target = new Mat(square, new Rect(0, 0, width, height)))
        {
            if (width == image.Width && height == image.Height) image.CopyTo(target);
            else Cv2.Resize(image, target, new Size(width, height), 0, 0, InterpolationFlags.Area);
        }

        double scaleX = (double)width / image.Width, scaleY = (double)height / image.Height;
        return DetectSquare(square)
            .Select(face => new DetectedFace(
                new RectD(face.Box.Left / scaleX, face.Box.Top / scaleY, face.Box.Width / scaleX, face.Box.Height / scaleY),
                face.Points.Select(p => new PointD(p.X / scaleX, p.Y / scaleY)).ToArray(),
                face.Score))
            .ToList();
    }

    /// <summary>Kare görüntüde yüz arar.</summary>
    public IReadOnlyList<DetectedFace> DetectSquare(Mat square)
    {
        ArgumentNullException.ThrowIfNull(square);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (square.Width != square.Height) throw new ArgumentException("Giriş kare olmalı.", nameof(square));

        var size = square.Width;
        var bag = _pool.GetOrAdd(size, _ => []);
        if (!bag.TryTake(out var detector))
            detector = FaceDetectorYN.Create(modelPath, string.Empty, new Size(size, size), MinScore, NmsThreshold, TopK);
        try
        {
            using var rows = new Mat();
            detector.Detect(square, rows);
            var faces = new List<DetectedFace>();
            if (rows.Empty()) return faces;
            for (var i = 0; i < rows.Rows; i++)
            {
                var box = new RectD(rows.Get<float>(i, 0), rows.Get<float>(i, 1), rows.Get<float>(i, 2), rows.Get<float>(i, 3));
                var points = new PointD[5];
                for (var p = 0; p < points.Length; p++)
                    points[p] = new PointD(rows.Get<float>(i, 4 + 2 * p), rows.Get<float>(i, 5 + 2 * p));
                faces.Add(new DetectedFace(box, points, rows.Get<float>(i, 14)));
            }
            return faces;
        }
        finally
        {
            bag.Add(detector);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var bag in _pool.Values)
            while (bag.TryTake(out var detector))
                detector.Dispose();
    }
}
