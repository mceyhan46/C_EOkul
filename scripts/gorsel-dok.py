"""Karşılaştırma görsellerini CI günlüğüne base64 olarak yazar (yalnız kamu malı örnekten üretilenler).

Kullanım: python scripts/gorsel-dok.py <en fazla genişlik> <dosya>...
En fazla genişlik 0 ise dosya olduğu gibi yazılır.
"""
import base64
import io
import sys
from pathlib import Path

from PIL import Image


def main():
    genislik = int(sys.argv[1])
    for ad in sys.argv[2:]:
        yol = Path(ad)
        veri = yol.read_bytes()
        if genislik:
            gorsel = Image.open(io.BytesIO(veri)).convert("RGB")
            if gorsel.width > genislik:
                gorsel = gorsel.resize((genislik, round(gorsel.height * genislik / gorsel.width)), Image.LANCZOS)
            tampon = io.BytesIO()
            gorsel.save(tampon, "JPEG", quality=85)
            veri = tampon.getvalue()
        metin = base64.b64encode(veri).decode("ascii")
        print(f"###DOSYA {yol.name}")
        for i in range(0, len(metin), 4000):
            print("###B " + metin[i:i + 4000])
        print("###SON")


if __name__ == "__main__":
    main()
