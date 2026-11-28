using System.Collections.Concurrent;
using EOkulStudio.Core.Processing;
using EOkulStudio.Core.Settings;
using EOkulStudio.Imaging;
using OpenCvSharp;

namespace EOkulStudio.Imaging.Tests;

/// <summary>Her fotoğrafın son ara görüntülerini saklar.</summary>
internal sealed class RecordingInspector : IPipelineInspector
{
    private readonly ConcurrentDictionary<string, PipelineImages> _images = new();

    public void Inspect(string sourceName, PipelineImages images) =>
        _images[sourceName] = images with
        {
            Original = images.Original.Clone(),
            Alpha = images.Alpha.Clone(),
            WithoutDecontamination = images.WithoutDecontamination.Clone(),
            OnDark = images.OnDark.Clone(),
            Result = images.Result.Clone(),
        };

    public PipelineImages Take(string sourceName) =>
        _images.TryRemove(sourceName, out var images)
            ? images
            : throw new InvalidOperationException("Ara görüntü yok: " + sourceName);
}

/// <summary>Modeller bir kez yüklenir.</summary>
public sealed class PipelineFixture : IDisposable
{
    internal RecordingInspector Inspector { get; } = new();

    internal PhotoPipeline Pipeline { get; }

    public PipelineFixture() => Pipeline = PhotoPipeline.Create(AppContext.BaseDirectory, 2, Inspector);

    internal static PipelineOptions Options { get; } = PipelineOptions.From(new AppSettings());

    internal PipelineResult Run(byte[] source, string name = "örnek", PipelineOptions? options = null) =>
        Pipeline.Process(source, name, options ?? Options);

    public void Dispose() => Pipeline.Dispose();
}

internal static class TestImages
{
    public static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "astronaut.png");

    public static byte[] SampleBytes() => File.ReadAllBytes(SamplePath);

    public static Mat Sample() => ImageDecoder.Decode(SampleBytes());

    public static byte[] Png(Mat image) => Encode(image, ".png");

    public static byte[] Encode(Mat image, string extension, params int[] parameters)
    {
        Cv2.ImEncode(extension, image, out var data, parameters).Should().BeTrue();
        return data;
    }

    public static byte[] Transform(Func<Mat, Mat> transform)
    {
        using var sample = Sample();
        using var changed = transform(sample);
        return Png(changed);
    }

    public static Mat Resized(Mat image, int width, int height)
    {
        var result = new Mat();
        Cv2.Resize(image, result, new Size(width, height), 0, 0,
            width < image.Width ? InterpolationFlags.Area : InterpolationFlags.Cubic);
        return result;
    }

    /// <summary>Pozitif açı: saat yönü.</summary>
    public static Mat Rotated(Mat image, double degrees)
    {
        using var matrix = Cv2.GetRotationMatrix2D(new Point2f(222, 120), -degrees, 1);
        var result = new Mat();
        Cv2.WarpAffine(image, result, matrix, image.Size(), InterpolationFlags.Cubic, BorderTypes.Reflect101);
        return result;
    }

    public static Mat DecodeColor(byte[] data) => Cv2.ImDecode(data, ImreadModes.Color);

    public static double MeanDifference(Mat a, Mat b)
    {
        a.Size().Should().Be(b.Size());
        using var difference = new Mat();
        Cv2.Absdiff(a, b, difference);
        var mean = Cv2.Mean(difference);
        return (mean.Val0 + mean.Val1 + mean.Val2) / 3;
    }

    public static Vec3b[] Pixels(Mat image)
    {
        using var copy = image.Clone();
        copy.GetArray(out Vec3b[] pixels);
        return pixels;
    }
}
