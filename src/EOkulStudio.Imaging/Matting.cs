using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Imaging;
using OpenCvSharp;
using OpenCvSharp.XImgProc;

namespace EOkulStudio.Imaging;

/// <summary>Bir bölgenin çalışma çözünürlüğündeki görüntüsü ve inceltilmiş kişi maskesi.</summary>
internal sealed class Matte : IDisposable
{
    public required PixelRect Region { get; init; }

    /// <summary>Bölgenin (gerekirse küçültülmüş) görüntüsü, 8 bit BGR.</summary>
    public required Mat Image { get; init; }

    /// <summary>Aynı boyutta 0..1 maske, CV_32FC1.</summary>
    public required Mat Alpha { get; init; }

    /// <summary>Maskeden ölçülen saç tepesi (kaynak koordinatı).</summary>
    public PointD? HeadTop { get; init; }

    public double ScaleX => (double)Image.Width / Region.Width;
    public double ScaleY => (double)Image.Height / Region.Height;

    public void Dispose()
    {
        Image.Dispose();
        Alpha.Dispose();
    }
}

/// <summary>Model maskesini kaynak ayrıntısına oturtur ve yalnız yüzün sahibini bırakır.</summary>
internal static class Matting
{
    private const double GuideRadius = 0.025;
    private const double GuideEpsilon = 1e-5 * 255 * 255;
    private const double LevelLow = 0.04;
    private const double LevelRange = 0.92;
    private const double ReachRadius = 0.08;

    /// <returns>Model yüzü kişi olarak göremediyse null.</returns>
    public static Matte? Compute(
        IMattingModel model, Mat source, FaceData face, PixelRect region, double scale,
        CancellationToken cancellationToken)
    {
        var image = new Mat();
        using (var view = new Mat(source, new Rect(region.X, region.Y, region.Width, region.Height)))
        {
            if (scale < 1)
            {
                var size = new Size(
                    Math.Max(1, (int)Math.Round(region.Width * scale)),
                    Math.Max(1, (int)Math.Round(region.Height * scale)));
                Cv2.Resize(view, image, size, 0, 0, InterpolationFlags.Area);
            }
            else
            {
                view.CopyTo(image);
            }
        }

        var alpha = new Mat();
        var keep = false;
        try
        {
            using (var small = model.Predict(image, cancellationToken))
                Cv2.Resize(small, alpha, image.Size(), 0, 0, InterpolationFlags.Cubic);
            Clamp(alpha);

            double scaleX = (double)image.Width / region.Width, scaleY = (double)image.Height / region.Height;
            PointD ToWork(PointD p) => new((p.X - region.X) * scaleX, (p.Y - region.Y) * scaleY);
            var corner = ToWork(new PointD(face.Box.Left, face.Box.Top));
            var faceBox = new RectD(corner.X, corner.Y, face.Box.Width * scaleX, face.Box.Height * scaleY);

            alpha.GetArray(out float[] raw);
            if (MatteAnalysis.FaceCoverage(raw, alpha.Width, alpha.Height, faceBox) < MatteAnalysis.MinFaceCoverage)
                return null;

            var unit = CropGeometry.FaceUnit(face.Box) * scaleX;
            var radius = Math.Max(2, (int)Math.Round(unit * GuideRadius));
            using (var refined = new Mat())
            {
                // Kılavuz 8 bit olmalı; ondalıklı 3 kanallı kılavuzda süzgeç yalnızca bulanıklaştırıyor.
                CvXImgProc.GuidedFilter(image, alpha, refined, radius, GuideEpsilon);
                // Düzeyler: (a - 0,04) / 0,92
                refined.ConvertTo(alpha, MatType.CV_32FC1, 1.0 / LevelRange, -LevelLow / LevelRange);
            }
            Clamp(alpha);

            alpha.GetArray(out float[] values);
            var person = MatteAnalysis.FindPerson(
                values, alpha.Width, alpha.Height, ToWork(face.Box.Center), faceBox.Width, face.RollDegrees);
            if (person is null) return null;

            RemoveOtherRegions(alpha, person.Mask, Math.Max(3, (int)(ReachRadius * faceBox.Width)));

            PointD? headTop = person.HeadTop is { } top
                ? new PointD(top.X / scaleX + region.X, top.Y / scaleY + region.Y)
                : null;
            keep = true;
            return new Matte { Region = region, Image = image, Alpha = alpha, HeadTop = headTop };
        }
        finally
        {
            if (!keep)
            {
                image.Dispose();
                alpha.Dispose();
            }
        }
    }

    /// <summary>Kişiden kopuk parçaları siler; ince saç telleri için kişinin çevresine pay bırakılır.</summary>
    private static void RemoveOtherRegions(Mat alpha, byte[] personMask, int reach)
    {
        using var mask = new Mat(alpha.Rows, alpha.Cols, MatType.CV_8UC1);
        mask.SetArray(personMask);
        using var element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2 * reach + 1, 2 * reach + 1));
        using var grown = new Mat();
        Cv2.Dilate(mask, grown, element);
        using var weight = new Mat();
        grown.ConvertTo(weight, MatType.CV_32FC1, 1.0 / 255.0);
        Cv2.Multiply(alpha, weight, alpha);
    }

    private static void Clamp(Mat values)
    {
        Cv2.Threshold(values, values, 1, 1, ThresholdTypes.Trunc);
        Cv2.Threshold(values, values, 0, 0, ThresholdTypes.Tozero);
    }
}
