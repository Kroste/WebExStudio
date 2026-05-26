# Avalonia 12 — Zoom/Pan-Canvas: Koordinaten- & Hit-Test-Fallen

> Projektneutrale Lektionen aus realen Bugfixes (visueller Node-Editor mit zoom-/
> verschiebbarem Canvas). Direkt auf andere Avalonia-12-Projekte mit gezoomtem/
> gescrolltem Canvas, Diagramm-/Graph-Editor oder Map-Control übertragbar.

## TL;DR — Checkliste

Wenn ein Control einen **`RenderTransform`** (Zoom/Pan) trägt:

- [ ] **Zeiger-Positionen für Welt-Mathematik NICHT relativ zum transformierten Control messen.**
      `GetPosition(transformiertesControl)` liefert bereits **lokale (= Welt-)Koordinaten** —
      eine zweite Inverse (`(p-pan)/scale`) entzerrt doppelt.
- [ ] Stattdessen relativ zu einem **untransformierten, bildschirmfüllenden Vorfahren**
      (Viewport-Container) messen und **genau einmal** in Weltkoords umrechnen.
- [ ] **Drop-Ziel (`AllowDrop` + DragOver/Drop-Handler) NICHT auf das transformierte Control** —
      dessen Trefferfläche ist nur das verschobene/skalierte Band. Auf den untransformierten
      Container legen; DragOver/Drop bubblen aus den Kindern.
- [ ] Der Viewport-Container braucht `Background="Transparent"` (sonst kein Hit-Test).
- [ ] Transform-Mathematik in einen **reinen, unit-getesteten** Typ ziehen.
- [ ] **Immer mit Zoom UND Scroll testen** — bei Reset-View (Identität) sind beide Bugs unsichtbar.

---

## Setup, das diese Fallen erzeugt

```
Grid  (Viewport, untransformiert, Background="Transparent")   ← hier messen & droppen
 ├─ GridOverlay        (Hintergrund-Raster, IsHitTestVisible=False)
 ├─ Canvas             (Nodes als Kinder; RenderTransform = Zoom∘Pan)  ← NICHT hier messen/droppen
 └─ ConnectionRenderer (Wires; gleicher RenderTransform, IsHitTestVisible=False)
```

```csharp
// Zoom (ScaleTransform) + Pan (TranslateTransform), Ursprung oben-links
var group = new TransformGroup();
group.Children.Add(_zoom);   // ScaleTransform
group.Children.Add(_pan);    // TranslateTransform
canvas.RenderTransform = group;
canvas.RenderTransformOrigin = RelativePoint.TopLeft;
```

Welt → Bildschirm: `screen = world * scale + pan`. Inverse (Bildschirm → Welt):
`world = (screen - pan) / scale`.

---

## Falle 1 — Zeiger-Koordinaten doppelt entzerrt

**Symptom:** Strichlinie der gezogenen Verbindung und das Auswahl-Gummiband „kleben"
nicht am Cursor, sondern sind nach Zoom/Scroll um den Pan/Zoom-Betrag versetzt.
Bei Reset-View stimmt alles.

**Ursache:** `e.GetPosition(canvas)` bzw. `GetCurrentPoint(canvas).Position` rechnen
intern root → `canvas` **inklusive** des RenderTransforms von `canvas`. Das Ergebnis liegt
also im **lokalen Raum** des Canvas — und der ist (per Definition des Transforms) genau der
Welt-Raum, in dem die Kinder via `Canvas.SetLeft/SetTop` positioniert sind. Wer darauf noch
`(p - pan)/scale` anwendet, transformiert ein zweites Mal.

```csharp
// FALSCH: doppelte Entzerrung
var world = CanvasToWorld(e.GetPosition(canvas));         // GetPosition ist schon Welt!

// RICHTIG: relativ zum untransformierten Vorfahren messen, dann genau einmal umrechnen
var viewport = e.GetCurrentPoint(canvas.GetVisualParent() ?? canvas).Position;
var world    = CanvasToWorld(viewport);                  // = (viewport - pan) / scale
```

In Handlern, die am untransformierten Container hängen, einfach `e.GetPosition(container)`
nehmen und einmal `CanvasToWorld` anwenden.

> Merksatz: **`GetPosition(v)` ist immer im Koordinatenraum von `v` *vor* dessen RenderTransform.**
> Für Welt-Mathematik gegen ein Element messen, das **keinen** Transform trägt.

---

## Falle 2 — Drag&Drop nur in einem verschobenen Band

**Symptom:** Node aus der Palette ziehen klappt nur in einem festen (horizontalen/vertikalen)
Bereich. Außerhalb verschwindet die Drag-Vorschau und beim Loslassen entsteht nichts.
**Node-Verschieben** im Flow funktioniert dagegen überall.

**Ursache:** `DragDrop.SetAllowDrop` + die DragOver/Drop-Handler hingen am **transformierten**
Canvas. Dessen Hit-Test-Geometrie im Bildschirmraum ist die *transformierte* Bounds, also nur
das Band `[pan, pan + scale·Größe]`. Nach Zoom/Scroll deckt es das Sichtfeld nicht mehr ab →
außerhalb kein `DragOver` (Ghost weg via `DragLeave`) und kein `Drop`.
Node-Verschieben ist nie betroffen, weil es beim Start `e.Pointer.Capture(...)` aufruft und so
*alle* Bewegungen bekommt — unabhängig von der Drop-Treffer-Logik. (Genau dieser Unterschied —
„nur bei Drag&Drop kaputt" — ist der diagnostische Fingerabdruck.)

```csharp
// FALSCH: Drop-Ziel ist das gezoomte/gescrollte Control
DragDrop.SetAllowDrop(canvas, true);
canvas.AddHandler(DragDrop.DragOverEvent, OnDragOver);
canvas.AddHandler(DragDrop.DropEvent,     OnDrop);

// RICHTIG: Drop-Ziel ist der untransformierte, bildschirmfüllende Container
DragDrop.SetAllowDrop(viewport, true);          // viewport.Background = "Transparent"!
viewport.AddHandler(DragDrop.DragOverEvent, OnDragOver);
viewport.AddHandler(DragDrop.DropEvent,     OnDrop);
viewport.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

private void OnDrop(object? sender, DragEventArgs e)
{
    var world = CanvasToWorld(e.GetPosition(viewport));   // einmal entzerren
    // ... Node an world anlegen
}
```

`DragOverEvent`/`DropEvent`/`DragLeaveEvent` sind **bubbelnde** RoutedEvents — ein Handler am
Container fängt auch Drops über Kind-Elementen (Nodes). Wichtig: der Container muss hit-testbar
sein (`Background="Transparent"` genügt; `null`-Background = kein Hit).

---

## Reine, testbare Transform-Mathematik

Maus-/Drag-Interaktion ist kaum unit-testbar. Die zugrunde liegende Mathematik schon — also als
reinen Wert-Typ kapseln und den verwendet das Control:

```csharp
public readonly record struct ViewTransform(double Scale, double PanX, double PanY)
{
    public static readonly ViewTransform Identity = new(1, 0, 0);

    public Point ToWorld(Point viewport)            // Bildschirm → Welt (einmalige Inverse)
        => new((viewport.X - PanX) / Scale, (viewport.Y - PanY) / Scale);

    public Point ToScreen(Point world)              // Welt → Bildschirm (= RenderTransform)
        => new(world.X * Scale + PanX, world.Y * Scale + PanY);

    public ViewTransform ZoomAround(Point pivot, double factor, double min, double max)
    {                                               // Punkt unter dem Cursor bleibt stehen
        var s = Math.Clamp(Scale * factor, min, max);
        var f = s / Scale;
        return new(s, pivot.X - f * (pivot.X - PanX), pivot.Y - f * (pivot.Y - PanY));
    }
}
```

Aussagekräftige Tests:

- **Round-Trip:** `ToWorld(ToScreen(w)) == w` und `ToScreen(ToWorld(v)) == v` bei nicht-trivialem
  Scale+Pan.
- **Bug-Klasse dokumentieren:** `ToWorld(bereits-Welt-Punkt)` ist **nur bei Identität** ein No-Op —
  zeigt, warum die doppelte Entzerrung bei Reset-View unsichtbar bleibt.
- **ZoomAround:** `zoomed.ToScreen(orig.ToWorld(pivot)) == pivot` (Pivot bleibt fix); Scale wird
  geclampt.

---

## Avalonia-12-Notizen am Rande

- `TextBox.Watermark` ist **obsolet** (Warnung `AVLN5001`) → `PlaceholderText` verwenden.
- Mausrad-Zoom/-Scroll am besten auf der **untransformierten** Viewport-Fläche behandeln
  (`PointerWheelChanged` dort) und die Cursor-Position relativ zu *ihr* nehmen — sonst wandert
  die Trefferfläche mit dem Pan und Scrollen stoppt, sobald der Cursor das verschobene Control
  verlässt.
- Gemeinsamer Transform für Canvas **und** Overlay/Renderer: dieselbe `TransformGroup`-Instanz
  zuweisen und bei beiden `RenderTransformOrigin = RelativePoint.TopLeft`, sonst driften die
  Ebenen nach einem Resize auseinander.

---

*Quelle: zwei Bugfixes im WebExStudio-Node-Editor (Avalonia 12.0.x, .NET 10).*
