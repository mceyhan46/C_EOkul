using System.Globalization;
using EOkulStudio.Core.Files;
using EOkulStudio.Core.Settings;

namespace EOkulStudio.Cli;

internal sealed record CliArguments(
    IReadOnlyList<string> Inputs,
    AppSettings Settings,
    bool Resuming,
    int? Workers,
    string? ComparisonDirectory)
{
    public const string Usage = """
        Kullanım: eokul-cli <klasör veya dosya>... [seçenekler]

          --alt-klasorsuz                      Alt klasörleri tarama
          --var-olan atla|uzerine-yaz|sonek    Çıktı zaten varsa (varsayılan: atla)
          --devam                              Yarım kalan işi sürdür
          --calisan N                          Aynı anda işlenen fotoğraf sayısı (1-16)
          --kalite N                           JPEG kalitesi (70-100, varsayılan 100)
          --en-az-kb N                         En küçük dosya boyutu (varsayılan 20)
          --en-cok-kb N                        En büyük dosya boyutu (varsayılan 150)
          --karsilastirma <klasör>             Öncesi/sonrası görselleri bu klasöre yaz

        Çıktılar her fotoğraf klasörünün içindeki E_Okul klasörüne yazılır; asıl dosyalar değiştirilmez.
        """;

    /// <exception cref="ArgumentException">Geçersiz seçenek.</exception>
    public static CliArguments Parse(IReadOnlyList<string> args)
    {
        var inputs = new List<string>();
        var settings = new AppSettings();
        var resuming = false;
        int? workers = null;
        string? comparison = null;

        for (var i = 0; i < args.Count; i++)
        {
            string Value() => ++i < args.Count ? args[i] : throw new ArgumentException($"{args[i - 1]} için değer eksik.");
            int Number()
            {
                var name = args[i];
                return int.TryParse(Value(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                    ? number
                    : throw new ArgumentException($"{name} için sayı bekleniyor.");
            }

            switch (args[i])
            {
                case "--alt-klasorsuz":
                    settings = settings with { IncludeSubfolders = false };
                    break;
                case "--var-olan":
                    var rule = Value();
                    settings = settings with
                    {
                        ExistingOutput = rule switch
                        {
                            "atla" => ExistingOutputPolicy.Skip,
                            "uzerine-yaz" => ExistingOutputPolicy.Overwrite,
                            "sonek" => ExistingOutputPolicy.AddSuffix,
                            _ => throw new ArgumentException($"Bilinmeyen kural: {rule}"),
                        },
                    };
                    break;
                case "--devam":
                    resuming = true;
                    break;
                case "--calisan":
                    workers = Number();
                    break;
                case "--kalite":
                    settings = settings with { JpegQuality = Number() };
                    break;
                case "--en-az-kb":
                    settings = settings with { MinFileSizeKb = Number() };
                    break;
                case "--en-cok-kb":
                    settings = settings with { MaxFileSizeKb = Number() };
                    break;
                case "--karsilastirma":
                    comparison = Value();
                    break;
                case var option when option.StartsWith("--", StringComparison.Ordinal):
                    throw new ArgumentException($"Bilinmeyen seçenek: {option}");
                default:
                    inputs.Add(args[i]);
                    break;
            }
        }

        if (inputs.Count == 0) throw new ArgumentException("Klasör veya dosya verilmedi.");
        if (comparison is not null) CheckOutsideSources(Path.GetFullPath(comparison), inputs);
        return new CliArguments(inputs, settings.Normalized(), resuming, workers, comparison);
    }

    /// <summary>Karşılaştırma görselleri fotoğraf klasörlerine yazılmaz.</summary>
    private static void CheckOutsideSources(string comparison, IEnumerable<string> inputs)
    {
        foreach (var input in inputs)
        {
            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(input));
            var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
            var relative = Path.GetRelativePath(folder, comparison);
            var outside = relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                          || Path.IsPathRooted(relative);
            if (!outside)
                throw new ArgumentException("Karşılaştırma klasörü, fotoğraf klasörünün dışında olmalı.");
        }
    }
}
