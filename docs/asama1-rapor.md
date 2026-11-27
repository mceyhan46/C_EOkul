# Aşama 1 raporu: iskelet ve Core (27.11.2026)

Dal: `asama-1` · Son geçen derleme: [CI çalıştırması](https://github.com/mceyhan46/C_EOkul/actions/runs/46012765198) (`e5eccac`)

Bu sürüm **henüz fotoğraf işlemez.** Program yalnızca bir yer tutucu pencere açar.

## Yapılanlar
| Katman | İçerik |
|---|---|
| Çözüm | `EOkulStudio.sln` (yalnız x64), merkezi paket sürümleri, uyarılar hata sayılır |
| Core | Dosya süzgeci, `E_Okul` yolu ve hariç tutma, çıktı adı ve çakışma, var olan çıktı kararı, sorun açıklamaları, işlem kaydı modeli, kırpma geometrisi ve kadraj denetimi, ayar doğrulama |
| Infrastructure | Klasör tarayıcı, güvenli yazma (geçici dosya → doğrula → yer değiştir), gizli işlem kaydı, ayar dosyası |
| Imaging | Yalnız yerel kütüphane hazırlığı (boru hattı Aşama 2) |
| App | DI, Serilog (14 günlük), genel hata yakalama, Windows temasını izleyen WPF-UI penceresi |
| CI | Windows'ta derleme, test, kapsam eşiği, tek dosya yayım, DLL denetimi, duman testi |

## Doğrulananlar (GitHub Windows sunucusu)
- Derleme: 0 uyarı, 0 hata.
- Testler: 158 / 158 geçti.
- Satır kapsamı: Core %99,7 (CI eşiği %85), Infrastructure %92,8.
- Kırpma hesabı, Android sürümünün Java kodundan üretilen 40 örnekle aynı (tolerans 0,000001 piksel).
- Testlerde: tarama hiçbir dosyayı değiştirmiyor; Türkçe karakterli ve 260 karakterden uzun yollar; bağlantı (symlink) klasörleri; bozuk ayar ve işlem kaydı.
- Tek dosya paket, Türkçe karakterli klasörden açıldı; pencere çizildi; OpenCV 4.13.0 ve ONNX Runtime 1.32.0 yüklendi.
- Eksik Visual C++ DLL yok. Paket: 8 dosya, 253,7 MB.

## Doğrulanamayanlar
- Windows 11'de çalışma (CI sunucusu Windows Server 2025).
- Pencerenin görünümü ve açık/koyu tema: ekran görüntüsü incelenmedi.
- Fotoğraf işleme, hız (≤1,5 sn), 1000 fotoğraflık iş, görüntü determinizmi: Aşama 2–3.
- Ayrı bir inceleyiciyle kontrol yapılamadı (kullanım sınırına takıldı). Kodu kendim yeniden inceledim.

## İncelemede bulunup düzeltilenler
1. Başarısız yeniden işlemden sonra eski çıktının programa ait olduğu unutuluyordu; düzeltilen fotoğraf "atlanabilirdi".
2. `E_Okul` bir bağlantı klasörüyse asıl fotoğrafın üzerine yazılabilirdi; artık yazılmıyor.
3. Tarayıcı her alt klasörde gereksiz bağlantı sorgusu yapıyordu; izin hatasında tarama kesilebilirdi.
4. Model dosyaları eksikken paket hatasız üretilebiliyordu.

## Teknik not
xUnit v3 4 ve .NET 10 nedeniyle testler Microsoft.Testing.Platform ile çalışıyor; kapsam için `Microsoft.Testing.Extensions.CodeCoverage` kullanılıyor (yalnız test; programla dağıtılmaz). WPF-UI lisansı: [MIT](https://www.nuget.org/packages/WPF-UI/4.3.0).

## Onayınızı bekleyen kararlar
| # | Şu anki davranış | Not |
|---|---|---|
| 1 | JPEG kalitesi 100, dosya 20–150 KB; boşluklar üst %3, alt %27, yanlar %12 | Android sürümünden alındı; **resmi e-Okul kuralıyla doğrulanmadı** |
| 2 | Asıl fotoğraf değişmişse, ayar "atla" olsa da programın kendi eski çıktısı yenilenir | |
| 3 | Alt klasörler varsayılan olarak taranır | |
| 4 | Kırpma fotoğrafın dışına taşarsa beyaz doldurulur | Android sürümü kırpmayı sınıra sıkıştırıyordu |
| 5 | `.jfif` desteklenmiyor olarak listelenir | |
| 6 | Yolunda `E_Okul` adlı klasör bulunan fotoğraflar reddedilir | Asıl fotoğrafları bu adlı bir klasörde tutmayın |

## Sıradaki adım: Aşama 2
Görüntü işleme boru hattı (8 adım), arayüzsüz komut satırı aracı, saç/kenar kalitesi için öncesi–sonrası görselleri.
Başlamak için onayınız bekleniyor.
