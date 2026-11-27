namespace EOkulStudio.Core.Cropping;

/// <summary>Dik (eğimi düzeltilmiş) koordinatlarda kırpma hesabı. Android sürümündeki Crop sınıfından taşınmıştır.</summary>
public static class CropGeometry
{
    /// <summary>Göz ortasının yüz kutusu ortasından en çok bu kadar (yüz genişliği oranı) sapmasına güvenilir.</summary>
    public const double CenterTrust = 0.25;

    /// <summary>Yüz büyüklüğü birimi: uzun ve kare yüz kutularına karşı dayanıklı.</summary>
    public static double FaceUnit(RectD face) => 0.5 * (face.Height + 1.25 * face.Width);

    /// <summary>Yüzün yatay merkezi: göz ortası, yoksa burun; kutu ortasından fazla sapıyorsa kutu ortası.</summary>
    public static double FaceCenterX(RectD face, (PointD A, PointD B)? eyes, double? noseX)
    {
        var boxCenter = face.Center.X;
        var candidate = eyes is { } pair ? (pair.A.X + pair.B.X) / 2.0 : noseX;
        if (candidate is not { } value || !double.IsFinite(value)) return boxCenter;
        return Math.Abs(value - boxCenter) > CenterTrust * Math.Max(1.0, face.Width) ? boxCenter : value;
    }

    /// <summary>
    /// Boşluk kurallarını sağlayan, hedef oranlı kırpma dikdörtgeni. Görüntü sınırına göre kısılmaz:
    /// taşan kısım beyaz doldurulur.
    /// </summary>
    /// <param name="face">Yüz kutusu.</param>
    /// <param name="headTop">Ölçülen saç tepesi y'si; bilinmiyorsa null.</param>
    /// <param name="centerX">Yüzün yatay merkezi.</param>
    /// <returns>Dikdörtgen ve (hedef boyuttan küçük kaldığı için) büyütülüp büyütülmediği.</returns>
    public static (RectD Box, bool Enlarged) ComputeBox(RectD face, double? headTop, double centerX, CropRules rules)
    {
        rules = rules.Normalized();
        var unit = FaceUnit(face);
        var measured = headTop is not null;
        var top = headTop ?? face.Top - unit * rules.HairAllowance;
        top = Math.Min(top, face.Top - unit * (measured ? rules.MeasuredHairMin : rules.HairMin));
        var headHeight = Math.Max(face.Bottom - top, 1.0);

        var vertical = headHeight / Math.Max(0.10, 1.0 - rules.TopGap - rules.BottomGap);
        var horizontal = Math.Max(1.0, face.Width)
                         / (rules.Ratio * Math.Max(0.10, 1.0 - rules.LeftGap - rules.RightGap));
        var natural = Math.Max(vertical, horizontal);

        var height = Math.Max(natural, rules.TargetHeight);
        var width = height * rules.Ratio;
        var left = centerX - width * (1.0 + rules.LeftGap - rules.RightGap) / 2.0;
        return (new RectD(left, top - rules.TopGap * height, width, height), natural < rules.TargetHeight);
    }
}
