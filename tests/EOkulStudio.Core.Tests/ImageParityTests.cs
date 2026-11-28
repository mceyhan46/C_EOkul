using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Imaging;

namespace EOkulStudio.Core.Tests;

/// <summary>
/// Android sürümündeki Java kodundan (Quality.analyze, Finish.noiseSigma ve Finish.unsharp) yapay görüntülerle
/// üretilen başvuru değerleri. Görüntüler iki dilde aynı tam sayı kurallarıyla kurulur.
/// </summary>
public class ImageParityTests
{
    private sealed record Case(
        int Width, int Height, long Seed, int Cell, int Noise, int Blur, int Level,
        double BlurRatio, double Brightness, double Sigma, uint InputHash, uint SharpenedHash);

    private static readonly Case[] Cases =
    [
        new(200, 260, 1, 9, 0, 0, 60, 0.197123367103, 115.208908346763, 2.797903191502, 3400792357u, 3012062502u),
        new(200, 260, 2, 9, 4, 0, 60, 0.195826902298, 115.221863799146, 4.207732912240, 4180435119u, 1481797318u),
        new(320, 400, 3, 16, 12, 0, 90, 0.171373304417, 145.189303127610, 6.142235887524, 3085062498u, 4088144631u),
        new(320, 400, 4, 16, 0, 2, 90, 0.379806844274, 144.883077572408, 0.117543236497, 85010360u, 4041844364u),
        new(640, 800, 5, 24, 3, 6, 20, 0.302023975273, 72.692117096647, 0.168301441681, 3319067853u, 3464450077u),
        new(150, 190, 6, 5, 25, 0, 120, 0.185700377219, 175.070635879630, 12.940593652911, 1390084705u, 3772674378u),
        new(500, 640, 7, 40, 2, 1, 0, 0.205981885461, 54.792257846009, 0.347689447441, 1082330803u, 2449253476u),
        new(133, 171, 8, 7, 6, 0, 150, 0.205131072471, 205.070616218064, 5.441577590635, 1151947048u, 2503361879u),
        new(266, 342, 9, 11, 0, 0, 80, 0.185387547218, 135.192226277390, 2.434397295973, 1114102933u, 702864658u),
        new(90, 110, 10, 3, 8, 3, 40, 0.346436651283, 93.957523898235, 0.419004473918, 1798071374u, 966717499u),
    ];

    [Fact]
    public void Quality_matches_the_android_version()
    {
        foreach (var c in Cases)
        {
            var image = Build(c);
            Hash(image).Should().Be(c.InputHash, "test görüntüsü Java ile aynı olmalı");
            var area = new RectD(c.Width * 0.08, c.Height * 0.05,
                c.Width * 0.91 - c.Width * 0.08, c.Height * 0.97 - c.Height * 0.05);

            var report = Quality.Analyze(image, c.Width, c.Height, area);

            report.BlurRatio.Should().BeApproximately(c.BlurRatio, 1e-9);
            report.Brightness.Should().BeApproximately(c.Brightness, 1e-9);
        }
    }

    [Fact]
    public void Noise_estimate_and_sharpening_match_the_android_version()
    {
        foreach (var c in Cases)
        {
            var image = Build(c);
            var sigma = Unsharp.NoiseSigma(image, c.Width, c.Height);
            sigma.Should().BeApproximately(c.Sigma, 1e-9);

            Unsharp.Apply(image, c.Width, c.Height, Unsharp.AmountFor(sigma));

            Hash(image).Should().Be(c.SharpenedHash, $"tohum {c.Seed}");
        }
    }

    [Fact]
    public void Sharpening_with_no_amount_changes_nothing()
    {
        var image = Build(Cases[0]);
        var copy = (byte[])image.Clone();

        Unsharp.Apply(copy, Cases[0].Width, Cases[0].Height, 0);

        copy.Should().Equal(image);
    }

    [Fact]
    public void Flat_image_is_dark_or_bright_but_has_no_blur_ratio()
    {
        var image = new byte[60 * 80 * 3];

        var report = Quality.Analyze(image, 60, 80, new RectD(0, 0, 60, 80));

        report.IsDark.Should().BeTrue();
        report.IsBlurry.Should().BeFalse();
        Unsharp.NoiseSigma(image, 60, 80).Should().Be(0);
        Unsharp.NoiseSigma(new byte[12], 2, 2).Should().Be(0);
    }

    [Fact]
    public void Wrong_buffer_size_is_rejected()
    {
        var analyze = () => Quality.Analyze(new byte[10], 4, 4, new RectD(0, 0, 4, 4));
        var sharpen = () => Unsharp.NoiseSigma(new byte[10], 4, 4);

        analyze.Should().Throw<ArgumentException>();
        sharpen.Should().Throw<ArgumentException>();
    }

    /// <summary>BGR sırasında yapay görüntü; Java üretecindeki ilk kanal kırmızıdır.</summary>
    private static byte[] Build(Case c)
    {
        int w = c.Width, h = c.Height;
        var seed = c.Seed;
        var channels = new[] { new int[w * h], new int[w * h], new int[w * h] };
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var value = c.Level + (x / c.Cell + y / c.Cell) % 2 * 60 + (x + 2 * y) % 40;
                for (var k = 0; k < 3; k++)
                {
                    seed = (seed * 1103515245L + 12345L) % 2147483648L;
                    var noise = c.Noise == 0 ? 0 : (int)((seed >> 16) % (2 * c.Noise + 1)) - c.Noise;
                    channels[k][y * w + x] = Math.Clamp(value + k * 7 + noise, 0, 255);
                }
            }
        }

        for (var pass = 0; pass < c.Blur; pass++)
        {
            for (var k = 0; k < 3; k++)
            {
                var output = new int[w * h];
                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var sum = 0;
                        for (var dy = -1; dy <= 1; dy++)
                            for (var dx = -1; dx <= 1; dx++)
                                sum += channels[k][Math.Clamp(y + dy, 0, h - 1) * w + Math.Clamp(x + dx, 0, w - 1)];
                        output[y * w + x] = sum / 9;
                    }
                }
                channels[k] = output;
            }
        }

        var image = new byte[w * h * 3];
        for (var i = 0; i < w * h; i++)
        {
            image[i * 3] = (byte)channels[2][i];
            image[i * 3 + 1] = (byte)channels[1][i];
            image[i * 3 + 2] = (byte)channels[0][i];
        }
        return image;
    }

    /// <summary>Java tarafıyla aynı sırada (mavi, yeşil, kırmızı) FNV-1a özeti.</summary>
    private static uint Hash(byte[] image)
    {
        var hash = 0x811c9dc5u;
        foreach (var value in image) hash = unchecked((hash ^ value) * 16777619u);
        return hash;
    }
}
