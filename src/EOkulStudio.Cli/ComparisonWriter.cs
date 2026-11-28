using System.Collections.Concurrent;
using System.Globalization;
using EOkulStudio.Imaging;
using OpenCvSharp;

namespace EOkulStudio.Cli;

/// <summary>
/// Her fotoğraf için bir ızgara görseli yazar. Üst sıra: öncesi, maske, renk arındırmasız, koyu zemin, sonuç.
/// Alt sıra: saç ve baş kenarının büyütülmüşü (renk arındırmasız, arındırılmış koyu zemin, öncesi).
/// </summary>
internal sealed class ComparisonWriter(string directory) : IPipelineInspector
{
    private const int Gap = 8;
    private const int Title = 26;
    private static readonly Scalar Page = new(128, 128, 128);

    private readonly ConcurrentDictionary<string, int> _used = new(StringComparer.OrdinalIgnoreCase);

    public void Inspect(string sourceName, PipelineImages images)
    {
        int width = images.Original.Width, height = images.Original.Height;
        using var alpha = new Mat();
        Cv2.CvtColor(images.Alpha, alpha, ColorConversionCodes.GRAY2BGR);
        using var result = new Mat();
        Cv2.Resize(images.Result, result, new Size(width, height), 0, 0, InterpolationFlags.Nearest);

        (string Label, Mat Image)[] top =
        [
            ("oncesi", images.Original), ("maske", alpha), ("arindirmasiz", images.WithoutDecontamination),
            ("koyu zemin", images.OnDark), ("sonuc " + Describe(images), result),
        ];

        // Baş ve saç kenarı: üst yarı, 2 kat.
        var head = new Rect(width / 8, 0, width * 3 / 4, height / 2);
        (string Label, Mat Source)[] zoom =
        [
            ("sac: oncesi", images.Original), ("sac: arindirmasiz", images.WithoutDecontamination),
            ("sac: sonuc", result), ("sac: koyu zemin", images.OnDark),
        ];

        var pageWidth = Math.Max(top.Length * (width + Gap), zoom.Length * (head.Width * 2 + Gap)) + Gap;
        var pageHeight = Title + height + Title + head.Height * 2 + 2 * Gap;
        using var page = new Mat(pageHeight, pageWidth, MatType.CV_8UC3, Page);

        for (var i = 0; i < top.Length; i++)
            Place(page, top[i].Image, top[i].Label, Gap + i * (width + Gap), 0);

        for (var i = 0; i < zoom.Length; i++)
        {
            using var view = new Mat(zoom[i].Source, head);
            using var enlarged = new Mat();
            Cv2.Resize(view, enlarged, new Size(head.Width * 2, head.Height * 2), 0, 0, InterpolationFlags.Nearest);
            Place(page, enlarged, zoom[i].Label, Gap + i * (head.Width * 2 + Gap), Title + height + Gap);
        }

        if (!Cv2.ImEncode(".jpg", page, out var data, [(int)ImwriteFlags.JpegQuality, 92]))
            throw new InvalidOperationException("Karşılaştırma görseli yazılamadı.");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, NameFor(sourceName)), data);
    }

    private static string Describe(PipelineImages images) => string.Create(CultureInfo.InvariantCulture,
        $"(egim {images.Roll:0.0}, belirsiz {images.Stats.Uncertain:0.000})");

    /// <summary>Farklı klasörlerdeki aynı adlı fotoğraflar birbirinin üzerine yazılmaz.</summary>
    private string NameFor(string sourcePath)
    {
        var name = Path.GetFileName(sourcePath);
        var number = _used.AddOrUpdate(name, 1, (_, previous) => previous + 1);
        return number == 1 ? name + ".karsilastirma.jpg" : $"{name}.{number}.karsilastirma.jpg";
    }

    private static void Place(Mat page, Mat image, string label, int x, int y)
    {
        Cv2.PutText(page, label, new Point(x + 2, y + Title - 8), HersheyFonts.HersheySimplex, 0.55, Scalar.All(0), 1,
            LineTypes.AntiAlias);
        using var target = new Mat(page, new Rect(x, y + Title, image.Width, image.Height));
        image.CopyTo(target);
    }
}
