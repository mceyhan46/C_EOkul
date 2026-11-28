"""Kamu malı örnek görselden deneme klasörü üretir (yalnız CI ve geliştirme için).

Kullanım: python scripts/ornek-uret.py <hedef klasör> [adet]
adet verilirse yük denemesi için o kadar fotoğraf, 10 alt klasöre dağıtılarak yazılır.
"""
import io
import sys
from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter

kaynak = Path(__file__).resolve().parent.parent / "tests/EOkulStudio.Imaging.Tests/samples/astronaut.png"


def turler():
    asil = Image.open(kaynak).convert("RGB")
    yield "ornek.png", asil, {}
    yield "eğik başlı.jpg", asil.rotate(-12, Image.BICUBIC, center=(222, 120), fillcolor=(90, 90, 90)), {}
    buyuk = asil.resize((2400, 2400), Image.BICUBIC)
    exif = Image.Exif()
    exif[0x0112] = 6  # görüntülerken saat yönünde 90 derece döndür
    yield "büyük yan çekim.jpg", buyuk.transpose(Image.ROTATE_90), {"exif": exif.tobytes()}
    yield "karanlık.jpg", ImageEnhance.Brightness(asil).enhance(0.35), {}
    yield "bulanık.jpg", asil.filter(ImageFilter.GaussianBlur(2)), {}
    yield "küçük.jpg", asil.resize((200, 200), Image.BOX), {}
    yield "dikey çekim.jpg", asil.crop((60, 0, 450, 512)).resize((1170, 1536), Image.BICUBIC), {}
    iki = Image.new("RGB", (1024, 512))
    iki.paste(asil, (0, 0))
    iki.paste(asil, (512, 0))
    yield "iki yüz.jpg", iki, {}
    yield "yüzsüz.jpg", Image.linear_gradient("L").resize((600, 800)).convert("RGB"), {}


def kaydet(yol, gorsel, ek):
    tampon = io.BytesIO()
    if yol.suffix == ".png":
        gorsel.save(tampon, "PNG")
    else:
        gorsel.save(tampon, "JPEG", quality=92, **ek)
    yol.parent.mkdir(parents=True, exist_ok=True)
    yol.write_bytes(tampon.getvalue())
    return tampon.getvalue()


def main():
    hedef = Path(sys.argv[1])
    if len(sys.argv) == 2:
        for sira, (ad, gorsel, ek) in enumerate(turler()):
            klasor = hedef / ("9-A Şubesi" if sira % 2 == 0 else "9-B/İç klasör")
            kaydet(klasor / ad, gorsel, ek)
        (hedef / "9-A Şubesi" / "bozuk.jpg").write_text("fotoğraf değil", encoding="utf-8")
        return

    adet = int(sys.argv[2])
    # Yük denemesi: farklı boyutlarda, tek yüzlü fotoğraflar.
    secilen = [t for t in turler() if t[0] not in ("iki yüz.jpg", "yüzsüz.jpg")]
    hazir = []
    for ad, gorsel, ek in secilen:
        yol = hedef / "taslak" / ad
        hazir.append((Path(ad).suffix, kaydet(yol, gorsel, ek)))
        yol.unlink()
    (hedef / "taslak").rmdir()
    for sira in range(adet):
        uzanti, veri = hazir[sira % len(hazir)]
        yol = hedef / f"sınıf {sira % 10:02d}" / f"öğrenci {sira:04d}{uzanti}"
        yol.parent.mkdir(parents=True, exist_ok=True)
        yol.write_bytes(veri)


if __name__ == "__main__":
    main()
