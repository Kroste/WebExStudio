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

## Tooling-Regeln (nicht wegkonfigurieren)

- **`.editorconfig` + `<EnforceCodeStyleInBuild>true</…>`**: Stilregeln sind zusammen mit
  `TreatWarningsAsErrors` **Compile-Fehler**, nicht Review-Fundsachen. Ohne die Property
  wäre die `.editorconfig` zahnlos (Regeln nur im Editor). Verstöße beheben statt die
  Regel zu lockern; `dotnet format style WebExStudio.slnx --diagnostics IDE…` erledigt die
  mechanischen Fälle.
- **`global.json`**: pinnt das SDK **und** enthält den Block
  `"test": { "runner": "Microsoft.Testing.Platform" }`. Der gehört genau dorthin — eine
  `dotnet.config` oder ein `<TestingPlatformDotnetTestSupport>` in der csproj wird ignoriert.
- **Tests laufen auf xunit.v3 / Microsoft.Testing.Platform.** Die Testprojekte sind
  `<OutputType>Exe</OutputType>` (xunit.v3 erzeugt einen eigenen Entry-Point) und
  referenzieren **weder** `Microsoft.NET.Test.Sdk` **noch** `xunit.runner.visualstudio` —
  beide gehören zum alten VSTest-Pfad und reaktivieren ihn. Auch keine VSTest-Flags an
  `dotnet test` durchreichen (`--nologo` lässt den Lauf mit Exitcode 5 und „keine Tests
  ausgeführt" abbrechen).
- **`.vscode/settings.json` braucht `dotnet.defaultSolution`**, weil die Solution im
  `.slnx`-Format vorliegt. Fehlt der Eintrag, generiert sich das C# Dev Kit eine eigene
  `.sln` im Workspace-Cache und der Test-Explorer arbeitet auf einer veralteten Projektliste.
- **GitHub Actions auf Node-24-Majors halten** (Stand 2026-08: checkout@v7, setup-dotnet@v6,
  cache@v6, upload-artifact@v7, action-gh-release@v3).

## Sicherheit & Persistenz

- **Geheimwerte nie im Klartext auf Platte**: API-Key und Proxy-Passwort laufen durch
  `WebExStudio.Core.Security.SecretProtection` (Windows DPAPI, sonst AES mit Rechner-/
  Benutzerbindung, Format `v1:<base64>`). Neue Geheimfelder in `AppSettings` gehören in
  `Decrypt`/`Encrypt` — dann kann keine Aufrufstelle es vergessen.
- **Persistente JSON-Dateien immer über `WebExStudio.Core.Storage.JsonFileStore`**:
  `WriteAtomic` (tmp + Move) und `Quarantine` (`.broken`). Quarantäne **nur** bei
  `JsonException` — bei IO-Fehlern ist der Inhalt intakt, ein Verschieben würde gute Daten
  wegräumen.
- **Der `${masked}`-Renderer muss vor dem Laden der NLog-Config registriert sein.** Jeder
  Einstiegspunkt ruft `MaskedLayoutRenderer.Register()` direkt vor
  `LoadConfigurationFromFile` auf. Der `[ModuleInitializer]` allein reicht dort nicht (er
  läuft erst beim ersten Berühren von Core, also zu spät); er deckt die Prozesse ohne
  eigenes `Main` ab. Symptom bei falscher Reihenfolge: im Log steht nur `}}`.
- **Logs liegen unter `~/.config/WebExStudio/logs` bzw. `%AppData%\WebExStudio\logs`**,
  nicht neben der Exe — im AppImage und bei systemweiter Installation ist das
  Exe-Verzeichnis read-only.

## NuGet-Pakete (Central Package Management)

Alle Paketversionen werden zentral in **`Directory.Packages.props`** (Repo-Wurzel)
gepflegt. Die `.csproj`-Dateien enthalten `<PackageReference>` **ohne**
`Version`-Attribut. Beim Hinzufügen eines Pakets: Eintrag in
`Directory.Packages.props` ergänzen + versionslose Referenz im Projekt.

## Code-Konventionen

- MVVM einhalten: Logik in ViewModels/Core/Engine, Views möglichst dünn.
- Bestehenden Stil übernehmen (Namensgebung, Kommentardichte, Idiome der Umgebung).
- **Logging-Pflicht:** Jede nennenswerte Aktion wird über NLog protokolliert
  (`LogManager.GetCurrentClassLogger()`, Meldungen auf Deutsch) — inkl. KI-Chat
  (Anfrage/Antwort), Flow-Generierung, Ausführung, Laden/Speichern.
- **Globaler Exception-Handler:** `GlobalExceptionHandler.Register()` (in
  `Program.Main`) fängt AppDomain-, Task- und UI-Thread-Ausnahmen ab →
  NLog-Fatal + Fehlerdialog. Nicht entfernen; neue Einstiegspunkte (z. B.
  weitere Prozesse) bekommen dasselbe Muster.
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
