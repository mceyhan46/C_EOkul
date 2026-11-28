"""Kamu malı örnekten 24 megapiksellik dikey fotoğraflar üretir (yalnız süre ve bellek ölçümü).

Kullanım: python scripts/buyuk-uret.py <hedef klasör> <adet>
"""
import io
import sys
from pathlib import Path

from PIL import Image

kaynak = Path(__file__).resolve().parent.parent / "tests/EOkulStudio.Imaging.Tests/samples/astronaut.png"


def main():
    hedef, adet = Path(sys.argv[1]), int(sys.argv[2])
    boyut = (4000, 6000)
    asil = Image.open(kaynak).convert("RGB").crop((52, 0, 393, 512)).resize(boyut, Image.BICUBIC)
    # Gerçek çekimlerdeki gibi ince doku: dosya çözme süresi düz bir büyütmeden kısa çıkmasın.
    gurultu = Image.effect_noise(boyut, 24).convert("RGB")
    tampon = io.BytesIO()
    Image.blend(asil, gurultu, 0.04).save(tampon, "JPEG", quality=92)
    hedef.mkdir(parents=True, exist_ok=True)
    for sira in range(adet):
        (hedef / f"büyük {sira:03d}.jpg").write_bytes(tampon.getvalue())


if __name__ == "__main__":
    main()
