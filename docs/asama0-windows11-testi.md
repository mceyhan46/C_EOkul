# Aşama 0: Windows 11 testi

Amaç: deneme programının geliştirme araçları kurulu olmayan bir Windows 11 x64 bilgisayarda açılıp açılmadığını görmek.

1. GitHub'da **Actions** sekmesini açın, en üstteki yeşil `asama0-uyumluluk` çalışmasına tıklayın.
2. Sayfanın altındaki **Artifacts** bölümünden `windows11-test-paketi` dosyasını indirin (GitHub'a giriş gerekir).
3. ZIP'e sağ tıklayıp **Tümünü ayıkla** deyin. ZIP'in içinden çalıştırmayın.
4. `EOkulSpike.exe` dosyasına çift tıklayın. İmzasız olduğu için SmartScreen uyarırsa **Ek bilgi → Yine de çalıştır**.
5. Pencerede **BAŞARILI** veya **BAŞARISIZ** yazar. Alttaki metnin tamamını kopyalayıp (Ctrl+A, Ctrl+C) gönderin.
6. Pencere hiç açılmazsa veya bir DLL hatası çıkarsa hata penceresinin ekran görüntüsünü gönderin.

Mümkünse iki durumu ayrı deneyin ve hangisi olduğunu yazın:

- **Temiz bilgisayar:** Ayarlar → Uygulamalar listesinde "Microsoft Visual C++ 2015-2022 Redistributable (x64)" **yok**.
- **Redistributable kurulu bilgisayar.**

İpucu: Temiz ortam için Windows 11 Pro'daki **Windows Sandbox** kullanılabilir (Home sürümünde yoktur).

Programın içindeki örnek fotoğraf bir NASA astronot portresidir (kamu malı, scikit-image örnek verisi). Gerçek öğrenci fotoğrafı gerekmez.
