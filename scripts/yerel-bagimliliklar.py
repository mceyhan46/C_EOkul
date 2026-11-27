"""Yerel (native) DLL bağımlılıklarını denetler.

Kullanım: yerel-bagimliliklar.py <dağıtım klasörü> <ayrı yayım klasörü>
İkinci klasördeki (tek dosyaya gömülen DLL'leri de içeren) dosyaların istediği her Visual C++ DLL'i
dağıtım klasöründe exe'nin yanında bulunmalıdır; yoksa çıkış kodu 1 olur.
"""
import os
import re
import sys

import pefile

VC_REDIST = re.compile(r"^(vcruntime\d+(_\d+)?|msvcp\d+(_\w+)?|vcomp\d+|concrt\d+|mfc\d+\w*)\.dll$", re.I)
API_SET = re.compile(r"^(api|ext)-ms-", re.I)


def main(shipped: str, folder: str) -> int:
    beside_exe = {name.lower() for name in os.listdir(shipped)}
    bundled = {name.lower() for name in os.listdir(folder)}
    needed: dict[str, set[str]] = {}
    for name in sorted(os.listdir(folder), key=str.lower):
        if not name.lower().endswith((".dll", ".exe")):
            continue
        pe = pefile.PE(os.path.join(folder, name), fast_load=True)
        if pe.OPTIONAL_HEADER.DATA_DIRECTORY[14].VirtualAddress:  # yönetilen (.NET) derleme
            continue
        pe.parse_data_directories(directories=[1, 13])  # import ve delay import
        sections = (("DIRECTORY_ENTRY_IMPORT", ""), ("DIRECTORY_ENTRY_DELAY_IMPORT", " (gecikmeli)"))
        imports = [
            (entry.dll.decode(), suffix)
            for attribute, suffix in sections
            for entry in getattr(pe, attribute, [])
        ]
        for dll, suffix in imports:
            if VC_REDIST.match(dll):
                needed.setdefault(dll.lower(), set()).add(name + suffix)
        if re.match(r"(onnxruntime|opencv)", name, re.I):
            print(f"{name}:")
            for dll, suffix in sorted(imports, key=lambda item: item[0].lower()):
                if not API_SET.match(dll):
                    where = "pakette" if dll.lower() in bundled else "sistemden"
                    print(f"    {dll}{suffix}  [{where}]")
    print("\nGereken Visual C++ DLL'leri:")
    missing = 0
    for dll, users in sorted(needed.items()):
        ok = dll in beside_exe
        missing += not ok
        print(f"    {dll}: {'exe yanında' if ok else 'EKSİK'} <- {', '.join(sorted(users))}")
    print(f"\nEksik sayısı: {missing}")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
