using EOkulStudio.Core.Records;

namespace EOkulStudio.Core.Files;

public enum ExistingOutputPolicy
{
    Skip,
    Overwrite,
    AddSuffix,
}

public enum OutputAction
{
    /// <summary>Atanan adla yaz (varsa üzerine).</summary>
    Write,

    /// <summary>Var olan dosyaya dokunma; yeni bir "_N" adıyla yaz.</summary>
    WriteWithSuffix,

    /// <summary>Yarım kalan iş sürdürülürken zaten tamamlanmış.</summary>
    SkipAlreadyProcessed,

    /// <summary>Çıktı var ve ayar "atla".</summary>
    SkipExisting,
}

public static class OutputDecision
{
    /// <param name="outputExists">Atanan adla dosya E_Okul klasöründe var mı?</param>
    /// <param name="entry">Bu kaynağın önceki kaydı.</param>
    /// <param name="sourceSha256">Kaynağın şimdiki özeti.</param>
    /// <param name="resuming">Yarım kalan bir iş mi sürdürülüyor?</param>
    public static OutputAction Decide(
        bool outputExists,
        RecordEntry? entry,
        string sourceSha256,
        ExistingOutputPolicy policy,
        bool resuming)
    {
        if (!outputExists) return OutputAction.Write;

        if (entry?.OutputSha256 is { } written)
        {
            var unchanged = string.Equals(written, sourceSha256, StringComparison.OrdinalIgnoreCase);
            if (!unchanged) return OutputAction.Write; // fotoğraf değişmiş: kendi eski çıktısı yenilenir
            if (resuming && entry.HasOutput) return OutputAction.SkipAlreadyProcessed;
        }

        return policy switch
        {
            ExistingOutputPolicy.Overwrite => OutputAction.Write,
            ExistingOutputPolicy.AddSuffix => OutputAction.WriteWithSuffix,
            _ => OutputAction.SkipExisting,
        };
    }
}
