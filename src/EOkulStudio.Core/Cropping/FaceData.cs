namespace EOkulStudio.Core.Cropping;

/// <summary>Yüz bulucunun sonucu (kaynak koordinatları). Gözlerin sırası önemsizdir.</summary>
/// <param name="Roll">
/// Ayrıca ölçülen baş eğimi (derece, saat yönü pozitif). Verilmezse göz çizgisinden hesaplanır; yüz bulucunun
/// göz noktaları eğik başlarda eğimi olduğundan çok küçük gösterdiği için boru hattı bu değeri doldurur.
/// </param>
public sealed record FaceData(RectD Box, PointD EyeA, PointD EyeB, PointD? Nose = null, double? Roll = null)
{
    public PointD ImageLeftEye => EyeA.X <= EyeB.X ? EyeA : EyeB;
    public PointD ImageRightEye => EyeA.X <= EyeB.X ? EyeB : EyeA;

    /// <summary>Baş eğimi: ölçülmüşse o değer, yoksa göz çizgisinin yatayla açısı (-90..90 derece).</summary>
    public double RollDegrees => Roll ??
        Math.Atan2(ImageRightEye.Y - ImageLeftEye.Y, ImageRightEye.X - ImageLeftEye.X) * 180.0 / Math.PI;
}
