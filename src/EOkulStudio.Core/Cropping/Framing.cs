using EOkulStudio.Core.Results;

namespace EOkulStudio.Core.Cropping;

/// <summary>Kadraj denetimi: yüz ve gözlerin kırpma içindeki konumu. Android sürümündeki Framing sınıfından taşınmıştır.</summary>
public static class Framing
{
    /// <summary>Tüm koordinatlar aynı dik koordinat sistemindedir.</summary>
    public static IReadOnlyList<IssueCode> Check(
        RectD face,
        double? headTop,
        (PointD A, PointD B)? eyes,
        RectD box,
        CropRules rules,
        double? centerX = null)
    {
        var issues = new List<IssueCode>();
        if (box.Width <= 0 || box.Height <= 0) return issues;

        if ((headTop ?? face.Top) < box.Top) issues.Add(IssueCode.HeadCut);
        else if (headTop is { } top && top - box.Top < 0.012 * box.Height) issues.Add(IssueCode.HeadTight);

        if (face.Bottom > box.Bottom - 0.02 * box.Height) issues.Add(IssueCode.ChinCut);

        var faceHeight = face.Height / box.Height;
        if (faceHeight < rules.FaceHeightMin) issues.Add(IssueCode.FaceSmallInFrame);
        else if (faceHeight > rules.FaceHeightMax) issues.Add(IssueCode.FaceLargeInFrame);

        if (Math.Abs((centerX ?? face.Center.X) - box.Center.X) > 0.15 * box.Width) issues.Add(IssueCode.OffCenter);

        if (eyes is { } pair)
        {
            var eyeLine = ((pair.A.Y + pair.B.Y) / 2.0 - box.Top) / box.Height;
            if (eyeLine < rules.EyeLineMin || eyeLine > rules.EyeLineMax) issues.Add(IssueCode.EyeLevel);
            var angle = Math.Abs(Math.Atan2(pair.B.Y - pair.A.Y, pair.B.X - pair.A.X) * 180.0 / Math.PI);
            if (angle > 8.0 && angle < 172.0) issues.Add(IssueCode.EyeTilt);
        }

        return issues;
    }
}
