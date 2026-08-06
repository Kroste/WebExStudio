#!/usr/bin/env bash
#
# Release-Helfer (Linux/macOS): prüft sauberen Git-Zustand, liest die Version aus
# Directory.Build.props (<Version>), setzt einen annotierten Tag vX.Y.Z und pusht
# ihn — das triggert .github/workflows/release.yml (Build + GitHub-Release).
#
# Aufruf i. d. R. über den VS-Code-Task "release (tag + push)".
#
set -euo pipefail
cd "$(dirname "$0")/.."

# 1) Arbeitsbaum muss sauber sein — kein halbfertiger Release-Stand.
if [[ -n "$(git status --porcelain)" ]]; then
  echo "FEHLER: Arbeitsbaum nicht sauber — bitte erst committen oder stashen." >&2
  git status --short >&2
  exit 1
fi

# 2) Keine ungepushten Commits (nur prüfen, wenn ein Upstream gesetzt ist).
if git rev-parse --abbrev-ref '@{u}' >/dev/null 2>&1; then
  if [[ -n "$(git log --oneline '@{u}..' 2>/dev/null)" ]]; then
    echo "FEHLER: Es gibt ungepushte Commits — erst 'git push', dann taggen." >&2
    exit 1
  fi
fi

# 3) Version aus Directory.Build.props lesen (portabel via sed, kein grep -P).
version="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' Directory.Build.props | head -1)"
if [[ -z "$version" ]]; then
  echo "FEHLER: <Version> in Directory.Build.props nicht gefunden." >&2
  exit 1
fi
tag="v$version"

# 4) Tag darf noch nicht existieren (sonst <Version> vorher erhöhen).
if git rev-parse -q --verify "refs/tags/$tag" >/dev/null; then
  echo "FEHLER: Tag $tag existiert bereits — bitte <Version> in Directory.Build.props erhöhen." >&2
  exit 1
fi

echo "Setze Release-Tag $tag und pushe ..."
git tag -a "$tag" -m "Release $tag"
git push origin "$tag"
echo "OK: $tag gepusht — die Release-Action läuft jetzt an."
