using System.Text.Json.Serialization;

namespace EOkulStudio.Core.Cropping;

/// <summary>
/// Vesikalık kırpma kuralları. Boşluk varsayılanları Android sürümünün ayar varsayılanlarıdır.
/// Boşluklar kırpma yüksekliğinin (üst, alt) veya genişliğinin (sol, sağ) oranıdır.
/// </summary>
public sealed record CropRules
{
    public int TargetWidth { get; init; } = 133;
    public int TargetHeight { get; init; } = 171;

    /// <summary>Saç tepesinin üstündeki boşluk.</summary>
    public double TopGap { get; init; } = 0.03;

    /// <summary>Çene altındaki boşluk.</summary>
    public double BottomGap { get; init; } = 0.27;

    public double LeftGap { get; init; } = 0.12;
    public double RightGap { get; init; } = 0.12;

    /// <summary>Saç tepesi ölçülemezse yüz kutusunun üstüne eklenen pay (yüz birimi).</summary>
    public double HairAllowance { get; init; } = 0.35;

    /// <summary>Tahmini kullanımda baş üstü en az bu kadar yukarıda sayılır (yüz birimi).</summary>
    public double HairMin { get; init; } = 0.10;

    /// <summary>Saç tepesi arka plan maskesinden ölçüldüğünde kullanılan en az pay.</summary>
    public double MeasuredHairMin { get; init; } = 0.01;

    /// <summary>Bundan eğik başlar "kontrol gerekli" sayılır (derece).</summary>
    public double MaxTiltDegrees { get; init; } = 20;

    /// <summary>Bundan alçak yüz kutusu (kaynak pikseli) "yüz çok küçük" sayılır.</summary>
    public double MinFaceHeight { get; init; } = 80;

    public double EyeLineMin { get; init; } = 0.15;
    public double EyeLineMax { get; init; } = 0.50;
    public double FaceHeightMin { get; init; } = 0.30;
    public double FaceHeightMax { get; init; } = 0.72;

    [JsonIgnore]
    public double Ratio => (double)TargetWidth / TargetHeight;

    /// <summary>Değerleri geçerli aralıklara çeker (elle bozulmuş ayar dosyasına karşı).</summary>
    public CropRules Normalized()
    {
        var defaults = new CropRules();
        var top = Clamp(TopGap, 0.01, 0.25, defaults.TopGap);
        var bottom = Clamp(BottomGap, 0.05, 0.55, defaults.BottomGap);
        var left = Clamp(LeftGap, 0.05, 0.40, defaults.LeftGap);
        var right = Clamp(RightGap, 0.05, 0.40, defaults.RightGap);
        if (top + bottom > 0.85) bottom = 0.85 - top;
        if (left + right > 0.85) right = 0.85 - left;
        var eyeMin = Clamp(EyeLineMin, 0, 1, defaults.EyeLineMin);
        var faceMin = Clamp(FaceHeightMin, 0, 1, defaults.FaceHeightMin);

        return this with
        {
            TargetWidth = Math.Clamp(TargetWidth, 40, 1200),
            TargetHeight = Math.Clamp(TargetHeight, 40, 1600),
            TopGap = top,
            BottomGap = bottom,
            LeftGap = left,
            RightGap = right,
            HairAllowance = Clamp(HairAllowance, 0, 1, defaults.HairAllowance),
            HairMin = Clamp(HairMin, 0, 1, defaults.HairMin),
            MeasuredHairMin = Clamp(MeasuredHairMin, 0, 1, defaults.MeasuredHairMin),
            MaxTiltDegrees = Clamp(MaxTiltDegrees, 1, 45, defaults.MaxTiltDegrees),
            MinFaceHeight = Clamp(MinFaceHeight, 0, 1000, defaults.MinFaceHeight),
            EyeLineMin = eyeMin,
            EyeLineMax = Math.Max(eyeMin, Clamp(EyeLineMax, 0, 1, defaults.EyeLineMax)),
            FaceHeightMin = faceMin,
            FaceHeightMax = Math.Max(faceMin, Clamp(FaceHeightMax, 0, 1, defaults.FaceHeightMax)),
        };
    }

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
