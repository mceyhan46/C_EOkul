using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Imaging;
using EOkulStudio.Core.Processing;
using EOkulStudio.Core.Results;
using OpenCvSharp;

namespace EOkulStudio.Imaging;

/// <summary>Karşılaştırma görselleri için ara görüntüler. Çağrı bittiğinde yok edilirler.</summary>
/// <param name="Original">Aynı kırpma, arka plan temizlenmeden.</param>
/// <param name="Alpha">Kişi maskesi (gri).</param>
/// <param name="WithoutDecontamination">Renk arındırma yapılmadan beyaza oturtulmuş hali.</param>
/// <param name="OnDark">Arındırılmış kişi koyu zeminde: beyaz hale burada görünür.</param>
/// <param name="Result">Kaydedilen JPEG'in açılmış hali (hedef boyutta).</param>
public sealed record PipelineImages(
    Mat Original, Mat Alpha, Mat WithoutDecontamination, Mat OnDark, Mat Result, MatteStats Stats, double Roll);

public interface IPipelineInspector
{
    void Inspect(string sourceName, PipelineImages images);
}

/// <summary>
/// Bir fotoğrafı sırayla işler: açma ve yön düzeltme, yüz bulma, arka plan maskesi, maske inceltme, renk arındırma
/// ve beyaza oturtma, eğimi düzelterek kırpma, küçültme ve JPEG.
/// </summary>
public sealed class PhotoPipeline : IPhotoPipeline, IDisposable
{
    /// <summary>Karşılaştırma görsellerinin hedef boyuta oranı.</summary>
    private const int InspectionScale = 3;

    private static readonly Scalar White = Scalar.All(255);
    private static readonly Scalar Dark = new(60, 40, 30);

    private readonly FaceDetector _detector;
    private readonly FaceAnalyzer _faces;
    private readonly IMattingModel _matting;
    private readonly IPipelineInspector? _inspector;

    public PhotoPipeline(FaceDetector detector, IMattingModel matting, string modelId, IPipelineInspector? inspector = null)
    {
        _detector = detector;
        _faces = new FaceAnalyzer(detector);
        _matting = matting;
        _inspector = inspector;
        ModelId = modelId;
    }

    public string ModelId { get; }

    /// <summary>Modelleri program klasöründen yükler.</summary>
    /// <param name="modelThreads">Bir model çalıştırmasının kullanacağı iş parçacığı sayısı.</param>
    public static PhotoPipeline Create(string baseDirectory, int modelThreads, IPipelineInspector? inspector = null)
    {
        NativeRuntime.PreloadVcRuntime(baseDirectory);
        var store = ModelStore.InDirectory(baseDirectory);
        var face = store.Load(ModelStore.FaceFile);
        var matting = new ModnetModel(store.Load(ModelStore.MattingFile), modelThreads);
        Cv2.SetNumThreads(Math.Max(1, modelThreads));
        return new PhotoPipeline(new FaceDetector(face.Path), matting, $"{matting.Id} {face.Id}", inspector);
    }

    public PipelineResult Process(
        byte[] source,
        string sourceName,
        PipelineOptions options,
        IProgress<PipelineStage>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        void Enter(PipelineStage stage)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(stage);
        }

        Enter(PipelineStage.Reading);
        Mat image;
        try
        {
            image = ImageDecoder.Decode(source);
        }
        catch (Exception ex) when (ex is InvalidDataException or OpenCVException)
        {
            return PipelineResult.Failed(IssueCode.CorruptFile);
        }

        using (image)
        {
            Enter(PipelineStage.FindingFace);
            var found = _faces.Find(image, cancellationToken);
            if (found.Face is not { } face) return PipelineResult.Failed(found.Problem ?? IssueCode.NoFace);

            Enter(PipelineStage.RemovingBackground);
            var rules = options.Crop.Normalized();
            var provisional = CropPlanner.Plan(face, null, image.Width, image.Height, rules);
            var matte = FindMatte(image, face, provisional.Crop, cancellationToken);
            if (matte is null)
                return new PipelineResult(null, [IssueCode.BackgroundFailed], provisional.Crop);

            try
            {
                var plan = CropPlanner.Plan(face, matte.HeadTop, image.Width, image.Height, rules);

                // Ölçülen saç tepesiyle kırpma bölgenin dışına taştıysa maske geniş bölgede yeniden çıkarılır.
                var needed = RoiPlanner.CropBounds(plan.Crop, image.Width, image.Height);
                if (!matte.Region.Contains(needed))
                {
                    var again = Matting.Compute(_matting, image, face, matte.Region.Union(needed),
                        RoiPlanner.WorkingScale(plan.Crop.Height), cancellationToken);
                    if (again is not null)
                    {
                        matte.Dispose();
                        matte = again;
                        plan = CropPlanner.Plan(face, matte.HeadTop, image.Width, image.Height, rules);
                    }
                }

                Enter(PipelineStage.Rendering);
                return Render(image, sourceName, face, matte, plan, options, cancellationToken);
            }
            finally
            {
                matte.Dispose();
            }
        }
    }

    private Matte? FindMatte(Mat image, FaceData face, RotatedCrop crop, CancellationToken cancellationToken)
    {
        var scale = RoiPlanner.WorkingScale(crop.Height);
        foreach (var wide in (bool[])[false, true])
        {
            var region = RoiPlanner.Compute(face.Box, crop, image.Width, image.Height, wide);
            var matte = Matting.Compute(_matting, image, face, region, scale, cancellationToken);
            if (matte is not null) return matte;
        }
        return null;
    }

    private PipelineResult Render(
        Mat image, string sourceName, FaceData face, Matte matte, CropPlan plan, PipelineOptions options,
        CancellationToken cancellationToken)
    {
        int width = options.Crop.Normalized().TargetWidth, height = options.Crop.Normalized().TargetHeight;
        var unit = CropGeometry.FaceUnit(face.Box) * matte.ScaleX;

        using var composite = Compositor.OnWhite(matte.Image, matte.Alpha, unit);
        cancellationToken.ThrowIfCancellationRequested();
        using var output = Renderer.Output(composite, matte.Region, plan.Crop, width, height, White);
        var stats = MeasureMatte(matte, face, plan.Crop, width, height);

        var issues = plan.Issues.ToList();
        var quality = MeasureQuality(image, plan.Crop);
        if (quality.IsBlurry) issues.Add(IssueCode.Blurry);
        if (quality.IsDark) issues.Add(IssueCode.LowLight);
        if (stats.IsDoubtful) issues.Add(IssueCode.UncertainBackground);

        var jpeg = JpegFile.Fit(
            q => Renderer.EncodeJpeg(output, q), options.JpegQuality, options.MinBytes, options.MaxBytes);
        if (!jpeg.WithinLimits) issues.Add(IssueCode.FileSizeOutOfRange);

        if (_inspector is not null)
            Inspect(sourceName, matte, plan.Crop, unit, jpeg.Data, width, height, stats, face.RollDegrees);

        return new PipelineResult(jpeg.Data, issues, plan.Crop);
    }

    private static MatteStats MeasureMatte(Matte matte, FaceData face, RotatedCrop crop, int width, int height)
    {
        using var scaled = new Mat();
        matte.Alpha.ConvertTo(scaled, MatType.CV_32FC1, 255);
        using var warped = Renderer.Warp(scaled, matte.Region, crop, width, height, InterpolationFlags.Linear, Scalar.All(0));
        using var bytes = new Mat();
        warped.ConvertTo(bytes, MatType.CV_8UC1);
        bytes.GetArray(out byte[] alpha);

        // Göz hizasının çıktıdaki satırı.
        var eyes = new PointD((face.EyeA.X + face.EyeB.X) / 2, (face.EyeA.Y + face.EyeB.Y) / 2)
            .RotateAround(crop.Center, -crop.AngleDegrees);
        var eyeRow = (int)((eyes.Y - (crop.CenterY - crop.Height / 2)) / crop.Height * height);
        return MatteAnalysis.Measure(alpha, width, height, eyeRow);
    }

    private static QualityReport MeasureQuality(Mat image, RotatedCrop crop)
    {
        const int maxWidth = 640;
        var bounds = RoiPlanner.CropBounds(crop, image.Width, image.Height);
        using var view = new Mat(image, new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        using var sample = new Mat();
        if (view.Width > maxWidth)
        {
            var size = new Size(maxWidth, Math.Max(1, (int)Math.Round((double)view.Height * maxWidth / view.Width)));
            Cv2.Resize(view, sample, size, 0, 0, InterpolationFlags.Area);
        }
        else
        {
            view.CopyTo(sample);
        }
        return Quality.Analyze(Renderer.Bytes(sample), sample.Width, sample.Height,
            new RectD(0, 0, sample.Width, sample.Height));
    }

    private void Inspect(
        string sourceName, Matte matte, RotatedCrop crop, double unit, byte[] jpeg, int width, int height,
        MatteStats stats, double roll)
    {
        int w = width * InspectionScale, h = height * InspectionScale;
        Mat Large(Mat image, Scalar fill) =>
            Renderer.Warp(image, matte.Region, crop, w, h, InterpolationFlags.Cubic, fill);

        using var original = Large(matte.Image, White);
        using var gray = new Mat();
        matte.Alpha.ConvertTo(gray, MatType.CV_8UC1, 255);
        using var alpha = Large(gray, Scalar.All(0));
        using var plain = Compositor.Plain(matte.Image, matte.Alpha, White);
        using var plainLarge = Large(plain, White);
        using var dark = Compositor.On(matte.Image, matte.Alpha, unit, Dark);
        using var darkLarge = Large(dark, Dark);
        using var result = Cv2.ImDecode(jpeg, ImreadModes.Color);
        _inspector!.Inspect(sourceName,
            new PipelineImages(original, alpha, plainLarge, darkLarge, result, stats, roll));
    }

    public void Dispose()
    {
        _detector.Dispose();
        _matting.Dispose();
    }
}
