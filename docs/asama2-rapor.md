# Aşama 2 raporu: görüntü işleme (28.11.2026)

Dal: `asama-2` · Son geçen derleme: [CI](https://github.com/mceyhan46/C_EOkul/actions/runs/46127756131) (`0b1855d`) · [Yük denemesi](https://github.com/mceyhan46/C_EOkul/actions/runs/46127263835) (`adf173f`; son sürümden yalnız test dosyasındaki uyarı düzeltmesiyle ayrılır)

Fotoğraflar artık **komut satırı aracıyla** işleniyor. Arayüz henüz yok. Sınamalar tek bir kamu malı portreden üretilen görsellerle yapıldı; **gerçek öğrenci fotoğrafıyla denenmedi.**

## Yapılanlar
| Adım | İçerik |
|---|---|
| 1. Açma | Salt okunur; EXIF yönü (JPEG, PNG, WebP, TIFF) |
| 2. Yüz | YuNet; eğim yinelemeli ölçülür; yüz yok / birden fazla yüz ise çıktı üretilmez |
| 3–4. Maske | MODNet, yüz çevresindeki bölgede; kenar inceltme; yalnız yüze bağlı kişi tutulur; olmazsa geniş bölgeyle yeniden denenir |
| 5. Beyaz zemin | Eski zemin rengi kenarlardan arındırılır |
| 6. Kırpma | 133:171, eğim düzeltme, taşan yer beyaz |
| 7. JPEG | Tam 133x171, hafif keskinleştirme, 300 DPI, 20–150 KB |
| 8. Kayıt | Güvenli yazma, `E_Okul` içinde işlem kaydı, kaldığı yerden devam |
| Araç | `eokul-cli`, sürükle-bırak dosyası, öncesi/sonrası görselleri |

Kırpma varsayılanları: saç üstünde %3, çene altında %27, yanlarda %12 boşluk. Ölçülen eğim 20°'yi aşarsa veya yüz yüksekliği 80 pikselin altındaysa sonuç "kontrol gerekli" olur.

## Doğrulananlar (GitHub sunucusu, Windows Server 2025)
- Derleme 0 uyarı; testler **265 / 265**.
- Satır kapsamı: Core %99,9 · Imaging %95,5 · Infrastructure %92,9.
- Asıl dosyalar değişmedi (SHA-256 ve değiştirilme zamanı).
- Arka plan ortalaması **254,97** (hedef ≥254); kişinin hemen dışındaki halka 254,38.
- Eski zemin rengi sızması (yeşil zemin testi): arındırmasız 34,9 → sonuç 1,0.
- Aynı bilgisayarda paralel işlemde çıktılar bayt bayt aynı; farklı işlemcideki başvuru görselinden ortalama fark 0,002 / 255.
- 10 dosyalık örnek (Türkçe yollar): 4 başarılı, 3 kontrol gerekli (bulanık, karanlık, küçük), 3 beklenen hata (iki yüz, yüzsüz, bozuk). İkinci çalıştırmada 7 çıktı atlandı.
- Görsel inceleme: belirgin hale yok; saç kenarları iyi, en ince teller hafif yumuşak.

## Hız ve bellek
Sunucular 2 fiziksel / 4 mantıksal çekirdekli AMD EPYC (16 GB); model çalıştırmadan çalıştırmaya değişiyor, farklı satır grupları birbiriyle kıyaslanmamalı.

| Deneme | Paralel | Fotoğraf başına süre | Toplam hızda fotoğraf başına | En yüksek bellek |
|---|---|---|---|---|
| 40 adet 24 MP (EPYC 9V74) | 1 | 0,85 sn | 0,85 sn | 668 MB |
| 40 adet 24 MP (EPYC 9V74) | 2 | 1,12 sn | 0,56 sn | 1012 MB |
| 200 küçük (EPYC 9V45) | 1 | 0,33 sn | 0,33 sn | 679 MB |
| 200 küçük (EPYC 9V45) | 2 | 0,49 sn | 0,24 sn | 911 MB |
| 200 küçük (EPYC 9V45) | 4 | 0,78 sn | 0,20 sn | 1438 MB |
| **1000 küçük (EPYC 9V45)** | 2 | 0,49 sn | 0,25 sn | 915 MB |

- 1000 fotoğraf 246 saniyede bitti: 571 başarılı, 429 kontrol gerekli (örneklerin bir bölümü bilerek bozuk), 0 hata. Bellek büyümedi (100. fotoğrafta 549 MB, 1000.'de 376 MB). `--devam` 1000'ini de atladı.
- **≤1,5 sn hedefi ortalamada sağlandı, her fotoğrafta değil:** daha yavaş bir sunucuda (EPYC 7763) 10 dosyalık örnekte ilk fotoğraflardan ikisi 1,52 ve 1,69 sn sürdü (ortalama 0,79).
- 24 MP görseller küçük portreden büyütüldü; gerçek kamera dosyasıyla ve okul bilgisayarında ölçülmedi.

## Zayıf noktalar
1. **Eğim düzeltme yeterince doğru değil.** Yapay döndürmede ölçülen değerler: 15° → 8,6°; 8° → 2,7°; −8° → −3,3°; −15° → −11,6°. Başka bir 12° örneğinde sonuç neredeyse tamdı. Aynı yöntemin ön denemesinde (±8–30°) ortalama hata yaklaşık 3°, en çok 6,5°. Kalan eğim için uyarı verilmiyor; test toleransı bu yüzden 7°. Seçenekler: (a) ayrı bir yüz noktası modeli (ör. MediaPipe Face Mesh; lisans denetimi ve onayınız gerekir), (b) Aşama 3'te elle döndürme. İkisi birlikte de olabilir.
2. **Arka plandaki nesne kişide kalabiliyor.** "Küçük" örnekte soldaki kırmızı bayrak silinmedi ve arka plan denetimi bunu yakalamadı (fotoğraf yalnız küçük olduğu için işaretlendi). Sonuçlar gözle kontrol edilmeli.
3. **Çeşitlilik denenmedi:** koyu veya kıvırcık saç, gözlük, başörtüsü, çocuk yüzü, desenli ya da beyaza yakın zemin.

## Diğer doğrulanamayanlar ve eksikler
- Windows 11'de çalışma.
- Sürükle-bırak dosyası otomatik testte çalıştırılmadı.
- 8 ve üstü çekirdekte paralel işlem ayarı.
- Renk profili (ICC, ör. telefonlardaki Display P3) dönüştürülmüyor; renkler hafif farklı çıkabilir.
- "Gözler kapalı olabilir" uyarısı yapılmadı (isteğe bağlıydı).
- Omuz kesikliği uyarısı yalnız alt kenar için; yan kenarlar için yok.
- Ayrı bir inceleyiciyle kod kontrolü yapılmadı.

## Teknik not
OpenCV'nin kenar inceltme süzgeci (guided filter), 3 kanallı ondalık kılavuzla yalnızca bulanıklaştırıyordu (Aşama 0 denemesi bu biçimi kullanmıştı). 8 bit gri kılavuza geçildi.

## Onayınızı bekleyen kararlar
| # | Şu anki davranış | Not |
|---|---|---|
| 1 | Büyük fotoğraflarda maske, kırpma yüksekliği 684 piksel olacak şekilde küçültülmüş kopyada hesaplanır | Şartname "tam ROI çözünürlüğü" diyor; hız ve bellek için sapma (çıktının 4 katı) |
| 2 | En büyük yüzün %35'inden dar yüzler sayılmaz | Arkadaki afiş vb. yüzünden hata vermemesi için |
| 3 | Kişi ayrılamazsa yeni hata: "Arka plan kişiden ayrılamadı", çıktı yok | |
| 4 | "Arka plan güveni düşük": yarı saydam / kesin piksel oranı >0,12 veya göz üstünde yan kenarların >%0,5'i dolu | Az örnekle belirlendi |
| 5 | Beyaza karıştırma normal (sRGB) renk uzayında | Ön denemede yeşil sızma: sRGB 3,5, lineer 1,6 (arındırmasız 18,0). sRGB'de kalmanın gerekçesi kayıtlı değil; isterseniz iki seçeneğin görsellerini karşılaştırıp karar verelim |
| 6 | Paralel fotoğraf sayısı: mantıksal çekirdeğin yarısı, en çok 4 | 4 mantıksal çekirdekte süre yaklaşık %27 kısaldı, bellek yaklaşık 230 MB arttı |

Cevap bekleyen soru: e-Okul'un resmi dosya boyutu sınırı nedir? (Şu an 20–150 KB varsayılıyor; doğrulanmadı.)

## Deneme (isterseniz)
1. [CI sayfasından](https://github.com/mceyhan46/C_EOkul/actions/runs/46127756131) `eokul-foto-komut-satiri` paketini indirip bir klasöre açın (GitHub girişi gerekir; 14 gün saklanır).
2. Fotoğraf klasörünü `Fotograflari-isle.bat` üzerine sürükleyin. Program imzasız olduğundan Windows uyarabilir.
3. Çıktılar her klasörün `E_Okul` klasöründe; karşılaştırma görselleri programın yanındaki `karsilastirma` klasöründe.
4. Karşılaştırma görsellerinde yüzler vardır; işiniz bitince silin. **Öğrenci fotoğraflarını bana veya depoya göndermeyin**; gördüğünüz sorunu tarif etmeniz yeterli.

## Sıradaki adım: Aşama 3
Önce ekran taslakları, onayınızdan sonra arayüz kodu. Başlamak için onayınız bekleniyor.
