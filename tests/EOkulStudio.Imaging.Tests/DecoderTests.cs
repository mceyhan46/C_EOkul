using System.Buffers.Binary;
using System.Text;
using OpenCvSharp;

namespace EOkulStudio.Imaging.Tests;

/// <summary>Yön etiketi, saydamlık ve renk derinliği: gerçek çözücüyle.</summary>
public sealed class DecoderTests
{
    private const int W = 64;
    private const int H = 48;

    /// <summary>Dört farklı renkli çeyrek: her yön dönüşümü ayırt edilebilir.</summary>
    private static Mat Quadrants()
    {
        var image = new Mat(H, W, MatType.CV_8UC3, Scalar.All(0));
        Scalar[] colours = [new(0, 0, 220), new(0, 220, 0), new(220, 0, 0), new(220, 220, 220)];
        for (var i = 0; i < 4; i++)
        {
            using var part = new Mat(image, new Rect(i % 2 * W / 2, i / 2 * H / 2, W / 2, H / 2));
            part.SetTo(colours[i]);
        }
        return image;
    }

    /// <summary>EXIF tanımına göre görüntülenen (x, y) pikselinin saklanan görüntüdeki yeri.</summary>
    private static (int X, int Y) Stored(int orientation, int x, int y) => orientation switch
    {
        2 => (W - 1 - x, y),
        3 => (W - 1 - x, H - 1 - y),
        4 => (x, H - 1 - y),
        5 => (y, x),
        6 => (y, H - 1 - x),
        7 => (W - 1 - y, H - 1 - x),
        8 => (W - 1 - y, x),
        _ => (x, y),
    };

    private static void AssertOriented(byte[] file, int orientation, int tolerance)
    {
        using var stored = Quadrants();
        using var decoded = ImageDecoder.Decode(file);
        var turned = orientation >= 5;
        decoded.Size().Should().Be(turned ? new Size(H, W) : new Size(W, H), $"yön {orientation}");

        foreach (var (u, v) in new[] { (0.2, 0.2), (0.8, 0.2), (0.2, 0.8), (0.8, 0.8) })
        {
            int x = (int)(u * decoded.Width), y = (int)(v * decoded.Height);
            var (sx, sy) = Stored(orientation, x, y);
            var expected = stored.Get<Vec3b>(sy, sx);
            var actual = decoded.Get<Vec3b>(y, x);
            Math.Abs(actual.Item0 - expected.Item0).Should().BeLessThanOrEqualTo(tolerance, $"yön {orientation}");
            Math.Abs(actual.Item1 - expected.Item1).Should().BeLessThanOrEqualTo(tolerance, $"yön {orientation}");
            Math.Abs(actual.Item2 - expected.Item2).Should().BeLessThanOrEqualTo(tolerance, $"yön {orientation}");
        }
    }

    private static byte[] ExifBlock(int orientation) =>
    [
        (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8, 0, 1,
        0x01, 0x12, 0, 3, 0, 0, 0, 1, 0, (byte)orientation, 0, 0, 0, 0, 0, 0,
    ];

    [Fact]
    public void Jpeg_orientation_is_applied_once()
    {
        using var image = Quadrants();
        var jpeg = TestImages.Encode(image, ".jpg", (int)ImwriteFlags.JpegQuality, 100);
        for (var orientation = 1; orientation <= 8; orientation++)
        {
            byte[] payload = [.. "Exif\0\0"u8, .. ExifBlock(orientation)];
            byte[] file =
            [
                0xFF, 0xD8, 0xFF, 0xE1, 0, (byte)(payload.Length + 2), .. payload, .. jpeg.AsSpan(2),
            ];
            AssertOriented(file, orientation, 12);
        }
    }

    [Fact]
    public void Png_orientation_is_applied_once()
    {
        using var image = Quadrants();
        var png = TestImages.Png(image);
        const int afterHeader = 8 + 25; // imza + IHDR
        for (var orientation = 1; orientation <= 8; orientation++)
        {
            var exif = ExifBlock(orientation);
            byte[] body = [.. "eXIf"u8, .. exif];
            var length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, exif.Length);
            var crc = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(body));
            byte[] file = [.. png.AsSpan(0, afterHeader), .. length, .. body, .. crc, .. png.AsSpan(afterHeader)];
            AssertOriented(file, orientation, 0);
        }
    }

    [Fact]
    public void Webp_orientation_is_applied_once()
    {
        using var image = Quadrants();
        // 101: kayıpsız WebP
        var webp = TestImages.Encode(image, ".webp", (int)ImwriteFlags.WebPQuality, 101);
        var data = webp.AsSpan(12).ToArray();
        for (var orientation = 1; orientation <= 8; orientation++)
        {
            byte[] header = [0x08, 0, 0, 0, W - 1, 0, 0, H - 1, 0, 0];
            byte[] body = [.. "WEBP"u8, .. Chunk("VP8X", header), .. data, .. Chunk("EXIF", ExifBlock(orientation))];
            var length = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, body.Length);
            byte[] file = [.. "RIFF"u8, .. length, .. body];
            AssertOriented(file, orientation, 0);
        }

        static byte[] Chunk(string name, byte[] content)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, content.Length);
            return [.. Encoding.ASCII.GetBytes(name), .. length, .. content];
        }
    }

    [Fact]
    public void Tiff_orientation_is_applied_once()
    {
        for (var orientation = 1; orientation <= 8; orientation++)
            AssertOriented(Tiff(orientation), orientation, 0);
    }

    [Fact]
    public void Bmp_and_tiff_are_read()
    {
        using var image = Quadrants();
        foreach (var extension in new[] { ".bmp", ".tif" })
        {
            using var decoded = ImageDecoder.Decode(TestImages.Encode(image, extension));
            TestImages.MeanDifference(decoded, image).Should().Be(0);
        }
    }

    [Fact]
    public void Transparent_areas_become_white()
    {
        using var image = new Mat(4, 4, MatType.CV_8UC4, new Scalar(0, 0, 200, 0));
        using (var part = new Mat(image, new Rect(0, 0, 2, 4))) part.SetTo(new Scalar(0, 0, 200, 255));
        using (var half = new Mat(image, new Rect(2, 0, 1, 4))) half.SetTo(new Scalar(0, 0, 200, 128));

        using var decoded = ImageDecoder.Decode(TestImages.Png(image));

        decoded.Type().Should().Be(MatType.CV_8UC3);
        decoded.Get<Vec3b>(0, 0).Should().Be(new Vec3b(0, 0, 200));
        decoded.Get<Vec3b>(0, 3).Should().Be(new Vec3b(255, 255, 255));
        decoded.Get<Vec3b>(0, 2).Should().Be(new Vec3b(127, 127, 227));
    }

    [Fact]
    public void Gray_and_16_bit_images_become_8_bit_colour()
    {
        using var gray = new Mat(5, 6, MatType.CV_8UC1, new Scalar(77));
        using var deep = new Mat(5, 6, MatType.CV_16UC3, new Scalar(257 * 10, 257 * 20, 257 * 30));

        using var first = ImageDecoder.Decode(TestImages.Png(gray));
        using var second = ImageDecoder.Decode(TestImages.Png(deep));

        first.Get<Vec3b>(2, 2).Should().Be(new Vec3b(77, 77, 77));
        second.Get<Vec3b>(2, 2).Should().Be(new Vec3b(10, 20, 30));
    }

    [Fact]
    public void Broken_content_throws_a_clear_error()
    {
        var act = () => ImageDecoder.Decode([1, 2, 3, 4, 5, 6]);

        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>Sıkıştırmasız, yön etiketli RGB TIFF.</summary>
    private static byte[] Tiff(int orientation)
    {
        using var image = Quadrants();
        var pixels = TestImages.Pixels(image);
        const int entries = 10;
        const int bitsOffset = 8 + 2 + entries * 12 + 4;
        const int dataOffset = bitsOffset + 6;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("II"u8);
        writer.Write((ushort)42);
        writer.Write(8u);
        writer.Write((ushort)entries);
        void Entry(ushort tag, ushort type, uint count, uint value)
        {
            writer.Write(tag);
            writer.Write(type);
            writer.Write(count);
            writer.Write(value);
        }
        Entry(256, 3, 1, W);
        Entry(257, 3, 1, H);
        Entry(258, 3, 3, bitsOffset);
        Entry(259, 3, 1, 1);
        Entry(262, 3, 1, 2);
        Entry(273, 4, 1, dataOffset);
        Entry(274, 3, 1, (uint)orientation);
        Entry(277, 3, 1, 3);
        Entry(278, 3, 1, H);
        Entry(279, 4, 1, W * H * 3);
        writer.Write(0u);
        for (var i = 0; i < 3; i++) writer.Write((ushort)8);
        foreach (var pixel in pixels)
        {
            writer.Write(pixel.Item2);
            writer.Write(pixel.Item1);
            writer.Write(pixel.Item0);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }
}
