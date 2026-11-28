using OpenCvSharp;

namespace EOkulStudio.Imaging;

/// <summary>Ön plan rengini arka plandan arındırır ve görüntüyü yeni zemine oturtur.</summary>
internal static class Compositor
{
    private const double Tiny = 1e-5;

    /// <summary>
    /// Kenar pikselleri eski arka planın rengini taşır (ör. yeşil perde yansıması). "Blur fusion" yöntemiyle
    /// (Forte ve Pitié, 2020) her pikselin ön plan rengi komşuluktan tahmin edilir, sonra beyaza oturtulur.
    /// </summary>
    /// <param name="image">8 bit BGR.</param>
    /// <param name="alpha">CV_32FC1, 0..1.</param>
    /// <param name="faceUnit">Görüntü ölçeğinde yüz birimi; bulanıklaştırma yarıçapı buna göre seçilir.</param>
    /// <returns>8 bit BGR.</returns>
    public static Mat OnWhite(Mat image, Mat alpha, double faceUnit)
    {
        using var original = ToUnit(image);
        using var alpha3 = Expand(alpha);
        using var foreground = EstimateForeground(original, alpha, alpha3, faceUnit);
        return Blend(foreground, alpha3, Scalar.All(1));
    }

    /// <summary>Renk arındırma olmadan düz birleştirme (karşılaştırma görselleri için).</summary>
    public static Mat Plain(Mat image, Mat alpha, Scalar background)
    {
        using var original = ToUnit(image);
        using var alpha3 = Expand(alpha);
        return Blend(original, alpha3, new Scalar(background.Val0 / 255, background.Val1 / 255, background.Val2 / 255));
    }

    /// <summary>Arındırılmış ön planı verilen zemine oturtur.</summary>
    public static Mat On(Mat image, Mat alpha, double faceUnit, Scalar background)
    {
        using var original = ToUnit(image);
        using var alpha3 = Expand(alpha);
        using var foreground = EstimateForeground(original, alpha, alpha3, faceUnit);
        return Blend(foreground, alpha3, new Scalar(background.Val0 / 255, background.Val1 / 255, background.Val2 / 255));
    }

    private static Mat ToUnit(Mat image)
    {
        var result = new Mat();
        image.ConvertTo(result, MatType.CV_32FC3, 1.0 / 255.0);
        return result;
    }

    private static Mat Expand(Mat alpha)
    {
        var result = new Mat();
        Cv2.Merge([alpha, alpha, alpha], result);
        return result;
    }

    private static Mat Blend(Mat foreground, Mat alpha3, Scalar background)
    {
        // arka + (ön - arka) * a
        using var zemin = new Mat(foreground.Size(), MatType.CV_32FC3, background);
        using var difference = new Mat();
        Cv2.Subtract(foreground, zemin, difference);
        Cv2.Multiply(difference, alpha3, difference);
        Cv2.Add(zemin, difference, difference);
        var result = new Mat();
        difference.ConvertTo(result, MatType.CV_8UC3, 255);
        return result;
    }

    private static Mat EstimateForeground(Mat image, Mat alpha, Mat alpha3, double faceUnit)
    {
        using var inverse = new Mat();
        alpha3.ConvertTo(inverse, MatType.CV_32FC3, -1, 1); // 1 - a

        var foreground = image.Clone();
        using var background = image.Clone();
        int[] radii = [Math.Max(3, (int)(faceUnit * 0.5) | 1), 7];

        foreach (var radius in radii)
        {
            var kernel = new Size(radius, radius);
            using var meanAlpha = new Mat();
            Cv2.Blur(alpha3, meanAlpha, kernel);

            // F = bulanık(F * a) / (bulanık(a) + ε)
            using var weighted = new Mat();
            Cv2.Multiply(foreground, alpha3, weighted);
            Cv2.Blur(weighted, weighted, kernel);
            using var divisor = new Mat();
            meanAlpha.ConvertTo(divisor, MatType.CV_32FC3, 1, Tiny);
            using var meanForeground = new Mat();
            Cv2.Divide(weighted, divisor, meanForeground);

            // B = bulanık(B * (1 - a)) / (1 - bulanık(a) + ε)
            Cv2.Multiply(background, inverse, weighted);
            Cv2.Blur(weighted, weighted, kernel);
            meanAlpha.ConvertTo(divisor, MatType.CV_32FC3, -1, 1 + Tiny);
            Cv2.Divide(weighted, divisor, background);

            // F = F̄ + a * (I - a * F̄ - (1 - a) * B̄)
            using var predicted = new Mat();
            Cv2.Multiply(alpha3, meanForeground, predicted);
            using var behind = new Mat();
            Cv2.Multiply(inverse, background, behind);
            using var residual = new Mat();
            Cv2.Subtract(image, predicted, residual);
            Cv2.Subtract(residual, behind, residual);
            Cv2.Multiply(residual, alpha3, residual);
            Cv2.Add(meanForeground, residual, foreground);

            // 0..1 aralığına kırp
            using var one = new Mat(foreground.Size(), MatType.CV_32FC3, Scalar.All(1));
            using var zero = new Mat(foreground.Size(), MatType.CV_32FC3, Scalar.All(0));
            Cv2.Min(foreground, one, foreground);
            Cv2.Max(foreground, zero, foreground);
        }
        return foreground;
    }
}
