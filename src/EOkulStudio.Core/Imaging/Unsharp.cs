namespace EOkulStudio.Core.Imaging;

/// <summary>
/// Yalnızca parlaklığa uygulanan hafif keskinleştirme. Android sürümündeki Finish sınıfından taşınmıştır:
/// gürültülü karede keskinleştirme gürültüyü de büyüttüğü için ölçülen gürültü arttıkça azaltılır.
/// </summary>
public static class Unsharp
{
    public const double BaseAmount = 0.35;
    private const double Sigma = 0.8;
    private const float Limit = 22f;

    /// <summary>Immerkær gürültü tahmini: parlaklığın standart sapması (0..255 ölçeği).</summary>
    public static double NoiseSigma(ReadOnlySpan<byte> bgr, int width, int height)
    {
        var y = Luma(bgr, width, height);
        var sum = 0.0;
        var n = 0;
        for (var j = 1; j < height - 1; j++)
        {
            for (var i = 1; i < width - 1; i++)
            {
                var o = j * width + i;
                double v = y[o - width - 1] - 2 * y[o - width] + y[o - width + 1]
                           - 2 * y[o - 1] + 4 * y[o] - 2 * y[o + 1]
                           + y[o + width - 1] - 2 * y[o + width] + y[o + width + 1];
                sum += Math.Abs(v);
                n++;
            }
        }
        return n == 0 ? 0 : Math.Sqrt(Math.PI / 2.0) * sum / (6.0 * n);
    }

    public static double AmountFor(double noiseSigma) => BaseAmount * (1.0 - Math.Min(0.8, noiseSigma / 6.0));

    /// <summary>Görüntüyü yerinde keskinleştirir.</summary>
    public static void Apply(Span<byte> bgr, int width, int height, double amount)
    {
        var y = Luma(bgr, width, height);
        if (!(amount > 0.01)) return;
        var blurred = Gauss(y, width, height, Sigma);
        for (var i = 0; i < y.Length; i++)
        {
            var difference = (float)(amount * (y[i] - blurred[i]));
            difference = Math.Max(-Limit, Math.Min(Limit, difference));
            for (var c = 0; c < 3; c++) bgr[i * 3 + c] = Clip(bgr[i * 3 + c] + difference);
        }
    }

    private static float[] Luma(ReadOnlySpan<byte> bgr, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (bgr.Length != width * height * 3) throw new ArgumentException("Görüntü boyutu uyuşmuyor.", nameof(bgr));
        var y = new float[width * height];
        for (var i = 0; i < y.Length; i++)
            y[i] = 0.299f * bgr[i * 3 + 2] + 0.587f * bgr[i * 3 + 1] + 0.114f * bgr[i * 3];
        return y;
    }

    private static byte Clip(float value) =>
        value < 0 ? (byte)0 : value > 255 ? (byte)255 : (byte)MathF.Floor(value + 0.5f);

    private static float[] Gauss(float[] a, int w, int h, double sigma)
    {
        var r = (int)Math.Ceiling(sigma * 2.5);
        var kernel = new float[2 * r + 1];
        var sum = 0f;
        for (var i = -r; i <= r; i++)
        {
            kernel[i + r] = (float)Math.Exp(-(i * i) / (2 * sigma * sigma));
            sum += kernel[i + r];
        }
        for (var i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        var temp = new float[a.Length];
        var output = new float[a.Length];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var s = 0f;
                for (var i = -r; i <= r; i++) s += kernel[i + r] * a[y * w + Math.Clamp(x + i, 0, w - 1)];
                temp[y * w + x] = s;
            }
        }
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var s = 0f;
                for (var i = -r; i <= r; i++) s += kernel[i + r] * temp[Math.Clamp(y + i, 0, h - 1) * w + x];
                output[y * w + x] = s;
            }
        }
        return output;
    }
}
