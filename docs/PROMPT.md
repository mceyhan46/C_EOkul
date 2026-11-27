# GÖREV: "E-Okul Foto Studio" Windows masaüstü uygulaması (C# / WPF) — SIFIRDAN YENİ PROJE

Sen kıdemli bir .NET masaüstü geliştiricisi ve görüntü işleme mühendisisin. Türkçe, kısa ve dürüst yanıt ver. Derleyemediğin/çalıştıramadığın şeyi "çalışıyor" diye sunma; neyi doğruladığını, neyi doğrulayamadığını açıkça yaz.

## 1. Ürün
Var olan öğrenci fotoğraflarını **toplu ve otomatik** olarak vesikalığa dönüştüren, tamamen çevrimdışı çalışan bir **Windows 11** uygulaması. (Android sürümü mevcut: Kotlin/Compose, repo mceyhan46/EOkul_Foto — yalnızca kırpma/çıktı kuralları için bakabilirsin; o depoda değişiklik yapma.)

**KAPSAM DIŞI (ekleme, iskeletini bile kurma):** kamera/webcam ile çekim, sınıf listesi içe aktarma (Excel/CSV), sınıf/öğrenci yönetimi, ad-numara eşleştirme, fotoğrafı öğrenciler arasında taşıma/yer değiştirme, CSV veya başka rapor dosyası, ZIP çıktısı.

**Akış:**
1. Kullanıcı istediği klasörü seçer (alternatif: tek/çoklu dosya seçimi, sürükle-bırak). "Alt klasörleri dahil et" seçeneği olur.
2. Uygulama desteklenen dosyaları (JPG, JPEG, PNG, BMP, TIFF, WebP) listeler ve işlemeyi **otomatik başlatır** (ayardan kapatılabilir). HEIC gibi desteklenmeyen dosyaları sessizce atlama; listede nedeniyle göster.
3. Her fotoğrafta yüzü bulur, **saç teline kadar kaliteli arka plan temizler, arka planı SAF BEYAZ (255,255,255) yapar**, vesikalık kuralına göre kırpar, **133x171 px** JPEG olarak kaydeder.
4. İlerleme çubuğu, kalan süre, iptal ve kaldığı yerden devam. Bir dosyadaki hata tüm işlemi durdurmaz.
5. İşlem sonunda ekranda özet: Başarılı / Kontrol gerekli / Başarısız. Filtrelenebilir küçük resim listesi ve öncesi-sonrası görünümü (rapor dosyası üretilmez).
6. Sorunlu veya beğenilmeyen fotoğraflarda kırpma kutusu elle düzeltilip kaynak dosyadan yeniden üretilebilir (isteğe bağlı adım; otomatik akışı kesmez).

**Dosya kuralları:**
- Kaynak dosyalar **salt okunur** kabul edilir: asla değiştirilmez, taşınmaz, silinmez.
- Çıktılar, **işlenen her fotoğrafın bulunduğu klasörün içindeki `E_Okul` klasörüne** yazılır (`A\x.jpg` → `A\E_Okul\x.jpg`; `A\B\y.jpg` → `A\B\E_Okul\y.jpg`). Farklı klasörlerden seçilen veya sürüklenen dosyalarda da aynı kural geçerlidir; ayrıca çıktı klasörü sorulmaz. Kaynak klasöre bunun dışında hiçbir şey yazılmaz.
- Adı `E_Okul` olan klasörler (büyük/küçük harf duyarsız) taramaya dahil edilmez; kullanıcı doğrudan böyle bir klasörü seçerse işleme yapılmaz, nedeni gösterilir. `E_Okul` yalnızca yazılacak çıktı varsa oluşturulur. Klasöre yazılamıyorsa (salt okunur disk, izin yok) o dosyalar nedeni ve çözüm önerisiyle "Başarısız" listelenir.
- Çıktı adı = kaynak dosya adı + `.jpg`. Aynı ada düşen dosyalar (ör. `a.png` ve `a.jpg`) birbirini ezmez, sonek alır (`a_2.jpg`); eşleme deterministik olur.
- Çıktı zaten varsa davranış ayarlanabilir: atla (varsayılan) / üzerine yaz / sonek ekle.
- Güvenli yazma: geçici dosya → doğrula → atomik yer değiştir. Yarım dosya kalmaz.
- Yüz yoksa veya birden çok yüz varsa otomatik çıktı üretme; "Başarısız / yüz seçilmeli" olarak listele. Güveni düşük sonuçlar kaydedilir ama "Kontrol gerekli" işaretlenir. Elle düzeltme, o fotoğrafın kendi çıktısının üzerine yazar.
- Ayarlar: son kullanılan klasörler, JPEG kalitesi, dosya boyutu üst sınırı, kırpma kuralları, matting modeli.

### 1.1 Kullanıcı ekranı (pratik, kullanışlı, ergonomik)
Hedef kullanıcı teknik olmayan okul personelidir. Arayüz tamamen Türkçe; hiçbir ayara dokunmadan "klasör seç → sonuç al" akışı çalışmalı.
- **Tek pencere, üç durum:**
  1. **Başlangıç:** ortada büyük sürükle-bırak alanı, tek belirgin düğme **Klasör Seç**, ikincil **Fotoğraf Seç**, "Alt klasörleri dahil et" kutusu, son kullanılan klasörler.
  2. **İşleniyor:** ilerleme çubuğu, "37 / 120", kalan süre, işlenen fotoğrafların canlı küçük resimleri, **İptal** düğmesi. Yarım kalan iş açılışta **Devam Et** olarak sunulur.
  3. **Sonuçlar:** üstte sayılı filtreler (Tümü / Kontrol gerekli / Başarısız / Başarılı; sorun varsa ilk açılışta sorunlular seçili), solda küçük resim ızgarası, sağda büyük öncesi-sonrası önizleme ve sorun açıklaması. Düğmeler: **Onayla**, **Kırpmayı Düzelt**, **Tekrar Dene**, **E_Okul Klasörünü Aç**, **Yeni İşlem**.
- **Kırpma düzeltme:** oranı kilitli (133:171) çerçeve; fareyle sürükleme, tekerlekle yakınlaştırma, ok tuşlarıyla ince ayar, döndürme; baş üstü / göz hattı / çene kılavuz çizgileri; canlı 133x171 önizleme; **Kaydet ve Sonraki**, **Otomatiğe Dön**. Birden çok yüzde doğru yüz tıklanarak seçilir; yüz bulunamadıysa çerçeve elle yerleştirilir.
- **Klavye:** Ctrl+O klasör seç, oklar önceki/sonraki fotoğraf, Enter onayla, E düzelt, Esc geri/iptal. Odak sırası mantıklı, kısayollar ipucu metninde görünür.
- **Ergonomi:** ana düğmeler her durumda aynı yerde; tıklama hedefi en az 32x32 px; durum yalnız renkle değil simge + metinle; gereksiz onay penceresi yok (yalnız iptal ve üzerine yazma gibi geri alınamaz işlemlerde); hata mesajları sade Türkçe ve ne yapılacağını söyler (teknik ayrıntı yalnız logda).
- 1366x768 ekranda kaydırmasız kullanılabilir; %100–200 ölçeklemede bozulmaz; pencere boyutu ve konumu hatırlanır. Gelişmiş ayarlar dişli simgesinin arkasında, **Varsayılanlara Dön** ile.

## 2. Teknoloji kararları (SABİT — uyumluluk için)
- **.NET 10 LTS**, `net10.0-windows`, **WPF**, **x64 only** (`PlatformTarget=x64`, `RuntimeIdentifier=win-x64`; AnyCPU YASAK — yerel DLL yükleme hatası çıkarır). Hedef: **yalnızca Windows 11 x64** (ana test: 24H2 ve üzeri). Windows 10 desteği gerekmiyor.
- Mimari: MVVM (**CommunityToolkit.Mvvm**), `Microsoft.Extensions.DependencyInjection/Hosting`, **Serilog** (dosyaya log), katmanlar: `Core` (saf kurallar, UI/IO bağımsız — kolay test), `Imaging` (matting/yüz/kırpma), `Infrastructure` (dosya tarama, güvenli yazma, işlem kaydı), `App` (WPF). Çözüm: `EOkulStudio.sln`.
- **Arka plan/alfa (matting):** ONNX Runtime ile **MODNet (photographic portrait matting, ONNX)** birincil model. Yalnızca "mask" değil **alfa matte** (yarı saydam saç telleri) üretilecek. Modeli `IMattingModel` ile soyutla; daha kaliteli model (ör. BiRefNet portrait / ViTMatte ONNX) sonradan takılabilsin, ayarlardan seçilsin.
- **Kenar iyileştirme (saç teli kalitesi):** model alfa'sını tam çözünürlükte **Guided Filter** (OpenCvSharp.XImgProc veya kendi uygulaman) ile rafine et; ardından **ön plan rengi arındırma (foreground color decontamination / estimate foreground)** uygula ki saç kenarında eski arka plan rengi (yeşil/gri halka, "halo") kalmasın. Sonra `C = α·F + (1−α)·beyaz` ile beyaza kompozit et. Sadece alfa ile çarpıp beyaza basmak YETERSİZDİR.
- **Yüz bulma + hizalama:** OpenCV `FaceDetectorYN` + **YuNet ONNX** (5 yüz noktası) → göz hattı ve baş eğimi (roll) ile kırpmayı hizala. Yüz yok / birden çok yüz / çok küçük / çok eğik durumları ayrı uyarı kodlarıyla işlensin.
- **Görüntü:** `OpenCvSharp4` + `OpenCvSharp4.runtime.win` (contrib/ximgproc içerdiğini DOĞRULA; içermiyorsa guided filter'ı kendin yaz). EXIF yönü için `MetadataExtractor` veya hafif bir çözüm; yön iki kez uygulanmasın. Türkçe karakterli ve uzun dosya yollarında okuma/yazma çalışmalı (gerekirse bayt dizisi + `ImDecode`/`ImEncode`). SixLabors.ImageSharp lisansı (Split License) nedeniyle varsayılan DEĞİL.
- **ONNX Runtime:** `Microsoft.ML.OnnxRuntime` (CPU) varsayılan. GPU isteğe bağlı: `Microsoft.ML.OnnxRuntime.DirectML` — **ikisini aynı projeye birlikte ekleme**; ayrı yapılandırma olarak sun, CPU'ya otomatik geri dönüş olsun. Güncel kararlı sürümleri seç ve **sabitle** (`Directory.Packages.props`); sürüm seçimini ve model opset/IR uyumunu raporla.
- **Toplu işleme:** UI'den bağımsız, sınırlı eşzamanlılıklı kuyruk (`System.Threading.Channels`); ONNX iş parçacığı sayısı ile paralel dosya sayısını birlikte ayarla (CPU'yu aşırı yükleme); bellek kullanımı dosya sayısıyla büyümesin.
- **Ayar/işlem kaydı:** `System.Text.Json` (veritabanı yok).
- **UI:** WPF + **WPF-UI (Fluent)** tercih edilir (Windows 11 görünümü; lisans ve .NET 10 uyumunu doğrula, sorun varsa alternatif öner). Ekran davranışı için 1.1'e uy. Açık/koyu tema, DPI uyumlu, klavye erişilebilirliği, yüksek kontrast, sanallaştırılmış küçük resim listesi.
- **Test:** xUnit + AwesomeAssertions (veya FluentAssertions 7.x; 8+ ticari lisans ister); Core için birim testleri; Imaging için "golden image" testleri.
- **Paketleme:** `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`. `PublishTrimmed` varsayılan kapalı. Modeller exe yanında `models/` klasöründe, **SHA-256 ile bütünlük kontrolü**. Kurulum: **Inno Setup** betiği (`MinVersion=10.0.22000`; VC++ 2015-2022 x64 Redistributable kontrolü/kurulumu). Exe imzalama için yer hazırla (SmartScreen).
- **Lisans uyumu:** her bağımlılık ve MODEL için lisansı tabloya yaz ve kaynağından doğrula (beklenen: MODNet Apache-2.0, YuNet MIT, BiRefNet MIT). **Ticari kullanıma kapalı modelleri (ör. RMBG-1.4) KULLANMA.** Okullara dağıtılacak.
- **Depo ve CI:** kod, kullanıcının vereceği yeni GitHub deposunda tutulur. GitHub Actions (`windows-latest`) ile derleme + test **Aşama 0'dan itibaren** kurulur. `*.onnx` dosyaları depoya eklenmez; sabit adresten indirilip SHA-256 ile doğrulanır. Depoya, loglara ve CI artefaktlarına **gerçek öğrenci fotoğrafı konmaz** (KVKK); testlerde kullanım izni açık veya sentetik görseller kullanılır.

## 3. Görüntü işleme boru hattı (kesin sıra)
1. Kaynak dosyayı salt okunur aç → EXIF yönünü uygula.
2. YuNet ile yüz + noktalar → yüz kutusu, göz hattı, eğim. (Büyük görsellerde tespit küçültülmüş kopyada yapılır, koordinatlar geri ölçeklenir.)
3. Yüz çevresinde makul bir ROI al (omuzlar + saç üstü payı); matting modelini uygun giriş boyutunda (MODNet: ~512 kısa kenar, 32'nin katı) çalıştır; normalizasyon: (x−127.5)/127.5, RGB, NCHW float32.
4. Alfa'yı tam ROI çözünürlüğüne bicubic büyüt → Guided Filter (radius/eps ayarlanabilir) → [0,1] sıkıştır; alfa levels ile küçük gürültüyü temizle.
5. Ön plan rengi arındırma → beyaza kompozit (lineer uzayda karıştırma seçeneğini değerlendir).
6. Vesikalık kırpma: oran 133:171; baş üstü-çene ve göz hattı konumu ayarlanabilir (varsayılanları belgele); eğim düzeltme; sınır dışına taşarsa BEYAZ dolgu. Omuz/saç kaynak fotoğrafta kesikse uyar.
7. Yüksek kaliteli küçültme (INTER_AREA / Lanczos), hafif unsharp, **tam 133x171**, JPEG kalitesi ayarlanabilir; dosya boyutu üst sınırı yapılandırılabilir (e-Okul sınırını sor, uydurma).
8. Atomik kaydet. İşlem kaydına (`E_Okul\.eokul_islem.json`, gizli dosya) kaynak yolu + SHA-256, kırpma kutusu, model/ayar sürümü ve durum yaz (yeniden üretim ve kaldığı yerden devam için).
- Her aşama `IProgress` + `CancellationToken` destekli, UI'yi bloklamaz; ONNX oturumu bir kez oluşturulup yeniden kullanılır; `Mat`/`OrtValue` dispose edilir.
- Hata/uyarı sınıfları: bozuk/okunamayan dosya, yüz yok, çoklu yüz, düşük çözünürlük, bulanık, düşük ışık, aşırı eğik, gözler kapalı olabilir (isteğe bağlı), arka plan temizleme güveni düşük (belirsiz alfa piksel oranı eşiği).

## 4. ÇALIŞMA SIRASI (her aşamada dur ve rapor ver)
**Aşama 0 — Uyumluluk Spike'ı (uygulama kodundan önce ZORUNLU):** boş bir projede: (a) seçilen `OnnxRuntime` sürümüyle MODNet ve YuNet'i yükle, örnek portrede çıkarım yap; (b) OpenCvSharp runtime'ın yüklendiğini, `FaceDetectorYN` ve `XImgProc.GuidedFilter` erişilebilirliğini doğrula; (c) self-contained single-file publish et; `windows-latest` üzerinde çalıştır, ayrıca TEMİZ Windows 11'de (VC++ Redist'li/siz) denemem için artefakt ve adım adım test talimatı ver (CI sunucusu Windows 11 değildir, ikisini ayrı raporla); (d) exe + model + DLL boyutlarını ÖLÇ ve tablo ver. Sorun çıkarsa alternatif öner; onayım olmadan mimariyi değiştirme.
**Aşama 1 — İskelet + Core:** çözüm yapısı, DI, tema, Serilog, ayarlar; Core kuralları + testler (dosya tarama filtresi, çıktı adı ve çakışma kuralları, her klasör için `E_Okul` yolu, `E_Okul` klasörlerini hariç tutma, kırpma geometrisi).
**Aşama 2 — Imaging boru hattı:** 3. bölümdeki 8 adım, UI'siz bir CLI aracıyla klasör işleme; halo, saç teli, kenar keskinliği için öncesi/sonrası ızgara görselleri üret.
**Aşama 3 — Arayüz:** önce 1.1'e göre ekran taslaklarını göster ve onayımı al; sonra klasör/dosya seçimi, otomatik toplu işleme, ilerleme/iptal/devam, sonuç listesi, elle kırpma düzeltme.
**Aşama 4 — Paketleme:** publish, Inno Setup, SHA-256 kontrolü, sürümleme, CI'da kurulum dosyasının üretilip GitHub Release olarak yayımlanması.
Her aşama sonunda: ne bitti, ne test edildi, ne doğrulanamadı, bir sonraki adım.

## 5. Kabul ölçütleri
- Temiz Windows 11 x64'te kurulum + internetsiz çalışma; "DLL bulunamadı / BadImageFormat" yok.
- Klasör seçildikten sonra ek işlem gerekmeden tüm uygun fotoğraflar işlenir ve her klasörün çıktısı kendi `E_Okul` klasöründe oluşur.
- İlk kez kullanan biri açıklama okumadan klasör seçip sonuçlara ulaşabilir; tüm ana işlemler klavyeyle de yapılabilir.
- Kaynak dosyaların içeriği işlem öncesi ve sonrası birebir aynı (SHA-256 testi).
- Saç tellerinde belirgin halo yok; arka plan bölgesinde ortalama piksel ≥ 254; çıktı tam 133x171.
- 1 fotoğraf CPU'da hedef ≤ 1,5 sn (donanımı belirterek ölç).
- 1000 fotoğraflık toplu işlem çökmeden tamamlanır, bellek sürekli artmaz, arayüz yanıt vermeye devam eder.
- Aynı girdi + aynı ayar + aynı donanım/çalıştırma sağlayıcı = aynı çıktı.
- Core katmanı test kapsamı yüksek; CI yeşil.

## 6. Başlamadan önce bana SADECE şunları sor (tek mesajda)
e-Okul'un kabul ettiği dosya boyutu/biçim sınırı, GPU'suz bilgisayarlarda hedef işlem süresi, kurulumun internetsiz yapılıp yapılmayacağı, HEIC desteği gerekip gerekmediği. Cevap beklemeden Aşama 0'a başla.
