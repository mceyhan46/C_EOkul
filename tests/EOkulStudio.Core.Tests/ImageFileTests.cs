using System.Buffers.Binary;
using EOkulStudio.Core.Imaging;

namespace EOkulStudio.Core.Tests;

public class ExifOrientationTests
{
    private static byte[] Tiff(int orientation, bool little, ushort type = 3)
    {
        var data = new byte[8 + 2 + 2 * 12 + 4];
        data[0] = data[1] = (byte)(little ? 'I' : 'M');
        void U16(int at, int value)
        {
            if (little) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(at), (ushort)value);
            else BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(at), (ushort)value);
        }
        void U32(int at, int value)
        {
            if (little) BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at), (uint)value);
            else BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at), (uint)value);
        }
        U16(2, 42);
        U32(4, 8);
        U16(8, 2);
        U16(10, 0x0100); // genişlik etiketi
        U16(12, 3);
        U32(14, 1);
        U16(22, 0x0112);
        U16(24, type);
        U32(26, 1);
        if (type == 4) U32(30, orientation);
        else U16(30, orientation);
        return data;
    }

    private static byte[] Jpeg(byte[] tiff)
    {
        byte[] payload = [.. "Exif\0\0"u8, .. tiff];
        var length = payload.Length + 2;
        return [0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload, 0xFF, 0xD9];
    }

    private static byte[] Png(byte[] tiff)
    {
        static byte[] Chunk(string name, byte[] content)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, content.Length);
            return [.. length, .. System.Text.Encoding.ASCII.GetBytes(name), .. content, 0, 0, 0, 0];
        }
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Chunk("IHDR", new byte[13]), .. Chunk("eXIf", tiff)];
    }

    private static byte[] Webp(byte[] exif)
    {
        static byte[] Chunk(string name, byte[] content)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, content.Length);
            byte[] padding = content.Length % 2 == 1 ? [0] : [];
            return [.. System.Text.Encoding.ASCII.GetBytes(name), .. length, .. content, .. padding];
        }
        return [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, .. Chunk("VP8X", new byte[11]), .. Chunk("EXIF", exif)];
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reads_every_orientation_from_each_container(bool little)
    {
        for (var orientation = 1; orientation <= 8; orientation++)
        {
            var tiff = Tiff(orientation, little);
            ExifOrientation.Read(Jpeg(tiff)).Should().Be(orientation);
            ExifOrientation.Read(Png(tiff)).Should().Be(orientation);
            ExifOrientation.Read(Webp(tiff)).Should().Be(orientation);
            ExifOrientation.Read(Webp([.. "Exif\0\0"u8, .. tiff])).Should().Be(orientation);
        }
    }

    [Fact]
    public void Long_typed_value_is_accepted()
    {
        ExifOrientation.Read(Jpeg(Tiff(6, little: false, type: 4))).Should().Be(6);
    }

    [Fact]
    public void Invalid_data_means_no_rotation()
    {
        ExifOrientation.Read([]).Should().Be(1);
        ExifOrientation.Read("BM not an exif file"u8).Should().Be(1);
        ExifOrientation.Read(Jpeg(Tiff(9, true))).Should().Be(1);
        ExifOrientation.Read(Jpeg([1, 2, 3, 4, 5, 6, 7, 8, 9])).Should().Be(1);
        ExifOrientation.Read(Jpeg([(byte)'I', (byte)'I', 0, 0, 8, 0, 0, 0])).Should().Be(1);
        ExifOrientation.Read(Jpeg([(byte)'I', (byte)'I', 42, 0, 200, 0, 0, 0])).Should().Be(1);
        ExifOrientation.Read(Jpeg([(byte)'I', (byte)'I', 42, 0])).Should().Be(1);
        ExifOrientation.Read(Jpeg([(byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 0, 0])).Should().Be(1);
        ExifOrientation.Read([0xFF, 0xD8, 0xFF, 0xD9]).Should().Be(1);
    }

    [Fact]
    public void Truncated_files_do_not_throw()
    {
        foreach (var file in new[] { Jpeg(Tiff(6, true)), Png(Tiff(6, true)), Webp(Tiff(6, true)) })
            for (var length = 0; length < file.Length; length++)
                ExifOrientation.Read(file.AsSpan(0, length)).Should().BeOneOf(1, 6);
    }
}

public class JpegFileTests
{
    /// <summary>Yalnızca başlık yapısı gerçek olan sahte JPEG.</summary>
    private static byte[] Fake(int width, int height, int scanBytes = 20, bool jfif = true)
    {
        byte[] app0 = jfif
            ? [0xFF, 0xE0, 0, 16, .. "JFIF\0"u8, 1, 1, 0, 0, 1, 0, 1, 0, 0]
            : [];
        byte[] sof =
        [
            0xFF, 0xC0, 0, 11, 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 1, 1, 0x11, 0,
        ];
        byte[] sos = [0xFF, 0xDA, 0, 8, 1, 1, 0, 0, 63, 0];
        return [0xFF, 0xD8, .. app0, 0xFF, 0xC4, 0, 3, 0, .. sof, .. sos, .. new byte[scanBytes], 0xFF, 0xD9];
    }

    [Fact]
    public void Reads_the_size_from_the_frame_header()
    {
        JpegFile.ReadSize(Fake(133, 171)).Should().Be((133, 171));
        JpegFile.ReadSize([1, 2, 3, 4]).Should().BeNull();
        JpegFile.ReadSize([0xFF, 0xD8, 0xFF, 0xC0, 0, 4, 8, 0, 0xFF, 0xD9]).Should().BeNull();
        JpegFile.ReadSize([0xFF, 0xD8, 0xFF, 0xD9]).Should().BeNull();
    }

    [Fact]
    public void Segment_walk_stops_at_broken_data()
    {
        JpegFile.Segments([0xFF, 0xD8, 0x00, 0x00, 0x00, 0x00]).Should().BeEmpty();
        JpegFile.Segments([0xFF, 0xD8, 0xFF, 0xE0, 0xFF, 0xFF]).Should().BeEmpty();
        // Dolgu baytı ve uzunluksuz işaretçi atlanır.
        JpegFile.Segments([0xFF, 0xD8, 0xFF, 0xFF, 0xD0, 0xFF, 0xFE, 0, 2]).Should().ContainSingle();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Stamps_300_dpi(bool hasJfif)
    {
        var result = JpegFile.WithDpi(Fake(10, 20, jfif: hasJfif));

        var header = JpegFile.Segments(result)[0];
        header.Marker.Should().Be(0xE0);
        var payload = result.AsSpan(header.PayloadStart, header.PayloadLength).ToArray();
        payload[7].Should().Be(1);
        BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(8)).Should().Be(300);
        BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(10)).Should().Be(300);
        JpegFile.ReadSize(result).Should().Be((10, 20));
    }

    [Fact]
    public void Stamping_rejects_non_jpeg_data()
    {
        var act = () => JpegFile.WithDpi([1, 2, 3, 4, 5]);

        act.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(70_000)]
    [InlineData(150_000)]
    public void Padding_reaches_the_minimum_and_keeps_the_structure(int minBytes)
    {
        var original = Fake(133, 171);

        var padded = JpegFile.Pad(original, minBytes);

        padded.Length.Should().BeGreaterThanOrEqualTo(Math.Max(minBytes, original.Length));
        padded.Length.Should().BeLessThan(Math.Max(minBytes, original.Length) + 5);
        JpegFile.ReadSize(padded).Should().Be((133, 171));
        JpegFile.Segments(padded)[0].Marker.Should().Be(0xE0);
        padded.AsSpan(padded.Length - 24).ToArray().Should().Equal(original.AsSpan(original.Length - 24).ToArray());
    }

    [Fact]
    public void Fit_keeps_the_requested_quality_when_it_is_small_enough()
    {
        var result = JpegFile.Fit(_ => Fake(133, 171, 3000), 95, 0, 10_000);

        result.Quality.Should().Be(95);
        result.WithinLimits.Should().BeTrue();
    }

    [Fact]
    public void Fit_lowers_quality_until_the_file_fits()
    {
        var tried = new List<int>();

        var result = JpegFile.Fit(q => { tried.Add(q); return Fake(133, 171, q * 100); }, 100, 0, 5000);

        result.WithinLimits.Should().BeTrue();
        result.Data.Length.Should().BeLessThanOrEqualTo(5000);
        // Sınırın 1,5 katından büyükken adım 8, sonra 3.
        tried.Should().Equal(100, 92, 84, 76, 68, 65, 62, 59, 56, 53, 50, 47);
        result.Quality.Should().Be(47);
    }

    [Fact]
    public void Fit_reports_when_even_the_lowest_quality_is_too_big()
    {
        var result = JpegFile.Fit(_ => Fake(133, 171, 9000), 100, 0, 5000);

        result.Quality.Should().Be(JpegFile.MinQuality);
        result.WithinLimits.Should().BeFalse();
    }

    [Fact]
    public void Fit_pads_small_files_and_allows_no_upper_limit()
    {
        var result = JpegFile.Fit(_ => Fake(133, 171, 100), 100, 20 * 1024 + 200, 0);

        result.Data.Length.Should().BeGreaterThanOrEqualTo(20 * 1024 + 200);
        result.WithinLimits.Should().BeTrue();
    }

    [Fact]
    public void Fit_flags_a_minimum_above_the_maximum()
    {
        var result = JpegFile.Fit(_ => Fake(133, 171, 100), 100, 6000, 5000);

        result.WithinLimits.Should().BeFalse();
    }
}
