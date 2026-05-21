# WebExStudio

Visuelle Desktop-Anwendung zur Erstellung und Ausführung von Web-Automatisierungs-Workflows.

## Übersicht

WebExStudio ermöglicht es, browserbasierte Abläufe grafisch als Node-Graphen zu modellieren und direkt auszuführen. Die Ausführung basiert auf [Playwright](https://playwright.dev/dotnet/), die Oberfläche auf [Avalonia UI](https://avaloniaui.net/).

## Projektstruktur

| Projekt | Beschreibung |
|---|---|
| `WebExStudio.Core` | Datenmodelle, Flow-Serialisierung, Node-Definitionen |
| `WebExStudio.Engine` | Flow-Executor, Playwright-Integration, Tracing |
| `WebExStudio.UI` | Avalonia-Desktop-App, Node-Canvas, Properties- und Trace-Panel |

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Playwright-Browser (beim ersten Start installieren, siehe unten)

## Starten

```bash
dotnet run --project WebExStudio.UI
```

### Playwright-Browser installieren

```bash
dotnet build WebExStudio.Engine
pwsh WebExStudio.Engine/bin/Debug/net10.0/playwright.ps1 install
```

## Build

```bash
dotnet build
```

## Technologie-Stack

- **.NET 10** / C#
- **Avalonia 11** + ReactiveUI
- **Microsoft Playwright**
