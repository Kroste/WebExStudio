#!/usr/bin/env bash
#
# Baut ein Linux-AppImage aus dem self-contained Publish-Verzeichnis der GUI.
#
#   build/make-appimage.sh <publish-dir> <version> [<out-dir>]
#
# <publish-dir>  Ordner aus `dotnet publish … -r linux-x64` (enthält die Binary "WebExStudio").
# <version>      Versionsnummer für den Dateinamen (z. B. 0.50.1).
# <out-dir>      Zielordner für das fertige AppImage (Standard: aktuelles Verzeichnis).
#
# Ergebnis: WebExStudio-<version>-x86_64.AppImage
set -euo pipefail

PUBLISH_DIR="${1:?Publish-Verzeichnis fehlt}"
VERSION="${2:?Version fehlt}"
OUT_DIR="${3:-.}"

APP="WebExStudio"
ARCH="x86_64"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ICON_SRC="$SCRIPT_DIR/webexstudio.png"

[[ -x "$PUBLISH_DIR/$APP" ]] || { echo "Binary '$PUBLISH_DIR/$APP' nicht gefunden/ausführbar." >&2; exit 1; }
[[ -f "$ICON_SRC" ]] || { echo "Icon '$ICON_SRC' fehlt." >&2; exit 1; }
mkdir -p "$OUT_DIR"
OUT_DIR="$(cd "$OUT_DIR" && pwd)"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
appdir="$work/$APP.AppDir"
mkdir -p "$appdir/usr/bin"

# Kompletten Publish-Inhalt (Binary + .playwright + …) nach usr/bin (inkl. versteckter Dateien).
cp -a "$PUBLISH_DIR"/. "$appdir/usr/bin/"
chmod +x "$appdir/usr/bin/$APP"

# Icon (Top-Level für appimagetool + hicolor für Desktop-Integration).
install -Dm644 "$ICON_SRC" "$appdir/webexstudio.png"
install -Dm644 "$ICON_SRC" "$appdir/usr/share/icons/hicolor/256x256/apps/webexstudio.png"

# Desktop-Eintrag.
cat > "$appdir/$APP.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=WebExStudio
Comment=Visuelle Web-Automatisierung (Playwright)
Exec=WebExStudio
Icon=webexstudio
Categories=Development;Utility;
Terminal=false
EOF
install -Dm644 "$appdir/$APP.desktop" "$appdir/usr/share/applications/$APP.desktop"

# Startskript: führt die Binary relativ zum AppDir aus.
cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/WebExStudio" "$@"
EOF
chmod +x "$appdir/AppRun"

# appimagetool holen (continuous-Release).
tool="$work/appimagetool"
curl -fsSL -o "$tool" \
  "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-${ARCH}.AppImage"
chmod +x "$tool"

# AppImage bauen — ohne FUSE (Runner/Container-tauglich).
out="$OUT_DIR/${APP}-${VERSION}-${ARCH}.AppImage"
ARCH="$ARCH" APPIMAGE_EXTRACT_AND_RUN=1 "$tool" "$appdir" "$out"
echo "AppImage erstellt: $out"
