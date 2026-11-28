using EOkulStudio.Core.Cropping;

namespace EOkulStudio.Core.Imaging;

/// <param name="BlurRatio">Hafif bulanıklaştırmadan sonra kalan ayrıntı oranı; yüksekse görüntü zaten bulanıktır.</param>
/// <param name="Brightness">Bölgenin orta kısmının ortalama parlaklığı (0..255).</param>
public readonly record struct QualityReport(double BlurRatio, double Brightness)
{
    public const double BlurLimit = 0.25;
    public const double DarkLimit = 70.0;

    public bool IsBlurry => BlurRatio > BlurLimit;
    public bool IsDark => Brightness < DarkLimit;
}

/// <summary>Bulanıklık ve karanlık denetimi. Android sürümündeki Quality sınıfından taşınmıştır.</summary>
public static class Quality
{
    private const int SampleWidth = 160;

    /// <param name="bgr">Satır sıralı, piksel başına 3 bayt (mavi, yeşil, kırmızı).</param>
    /// <param name="area">İncelenecek alan (görüntü içinde kalmalı).</param>
    public static QualityReport Analyze(ReadOnlySpan<byte> bgr, int width, int height, RectD area)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (bgr.Length != width * height * 3) throw new ArgumentException("Görüntü boyutu uyuşmuyor.", nameof(bgr));

        const int w = SampleWidth;
        var h = Math.Max(8, (int)Math.Floor(w * area.Height / Math.Max(area.Width, 1.0) + 0.5));
        var gray = new double[w * h];

        // Alan ortalamalı küçültme (kutu süzgeci)
        for (var y = 0; y < h; y++)
        {
            double sy1 = area.Top + area.Height * y / h, sy2 = area.Top + area.Height * (y + 1) / h;
            var y1 = Math.Clamp((int)Math.Floor(sy1), 0, height);
            var y2 = Math.Max(y1 + 1, Math.Clamp((int)Math.Ceiling(sy2), 0, height + 1));
            for (var x = 0; x < w; x++)
            {
                double sx1 = area.Left + area.Width * x / w, sx2 = area.Left + area.Width * (x + 1) / w;
                var x1 = Math.Clamp((int)Math.Floor(sx1), 0, width);
                var x2 = Math.Max(x1 + 1, Math.Clamp((int)Math.Ceiling(sx2), 0, width + 1));
                var sum = 0.0;
                var count = 0;
                for (var yy = y1; yy < Math.Min(y2, height); yy++)
                {
                    for (var xx = x1; xx < Math.Min(x2, width); xx++)
                    {
                        var offset = (yy * width + xx) * 3;
                        sum += 0.299 * bgr[offset + 2] + 0.587 * bgr[offset + 1] + 0.114 * bgr[offset];
                        count++;
                    }
                }
                gray[y * w + x] = count > 0 ? sum / count : 0;
            }
        }

        var sharp = LaplacianVariance(gray, w, h);
        var blurred = Box3(gray, w, h);
        var ratio = sharp < 1e-6 ? 0.0 : LaplacianVariance(blurred, w, h) / sharp;
        // Hareket bulanıklığı tek eksende ayrıntıyı yok eder; genel ölçüde kaybolduğu için yönlü bakılır.
        ratio = Math.Max(ratio, Math.Max(
            DirectionalRatio(gray, blurred, w, h, horizontal: true),
            DirectionalRatio(gray, blurred, w, h, horizontal: false)));

        int cx1 = (int)(w * 0.2), cx2 = (int)(w * 0.8), cy1 = (int)(h * 0.2), cy2 = (int)(h * 0.8);
        var total = 0.0;
        var samples = 0;
        for (var y = cy1; y < cy2; y++)
        {
            for (var x = cx1; x < cx2; x++)
            {
                total += gray[y * w + x];
                samples++;
            }
        }
        return new QualityReport(ratio, total / samples);
    }

    private static double LaplacianVariance(double[] g, int w, int h)
    {
        double sum = 0, squares = 0;
        var n = 0;
        for (var y = 1; y < h - 1; y++)
        {
            for (var x = 1; x < w - 1; x++)
            {
                var value = 4 * g[y * w + x] - g[(y - 1) * w + x] - g[(y + 1) * w + x]
                            - g[y * w + x - 1] - g[y * w + x + 1];
                sum += value;
                squares += value * value;
                n++;
            }
        }
        var mean = sum / n;
        return squares / n - mean * mean;
    }

    private static double DirectionalVariance(double[] g, int w, int h, bool horizontal)
    {
        double sum = 0, squares = 0;
        var n = 0;
        var step = horizontal ? 1 : w;
        for (var y = 1; y < h - 1; y++)
        {
            for (var x = 1; x < w - 1; x++)
            {
                var o = y * w + x;
                var value = 2 * g[o] - g[o - step] - g[o + step];
                sum += value;
                squares += value * value;
                n++;
            }
        }
        var mean = sum / n;
        return squares / n - mean * mean;
    }

    private static double DirectionalRatio(double[] g, double[] blurred, int w, int h, bool horizontal)
    {
        var sharp = DirectionalVariance(g, w, h, horizontal);
        return sharp < 1e-6 ? 0.0 : DirectionalVariance(blurred, w, h, horizontal) / sharp;
    }

    private static double[] Box3(double[] g, int w, int h)
    {
        var output = new double[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var sum = 0.0;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                        sum += g[Math.Clamp(y + dy, 0, h - 1) * w + Math.Clamp(x + dx, 0, w - 1)];
                output[y * w + x] = sum / 9;
            }
        }
        return output;
    }
}
