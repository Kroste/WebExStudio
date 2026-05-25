# CLAUDE.md – Projektkonventionen für WebExStudio

Diese Datei gilt für alle Arbeiten an diesem Repository. Sie wird zu Beginn jeder
Session in den Kontext geladen. Bitte die folgenden Regeln **bei jeder Änderung** beachten.

## Über das Projekt

WebExStudio ist eine C#/Avalonia-Desktop-App zum visuellen Erstellen von
Web-Automatisierungs-Flows (Node-RED-Paradigma, Ausführung über Playwright).

- **Sprache:** .NET 10 (`net10.0`), Avalonia 12.0.x, ReactiveUI (MVVM, via `ReactiveUI.Avalonia`), NLog, xUnit.
- **Solution:** `WebExStudio.slnx`
- **Projekte:**
  - `WebExStudio.Core` – Modelle, Serialisierung, Node-Katalog
  - `WebExStudio.Engine` – Flow-Ausführung (Executor, Actions, ExecutionContext)
  - `WebExStudio.UI` – Avalonia-Oberfläche (Views, ViewModels, Controls)
  - `WebExStudio.Core.Tests` / `WebExStudio.Engine.Tests` / `WebExStudio.UI.Tests`
- **Sprache der UI / Logs / Commits:** Deutsch.

## Regeln bei jeder Änderung (Checkliste)

Vor dem Abschluss einer Aufgabe immer prüfen:

1. **Tests schreiben/anpassen** – nach Möglichkeit für jede neue Logik einen Test
   im passenden `*.Tests`-Projekt. Bugfixes bekommen einen Regressionstest.
   Reine UI-Interaktion (Drag, Maus) ist schwer testbar – dann mindestens die
   zugrunde liegende ViewModel-/Core-Logik testen (Beispiel: `GroupExtractTests`).
2. **README.md aktualisieren** – neue Node-Typen, Features oder geändertes
   Verhalten in der `README.md` dokumentieren (inkl. Beispielen, falls sinnvoll).
   Beispiel-Flows liegen als eigene Projekte unter `projects/`.
3. **Versionsnummer anheben** – siehe unten.
4. **Build + Tests grün** – `dotnet build WebExStudio.slnx` und
   `dotnet test WebExStudio.slnx` müssen fehlerfrei durchlaufen.
5. **Committen + Pushen** – nach jeder abgeschlossenen Änderung alles stagen, committen
   (`git add -A` + `git commit`) mit einer kurzen deutschen Commit-Nachricht und
   anschließend automatisch `git push` (Auto-Push ist vom Nutzer dauerhaft erlaubt).

## Versionierung (SemVer)

Einzige Quelle der Wahrheit: `<Version>` in **`Directory.Build.props`** (Repo-Wurzel).
Alle Projekte erben diesen Wert; das About-Fenster liest ihn zur Laufzeit aus.

Bei jeder inhaltlichen Änderung die Version nach [SemVer](https://semver.org) anheben:

- **PATCH** (`0.1.0` → `0.1.1`): Bugfix, kleine interne Änderung.
- **MINOR** (`0.1.0` → `0.2.0`): neues Feature, abwärtskompatibel (Normalfall hier).
- **MAJOR** (`0.1.0` → `1.0.0`): inkompatible Änderung am Flow-Format o. Ä.

Reine Doku-/Test-/Tooling-Änderungen ohne Auswirkung auf die App müssen die
Version nicht anheben.

> Releases: Die CI überschreibt die Version bei einem Git-Tag `vX.Y.Z` automatisch
> per `-p:Version`. Der Wert in `Directory.Build.props` ist die Dev-/Basisversion –
> idealerweise passend zum nächsten geplanten Tag.

## Code-Konventionen

- MVVM einhalten: Logik in ViewModels/Core/Engine, Views möglichst dünn.
- Bestehenden Stil übernehmen (Namensgebung, Kommentardichte, Idiome der Umgebung).
- **Logging-Pflicht:** Jede nennenswerte Aktion wird über NLog protokolliert
  (`LogManager.GetCurrentClassLogger()`, Meldungen auf Deutsch) — inkl. KI-Chat
  (Anfrage/Antwort), Flow-Generierung, Ausführung, Laden/Speichern.
- **Secrets maskieren:** Vor dem Loggen von Inhalten, die Geheimnisse enthalten können
  (KI-Chat-Texte, Flow-/Config-JSON, Proxy/Anbieter-Daten), immer
  `WebExStudio.Core.Logging.SecretMasker.Mask(...)` verwenden und konkrete Geheimwerte
  (API-Key, Proxy-Passwort) als `literalSecrets` mitgeben. Nie API-Key/Passwörter im Klartext loggen.
- NLog-Layouts: literale `:` in Renderer-Parametern als `\:` escapen (sonst scheitert der
  Config-Load und es wird gar nichts mehr geloggt) — durch `NLogConfigTests` abgesichert.

## Nützliche Befehle

```bash
dotnet build WebExStudio.slnx        # Build
dotnet test  WebExStudio.slnx        # Alle Tests
dotnet run --project WebExStudio.UI  # App starten
```
