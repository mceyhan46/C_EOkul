using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Imaging;
using EOkulStudio.Core.Results;
using OpenCvSharp;

namespace EOkulStudio.Imaging;

/// <param name="Face">Tek yüz bulunduysa verisi (görüntü koordinatında).</param>
/// <param name="Problem">Yüz yoksa veya birden fazlaysa nedeni.</param>
public sealed record FaceResult(FaceData? Face, IssueCode? Problem);

/// <summary>Fotoğraftaki yüzü bulur ve baş eğimini ölçer.</summary>
public sealed class FaceAnalyzer(FaceDetector detector)
{
    /// <summary>İlk aramada kabul edilen en düşük güven.</summary>
    public const float AcceptScore = 0.6f;

    /// <summary>En büyük yüzün bu oranından dar yüzler (arkadaki afiş vb.) sayılmaz.</summary>
    public const double IgnoreSmallerThan = 0.35;

    private const double ProbeFaceSide = 160;

    public FaceResult Find(Mat image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var faces = Search(image, 640);
        // Çok yakın çekimde yüz büyük girişte kaçabilir.
        if (faces.Count == 0) faces = Search(image, 320);
        if (faces.Count == 0) return new FaceResult(null, IssueCode.NoFace);

        var widest = faces.Max(f => f.Box.Width);
        var relevant = faces.Where(f => f.Box.Width >= IgnoreSmallerThan * widest).ToList();
        if (relevant.Count > 1) return new FaceResult(null, IssueCode.MultipleFaces);

        return new FaceResult(Measure(image, relevant[0], cancellationToken), null);
    }

    private List<DetectedFace> Search(Mat image, int size) =>
        detector.Detect(image, size).Where(f => f.Score >= AcceptScore).ToList();

    private FaceData Measure(Mat image, DetectedFace face, CancellationToken cancellationToken)
    {
        var center = face.Box.Center;
        var side = Math.Max(face.Box.Width, face.Box.Height);
        var shrink = Math.Min(1.0, ProbeFaceSide / side);

        using var small = new Mat();
        if (shrink < 1) Cv2.Resize(image, small, default, shrink, shrink, InterpolationFlags.Area);
        else image.CopyTo(small);

        DetectedFace? Probe(RollProbe probe)
        {
            var sampleSide = side * shrink * probe.Scale;
            var pivot = new Point2f((float)(center.X * shrink), (float)(center.Y * shrink));
            using var matrix = Cv2.GetRotationMatrix2D(pivot, probe.AngleDegrees, probe.Size / sampleSide);
            matrix.Set(0, 2, matrix.Get<double>(0, 2) + probe.Size / 2.0 - pivot.X);
            matrix.Set(1, 2, matrix.Get<double>(1, 2) + probe.Size / 2.0 - pivot.Y);

            using var sample = new Mat();
            Cv2.WarpAffine(small, sample, matrix, new Size(probe.Size, probe.Size),
                InterpolationFlags.Linear, BorderTypes.Replicate);
            if (probe.Mirrored) Cv2.Flip(sample, sample, FlipMode.Y);

            // Karenin ortasındaki yüz aranır; komşu yüzler karışmasın.
            return detector.DetectSquare(sample)
                .Where(f => Math.Abs(f.Box.Center.X - probe.Size / 2.0) < 0.2 * probe.Size)
                .MaxBy(f => f.Box.Width * f.Box.Height);
        }

        var roll = RollEstimator.Estimate(probe => Probe(probe)?.Points, cancellationToken);

        // Dikleştirilmiş örnekte son bir arama: kutu ve noktalar eğik fotoğraftakinden daha doğrudur.
        var upright = new RollProbe(2.5, 320, roll, false);
        if (Probe(upright) is not { } found)
            return new FaceData(face.Box, face.Points[0], face.Points[1], face.Points[2], roll);

        PointD Back(PointD point) => RollEstimator.ToSource(point, upright, center, side);
        var scale = side * upright.Scale / upright.Size;
        var middle = Back(found.Box.Center);
        double width = found.Box.Width * scale, height = found.Box.Height * scale;
        return new FaceData(
            new RectD(middle.X - width / 2, middle.Y - height / 2, width, height),
            Back(found.Points[0]), Back(found.Points[1]), Back(found.Points[2]), roll);
    }
}
