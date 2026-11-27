namespace EOkulStudio.Core.Cropping;

/// <summary>Yüz bulucunun sonucu (kaynak koordinatları). Gözlerin sırası önemsizdir.</summary>
public sealed record FaceData(RectD Box, PointD EyeA, PointD EyeB, PointD? Nose = null)
{
    public PointD ImageLeftEye => EyeA.X <= EyeB.X ? EyeA : EyeB;
    public PointD ImageRightEye => EyeA.X <= EyeB.X ? EyeB : EyeA;

    /// <summary>Göz çizgisinin yatayla açısı (-90..90 derece).</summary>
    public double RollDegrees =>
        Math.Atan2(ImageRightEye.Y - ImageLeftEye.Y, ImageRightEye.X - ImageLeftEye.X) * 180.0 / Math.PI;
}
