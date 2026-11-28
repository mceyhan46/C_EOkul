using EOkulStudio.Core.Cropping;

namespace EOkulStudio.Core.Imaging;

/// <summary>Yüzün çevresinden alınıp döndürülen, yüz bulucuya verilen kare örnek.</summary>
/// <param name="Scale">Kare kenarının yüz boyutuna oranı.</param>
/// <param name="Size">Karenin piksel boyutu (yüz bulucu girişi).</param>
/// <param name="AngleDegrees">Düzeltilmesi denenen baş eğimi (saat yönü pozitif).</param>
/// <param name="Mirrored">Kare yatay aynalanmış mı?</param>
public readonly record struct RollProbe(double Scale, int Size, double AngleDegrees, bool Mirrored);

/// <summary>Örnekte yüz bulur; 5 noktayı (göz, göz, burun, ağız, ağız) örnek koordinatında döndürür.</summary>
public delegate IReadOnlyList<PointD>? ProbeLandmarks(RollProbe probe);

/// <summary>
/// Baş eğimini ölçer. YuNet'in nokta konumları eğik yüzlerde eğimi olduğundan çok küçük gösterir; bu yüzden
/// yüz, tahmin edilen eğim kadar geri döndürülüp kalan eğim yeniden ölçülür.
/// </summary>
public static class RollEstimator
{
    public const int MaxIterations = 6;
    public const double MaxRoll = 45;

    /// <summary>Kalan eğim olduğundan küçük ölçüldüğü için adım büyütülür.</summary>
    public const double Gain = 1.8;

    public const double Tolerance = 0.5;

    public static IReadOnlyList<(double Scale, int Size)> Probes { get; } = [(2.0, 160), (3.0, 320), (2.5, 224)];

    public static double Estimate(ProbeLandmarks detect, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detect);
        var total = 0.0;
        var values = new List<double>(Probes.Count * 2);
        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            values.Clear();
            foreach (var (scale, size) in Probes)
            {
                foreach (var mirrored in (ReadOnlySpan<bool>)[false, true])
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (detect(new RollProbe(scale, size, total, mirrored)) is not { Count: >= 5 } points) continue;
                    var angle = Residual(points);
                    values.Add(mirrored ? -angle : angle);
                }
            }
            if (values.Count == 0) break;

            var step = TrimmedMean(values);
            total = Math.Clamp(total + Gain * step, -MaxRoll, MaxRoll);
            if (Math.Abs(step) < Tolerance) break;
        }
        return total;
    }

    /// <summary>Göz çizgisi, ağız çizgisi ve göz-ağız ekseninin ortalama eğimi (derece).</summary>
    public static double Residual(IReadOnlyList<PointD> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var eyes = Degrees(Math.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X));
        var mouth = Degrees(Math.Atan2(points[4].Y - points[3].Y, points[4].X - points[3].X));
        var eyeMid = new PointD((points[0].X + points[1].X) / 2, (points[0].Y + points[1].Y) / 2);
        var mouthMid = new PointD((points[3].X + points[4].X) / 2, (points[3].Y + points[4].Y) / 2);
        var axis = Degrees(Math.Atan2(eyeMid.X - mouthMid.X, mouthMid.Y - eyeMid.Y));
        return (eyes + mouth + axis) / 3;
    }

    /// <summary>
    /// Örnek karedeki noktayı kaynak koordinatına çevirir.
    /// </summary>
    /// <param name="point">Örnek karedeki (aynalanmamış) nokta.</param>
    /// <param name="faceCenter">Karenin ortasına gelen kaynak noktası.</param>
    /// <param name="faceSide">Yüz kutusunun uzun kenarı (kaynak pikseli).</param>
    public static PointD ToSource(PointD point, RollProbe probe, PointD faceCenter, double faceSide)
    {
        var scale = faceSide * probe.Scale / probe.Size;
        var half = probe.Size / 2.0;
        return new PointD(faceCenter.X + (point.X - half) * scale, faceCenter.Y + (point.Y - half) * scale)
            .RotateAround(faceCenter, probe.AngleDegrees);
    }

    /// <summary>En küçük ve en büyük beşte birlik dilim atılarak ortalama.</summary>
    private static double TrimmedMean(List<double> values)
    {
        values.Sort();
        var trim = values.Count / 5;
        var sum = 0.0;
        for (var i = trim; i < values.Count - trim; i++) sum += values[i];
        return sum / (values.Count - 2 * trim);
    }

    private static double Degrees(double radians) => radians * 180.0 / Math.PI;
}
