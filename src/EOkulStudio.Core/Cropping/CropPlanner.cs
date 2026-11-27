using EOkulStudio.Core.Results;

namespace EOkulStudio.Core.Cropping;

public sealed record CropPlan(RotatedCrop Crop, IReadOnlyList<IssueCode> Issues, Overflow Overflow);

/// <summary>Yüz verisinden eğimi düzeltilmiş kırpma planı üretir.</summary>
public static class CropPlanner
{
    /// <summary>Alt kenarda bu orandan fazla taşma "omuz kesik" sayılır.</summary>
    public const double BottomOverflowTolerance = 0.01;

    /// <param name="measuredHeadTop">Maskeden ölçülen saç tepesi noktası (kaynak koordinatı); yoksa null.</param>
    public static CropPlan Plan(FaceData face, PointD? measuredHeadTop, int sourceWidth, int sourceHeight, CropRules rules)
    {
        ArgumentNullException.ThrowIfNull(face);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        rules = rules.Normalized();

        var roll = face.RollDegrees;
        var pivot = face.Box.Center;
        var leftEye = face.ImageLeftEye.RotateAround(pivot, -roll);
        var rightEye = face.ImageRightEye.RotateAround(pivot, -roll);
        var noseX = face.Nose?.RotateAround(pivot, -roll).X;
        var headTop = measuredHeadTop?.RotateAround(pivot, -roll).Y;

        // Yüz kutusu eksenlere paralel bulunur; küçük eğimlerde boyutu korunur, merkez dönme noktasıdır.
        var centerX = CropGeometry.FaceCenterX(face.Box, (leftEye, rightEye), noseX);
        var (box, enlarged) = CropGeometry.ComputeBox(face.Box, headTop, centerX, rules);

        var center = box.Center.RotateAround(pivot, roll);
        var crop = new RotatedCrop(center.X, center.Y, box.Width, box.Height, roll);
        var overflow = crop.OverflowOutside(sourceWidth, sourceHeight);

        var issues = new List<IssueCode>();
        if (face.Box.Height < rules.MinFaceHeight) issues.Add(IssueCode.FaceTooSmall);
        if (enlarged) issues.Add(IssueCode.LowResolution);
        if (Math.Abs(roll) > rules.MaxTiltDegrees) issues.Add(IssueCode.ExcessiveTilt);

        var estimatedTop = face.Box.Top - CropGeometry.FaceUnit(face.Box) * rules.HairMin;
        var headCut = measuredHeadTop is { } point ? point.Y <= 1.0 : estimatedTop < 0;
        if (headCut) issues.Add(IssueCode.HeadCutInSource);
        if (overflow.Bottom > BottomOverflowTolerance) issues.Add(IssueCode.ShouldersCut);

        // Eğim düzeltildiği için gözler yataydır; göz eğimi denetlenmez.
        issues.AddRange(Framing.Check(face.Box, headTop, (leftEye, rightEye), box, rules, centerX)
            .Where(code => code != IssueCode.EyeTilt));

        return new CropPlan(crop, issues, overflow);
    }
}
