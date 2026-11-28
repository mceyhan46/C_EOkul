# E-Okul Foto Studio (Windows 11)

Var olan öğrenci fotoğraflarını toplu olarak 133x171 vesikalığa dönüştürecek çevrimdışı Windows uygulaması.

**Durum:** Aşama 2 (görüntü işleme). Fotoğraflar komut satırı aracıyla işlenir; arayüz henüz yoktur.

- Raporlar: [Aşama 0](docs/asama0-rapor.md) · [Aşama 1](docs/asama1-rapor.md) · [Aşama 2](docs/asama2-rapor.md)
- Windows 11 uyumluluk testi: [docs/asama0-windows11-testi.md](docs/asama0-windows11-testi.md)
- Gereksinimler: [docs/PROMPT.md](docs/PROMPT.md)

## Yapı
| Klasör | İçerik |
|---|---|
| `src/EOkulStudio.Core` | Kurallar: dosya, ad, kırpma, ayar (dış bağımlılık yok) |
| `src/EOkulStudio.Imaging` | Görüntü işleme |
| `src/EOkulStudio.Infrastructure` | Dosya tarama, yazma, kayıt ve ayar dosyaları |
| `src/EOkulStudio.App` | WPF arayüzü |
| `src/EOkulStudio.Cli` | Arayüzsüz toplu işlem aracı (`eokul-cli`) |
| `tests` | xUnit testleri |
| `spike/Spike` | Aşama 0 deneme programı |

## Komut satırı aracı
Fotoğraf klasörünü `Fotograflari-isle.bat` üzerine sürükleyip bırakın ya da:
```powershell
eokul-cli.exe "D:\Fotograflar" --karsilastirma "D:\Denetim"
```
Çıktılar her klasörün içindeki `E_Okul` klasörüne yazılır; asıl dosyalar değiştirilmez. Karşılaştırma görsellerinde öğrenci yüzleri bulunur; işiniz bitince silin.

## Derleme (Windows, .NET 10 SDK)
```powershell
./scripts/indir-modeller.ps1   # modelleri indirir, SHA-256 ile doğrular (depoda tutulmaz)
./scripts/vc-dosyalari.ps1     # Visual C++ DLL'lerini hazırlar (Visual Studio gerekir)
dotnet build EOkulStudio.sln -c Release -p:Platform=x64
dotnet test --solution EOkulStudio.sln -c Release -p:Platform=x64
```

> Bu depoya gerçek öğrenci fotoğrafı eklemeyin (KVKK).
