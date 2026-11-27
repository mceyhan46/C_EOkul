# Aşama 0 raporu: uyumluluk denemesi (27.11.2026)

## Sonuç
GitHub `windows-latest` sunucusunda tüm denemeler **geçti** ([çalıştırma](https://github.com/mceyhan46/C_EOkul/actions/runs/45920558521), dal `asama-0`).
Gerçek bir Windows 11 bilgisayarda **henüz denenmedi**.

## Doğrulananlar
Ortam: Windows Server 2025 (10.0.26100), x64, .NET 10.0.15, 4 mantıksal işlemci, CPU.

| Madde | Sonuç |
|---|---|
| (a) ONNX Runtime 1.32.0 ile MODNet çıkarımı | Geçti; aynı süreçte 6 çalıştırma bit bit aynı |
| (a) ONNX Runtime ile YuNet çıkarımı | Geçti; en yüksek yüz skoru 0,944 |
| (b) OpenCvSharp / OpenCV 4.13.0 yükleme | Geçti |
| (b) `FaceDetectorYN` | Geçti; 1 yüz, skor 0,932, eğim 1,0° |
| (b) `CvXImgProc.GuidedFilter` | Geçti (ximgproc pakette var) |
| (c) Self-contained tek dosya, Türkçe karakterli klasörden çalıştırma | Geçti (`--selftest`) |
| Türkçe karakterli yola JPEG yazma/okuma (133x171) | Geçti |

MODNet süresi (512x512, ön/son işlem dahil, ortanca): bir sunucuda 289 ms (Intel), diğerinde 402 ms (AMD).
Bu, tam boru hattının süresi **değildir**; 1,5 sn hedefi Aşama 2'de ölçülecek.

## Bulunan sorun ve çözümü
- `FaceDetectorYN`, model yolu Türkçe karakter içerince `Can't read ONNX file` hatası verdi
  ([ilk çalıştırma](https://github.com/mceyhan46/C_EOkul/actions/runs/45920350348)).
- Çözüm: `app.manifest` içinde `activeCodePage = UTF-8`. İkinci çalıştırmada geçti. Mimari değişmedi.
- Ana uygulamanın manifestine de eklenmeli.

## Visual C++ Redistributable
Statik DLL analizi: `onnxruntime.dll` şu dosyaları istiyor ve **pakette yoklar**:
`msvcp140.dll`, `msvcp140_1.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`.
`OpenCvSharpExtern.dll` bunları istemiyor.
Beklenti: Redistributable kurulu olmayan bilgisayarda ONNX Runtime yüklenmez. Bu, temiz makinede denenmedi.

## Sürümler ve model uyumu
| Bileşen | Sürüm | Model IR / opset |
|---|---|---|
| Microsoft.ML.OnnxRuntime | 1.32.0 | |
| OpenCvSharp4 + runtime.win | 4.13.0.20261107 | |
| MODNet `modnet_photographic.onnx` | SHA-256 `5069a5e3…02ae` | IR 8 / opset 17 |
| YuNet `face_detection_yunet_2023mar.onnx` | SHA-256 `8f2383e4…2fa4` | IR 6 / opset 11 |

Tam adres ve özetler: `models/models.json`.

## Boyutlar (ölçüldü)
| Dosya | MB |
|---|---:|
| EOkulSpike.exe (tek dosya, sıkıştırmasız) | 219,98 |
| models/modnet_photographic.onnx | 24,77 |
| models/face_detection_yunet_2023mar.onnx | 0,22 |
| samples/astronaut.png (yalnız deneme) | 0,75 |
| **Toplam klasör** | **245,73** |
| **Toplam ZIP** | **115,39** |

Exe içindeki büyük yerel DLL'ler (ayrı yayımdan): OpenCvSharpExtern 65,40; onnxruntime 19,65;
D3DCompiler 4,52; wpfgfx 1,87; PresentationNative 1,18; diğerleri 1 MB altı.

Pakete alınmayanlar: `opencv_videoio_ffmpeg4130_64.dll` (27,25 MB, LGPL; video kullanılmıyor) ve ONNX Runtime `.lib` dosyaları.
İlk çalıştırmada exe 247,24 MB, ZIP 132,27 MB idi.

## Lisanslar
| Bileşen | Lisans | Kontrol kaynağı |
|---|---|---|
| .NET / WPF | MIT | dotnet/runtime LICENSE.TXT (WPF deposu ayrıca bakılmadı) |
| ONNX Runtime | MIT | Debian paket telif dosyası (1.21.0); 1.32.0 lisans metni açılamadı |
| OpenCvSharp4 | Apache-2.0 | NuGet sayfası |
| OpenCV 4.13.0 | Apache-2.0 | opencv/opencv LICENSE |
| MODNet | Apache-2.0 | ZHKKKe/MODNet LICENSE ve README |
| MODNet ONNX dönüşümü | Apache-2.0 | yakhyo/modnet LICENSE |
| YuNet | MIT | opencv_zoo model klasörü LICENSE |
| Örnek fotoğraf | Kamu malı | scikit-image veri açıklaması |

`OpenCvSharpExtern.dll` içine statik bağlanan üçüncü taraf kütüphanelerin (görüntü kodlayıcıları, olasılıkla Tesseract) lisans listesi Aşama 4'te çıkarılmalı.

## Doğrulanamayanlar
- Windows 11'de çalışma (CI sunucusu Windows Server 2025).
- Visual C++ Redistributable olmayan temiz makine.
- Deneme penceresinin (WPF arayüzü) açılışı: CI yalnız `--selftest` modunu çalıştırdı.
- CI'ın ürettiği görseller incelenmedi (bu çalışma ortamından artefakt indirilemiyor).
- Saç teli / halo kalitesi (Aşama 2).
- Farklı donanımlar arasında bit bit aynı çıktı.
- Windows 11 N sürümleri: `OpenCvSharpExtern.dll` Media Foundation DLL'lerini istiyor; Media Feature Pack yoksa yüklenmeyebilir (tahmin, denenmedi).
- DirectML (GPU) ve WPF-UI.

## Riskler ve kararlar
1. **Visual C++ dosyaları:** (A) kurulum programı Redistributable'ı kursun (mevcut plan) veya
   (B) dört DLL uygulama klasörüne konsun. Öneri: B; yönetici yetkisi ve ayrı kurulum gerektirmez. Güncellemeleri bize kalır.
2. **OpenCvSharp4 bakım modunda;** aktif geliştirme OpenCvSharp5'te ([README](https://github.com/shimat/opencvsharp)).
   Öneri: denenmiş olan 4.13 ile devam. Değişiklik onay gerektirir.
3. **MODNet ONNX dosyası üçüncü taraf dönüşümü.** Resmi dosyayla karşılaştırılmadı. SHA-256 sabitlendi.
