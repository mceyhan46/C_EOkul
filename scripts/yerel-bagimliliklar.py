"""Yayım klasöründeki yerel (native) DLL'lerin içe aktardığı kütüphaneleri listeler."""
import os
import re
import sys

import pefile

VC_REDIST = re.compile(r"^(vcruntime\d+(_\d+)?|msvcp\d+(_\w+)?|vcomp\d+|concrt\d+|mfc\d+\w*)\.dll$", re.I)
API_SET = re.compile(r"^(api|ext)-ms-", re.I)


def main(folder: str) -> None:
    local = {name.lower() for name in os.listdir(folder)}
    needed: dict[str, set[str]] = {}
    for name in sorted(os.listdir(folder), key=str.lower):
        if not name.lower().endswith((".dll", ".exe")):
            continue
        pe = pefile.PE(os.path.join(folder, name), fast_load=True)
        clr = pe.OPTIONAL_HEADER.DATA_DIRECTORY[14].VirtualAddress
        if clr:  # yönetilen (.NET) derleme
            continue
        pe.parse_data_directories(directories=[1, 13])  # import ve delay import
        sections = (("DIRECTORY_ENTRY_IMPORT", ""), ("DIRECTORY_ENTRY_DELAY_IMPORT", " (gecikmeli)"))
        imports = [
            entry.dll.decode() + suffix
            for attribute, suffix in sections
            for entry in getattr(pe, attribute, [])
        ]
        for item in imports:
            dll = item.split(" ")[0].lower()
            if VC_REDIST.match(dll) and dll not in local:
                needed.setdefault(dll, set()).add(name + (" (gecikmeli)" if "(" in item else ""))
        if re.match(r"(onnxruntime|opencv)", name, re.I):
            print(f"{name}:")
            for item in sorted(imports, key=str.lower):
                if not API_SET.match(item):
                    where = "paket içinde" if item.split(" ")[0].lower() in local else "sistemden"
                    print(f"    {item}  [{where}]")
    print("\nPakette OLMAYAN Visual C++ Redistributable DLL'leri:")
    if not needed:
        print("    yok")
    for dll, users in sorted(needed.items()):
        print(f"    {dll} <- {', '.join(sorted(users))}")


if __name__ == "__main__":
    main(sys.argv[1])
