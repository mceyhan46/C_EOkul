using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace EOkulStudio.Infrastructure;

internal static class JsonFiles
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        // Türkçe harfler dosyada okunur kalsın.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
}
