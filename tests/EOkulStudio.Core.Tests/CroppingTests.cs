using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Results;

namespace EOkulStudio.Core.Tests;

public class GeometryTests
{
    [Fact]
    public void Positive_rotation_is_clockwise_on_screen()
    {
        var point = new PointD(1, 0).RotateAround(new PointD(0, 0), 90);

        point.X.Should().BeApproximately(0, 1e-9);
        point.Y.Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void Rectangle_edges_and_center()
    {
        var rect = new RectD(10, 20, 30, 40);

        rect.Right.Should().Be(40);
        rect.Bottom.Should().Be(60);
        rect.Center.Should().Be(new PointD(25, 40));
    }

    [Theory]
    [InlineData(66.5, 85.5, 133, 171, 1, 0, 0)] // birim dönüşüm
    [InlineData(166.5, 285.5, 133, 171, 1, 100, 200)] // öteleme
    [InlineData(133, 171, 266, 342, 2, 0.5, 0.5)] // 2 kat küçültme
    public void Output_to_source_matrix_aligns_pixel_centers(
        double cx, double cy, double width, double height, double scale, double tx, double ty)
    {
        var matrix = new RotatedCrop(cx, cy, width, height, 0).OutputToSourceMatrix(133, 171);

        matrix[0, 0].Should().BeApproximately(scale, 1e-9);
        matrix[0, 1].Should().BeApproximately(0, 1e-9);
        matrix[0, 2].Should().BeApproximately(tx, 1e-9);
        matrix[1, 0].Should().BeApproximately(0, 1e-9);
        matrix[1, 1].Should().BeApproximately(scale, 1e-9);
        matrix[1, 2].Should().BeApproximately(ty, 1e-9);
    }

    [Fact]
    public void Corners_follow_the_rotation()
    {
        var corners = new RotatedCrop(100, 100, 20, 40, 90).Corners();

        corners[0].X.Should().BeApproximately(120, 1e-9);
        corners[0].Y.Should().BeApproximately(90, 1e-9);
    }

    [Theory]
    [InlineData(50, 50, 0, 0, 0, 0)]
    [InlineData(40, 50, 0.1, 0, 0, 0)]
    [InlineData(50, 40, 0, 0.1, 0, 0)]
    [InlineData(60, 50, 0, 0, 0.1, 0)]
    [InlineData(50, 60, 0, 0, 0, 0.1)]
    public void Overflow_is_relative_to_the_crop(
        double cx, double cy, double left, double top, double right, double bottom)
    {
        var overflow = new RotatedCrop(cx, cy, 100, 100, 0).OverflowOutside(100, 100);

        overflow.Left.Should().BeApproximately(left, 1e-9);
        overflow.Top.Should().BeApproximately(top, 1e-9);
        overflow.Right.Should().BeApproximately(right, 1e-9);
        overflow.Bottom.Should().BeApproximately(bottom, 1e-9);
    }
}

public class CropGeometryTests
{
    private static readonly RectD Face = new(100, 100, 200, 200);

    [Theory]
    [InlineData(210, 230, 220)] // göz ortası kullanılır
    [InlineData(260, 280, 200)] // fazla sapan göz ortasına güvenilmez
    public void Center_uses_eyes_when_plausible(double eyeA, double eyeB, double expected) =>
        CropGeometry.FaceCenterX(Face, (new PointD(eyeA, 150), new PointD(eyeB, 150)), null).Should().Be(expected);

    [Fact]
    public void Center_falls_back_to_nose_then_box()
    {
        CropGeometry.FaceCenterX(Face, null, 190).Should().Be(190);
        CropGeometry.FaceCenterX(Face, null, null).Should().Be(200);
        CropGeometry.FaceCenterX(Face, null, double.NaN).Should().Be(200);
    }

    [Fact]
    public void Box_keeps_the_target_ratio_and_gaps()
    {
        var face = new RectD(450, 500, 300, 360);

        var (box, enlarged) = CropGeometry.ComputeBox(face, 440, 600, new CropRules());

        enlarged.Should().BeFalse();
        box.Height.Should().BeApproximately(600, 1e-9);
        (box.Width / box.Height).Should().BeApproximately(133.0 / 171.0, 1e-9);
        box.Top.Should().BeApproximately(422, 1e-9);
        ((box.Bottom - face.Bottom) / box.Height).Should().BeApproximately(0.27, 1e-9);
    }

    [Fact]
    public void Box_is_never_smaller_than_the_output()
    {
        var (box, enlarged) = CropGeometry.ComputeBox(new RectD(500, 500, 20, 20), null, 510, new CropRules());

        enlarged.Should().BeTrue();
        box.Height.Should().Be(171);
        box.Width.Should().BeApproximately(133, 1e-9);
    }

    [Fact]
    public void Box_may_extend_beyond_the_image()
    {
        var (box, _) = CropGeometry.ComputeBox(new RectD(0, 0, 300, 360), null, 150, new CropRules());

        box.Left.Should().BeNegative();
        box.Top.Should().BeNegative();
    }
}

/// <summary>Android sürümündeki FramingTest durumları.</summary>
public class FramingTests
{
    private static readonly CropRules Rules = new();
    private static readonly RectD Face = new(450, 500, 300, 360);
    private static readonly RectD Box =
        CropGeometry.ComputeBox(Face, 440, 600, new CropRules { BottomGap = 0.35 }).Box;

    private static IReadOnlyList<IssueCode> Check(RectD box, (PointD, PointD)? eyes = null, double? headTop = 440) =>
        Framing.Check(Face, headTop, eyes, box, Rules);

    private static (PointD, PointD) Eyes(double y, double tilt = 0) =>
        (new PointD(520, y - tilt), new PointD(680, y + tilt));

    [Fact]
    public void Default_crop_has_no_issues() => Check(Box).Should().BeEmpty();

    [Fact]
    public void Head_above_the_frame_is_reported() =>
        Check(Box with { Top = Box.Top + 120 }).Should().Contain(IssueCode.HeadCut);

    [Fact]
    public void Chin_below_the_frame_is_reported() =>
        Check(Box with { Top = Box.Top - 400 }).Should().Contain(IssueCode.ChinCut);

    [Fact]
    public void Off_center_face_is_reported() =>
        Check(Box with { Left = Box.Left + 150 }).Should().Contain(IssueCode.OffCenter);

    [Fact]
    public void Small_face_is_reported() =>
        Framing.Check(new RectD(500, 500, 60, 80), 490, null, new RectD(0, 0, 1200, 1200.0 / 133 * 171), Rules)
            .Should().Contain(IssueCode.FaceSmallInFrame);

    [Fact]
    public void Large_face_is_reported() =>
        Check(new RectD(440, 430, 320, 440)).Should().Contain(IssueCode.FaceLargeInFrame);

    [Fact]
    public void Tight_head_room_is_reported() =>
        Check(Box, headTop: Box.Top + 0.005 * Box.Height).Should().Contain(IssueCode.HeadTight);

    [Fact]
    public void Level_eyes_in_place_are_accepted() =>
        Check(Box, Eyes(Box.Top + 0.30 * Box.Height)).Should().BeEmpty();

    [Fact]
    public void Tilted_eyes_are_reported() =>
        Check(Box, Eyes(Box.Top + 0.30 * Box.Height, tilt: 30)).Should().Contain(IssueCode.EyeTilt);

    [Fact]
    public void Eyes_outside_the_band_are_reported() =>
        Check(Box, Eyes(Box.Top + 5)).Should().Contain(IssueCode.EyeLevel);

    [Fact]
    public void Empty_box_reports_nothing() => Check(new RectD(0, 0, 0, 0)).Should().BeEmpty();
}

public class CropPlannerTests
{
    private static readonly CropRules Rules = new();
    private static readonly RectD Box = new(450, 500, 300, 360);
    private static readonly PointD LeftEye = new(520, 620);
    private static readonly PointD RightEye = new(680, 620);
    private static readonly PointD Nose = new(600, 700);
    private static readonly PointD HeadTop = new(600, 440);

    private static (FaceData Face, PointD HeadTop) Tilted(double degrees)
    {
        PointD Turn(PointD p) => p.RotateAround(Box.Center, degrees);
        return (new FaceData(Box, Turn(LeftEye), Turn(RightEye), Turn(Nose)), Turn(HeadTop));
    }

    [Fact]
    public void Upright_face_gives_a_clean_plan()
    {
        var plan = CropPlanner.Plan(new FaceData(Box, LeftEye, RightEye, Nose), HeadTop, 1200, 1600, Rules);

        plan.Issues.Should().BeEmpty();
        plan.Crop.CenterX.Should().BeApproximately(600, 1e-9);
        plan.Crop.CenterY.Should().BeApproximately(722, 1e-9);
        plan.Crop.Height.Should().BeApproximately(600, 1e-9);
        plan.Crop.AngleDegrees.Should().BeApproximately(0, 1e-9);
        plan.Overflow.Should().Be(new Overflow(0, 0, 0, 0));
    }

    [Fact]
    public void Eye_order_does_not_matter()
    {
        var face = new FaceData(Box, RightEye, LeftEye, Nose);

        face.ImageLeftEye.Should().Be(LeftEye);
        CropPlanner.Plan(face, HeadTop, 1200, 1600, Rules).Crop.AngleDegrees.Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void Tilt_is_corrected_around_the_face()
    {
        var (face, headTop) = Tilted(10);

        var plan = CropPlanner.Plan(face, headTop, 1200, 1600, Rules);

        var expected = new PointD(600, 722).RotateAround(Box.Center, 10);
        face.RollDegrees.Should().BeApproximately(10, 1e-9);
        plan.Crop.AngleDegrees.Should().BeApproximately(10, 1e-9);
        plan.Crop.CenterX.Should().BeApproximately(expected.X, 1e-9);
        plan.Crop.CenterY.Should().BeApproximately(expected.Y, 1e-9);
        plan.Crop.Height.Should().BeApproximately(600, 1e-9);
        plan.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Excessive_tilt_is_reported()
    {
        var (face, headTop) = Tilted(-25);

        CropPlanner.Plan(face, headTop, 1200, 1600, Rules).Issues.Should().Contain(IssueCode.ExcessiveTilt);
    }

    [Fact]
    public void Small_face_is_reported_with_low_resolution()
    {
        var face = new FaceData(new RectD(500, 500, 40, 50), new PointD(510, 515), new PointD(530, 515));

        var plan = CropPlanner.Plan(face, null, 1200, 1600, Rules);

        plan.Issues.Should().Contain([IssueCode.FaceTooSmall, IssueCode.LowResolution]);
        plan.Crop.Height.Should().Be(171);
    }

    [Fact]
    public void Head_touching_the_top_edge_is_reported()
    {
        var plan = CropPlanner.Plan(
            new FaceData(Box, LeftEye, RightEye), new PointD(600, 0.5), 1200, 1600, Rules);

        plan.Issues.Should().Contain(IssueCode.HeadCutInSource);
    }

    [Fact]
    public void Estimated_head_top_outside_the_image_is_reported()
    {
        var face = new FaceData(new RectD(450, 5, 300, 360), new PointD(520, 125), new PointD(680, 125));

        CropPlanner.Plan(face, null, 1200, 1600, Rules).Issues.Should().Contain(IssueCode.HeadCutInSource);
    }

    [Fact]
    public void Missing_body_below_is_reported()
    {
        var plan = CropPlanner.Plan(new FaceData(Box, LeftEye, RightEye), HeadTop, 1200, 900, Rules);

        plan.Issues.Should().Contain(IssueCode.ShouldersCut);
        plan.Overflow.Bottom.Should().BeApproximately(122.0 / 600, 1e-9);
    }

    [Theory]
    [InlineData(0, 1600)]
    [InlineData(1200, 0)]
    public void Invalid_image_size_is_rejected(int width, int height)
    {
        var act = () => CropPlanner.Plan(new FaceData(Box, LeftEye, RightEye), null, width, height, Rules);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
