using System.Buffers.Binary;

namespace EOkulStudio.Core.Imaging;

/// <summary>
/// JPEG, PNG ve WebP dosyalarındaki EXIF yön etiketini (1..8) okur. TIFF dosyalarında yönü görüntü çözücü
/// kendisi uyguladığı için burada okunmaz. Bozuk veya eksik veride 1 (döndürme yok) döner.
/// </summary>
public static class ExifOrientation
{
    private const int OrientationTag = 0x0112;

    public static int Read(ReadOnlySpan<byte> file)
    {
        if (file.Length >= 4 && file[0] == 0xFF && file[1] == 0xD8) return FromJpeg(file);
        if (file.Length >= 8 && file[..8].SequenceEqual<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return FromPng(file);
        if (file.Length >= 12 && file[..4].SequenceEqual("RIFF"u8) && file[8..12].SequenceEqual("WEBP"u8))
            return FromWebp(file);
        return 1;
    }

    private static int FromJpeg(ReadOnlySpan<byte> file)
    {
        foreach (var segment in JpegFile.Segments(file))
        {
            if (segment.Marker != 0xE1) continue;
            var payload = file.Slice(segment.PayloadStart, segment.PayloadLength);
            if (payload.Length > 6 && payload[..6].SequenceEqual("Exif\0\0"u8)) return FromTiffHeader(payload[6..]);
        }
        return 1;
    }

    private static int FromPng(ReadOnlySpan<byte> file)
    {
        var position = 8;
        while (position + 8 <= file.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(file[position..]);
            if (length > (uint)(file.Length - position - 8)) break;
            if (file.Slice(position + 4, 4).SequenceEqual("eXIf"u8))
                return FromTiffHeader(file.Slice(position + 8, (int)length));
            position += 12 + (int)length;
        }
        return 1;
    }

    private static int FromWebp(ReadOnlySpan<byte> file)
    {
        var position = 12;
        while (position + 8 <= file.Length)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(file[(position + 4)..]);
            if (length > (uint)(file.Length - position - 8)) break;
            if (file.Slice(position, 4).SequenceEqual("EXIF"u8))
            {
                var data = file.Slice(position + 8, (int)length);
                if (data.Length > 6 && data[..6].SequenceEqual("Exif\0\0"u8)) data = data[6..];
                return FromTiffHeader(data);
            }
            position += 8 + (int)length + (int)(length & 1);
        }
        return 1;
    }

    /// <summary>"II*\0" veya "MM\0*" ile başlayan TIFF yapısının ilk dizininden yön etiketini okur.</summary>
    private static int FromTiffHeader(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8) return 1;
        var little = tiff[0] == 'I' && tiff[1] == 'I';
        if (!little && !(tiff[0] == 'M' && tiff[1] == 'M')) return 1;
        if (U16(tiff[2..], little) != 42) return 1;

        var directory = U32(tiff[4..], little);
        if (directory < 8 || directory > (uint)tiff.Length - 2) return 1;
        int count = U16(tiff[(int)directory..], little);
        var position = (int)directory + 2;
        for (var i = 0; i < count && position + 12 <= tiff.Length; i++, position += 12)
        {
            if (U16(tiff[position..], little) != OrientationTag) continue;
            // Tür 3 (SHORT) beklenir; bazı programlar 4 (LONG) yazar.
            var type = U16(tiff[(position + 2)..], little);
            var value = type == 4 ? U32(tiff[(position + 8)..], little) : U16(tiff[(position + 8)..], little);
            return value is >= 1 and <= 8 ? (int)value : 1;
        }
        return 1;
    }

    private static ushort U16(ReadOnlySpan<byte> data, bool little) =>
        little ? BinaryPrimitives.ReadUInt16LittleEndian(data) : BinaryPrimitives.ReadUInt16BigEndian(data);

    private static uint U32(ReadOnlySpan<byte> data, bool little) =>
        little ? BinaryPrimitives.ReadUInt32LittleEndian(data) : BinaryPrimitives.ReadUInt32BigEndian(data);
}
