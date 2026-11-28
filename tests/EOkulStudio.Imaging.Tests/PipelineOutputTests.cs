using System.Buffers.Binary;
using EOkulStudio.Core.Imaging;
using EOkulStudio.Core.Results;
using OpenCvSharp;

namespace EOkulStudio.Imaging.Tests;

/// <summary>Kabul ölçütleri: 133x171, beyaz zemin, hale ve renk bulaşması, belirlenimcilik.</summary>
public sealed class PipelineOutputTests(PipelineFixture fixture) : IClassFixture<PipelineFixture>
{
    private const int Width = 133;
    private const int Height = 171;

    [Fact]
    public void Produces_an_exact_300_dpi_jpeg_within_the_size_limits()
    {
        var result = fixture.Run(TestImages.SampleBytes());

        result.Issues.Should().NotContain(IssueCode.UncertainBackground);
        StatusRules.Evaluate(result.Issues).Should().NotBe(PhotoStatus.Failed);
        var jpeg = result.Jpeg!;
        JpegFile.ReadSize(jpeg).Should().Be((Width, Height));
        jpeg.Length.Should().BeInRange(20 * 1024, 150 * 1024);
        var header = JpegFile.Segments(jpeg)[0];
        header.Marker.Should().Be((byte)0xE0);
        BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(header.PayloadStart + 8)).Should().Be((ushort)300);
        using var decoded = TestImages.DecodeColor(jpeg);
        decoded.Size().Should().Be(new Size(Width, Height));
        result.Crop.Should().NotBeNull();
    }

    [Fact]
    public void Background_is_white_and_the_person_is_kept()
    {
        fixture.Run(TestImages.SampleBytes(), "zemin");
        var images = fixture.Inspector.Take("zemin");

        using var alpha = TestImages.Resized(images.Alpha, Width, Height);
        alpha.GetArray(out byte[] mask);
        var pixels = TestImages.Pixels(images.Result);

        var background = pixels.Where((_, i) => mask[i] < 2).ToList();
        background.Count.Should().BeGreaterThan(Width * Height / 10);
        var mean = background.Average(p => (p.Item0 + p.Item1 + p.Item2) / 3.0);
        TestContext.Current.TestOutputHelper?.WriteLine($"arka plan ortalaması: {mean:0.000}");
        mean.Should().BeGreaterThanOrEqualTo(254);

        // Üst köşeler beyaz; yüzün ortası beyaz değil.
        Brightness(pixels, 0, 0, 12, 12).Should().BeGreaterThanOrEqualTo(254);
        Brightness(pixels, Width - 12, 0, 12, 12).Should().BeGreaterThanOrEqualTo(254);
        Brightness(pixels, 52, 60, 30, 30).Should().BeLessThan(235);
        Brightness(pixels, 40, Height - 15, 50, 15).Should().BeLessThan(235);
    }

    [Fact]
    public void Old_background_colour_does_not_leak_into_the_edges()
    {
        // Örnekteki kişi, kendi maskesiyle doygun yeşil bir zemine oturtulup yeniden işlenir.
        using var sample = TestImages.Sample();
        var model = new ModnetModel(ModelStore.InDirectory(AppContext.BaseDirectory).Load(ModelStore.MattingFile), 2);
        byte[] green;
        using (model)
        using (var small = model.Predict(sample, TestContext.Current.CancellationToken))
        using (var alpha = new Mat())
        {
            Cv2.Resize(small, alpha, sample.Size(), 0, 0, InterpolationFlags.Cubic);
            Cv2.Threshold(alpha, alpha, 1, 1, ThresholdTypes.Trunc);
            Cv2.Threshold(alpha, alpha, 0, 0, ThresholdTypes.Tozero);
            using var onGreen = Compositor.Plain(sample, alpha, new Scalar(40, 200, 40));
            green = TestImages.Png(onGreen);
        }

        var result = fixture.Run(green, "yeşil");
        result.Jpeg.Should().NotBeNull();
        var images = fixture.Inspector.Take("yeşil");

        using var mask = images.Alpha.Clone();
        mask.GetArray(out byte[] values);
        using var clean = TestImages.Resized(images.Result, mask.Width, mask.Height);
        var cleaned = GreenExcess(TestImages.Pixels(clean), values);
        var plain = GreenExcess(TestImages.Pixels(images.WithoutDecontamination), values);

        TestContext.Current.TestOutputHelper?.WriteLine($"kenarda yeşil fazlası: arındırmasız {plain:0.0}, sonuç {cleaned:0.0}");
        plain.Should().BeGreaterThan(5);
        cleaned.Should().BeLessThan(plain / 2);
    }

    [Fact]
    public void No_bright_halo_remains_around_the_person()
    {
        fixture.Run(TestImages.SampleBytes(), "hale");
        var images = fixture.Inspector.Take("hale");

        // Kişinin dışındaki ince halka beyaz zeminde bembeyaz olmalı (kir veya gölge halesi yok).
        using var inside = new Mat();
        Cv2.Threshold(images.Alpha, inside, 5, 255, ThresholdTypes.Binary);
        using var element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(15, 15));
        using var grown = new Mat();
        Cv2.Dilate(inside, grown, element);
        using var ring = new Mat();
        Cv2.Subtract(grown, inside, ring);
        ring.GetArray(out byte[] ringMask);
        using var result = TestImages.Resized(images.Result, ring.Width, ring.Height);
        var pixels = TestImages.Pixels(result);

        var values = pixels.Where((_, i) => ringMask[i] > 0)
            .Select(p => (p.Item0 + p.Item1 + p.Item2) / 3.0).ToList();
        values.Should().NotBeEmpty();
        TestContext.Current.TestOutputHelper?.WriteLine($"dış halka ortalaması: {values.Average():0.00}");
        values.Average().Should().BeGreaterThanOrEqualTo(252);
    }

    [Fact]
    public void Same_input_gives_the_same_bytes_even_in_parallel()
    {
        var source = TestImages.SampleBytes();
        var first = fixture.Run(source).Jpeg!;

        var again = new byte[6][];
        Parallel.For(0, again.Length, new ParallelOptions { MaxDegreeOfParallelism = 3 },
            i => again[i] = fixture.Run(source, "paralel" + i).Jpeg!);

        foreach (var other in again) other.AsSpan().SequenceEqual(first).Should().BeTrue();
    }

    [Theory]
    [InlineData(-15)]
    [InlineData(-8)]
    [InlineData(8)]
    [InlineData(15)]
    public void Tilted_head_is_straightened(double angle)
    {
        fixture.Run(TestImages.SampleBytes(), "dik");
        var baseline = fixture.Inspector.Take("dik").Roll;

        var result = fixture.Run(TestImages.Transform(image => TestImages.Rotated(image, angle)), "eğik");

        result.Jpeg.Should().NotBeNull();
        var roll = fixture.Inspector.Take("eğik").Roll;
        TestContext.Current.TestOutputHelper?.WriteLine($"{angle}: taban {baseline:0.0}, ölçülen {roll:0.0}");
        // Ölçülen doğruluk (bu tek yüzün yapay döndürmeleri): ortalama hata yaklaşık 3, en çok 6,5 derece.
        (roll - baseline).Should().BeApproximately(angle, 7);
        result.Crop!.Value.AngleDegrees.Should().BeApproximately(roll, 1e-9);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(1024)]
    [InlineData(4000)]
    public void Works_across_source_resolutions(int size)
    {
        var source = TestImages.Transform(image => TestImages.Resized(image, size, size));

        var result = fixture.Run(source);

        JpegFile.ReadSize(result.Jpeg!).Should().Be((Width, Height));
        result.Issues.Contains(IssueCode.LowResolution).Should().Be(size == 200);
    }

    [Fact]
    public void Blurry_and_dark_photos_are_flagged()
    {
        var blurry = TestImages.Transform(image =>
        {
            var result = new Mat();
            Cv2.GaussianBlur(image, result, new Size(0, 0), 2);
            return result;
        });
        var dark = TestImages.Transform(image =>
        {
            var result = new Mat();
            image.ConvertTo(result, MatType.CV_8UC3, 0.35);
            return result;
        });

        fixture.Run(blurry).Issues.Should().Contain(IssueCode.Blurry);
        fixture.Run(dark).Issues.Should().Contain(IssueCode.LowLight);
        var clear = fixture.Run(TestImages.SampleBytes()).Issues;
        clear.Should().NotContain(IssueCode.Blurry);
        clear.Should().NotContain(IssueCode.LowLight);
    }

    [Fact]
    public void File_size_limits_are_applied()
    {
        var small = fixture.Run(TestImages.SampleBytes(),
            options: PipelineFixture.Options with { MinFileSizeKb = 0, MaxFileSizeKb = 6 });
        small.Jpeg!.Length.Should().BeLessThanOrEqualTo(6 * 1024);

        var large = fixture.Run(TestImages.SampleBytes(),
            options: PipelineFixture.Options with { MinFileSizeKb = 100, MaxFileSizeKb = 150 });
        large.Jpeg!.Length.Should().BeInRange(100 * 1024, 150 * 1024);

        var impossible = fixture.Run(TestImages.SampleBytes(),
            options: PipelineFixture.Options with { MinFileSizeKb = 0, MaxFileSizeKb = 1 });
        impossible.Issues.Should().Contain(IssueCode.FileSizeOutOfRange);
    }

    [Fact]
    public void Photos_without_exactly_one_face_produce_no_output()
    {
        using var plain = new Mat(600, 500, MatType.CV_8UC3, new Scalar(120, 160, 90));
        var none = fixture.Run(TestImages.Png(plain));
        none.Jpeg.Should().BeNull();
        none.Issues.Should().Equal(IssueCode.NoFace);

        var two = fixture.Run(TestImages.Transform(image =>
        {
            var result = new Mat();
            Cv2.HConcat([image, image], result);
            return result;
        }));
        two.Jpeg.Should().BeNull();
        two.Issues.Should().Equal(IssueCode.MultipleFaces);
    }

    [Fact]
    public void Unreadable_content_is_reported_as_corrupt()
    {
        foreach (var content in new[] { Array.Empty<byte>(), "bu bir fotoğraf değil"u8.ToArray(), new byte[] { 0xFF, 0xD8, 0xFF } })
            fixture.Run(content).Issues.Should().Equal(IssueCode.CorruptFile);
    }

    [Fact]
    public void Cancellation_stops_processing()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        var act = () => fixture.Pipeline.Process(
            TestImages.SampleBytes(), "iptal", PipelineFixture.Options, cancellationToken: cancel.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    private static double Brightness(Vec3b[] pixels, int x, int y, int width, int height)
    {
        var sum = 0.0;
        for (var row = y; row < y + height; row++)
            for (var column = x; column < x + width; column++)
            {
                var p = pixels[row * Width + column];
                sum += (p.Item0 + p.Item1 + p.Item2) / 3.0;
            }
        return sum / (width * height);
    }

    /// <summary>Maskenin yarı saydam kenarında yeşilin diğer kanalları aşma miktarı.</summary>
    private static double GreenExcess(Vec3b[] pixels, byte[] mask) =>
        pixels.Where((_, i) => mask[i] is > 50 and < 205)
            .Average(p => (double)Math.Max(0, p.Item1 - Math.Max(p.Item0, p.Item2)));
}
