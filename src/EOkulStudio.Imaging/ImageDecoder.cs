using EOkulStudio.Core.Imaging;
using OpenCvSharp;

namespace EOkulStudio.Imaging;

/// <summary>Dosya içeriğini yönü düzeltilmiş, 8 bit, 3 kanallı (BGR) görüntüye çevirir.</summary>
public static class ImageDecoder
{
    /// <exception cref="InvalidDataException">İçerik okunabilen bir görüntü değil.</exception>
    public static Mat Decode(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0) throw new InvalidDataException("Dosya boş.");

        var jpeg = content.Length > 2 && content[0] == 0xFF && content[1] == 0xD8;
        // JPEG'de yön elle uygulanır; diğerleri saydamlık korunarak açılır.
        var flags = jpeg ? ImreadModes.Color | ImreadModes.IgnoreOrientation : ImreadModes.Unchanged;

        using var raw = Cv2.ImDecode(content, flags);
        if (raw.Empty() || raw.Width < 1 || raw.Height < 1)
            throw new InvalidDataException("Görüntü okunamadı.");

        using var image = ToBgr(raw);
        // TIFF yönünü çözücü kendisi uygular; ExifOrientation bu tür için 1 döndürür.
        return Orient(image, ExifOrientation.Read(content));
    }

    private static Mat ToBgr(Mat raw)
    {
        using var eight = new Mat();
        switch (raw.Depth())
        {
            case MatType.CV_8U:
                raw.CopyTo(eight);
                break;
            case MatType.CV_16U:
                raw.ConvertTo(eight, MatType.CV_8U, 1.0 / 257.0);
                break;
            default:
                throw new InvalidDataException("Desteklenmeyen renk derinliği.");
        }

        var result = new Mat();
        switch (eight.Channels())
        {
            case 1:
                Cv2.CvtColor(eight, result, ColorConversionCodes.GRAY2BGR);
                break;
            case 3:
                eight.CopyTo(result);
                break;
            case 4:
                CompositeOnWhite(eight, result);
                break;
            default:
                result.Dispose();
                throw new InvalidDataException("Desteklenmeyen kanal sayısı.");
        }
        return result;
    }

    /// <summary>Saydam bölgeler beyaz zemine oturtulur.</summary>
    private static void CompositeOnWhite(Mat bgra, Mat result)
    {
        bgra.GetArray(out Vec4b[] source);
        var target = new Vec3b[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            var pixel = source[i];
            int alpha = pixel.Item3, rest = 255 - alpha;
            target[i] = new Vec3b(
                (byte)((pixel.Item0 * alpha + 255 * rest + 127) / 255),
                (byte)((pixel.Item1 * alpha + 255 * rest + 127) / 255),
                (byte)((pixel.Item2 * alpha + 255 * rest + 127) / 255));
        }
        result.Create(bgra.Rows, bgra.Cols, MatType.CV_8UC3);
        result.SetArray(target);
    }

    /// <returns>Yönü düzeltilmiş yeni görüntü.</returns>
    private static Mat Orient(Mat image, int orientation)
    {
        var result = new Mat();
        switch (orientation)
        {
            case 2:
                Cv2.Flip(image, result, FlipMode.Y);
                break;
            case 3:
                Cv2.Rotate(image, result, RotateFlags.Rotate180);
                break;
            case 4:
                Cv2.Flip(image, result, FlipMode.X);
                break;
            case 5:
                Cv2.Transpose(image, result);
                break;
            case 6:
                Cv2.Rotate(image, result, RotateFlags.Rotate90Clockwise);
                break;
            case 7:
                using (var transposed = new Mat())
                {
                    Cv2.Transpose(image, transposed);
                    Cv2.Rotate(transposed, result, RotateFlags.Rotate180);
                }
                break;
            case 8:
                Cv2.Rotate(image, result, RotateFlags.Rotate90Counterclockwise);
                break;
            default:
                image.CopyTo(result);
                break;
        }
        return result;
    }
}
