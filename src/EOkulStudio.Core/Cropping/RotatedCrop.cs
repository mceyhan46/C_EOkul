using System.Text.Json.Serialization;

namespace EOkulStudio.Core.Cropping;

/// <summary>
/// Kaynak görüntüdeki kırpma alanı: merkez, boyut ve eğim. Alan kaynağın dışına taşabilir; taşan yer beyaz doldurulur.
/// </summary>
/// <param name="AngleDegrees">Başın eğimi; pozitifse görüntünün sağındaki göz daha aşağıdadır.</param>
public readonly record struct RotatedCrop(double CenterX, double CenterY, double Width, double Height, double AngleDegrees)
{
    [JsonIgnore]
    public PointD Center => new(CenterX, CenterY);

    /// <summary>Kırpma içindeki oransal konumu (0..1) kaynak koordinatına çevirir.</summary>
    public PointD ToSource(double u, double v) =>
        new PointD(CenterX + (u - 0.5) * Width, CenterY + (v - 0.5) * Height).RotateAround(Center, AngleDegrees);

    /// <summary>Sol üst, sağ üst, sağ alt, sol alt.</summary>
    public IReadOnlyList<PointD> Corners() => [ToSource(0, 0), ToSource(1, 0), ToSource(1, 1), ToSource(0, 1)];

    /// <summary>
    /// Çıktı pikselinden kaynak pikseline 2x3 afin matris [a, b, tx; c, d, ty]
    /// (OpenCV WarpAffine, WARP_INVERSE_MAP ile). Piksel merkezleri hizalanır.
    /// </summary>
    public double[,] OutputToSourceMatrix(int outputWidth, int outputHeight) =>
        OutputToImageMatrix(outputWidth, outputHeight, new PointD(0, 0), 1, 1);

    /// <summary>
    /// Aynı matris, kaynaktan kesilip ölçeklenmiş bir görüntü için: sol üstü kaynakta
    /// <paramref name="origin"/> olan ve <paramref name="scaleX"/>, <paramref name="scaleY"/> ile ölçeklenen görüntü.
    /// </summary>
    public double[,] OutputToImageMatrix(int outputWidth, int outputHeight, PointD origin, double scaleX, double scaleY)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);
        var first = ToSource(0.5 / outputWidth, 0.5 / outputHeight);
        var right = ToSource(1.5 / outputWidth, 0.5 / outputHeight);
        var down = ToSource(0.5 / outputWidth, 1.5 / outputHeight);
        // Görüntüde piksel merkezi (i + 0.5) indeks i'ye karşılık gelir.
        return new[,]
        {
            { (right.X - first.X) * scaleX, (down.X - first.X) * scaleX, (first.X - origin.X) * scaleX - 0.5 },
            { (right.Y - first.Y) * scaleY, (down.Y - first.Y) * scaleY, (first.Y - origin.Y) * scaleY - 0.5 },
        };
    }

    /// <summary>Kırpmanın kaynak kenarlarını aşma miktarı; kırpma boyutuna oranla (0 = taşma yok).</summary>
    public Overflow OverflowOutside(int sourceWidth, int sourceHeight)
    {
        var corners = Corners();
        return new Overflow(
            Left: Math.Max(0, -corners.Min(c => c.X)) / Width,
            Top: Math.Max(0, -corners.Min(c => c.Y)) / Height,
            Right: Math.Max(0, corners.Max(c => c.X) - sourceWidth) / Width,
            Bottom: Math.Max(0, corners.Max(c => c.Y) - sourceHeight) / Height);
    }
}

public readonly record struct Overflow(double Left, double Top, double Right, double Bottom);
