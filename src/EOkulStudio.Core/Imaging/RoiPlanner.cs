using EOkulStudio.Core.Cropping;

namespace EOkulStudio.Core.Imaging;

/// <summary>Kaynak üzerinde tam piksel dikdörtgeni.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(PixelRect other) =>
        other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    public PixelRect Union(PixelRect other)
    {
        int x = Math.Min(X, other.X), y = Math.Min(Y, other.Y);
        return new PixelRect(x, y, Math.Max(Right, other.Right) - x, Math.Max(Bottom, other.Bottom) - y);
    }
}

/// <summary>Arka plan modeline verilecek bölgeyi ve çalışma ölçeğini belirler.</summary>
public static class RoiPlanner
{
    /// <summary>Kırpma yüksekliği bundan büyükse bölge küçültülerek işlenir (çıktı yüksekliğinin 4 katı).</summary>
    public const double WorkingCropHeight = 684;

    private const double CropMargin = 0.1;

    /// <summary>
    /// Kırpma alanını, omuzları ve saçı içeren bölge. Dar bölgede model yüzü arka plan sanabildiği için
    /// <paramref name="wide"/> ile daha geniş bir bölge istenebilir.
    /// </summary>
    public static PixelRect Compute(RectD face, RotatedCrop crop, int sourceWidth, int sourceHeight, bool wide = false)
    {
        var unit = CropGeometry.FaceUnit(face);
        var side = (wide ? 2.5 : 2.0) * unit;
        var above = (wide ? 1.5 : 1.2) * unit;
        var below = (wide ? 2.6 : 1.6) * unit;
        var centerX = face.Center.X;

        return Union(
            Bounds(crop, CropMargin),
            new RectD(centerX - side, face.Top - above, 2 * side, above + face.Height + below),
            sourceWidth, sourceHeight);
    }

    /// <summary>Kırpma alanının görüntü içinde kalan kısmını saran dikdörtgen.</summary>
    public static PixelRect CropBounds(RotatedCrop crop, int sourceWidth, int sourceHeight) =>
        Clip(Bounds(crop, 0), sourceWidth, sourceHeight);

    /// <summary>Bölgenin işlenmeden önce küçültüleceği oran (en çok 1).</summary>
    public static double WorkingScale(double cropHeight) =>
        cropHeight > WorkingCropHeight ? WorkingCropHeight / cropHeight : 1.0;

    private static RectD Bounds(RotatedCrop crop, double margin)
    {
        PointD[] corners =
        [
            crop.ToSource(-margin, -margin), crop.ToSource(1 + margin, -margin),
            crop.ToSource(1 + margin, 1 + margin), crop.ToSource(-margin, 1 + margin),
        ];
        double left = corners.Min(p => p.X), top = corners.Min(p => p.Y);
        return new RectD(left, top, corners.Max(p => p.X) - left, corners.Max(p => p.Y) - top);
    }

    private static PixelRect Union(RectD a, RectD b, int width, int height)
    {
        double left = Math.Min(a.Left, b.Left), top = Math.Min(a.Top, b.Top);
        return Clip(
            new RectD(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top),
            width, height);
    }

    private static PixelRect Clip(RectD rect, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var x = Math.Clamp((int)Math.Floor(rect.Left), 0, width - 1);
        var y = Math.Clamp((int)Math.Floor(rect.Top), 0, height - 1);
        var right = Math.Clamp((int)Math.Ceiling(rect.Right), x + 1, width);
        var bottom = Math.Clamp((int)Math.Ceiling(rect.Bottom), y + 1, height);
        return new PixelRect(x, y, right - x, bottom - y);
    }
}
