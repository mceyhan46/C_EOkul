using EOkulStudio.Core.Cropping;

namespace EOkulStudio.Core.Imaging;

/// <param name="Mask">Yüzü içeren bağlı kişi bölgesi: 255 içeride, 0 dışarıda.</param>
/// <param name="HeadTop">Bölgenin dik duruşta en üstteki noktası (maske koordinatı); bulunamazsa null.</param>
public sealed record PersonRegion(byte[] Mask, PointD? HeadTop);

/// <param name="Uncertain">Yarı saydam piksellerin kesin kişi piksellerine oranı.</param>
/// <param name="SideForeground">Göz hizasının üstünde, yan kenarlarda kişi sayılan piksellerin oranı.</param>
public readonly record struct MatteStats(double Uncertain, double SideForeground)
{
    public const double UncertainLimit = 0.12;
    public const double SideLimit = 0.005;

    public bool IsDoubtful => Uncertain > UncertainLimit || SideForeground > SideLimit;
}

/// <summary>Arka plan maskesi (0 arka plan, 1 kişi) üzerindeki kurallar. Diziler satır sıralıdır.</summary>
public static class MatteAnalysis
{
    /// <summary>Yüz bölgesinin ortalaması bunun altındaysa maske güvenilmez sayılır.</summary>
    public const double MinFaceCoverage = 0.9;

    /// <summary>Kişi eşiği.</summary>
    public const float Threshold = 0.5f;

    private const int EdgeColumns = 5;

    /// <summary>Yüz kutusunun iç bölümünde (%20–%80) maskenin ortalaması.</summary>
    /// <param name="face">Yüz kutusu (maske koordinatı).</param>
    public static double FaceCoverage(ReadOnlySpan<float> alpha, int width, int height, RectD face)
    {
        CheckSize(alpha.Length, width, height);
        var x0 = Math.Clamp((int)(face.Left + 0.2 * face.Width), 0, width);
        var x1 = Math.Clamp((int)(face.Left + 0.8 * face.Width), 0, width);
        var y0 = Math.Clamp((int)(face.Top + 0.2 * face.Height), 0, height);
        var y1 = Math.Clamp((int)(face.Top + 0.8 * face.Height), 0, height);
        if (x1 <= x0 || y1 <= y0) return 0;

        var sum = 0.0;
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
                sum += alpha[y * width + x];
        return sum / ((double)(x1 - x0) * (y1 - y0));
    }

    /// <summary>
    /// Yüz merkezini içeren bağlı kişi bölgesini (8 komşuluk) ve baş tepesini bulur.
    /// Kopuk parçalar (arkadaki kişi, nesne) bölgeye girmez.
    /// </summary>
    /// <param name="faceCenter">Yüz kutusunun merkezi (maske koordinatı).</param>
    /// <param name="faceWidth">Yüz kutusunun genişliği (maske pikseli).</param>
    /// <param name="rollDegrees">Baş eğimi; "yukarı" yönü buna göre belirlenir.</param>
    /// <returns>Yüz merkezi kişi bölgesinde değilse null.</returns>
    public static PersonRegion? FindPerson(
        ReadOnlySpan<float> alpha, int width, int height, PointD faceCenter, double faceWidth, double rollDegrees)
    {
        CheckSize(alpha.Length, width, height);
        var seedX = Math.Clamp((int)faceCenter.X, 0, width - 1);
        var seedY = Math.Clamp((int)faceCenter.Y, 0, height - 1);
        var seed = seedY * width + seedX;
        if (!(alpha[seed] >= Threshold)) return null;

        var mask = new byte[alpha.Length];
        var pending = new Stack<int>();
        mask[seed] = 255;
        pending.Push(seed);

        var (sin, cos) = Math.SinCos(-rollDegrees * Math.PI / 180.0);
        PointD? top = null;
        var topY = double.MaxValue;

        while (pending.Count > 0)
        {
            var index = pending.Pop();
            int x = index % width, y = index / width;

            // Dik duruştaki konum (yüz merkezi çevresinde -eğim kadar döndürülmüş).
            double dx = x + 0.5 - faceCenter.X, dy = y + 0.5 - faceCenter.Y;
            var uprightX = dx * cos - dy * sin;
            var uprightY = dx * sin + dy * cos;
            if (Math.Abs(uprightX) < 0.6 * faceWidth && uprightY < 0)
            {
                // Eşitlikte sonuç gezinme sırasına bağlı kalmasın.
                if (uprightY < topY - 1e-9
                    || (uprightY <= topY + 1e-9 && top is { } best && (y < best.Y || (y == (int)best.Y && x < best.X))))
                {
                    topY = uprightY;
                    top = new PointD(x + 0.5, y);
                }
            }

            for (var ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
            {
                for (var nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                {
                    var next = ny * width + nx;
                    if (mask[next] != 0 || !(alpha[next] >= Threshold)) continue;
                    mask[next] = 255;
                    pending.Push(next);
                }
            }
        }

        return new PersonRegion(mask, top);
    }

    /// <param name="alpha">Çıktı boyutundaki maske (0..255).</param>
    /// <param name="eyeRow">Çıktıda göz hizasının satırı.</param>
    public static MatteStats Measure(ReadOnlySpan<byte> alpha, int width, int height, int eyeRow)
    {
        CheckSize(alpha.Length, width, height);
        int soft = 0, solid = 0;
        foreach (var value in alpha)
        {
            if (value >= 230) solid++;
            else if (value > 25) soft++;
        }

        eyeRow = Math.Clamp(eyeRow, 1, height);
        var columns = Math.Min(EdgeColumns, width / 2);
        int side = 0, total = 0;
        for (var y = 0; y < eyeRow; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                if (alpha[y * width + x] > 127) side++;
                if (alpha[y * width + width - 1 - x] > 127) side++;
                total += 2;
            }
        }

        return new MatteStats((double)soft / Math.Max(1, solid), total == 0 ? 0 : (double)side / total);
    }

    private static void CheckSize(int length, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (length != width * height) throw new ArgumentException("Maske boyutu verilen ölçülerle uyuşmuyor.");
    }
}
