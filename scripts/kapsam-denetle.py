"""Cobertura raporlarından bir derlemenin satır kapsamını hesaplar; eşiğin altındaysa hata verir.

Kullanım: python kapsam-denetle.py <sonuç klasörü> <derleme adı> <eşik yüzdesi>
"""
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def main() -> int:
    folder, assembly, threshold = Path(sys.argv[1]), sys.argv[2].lower(), float(sys.argv[3])
    lines: dict[tuple[str, int], bool] = {}
    reports = sorted(folder.rglob("*.cobertura.xml"))
    for report in reports:
        for package in ET.parse(report).getroot().iter("package"):
            if package.get("name", "").lower().removesuffix(".dll") != assembly:
                continue
            for cls in package.iter("class"):
                name = cls.get("filename", "")
                # Derleyicinin ve kaynak üreticilerinin ürettiği dosyalar sayılmaz.
                if "\\obj\\" in name or "/obj/" in name:
                    continue
                for line in cls.iter("line"):
                    key = (name, int(line.get("number")))
                    lines[key] = lines.get(key, False) or int(line.get("hits")) > 0

    if not lines:
        print(f"HATA: {len(reports)} raporda '{assembly}' için satır bulunamadı.")
        return 1

    files: dict[str, list[int]] = {}
    for (name, number), hit in sorted(lines.items()):
        entry = files.setdefault(name, [0, 0])
        entry[0] += hit
        entry[1] += 1
    for name, (hit, total) in files.items():
        missing = [n for (f, n), h in sorted(lines.items()) if f == name and not h]
        short = name.replace("\\", "/").split("/src/")[-1]
        print(f"{100 * hit / total:6.1f}%  {short}" + (f"  (eksik satır: {missing})" if missing else ""))

    covered = 100 * sum(lines.values()) / len(lines)
    ok = covered >= threshold
    print(f"{'GEÇTİ' if ok else 'KALDI'}: {assembly} satır kapsamı %{covered:.1f} (eşik %{threshold:g})")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
