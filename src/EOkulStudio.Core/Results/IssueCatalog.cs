namespace EOkulStudio.Core.Results;

/// <param name="Message">Sorunun sade Türkçe açıklaması.</param>
/// <param name="Action">Kullanıcının yapabileceği şey.</param>
/// <param name="BlocksOutput">Doğruysa çıktı üretilmez.</param>
public sealed record IssueInfo(string Message, string Action, bool BlocksOutput);

public static class IssueCatalog
{
    public static IssueInfo Get(IssueCode code) => code switch
    {
        IssueCode.FileNotFound => Blocking("Dosya bulunamadı.", "Dosyanın yerinde olduğunu kontrol edip yeniden deneyin."),
        IssueCode.CorruptFile => Blocking("Dosya bozuk veya okunamıyor.", "Fotoğrafı yeniden kopyalayın."),
        IssueCode.UnsupportedFormat => Blocking("Bu dosya türü desteklenmiyor.", "Fotoğrafı JPG veya PNG olarak kaydedip ekleyin."),
        IssueCode.InsideOutputFolder => Blocking("Bu dosya bir E_Okul çıktı klasörünün içinde.", "E_Okul klasörünün dışındaki asıl fotoğrafları seçin."),
        IssueCode.NoFace => Blocking("Yüz bulunamadı.", "Kırpmayı elle yerleştirin veya başka bir fotoğraf kullanın."),
        IssueCode.MultipleFaces => Blocking("Birden fazla yüz var.", "Kullanılacak yüzü seçin."),
        IssueCode.OutputNotWritable => Blocking("Klasöre yazılamıyor.", "Fotoğrafları yazma izni olan bir klasöre kopyalayın."),
        IssueCode.ProcessingError => Blocking("Fotoğraf işlenirken hata oluştu.", "Tekrar deneyin; sürerse program günlüğünü gönderin."),

        IssueCode.FaceTooSmall => Review("Yüz fotoğrafta çok küçük.", "Yakından çekilmiş bir fotoğraf kullanın."),
        IssueCode.LowResolution => Review("Fotoğrafın çözünürlüğü düşük; sonuç bulanık olabilir.", "Daha büyük bir fotoğraf kullanın."),
        IssueCode.Blurry => Review("Fotoğraf bulanık görünüyor.", "Sonucu kontrol edin."),
        IssueCode.LowLight => Review("Fotoğraf karanlık görünüyor.", "Sonucu kontrol edin."),
        IssueCode.ExcessiveTilt => Review("Baş fazla eğik.", "Sonucu kontrol edin."),
        IssueCode.EyesMaybeClosed => Review("Gözler kapalı olabilir.", "Sonucu kontrol edin."),
        IssueCode.UncertainBackground => Review("Arka plan temizliği emin değil.", "Saç ve omuz kenarlarını kontrol edin."),
        IssueCode.HeadCutInSource => Review("Başın üstü asıl fotoğrafta kesik.", "Mümkünse fotoğrafı yeniden çekin."),
        IssueCode.ShouldersCut => Review("Omuz veya gövde asıl fotoğrafta kesik; altta beyaz boşluk kalıyor.", "Kırpmayı düzeltin veya daha geniş bir fotoğraf kullanın."),
        IssueCode.HeadCut => Review("Başın üstü çerçevenin dışında.", "Çerçeveyi yukarı taşıyın."),
        IssueCode.HeadTight => Review("Başın üstünde boşluk çok az.", "Çerçeveyi biraz yukarı alın."),
        IssueCode.ChinCut => Review("Çene çerçevenin alt kenarına çok yakın.", "Çerçeveyi aşağı alın."),
        IssueCode.FaceSmallInFrame => Review("Yüz çerçevede küçük kalıyor.", "Çerçeveyi daraltın."),
        IssueCode.FaceLargeInFrame => Review("Yüz çerçeveyi fazla dolduruyor.", "Çerçeveyi genişletin."),
        IssueCode.OffCenter => Review("Yüz yatayda ortalı değil.", "Çerçeveyi yüze doğru kaydırın."),
        IssueCode.EyeLevel => Review("Göz hizası beklenen aralığın dışında.", "Çerçeveyi yukarı veya aşağı kaydırın."),
        IssueCode.EyeTilt => Review("Göz çizgisi eğik.", "Çerçeveyi döndürün."),
        IssueCode.FileSizeOutOfRange => Review("Dosya boyutu ayarlanan sınırların dışında.", "Ayarlardaki boyut sınırlarını kontrol edin."),
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    private static IssueInfo Blocking(string message, string action) => new(message, action, true);

    private static IssueInfo Review(string message, string action) => new(message, action, false);
}
