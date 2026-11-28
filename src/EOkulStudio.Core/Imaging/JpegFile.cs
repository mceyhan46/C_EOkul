using System.Buffers.Binary;

namespace EOkulStudio.Core.Imaging;

/// <summary>Başlık bölümü: işaretçi ve yükün dosya içindeki yeri.</summary>
public readonly record struct JpegSegment(byte Marker, int PayloadStart, int PayloadLength);

/// <param name="Quality">Kullanılan JPEG kalitesi.</param>
/// <param name="WithinLimits">Dosya boyutu istenen sınırlar içinde mi?</param>
public sealed record FittedJpeg(byte[] Data, int Quality, bool WithinLimits);

/// <summary>JPEG dosya yapısı: boyut okuma, 300 DPI damgası ve dosya boyutu sınırları.</summary>
public static class JpegFile
{
    public const int Dpi = 300;

    /// <summary>Sıkıştırma kalitesi boyut için en fazla buraya kadar düşürülür.</summary>
    public const int MinQuality = 20;

    private const int MaxPayload = 65533;

    /// <summary>Görüntü verisi başlamadan önceki bölümler. Yapı bozuksa okunabilen kadarı döner.</summary>
    public static IReadOnlyList<JpegSegment> Segments(ReadOnlySpan<byte> jpeg)
    {
        var segments = new List<JpegSegment>();
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8) return segments;
        var position = 2;
        while (position + 4 <= jpeg.Length)
        {
            if (jpeg[position] != 0xFF) break;
            var marker = jpeg[position + 1];
            if (marker == 0xFF) { position++; continue; } // dolgu baytı
            if (marker == 0xD9) break;
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7) { position += 2; continue; }
            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg[(position + 2)..]);
            if (length < 2 || position + 2 + length > jpeg.Length) break;
            segments.Add(new JpegSegment(marker, position + 4, length - 2));
            if (marker == 0xDA) break;
            position += 2 + length;
        }
        return segments;
    }

    /// <summary>Başlıktaki görüntü boyutu; okunamazsa null.</summary>
    public static (int Width, int Height)? ReadSize(ReadOnlySpan<byte> jpeg)
    {
        foreach (var segment in Segments(jpeg))
        {
            // SOF0..SOF15; C4 (Huffman), C8 ve CC (aritmetik) çerçeve başlığı değildir.
            if (segment.Marker is < 0xC0 or > 0xCF || segment.Marker is 0xC4 or 0xC8 or 0xCC) continue;
            if (segment.PayloadLength < 5) return null;
            var payload = jpeg.Slice(segment.PayloadStart, segment.PayloadLength);
            return (BinaryPrimitives.ReadUInt16BigEndian(payload[3..]), BinaryPrimitives.ReadUInt16BigEndian(payload[1..]));
        }
        return null;
    }

    /// <summary>JFIF yoğunluğunu 300 DPI yapar; JFIF bölümü yoksa ekler.</summary>
    public static byte[] WithDpi(byte[] jpeg, int dpi = Dpi)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        if (ReadSize(jpeg) is null) throw new InvalidDataException("Geçerli bir JPEG değil.");

        foreach (var segment in Segments(jpeg))
        {
            if (segment.Marker != 0xE0 || segment.PayloadLength < 12
                || !jpeg.AsSpan(segment.PayloadStart, 5).SequenceEqual("JFIF\0"u8)) continue;
            var copy = (byte[])jpeg.Clone();
            var density = copy.AsSpan(segment.PayloadStart + 7, 5);
            density[0] = 1; // birim: inç
            BinaryPrimitives.WriteUInt16BigEndian(density[1..], (ushort)dpi);
            BinaryPrimitives.WriteUInt16BigEndian(density[3..], (ushort)dpi);
            return copy;
        }

        byte[] app0 =
        [
            0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 1,
            (byte)(dpi >> 8), (byte)dpi, (byte)(dpi >> 8), (byte)dpi, 0, 0,
        ];
        return [.. jpeg.AsSpan(0, 2), .. app0, .. jpeg.AsSpan(2)];
    }

    /// <summary>Dosyayı en az <paramref name="minBytes"/> olacak şekilde yorum (COM) bölümleriyle tamamlar.</summary>
    public static byte[] Pad(byte[] jpeg, int minBytes)
    {
        ArgumentNullException.ThrowIfNull(jpeg);
        if (jpeg.Length >= minBytes) return jpeg;

        // Dolgu, uygulama bölümlerinden (APPn) hemen sonra durur; JFIF başta kalır.
        var insertAt = 2;
        foreach (var segment in Segments(jpeg))
        {
            if (segment.Marker is < 0xE0 or > 0xEF) break;
            insertAt = segment.PayloadStart + segment.PayloadLength;
        }

        using var output = new MemoryStream(minBytes + 8);
        output.Write(jpeg, 0, insertAt);
        var missing = minBytes - jpeg.Length;
        Span<byte> header = stackalloc byte[4];
        while (missing > 0)
        {
            var payload = Math.Min(Math.Max(missing - 4, 0), MaxPayload);
            header[0] = 0xFF;
            header[1] = 0xFE;
            BinaryPrimitives.WriteUInt16BigEndian(header[2..], (ushort)(payload + 2));
            output.Write(header);
            output.Write(new byte[payload]);
            missing -= payload + 4;
        }
        output.Write(jpeg, insertAt, jpeg.Length - insertAt);
        return output.ToArray();
    }

    /// <summary>
    /// Üst boyut sınırına uyan en yüksek kaliteyi bulur: istenen kaliteyle yazar, sınırı aşarsa kaliteyi adım
    /// adım düşürür. Alt sınırın altındaki dosya dolguyla tamamlanır. Android sürümündeki kuralın aynısıdır.
    /// </summary>
    /// <param name="encode">Verilen kalitede JPEG üretir.</param>
    /// <param name="maxBytes">0 ise üst sınır yoktur.</param>
    public static FittedJpeg Fit(Func<int, byte[]> encode, int quality, int minBytes, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(encode);
        var current = Math.Clamp(quality, MinQuality, 100);
        var data = WithDpi(encode(current));
        while (maxBytes > 0 && data.Length > maxBytes && current > MinQuality)
        {
            current = Math.Max(MinQuality, current - (data.Length > maxBytes * 3L / 2 ? 8 : 3));
            data = WithDpi(encode(current));
        }
        data = Pad(data, minBytes);
        var within = data.Length >= minBytes && (maxBytes <= 0 || data.Length <= maxBytes);
        return new FittedJpeg(data, current, within);
    }
}
