using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Imaging;
using EOkulStudio.Core.Processing;
using EOkulStudio.Core.Results;
using EOkulStudio.Core.Settings;

namespace EOkulStudio.Core.Tests;

public class RollEstimatorTests
{
    /// <summary>Gerçek eğimi bilinen, eğimi eksik ölçen yapay yüz bulucu.</summary>
    private static ProbeLandmarks Detector(double trueRoll, double sensitivity)
    {
        return probe =>
        {
            var residual = (trueRoll - probe.AngleDegrees) * sensitivity;
            if (probe.Mirrored) residual = -residual;
            PointD[] upright = [new(40, 50), new(80, 50), new(60, 70), new(45, 90), new(75, 90)];
            return upright.Select(p => p.RotateAround(new PointD(60, 70), residual)).ToList();
        };
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-18)]
    [InlineData(30)]
    public void Finds_the_roll_even_when_each_measurement_is_too_small(double trueRoll)
    {
        // Gerçek yüz bulucu eğimi eksik ölçer (duyarlılık 1'den küçük); büyütme katsayısı buna göredir.
        foreach (var sensitivity in new[] { 0.8, 0.6, 0.4 })
        {
            var estimate = RollEstimator.Estimate(Detector(trueRoll, sensitivity));

            estimate.Should().BeApproximately(trueRoll, 3.0, $"duyarlılık {sensitivity}");
        }
    }

    [Fact]
    public void Roll_is_limited()
    {
        RollEstimator.Estimate(Detector(80, 0.6)).Should().Be(RollEstimator.MaxRoll);
    }

    [Fact]
    public void No_detection_means_no_roll()
    {
        RollEstimator.Estimate(_ => null).Should().Be(0);
        RollEstimator.Estimate(_ => [new PointD(0, 0)]).Should().Be(0);
    }

    [Fact]
    public void Cancellation_stops_the_estimate()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = () => RollEstimator.Estimate(_ => null, source.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Probe_points_map_back_to_the_source()
    {
        var probe = new RollProbe(2.0, 200, 90, false);
        var center = new PointD(500, 400);

        RollEstimator.ToSource(new PointD(100, 100), probe, center, 300).Should().Be(center);
        // Örnekte sağa 10 piksel = kaynakta 30 piksel; 90 derece saat yönünde döndürülünce aşağı.
        var point = RollEstimator.ToSource(new PointD(110, 100), probe, center, 300);
        point.X.Should().BeApproximately(500, 1e-9);
        point.Y.Should().BeApproximately(430, 1e-9);
    }

    [Fact]
    public void Measured_roll_overrides_the_eye_line()
    {
        var face = new FaceData(new RectD(0, 0, 100, 120), new PointD(30, 40), new PointD(70, 40));

        face.RollDegrees.Should().Be(0);
        (face with { Roll = 12.5 }).RollDegrees.Should().Be(12.5);
    }
}

public class MatteAnalysisTests
{
    private static float[] Mask(int width, int height, params (int X, int Y, int W, int H)[] boxes)
    {
        var alpha = new float[width * height];
        foreach (var box in boxes)
            for (var y = box.Y; y < box.Y + box.H; y++)
                for (var x = box.X; x < box.X + box.W; x++)
                    alpha[y * width + x] = 1;
        return alpha;
    }

    [Fact]
    public void Face_coverage_is_the_mean_of_the_inner_face_box()
    {
        var alpha = Mask(100, 100, (0, 0, 50, 100));

        MatteAnalysis.FaceCoverage(alpha, 100, 100, new RectD(0, 0, 40, 40)).Should().Be(1);
        MatteAnalysis.FaceCoverage(alpha, 100, 100, new RectD(60, 10, 30, 30)).Should().Be(0);
        MatteAnalysis.FaceCoverage(alpha, 100, 100, new RectD(30, 10, 40, 40)).Should().BeApproximately(0.5, 0.01);
        MatteAnalysis.FaceCoverage(alpha, 100, 100, new RectD(300, 300, 40, 40)).Should().Be(0);
    }

    [Fact]
    public void Keeps_only_the_region_that_holds_the_face()
    {
        // Gövde, ona bitişik saç ve kopuk bir nesne.
        var alpha = Mask(100, 100, (30, 30, 40, 70), (45, 12, 10, 18), (2, 2, 10, 10));

        var region = MatteAnalysis.FindPerson(alpha, 100, 100, new PointD(50, 50), 30, 0)!;

        region.Mask[50 * 100 + 50].Should().Be(255);
        region.Mask[15 * 100 + 50].Should().Be(255);
        region.Mask[5 * 100 + 5].Should().Be(0);
        region.HeadTop.Should().Be(new PointD(45.5, 12));
    }

    [Fact]
    public void Head_top_ignores_pixels_far_from_the_face_axis()
    {
        // Yana kaldırılmış el, başın üstünden yüksekte olsa da baş tepesi sayılmaz.
        var alpha = Mask(100, 100, (30, 30, 60, 70), (80, 5, 8, 25));

        var region = MatteAnalysis.FindPerson(alpha, 100, 100, new PointD(50, 55), 30, 0)!;

        region.HeadTop!.Value.Y.Should().Be(30);
    }

    [Fact]
    public void Head_top_follows_the_head_tilt()
    {
        var alpha = Mask(100, 100, (20, 20, 60, 80));

        var tilted = MatteAnalysis.FindPerson(alpha, 100, 100, new PointD(50, 60), 40, 30)!;

        // 30 derece sağa yatık başta "yukarı" sağ üsttür.
        tilted.HeadTop!.Value.Y.Should().Be(20);
        tilted.HeadTop!.Value.X.Should().BeGreaterThan(55);
    }

    [Fact]
    public void Face_on_background_gives_no_region()
    {
        var alpha = Mask(50, 50, (0, 0, 10, 10));

        MatteAnalysis.FindPerson(alpha, 50, 50, new PointD(30, 30), 10, 0).Should().BeNull();
        alpha[30 * 50 + 30] = float.NaN;
        MatteAnalysis.FindPerson(alpha, 50, 50, new PointD(30, 30), 10, 0).Should().BeNull();
    }

    [Fact]
    public void Clean_mask_is_not_doubtful()
    {
        var alpha = new byte[133 * 171];
        for (var y = 30; y < 171; y++)
            for (var x = 20; x < 110; x++)
                alpha[y * 133 + x] = 255;

        var stats = MatteAnalysis.Measure(alpha, 133, 171, 70);

        stats.Should().Be(new MatteStats(0, 0));
        stats.IsDoubtful.Should().BeFalse();
    }

    [Fact]
    public void Soft_or_edge_touching_masks_are_doubtful()
    {
        var soft = new byte[100 * 100];
        Array.Fill(soft, (byte)255, 0, 5000);
        Array.Fill(soft, (byte)128, 5000, 1000);
        MatteAnalysis.Measure(soft, 100, 100, 40).Uncertain.Should().BeApproximately(0.2, 1e-9);
        MatteAnalysis.Measure(soft, 100, 100, 40).IsDoubtful.Should().BeTrue();

        var side = new byte[100 * 100];
        for (var y = 0; y < 40; y++) side[y * 100 + 99] = 255;
        var stats = MatteAnalysis.Measure(side, 100, 100, 40);
        stats.SideForeground.Should().BeApproximately(0.1, 1e-9);
        stats.IsDoubtful.Should().BeTrue();
    }

    [Fact]
    public void Wrong_size_is_rejected()
    {
        var act = () => MatteAnalysis.Measure(new byte[10], 4, 4, 1);

        act.Should().Throw<ArgumentException>();
    }
}

public class RoiPlannerTests
{
    private static readonly RectD Face = new(900, 600, 200, 260);

    [Fact]
    public void Region_covers_the_crop_and_the_shoulders()
    {
        var crop = new RotatedCrop(1000, 800, 500, 643, 10);

        var roi = RoiPlanner.Compute(Face, crop, 3000, 3000);

        roi.Contains(RoiPlanner.CropBounds(crop, 3000, 3000)).Should().BeTrue();
        var unit = CropGeometry.FaceUnit(Face);
        roi.X.Should().BeLessThanOrEqualTo((int)(1000 - 2 * unit));
        roi.Bottom.Should().BeGreaterThanOrEqualTo((int)(Face.Bottom + 1.6 * unit));
        RoiPlanner.Compute(Face, crop, 3000, 3000, wide: true).Contains(roi).Should().BeTrue();
    }

    [Fact]
    public void Region_stays_inside_the_image()
    {
        var crop = new RotatedCrop(1000, 600, 900, 1157, 0);

        var roi = RoiPlanner.Compute(Face, crop, 1100, 900);

        roi.Should().Be(new PixelRect(460, 0, 640, 900));
    }

    [Fact]
    public void Region_is_never_empty()
    {
        var outside = new RotatedCrop(-5000, -5000, 100, 128, 0);

        RoiPlanner.CropBounds(outside, 200, 200).Should().Be(new PixelRect(0, 0, 1, 1));
    }

    [Fact]
    public void Rectangles_unite_and_contain()
    {
        var union = new PixelRect(0, 0, 10, 10).Union(new PixelRect(5, 20, 10, 5));

        union.Should().Be(new PixelRect(0, 0, 15, 25));
        union.Contains(new PixelRect(5, 20, 10, 5)).Should().BeTrue();
        union.Contains(new PixelRect(10, 20, 10, 5)).Should().BeFalse();
    }

    [Fact]
    public void Only_large_crops_are_scaled_down()
    {
        RoiPlanner.WorkingScale(400).Should().Be(1);
        RoiPlanner.WorkingScale(684).Should().Be(1);
        RoiPlanner.WorkingScale(1368).Should().Be(0.5);
    }

    [Theory]
    [InlineData(1000, 1000, 512, 512)]
    [InlineData(1500, 1000, 768, 512)]
    [InlineData(1000, 1400, 512, 704)]
    [InlineData(1001, 1000, 512, 512)]
    public void Model_input_has_a_512_short_side_in_multiples_of_32(int w, int h, int expectedW, int expectedH)
    {
        ModelInput.MattingSize(w, h).Should().Be((expectedW, expectedH));
    }
}

public class PipelineOptionTests
{
    [Fact]
    public void Options_come_from_normalized_settings()
    {
        var options = PipelineOptions.From(new AppSettings { JpegQuality = 500 });

        options.JpegQuality.Should().Be(100);
        options.MinBytes.Should().Be(20 * 1024 + 200);
        options.MaxBytes.Should().Be(150 * 1024);
        (options with { MinFileSizeKb = 0 }).MinBytes.Should().Be(0);
    }

    [Fact]
    public void Version_changes_only_when_an_output_setting_changes()
    {
        var first = PipelineOptions.From(new AppSettings());
        var same = PipelineOptions.From(new AppSettings { AutoStart = false });
        var other = PipelineOptions.From(new AppSettings { JpegQuality = 90 });

        first.Version.Should().MatchRegex("^1:[0-9a-f]{8}$");
        same.Version.Should().Be(first.Version);
        other.Version.Should().NotBe(first.Version);
    }

    [Fact]
    public void Failed_result_has_no_output()
    {
        var result = PipelineResult.Failed(IssueCode.NoFace);

        result.Jpeg.Should().BeNull();
        result.Crop.Should().BeNull();
        result.Issues.Should().Equal(IssueCode.NoFace);
    }

    [Theory]
    [InlineData(1, null, 1, 1)]
    [InlineData(2, null, 1, 2)]
    [InlineData(8, null, 2, 4)]
    [InlineData(32, null, 4, 8)]
    [InlineData(8, 8, 8, 1)]
    [InlineData(4, 99, 16, 1)]
    [InlineData(0, 0, 1, 1)]
    public void Cores_are_shared_between_photos_and_model_threads(int cores, int? requested, int workers, int threads)
    {
        ProcessingResources.For(cores, requested).Should().Be(new ProcessingResources(workers, threads));
    }
}
