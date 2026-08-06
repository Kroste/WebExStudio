#!/usr/bin/env python3
"""
Reproduzierbarer Icon-Build für WebExStudio (Kroste-Standard).

Erzeugt aus dem Master-PNG (Assets/webexstudio.png, 256x256, RGBA) ein
Multi-Resolution-Windows-Icon (Assets/webexstudio.ico) mit den Standard-Größen
16/24/32/48/64/128/256. Das .ico wird in der csproj über <ApplicationIcon>
in die Windows-.exe eingebettet; das PNG bleibt der plattformneutrale Master
(Fenster-/Tray-Icon via AvaloniaResource, AppImage).

Nutzung:
    python scripts/build_icon.py

Abhängigkeit: Pillow  (pip install pillow)

Design ändern: Ist ein neues Motiv gewünscht, das Master-PNG neu gestalten
(256x256, transparenter Hintergrund, Motiv in der App-Akzentfarbe #E0B14C auf
Dunkel) und dieses Skript erneut laufen lassen — das .ico wird dann daraus
abgeleitet. So bleiben PNG und ICO garantiert konsistent.
"""
from pathlib import Path
from PIL import Image

ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]

ROOT = Path(__file__).resolve().parent.parent
MASTER_PNG = ROOT / "WebExStudio.UI" / "Assets" / "webexstudio.png"
OUT_ICO = ROOT / "WebExStudio.UI" / "Assets" / "webexstudio.ico"


def main() -> None:
    if not MASTER_PNG.exists():
        raise SystemExit(f"Master-PNG fehlt: {MASTER_PNG}")

    master = Image.open(MASTER_PNG).convert("RGBA")
    if master.size != (256, 256):
        # Auf 256 normalisieren, damit alle Ableitungen sauber skalieren.
        master = master.resize((256, 256), Image.LANCZOS)

    # Pillow erzeugt die kleineren Auflösungen selbst (hochwertige Downscales)
    # und bettet sie alle in EINE .ico-Datei ein.
    master.save(OUT_ICO, format="ICO", sizes=[(s, s) for s in ICO_SIZES])
    print(f"OK  {OUT_ICO.relative_to(ROOT)}  ({', '.join(str(s) for s in ICO_SIZES)} px)")


if __name__ == "__main__":
    main()
