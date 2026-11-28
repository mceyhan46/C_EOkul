using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Imaging;
using OpenCvSharp;

namespace EOkulStudio.Imaging;

/// <summary>Kırpma alanını eğimi düzeltilmiş olarak çıktı boyutuna getirir.</summary>
internal static class Renderer
{
    private const int Supersample = 2;

    // IMWRITE_JPEG_SAMPLING_FACTOR = 7, 4:4:4 = 0x111111 (renk kanalları tam çözünürlükte).
    private const int SamplingFactorFlag = 7;
    private const int Sampling444 = 0x111111;

    /// <summary>
    /// Bölge görüntüsünden çıktıyı üretir: önce küçültme, sonra 2 kat boyutta döndürerek kesme, alan ortalamasıyla
    /// hedefe indirme ve hafif keskinleştirme. Görüntünün dışında kalan yer <paramref name="fill"/> olur.
    /// </summary>
    public static Mat Output(
        Mat composite, PixelRect region, RotatedCrop crop, int width, int height, Scalar fill, bool sharpen = true)
    {
        var workScale = (double)composite.Height / region.Height;
        var shrink = Math.Min(1.0, Supersample * height / (crop.Height * workScale));

        using var prepared = new Mat();
        if (shrink < 1) Cv2.Resize(composite, prepared, default, shrink, shrink, InterpolationFlags.Area);
        else composite.CopyTo(prepared);

        using var large = Warp(prepared, region, crop, width * Supersample, height * Supersample,
            InterpolationFlags.Cubic, fill);
        var result = new Mat();
        Cv2.Resize(large, result, new Size(width, height), 0, 0, InterpolationFlags.Area);
        if (sharpen) Sharpen(result, large);
        return result;
    }

    /// <summary>Kırpmayı verilen boyuta döndürerek keser. <paramref name="image"/> bölgenin ölçekli görüntüsüdür.</summary>
    public static Mat Warp(
        Mat image, PixelRect region, RotatedCrop crop, int width, int height, InterpolationFlags interpolation,
        Scalar fill)
    {
        var values = crop.OutputToImageMatrix(
            width, height, new PointD(region.X, region.Y),
            (double)image.Width / region.Width, (double)image.Height / region.Height);
        using var matrix = new Mat(2, 3, MatType.CV_64FC1);
        for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++)
                matrix.Set(row, column, values[row, column]);

        var result = new Mat();
        Cv2.WarpAffine(image, result, matrix, new Size(width, height),
            interpolation | InterpolationFlags.WarpInverseMap, BorderTypes.Constant, fill);
        return result;
    }

    /// <summary>Gürültü, büyük görüntünün orta bölümünde (yüz) ölçülür; beyaz zemin ölçümü yanıltır.</summary>
    private static void Sharpen(Mat result, Mat large)
    {
        var centre = new Rect(large.Width / 5, large.Height / 5, large.Width * 3 / 5, large.Height * 3 / 5);
        using var view = new Mat(large, centre);
        var sigma = Unsharp.NoiseSigma(Bytes(view), centre.Width, centre.Height);

        var pixels = Bytes(result);
        Unsharp.Apply(pixels, result.Width, result.Height, Unsharp.AmountFor(sigma));
        SetBytes(result, pixels);
    }

    /// <summary>3 kanallı 8 bit görüntünün satır sıralı baytları.</summary>
    public static byte[] Bytes(Mat image)
    {
        using var continuous = image.Clone();
        continuous.GetArray(out Vec3b[] pixels);
        return System.Runtime.InteropServices.MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
    }

    private static void SetBytes(Mat image, byte[] bytes) =>
        image.SetArray(System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Vec3b>(bytes.AsSpan()).ToArray());

    public static byte[] EncodeJpeg(Mat image, int quality)
    {
        int[] parameters =
        [
            (int)ImwriteFlags.JpegQuality, quality,
            (int)ImwriteFlags.JpegOptimize, 1,
            SamplingFactorFlag, Sampling444,
        ];
        if (!Cv2.ImEncode(".jpg", image, out var data, parameters))
            throw new InvalidOperationException("JPEG yazılamadı.");
        return data;
    }
}
