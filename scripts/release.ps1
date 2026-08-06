# Release-Helfer (Windows): prueft sauberen Git-Zustand, liest die Version aus
# Directory.Build.props (<Version>), setzt einen annotierten Tag vX.Y.Z und pusht
# ihn - das triggert .github/workflows/release.yml (Build + GitHub-Release).
#
# Aufruf i. d. R. ueber den VS-Code-Task "release (tag + push)".
# Bewusst reines ASCII (Windows-PowerShell-5.1-ANSI-Falle bei Umlauten).

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

# 1) Arbeitsbaum muss sauber sein.
if (git status --porcelain) {
    Write-Error "Arbeitsbaum nicht sauber - bitte erst committen oder stashen."
    exit 1
}

# 2) Keine ungepushten Commits (nur wenn ein Upstream gesetzt ist).
git rev-parse --abbrev-ref '@{u}' 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    $unpushed = git log --oneline '@{u}..' 2>$null
    if ($unpushed) {
        Write-Error "Es gibt ungepushte Commits - erst 'git push', dann taggen."
        exit 1
    }
}

# 3) Version aus Directory.Build.props lesen.
$match = Select-String -Path "Directory.Build.props" -Pattern "<Version>([^<]+)</Version>" | Select-Object -First 1
if (-not $match) {
    Write-Error "<Version> in Directory.Build.props nicht gefunden."
    exit 1
}
$version = $match.Matches[0].Groups[1].Value
$tag = "v$version"

# 4) Tag darf noch nicht existieren.
if (git tag --list $tag) {
    Write-Error "Tag $tag existiert bereits - bitte <Version> in Directory.Build.props erhoehen."
    exit 1
}

Write-Host "Setze Release-Tag $tag und pushe ..."
git tag -a $tag -m "Release $tag"
git push origin $tag
Write-Host "OK: $tag gepusht - die Release-Action laeuft jetzt an."
