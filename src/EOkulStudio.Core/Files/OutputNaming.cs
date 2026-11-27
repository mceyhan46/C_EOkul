using System.Globalization;

namespace EOkulStudio.Core.Files;

/// <summary>
/// Bir klasördeki kaynak dosyalara çıktı adı atar: kaynak adı + ".jpg"; aynı ada düşenler "_2", "_3" eki alır.
/// Sonuç dosyaların veriliş sırasından bağımsızdır. Daha önce kaydedilmiş eşlemeler korunur.
/// </summary>
public static class OutputNaming
{
    public const string Extension = ".jpg";

    /// <summary>Windows dosya adı sınırı.</summary>
    public const int MaxNameLength = 255;

    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    /// <param name="sourceFileNames">Aynı klasördeki kaynak dosya adları (yol içermez).</param>
    /// <param name="recorded">İşlem kaydındaki kaynak adı → çıktı adı eşlemeleri.</param>
    /// <returns>Kaynak adı → çıktı adı.</returns>
    public static IReadOnlyDictionary<string, string> Assign(
        IEnumerable<string> sourceFileNames,
        IReadOnlyDictionary<string, string>? recorded = null)
    {
        var sources = sourceFileNames
            .Distinct(Names)
            .Order(Names)
            .ToList();

        var result = new Dictionary<string, string>(Names);
        var taken = new HashSet<string>(Names);
        var kept = new Dictionary<string, string>(Names);

        if (recorded is not null)
        {
            // Listede artık olmayan kaynakların adları da ayrılır: onların çıktısının üzerine yazılmaz.
            foreach (var (source, output) in recorded.OrderBy(pair => pair.Key, Names))
                if (taken.Add(output)) kept[source] = output;
        }

        foreach (var source in sources)
            if (kept.TryGetValue(source, out var output)) result[source] = output;

        // Önce herkesin kendi doğal adı: "a_2.png" varken ikinci "a" dosyası "a_2.jpg" adını almaz.
        foreach (var source in sources)
        {
            if (result.ContainsKey(source)) continue;
            var natural = Compose(Path.GetFileNameWithoutExtension(source), string.Empty);
            if (taken.Add(natural)) result[source] = natural;
        }

        foreach (var source in sources)
        {
            if (result.ContainsKey(source)) continue;
            var natural = Compose(Path.GetFileNameWithoutExtension(source), string.Empty);
            var suffixed = NextFree(natural, taken.Contains);
            taken.Add(suffixed);
            result[source] = suffixed;
        }

        return result;
    }

    /// <summary>"ad.jpg" için kullanılmayan ilk "ad_N.jpg" (N ≥ 2).</summary>
    public static string NextFree(string outputName, Func<string, bool> isTaken)
    {
        var stem = Path.GetFileNameWithoutExtension(outputName);
        for (var number = 2; ; number++)
        {
            var candidate = Compose(stem, "_" + number.ToString(CultureInfo.InvariantCulture));
            if (!isTaken(candidate)) return candidate;
        }
    }

    private static string Compose(string stem, string suffix)
    {
        var room = MaxNameLength - Extension.Length - suffix.Length;
        if (stem.Length > room)
        {
            // Vekil çift (emoji vb.) ortadan bölünmez.
            if (char.IsHighSurrogate(stem[room - 1])) room--;
            stem = stem[..room];
        }
        return stem + suffix + Extension;
    }
}
