# WebExStudio

**Visual desktop app for building and running web automations** — browser workflows are modelled as a node graph (in the style of Node-RED) and executed directly via [Playwright](https://playwright.dev/dotnet/). UI: [Avalonia UI](https://avaloniaui.net/).

> **Idea & concept:** Lars Oste (originator). **Programming & implementation:** Claude (Anthropic) — built with Claude Code. Lars provides ideas and requirements; the AI does the technical implementation.

![Main window](docs/images/main-window.png)

---

## Contents

- [What can WebExStudio do?](#what-can-webexstudio-do)
- [Quick start](#quick-start)
- [The interface](#the-interface)
- [Core concepts](#core-concepts)
- [Usage (mouse & keyboard)](#usage-mouse--keyboard)
- [Node reference](#node-reference)
- [Examples](#examples)
- [Payload & placeholders](#payload--placeholders)
- [Credential vault (secrets)](#credential-vault-secrets)
- [Plugins (custom nodes)](#plugins-custom-nodes)
- [Command line (CLI / headless)](#command-line-cli--headless)
- [Settings (Browser / Network / AI)](#settings)
- [System tray & update check](#system-tray--update-check)
- [Logging](#logging)
- [AI: flow from a description](#ai-flow-from-a-description)
- [Importing legacy projects](#importing-legacy-projects)
- [File format (v2)](#file-format-v2)
- [Flow validation](#flow-validation)
- [Tests & Continuous Integration](#tests--continuous-integration)
- [Project structure](#project-structure)

---

## What can WebExStudio do?

- **Visual flow editor**: place nodes by drag & drop, connect them with wires.
- **Real branches & loops**: `if`/`foreach`/`for_range`/`get_links` have real output ports (e.g. `then`/`else`) — the entire flow is visibly wired, nothing is hidden in invisible tabs.
- **Subnodes**: reusable, named subroutines (like functions), called via a `call` node.
- **Payload data flow**: a single shared data object flows through the flow; placeholders `{key}` / `{payload.key}` are substituted everywhere.
- **Live execution**: during a run the view automatically follows the active node (even into subnodes) and highlights it.
- **Debug node with pause**: show the payload in the log and optionally halt the flow to inspect it.
- **Custom labels**: every node gets a freely chosen display name; plus pure comment/heading nodes (`label`/`caption`).
- **Freely selectable browser**: Chromium/Firefox/WebKit, system browser (Chrome/Edge), custom executable and driver path.
- **Legacy import**: old Python WebEX projects (nested `actions/*.json`) are converted into a single v2 flow.

---

## Quick start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Playwright browsers (install once, see below)

### Run

```bash
dotnet run --project WebExStudio.UI
```

### Install Playwright browsers (once)

```bash
dotnet build WebExStudio.Engine
pwsh WebExStudio.Engine/bin/Debug/net10.0/playwright.ps1 install
```

Alternatively you can use an already installed system browser (e.g. Google Chrome) in the [settings](#settings).

### Build

```bash
dotnet build          # entire solution (WebExStudio.slnx)
```

---

## The interface

![UI areas](docs/images/ui-overview.png)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  🌐 WebExStudio                                          ⚙  ❓  ℹ  —  ▢  ✕     │  ← Title bar (custom buttons)
├──────────────────────────────────────────────────────────────────────────────┤
│ ✨ New  📄 Open 🕘  💾 Save  ♻ Convert  ↶ ↷ │ ▶ Run ⏹ ⏸ 👣 ⏭ │ 🔎 🪄 ▦ ⊞ 🔍 │ ← Toolbar
├───────────────┬──────────────────────────────────────────────┬───────────────┤
│ Node palette  │  [ Main ] [ login ✕ ] [ submit ✕ ]           │ Properties    │
│  Search…      │                                              │               │
│  ▸ Start      │     ┌───────────────┐                        │ Label         │
│  ▸ Navigation │     │ 🚀 Function    │                        │ [__________]  │
│  ▸ Interaction│     └──────●────────┘                        │ Field 1 …     │
│  ▸ …          │            │  (wire)                          │ Field 2 …     │
│───────────────│     ┌──────●────────┐                        │               │
│ Subnodes      │     │ 🌐 Goto        │                        │ ℹ Description │
│  • login      │     │  Login page    │ ← label                 │   Example     │
│  • submit     │     └───────────────┘                        │               │
│  ＋ ✎ 🗑       │                                              │               │
├───────────────┴──────────────────────────────────────────────┴───────────────┤
│ Execution log   [Auto-scroll] [🗑 Clear]                                        │ ← Trace panel
│ 13:20:07  RUNNING  goto                                                         │
│ 13:20:08  SUCCESS  goto                                                         │
└──────────────────────────────────────────────────────────────────────────────┘
```

| Area | Purpose |
|---|---|
| **Title bar** | Custom (borderless) window bar: ⚙ Settings, ❓ Help, ℹ About, — Minimize, ▢ Max/Restore, ✕ Close. Double-click = maximize. |
| **Toolbar** | New/open/save, AI functions, Run/Stop/Pause/Resume, Fit/Reset view. |
| **Node palette** (top left) | All node types by category, searchable. **Click = preview** on the right (properties, hints, example — not inserted); **drag = drop into the flow** and edit it there. |
| **Subnodes** (bottom left) | List of all named subnodes. Double-click opens them as a tab; ＋ new, ✎ rename, 🗑 delete; dragging onto the canvas creates a `call` node. |
| **Tab bar** | Open tabs (Main + opened subnodes). Every tab except Main has a ✕. |
| **Canvas** | The flow graph: nodes + wires. Zoom/pan, right-click menu. |
| **Properties** (right) | Fields of the selected node + label + description/example. When you **click a palette node** a **read-only preview** appears here (properties + hints/example) — it becomes editable only once the node is in the flow. |
| **Execution log** (bottom) | Live trace per node (Running/Success/Error/Skipped) including debug output. Keeps the **last 5000 entries** — older ones drop off so long loops don't grow memory without bound. |

---

## Core concepts

### Flow, tabs & subnodes
- A **flow** is a single JSON document (version 2) with several **tabs** and all **nodes**.
- The **Main** tab is the entry point. Execution starts at all nodes with no incoming connection (entry nodes).
- **Subnodes** are named, reusable tabs (e.g. `login`, `configuration.general.identification`). They are called **by name** via a **`call`** node — the `call` node shows the subnode name directly. **Double-clicking a `call` node** in the flow opens the referenced subnode as a tab.

### Nodes, ports & wires
- Each node usually has **one input on top** and **outputs at the bottom**.
- **Control nodes have multiple outputs**:
  - `if_then_else` → `then` / `else`
  - `foreach` → `item` (per element) / `done`
  - `for_range` → `loop` / `done`
  - `get_links` → `per link` / `done`
- You draw a **wire** from a node's output port to another node's input port. Execution follows the wires.

```
        ┌───────────────┐
        │ ❓ If/Then/Else│
        └──●(then)──●(else)┐
           │          │
   ┌───────▼──┐   ┌────▼─────┐
   │ 🖱 Click  │   │ 🚪 Quit  │
   └──────────┘   └──────────┘
```

### Payload (data flow)
- There is **one** shared data store: the **payload**. It flows through the flow; every node can read/write it.
- It is set e.g. by the **`function`/Start** node (initial values as JSON) or via **`set_payload`**.
- **Placeholders** in fields are substituted: `{key}` **and** `{payload.key}` both resolve from the payload.

### Annotations
- **`caption`** (large heading) and **`label`** (comment text) are pure display nodes with no function — they are ignored during execution.
- In addition, **every** node has an optional **label** that appears below the type title (e.g. a click node labelled "Login button").

---

## Usage (mouse & keyboard)

| Action | How |
|---|---|
| **Add node** | Right-click empty space → menu; **or** drag from the palette onto the canvas (clicking the palette only shows the preview). |
| **Insert subnode call** | Drag a subnode from the list (bottom left) onto the canvas → creates a `call` node with the target set. |
| **Connect (wire)** | Drag from the **output port** (bottom) to another node's **input port** (top). With multiple outputs, grab the right port (then/else …). |
| **Delete wire** | Click the wire (turns red) → `Del`/`Backspace`; **or** right-click the wire → "Delete connection". |
| **Move node** | Drag a node with the left mouse button; **or** select node(s) and move them with the **arrow keys** (one grid step; **Shift+arrow** = fine, 1 px). |
| **Snap to grid** | Toolbar **▦ Align** — rounds all nodes of the active tab to the grid. |
| **Delete node** | Select node → `Del`; **or** right-click → "Delete node". |
| **Run flow** | Toolbar **▶ Run** **or** the **F5** key. |
| **Undo / Redo** | **Ctrl+Z** / **Ctrl+Y**; **or** toolbar **↶ / ↷**. |
| **Copy / Paste / Duplicate** | **Ctrl+C** / **Ctrl+V** (across tabs too) / **Ctrl+D**. Connections within the selection are preserved. |
| **Find node / jump** | **Ctrl+F** **or** toolbar **🔎 Search** → type, Enter jumps to the node (into subnodes too). |
| **Auto-layout** | Toolbar **🪄 Layout** arranges the tab's nodes top to bottom. |
| **Recently opened** | Toolbar **🕘** shows the most recently opened/saved flows. |
| **Multi-select** | **Ctrl**+click multiple nodes; **or** drag a **selection rectangle (rubber band)** on empty space. |
| **Group** | Select ≥ 2 nodes → right-click empty space → **"📦 Group"**. |
| **Group → subnode** | Right-click the **group header** → **"📦 Set up subnode"**; **or** straight from the selection: right-click empty space → **"📦 Subnode from selection"**. Enter a name + label — the nodes move into a new subnode tab, a `call` node is left in their place, external connections are rewired automatically. (Group header: double-click = rename, drag = move, right-click → "Ungroup".) |
| **Assign a label** | Select node → the **"Label"** field at the top of the properties panel. |
| **Error marker** | Nodes with a validation error show a **⚠** at the top right — the tooltip names the problem (live validation on every change). |
| **Help / quick guide** | Title bar **❓**; **or** About window (**ℹ**) → **"📖 Help / Quick guide"**. The help window shows **this README** (rendered, embedded) — so docs and help always match. It is **freely resizable**, and the JSON **examples** can be loaded directly via the **"📥 Load into flow"** button. |
| **Convert an old flow** | Toolbar **♻ Convert** → choose a project folder (Python flow) → it is converted to the new format and loaded (alternative to the CLI `--convert`). |
| **Pan** | Mouse wheel = vertical, **Shift**+wheel = horizontal; **or** middle mouse button / **Alt**+left drag. |
| **Zoom** | **Ctrl**+mouse wheel. |
| **Reset / fit view** | Toolbar **🔍 Reset View** / **⊞ Fit**. |
| **Maximize / full screen** | Title bar **☐** or double-click the title bar; **full screen** with **F11**. (Maximizing borderless windows can misbehave depending on the Linux window manager/Wayland compositor. The window has the fixed class **`WebExStudio`** — under KDE, for example, you can create a window rule to "force maximize".) |
| **Quit with unsaved changes** | Prompt with **Save / Discard / Cancel**. Escape and the title bar **✕** mean *Cancel*, so an accidental close costs nothing. |
| **Windows** | Every window — main window, settings, help, about, vault and all dialogs — uses the same custom title bar (drag, **—**, **☐**, **✕**) and is **freely resizable**. |
| **Create/rename/delete subnode** | Subnodes panel: **＋ / ✎ / 🗑**. |
| **Open subnode** | Double-click in the subnodes panel **or** double-click the `call` node in the flow → opens as a tab. |
| **Close tab** | **✕** on the tab (Main always stays open). |

---

## Node reference

> For the selected node, a description **and** an example appear on the right in the properties panel.

> **Error handling (every node):** in the properties panel under *"Error handling"* you can set, per node,
> **retries on error** (`retry`, 0 = off) and a **delay between attempts** (`retry_delay_ms`). If the node
> fails, it is retried up to `retry` times (with the pause); only then is the path treated as failed.
> Cancellation (Stop) and `quit` are never retried — handy for flaky pages/networks (e.g. a shaky `goto`
> or `get_value`).

### Start
| | Type | Name | Purpose | Example |
|---|---|---|---|---|
| 🚀 | `function` | Input | Input/start point; sets the initial payload (JSON). | `payload = {"host":"https://example.com"}` → `{payload.host}` |

### Navigation
| | Type | Name | Purpose | Example |
|---|---|---|---|---|
| 🌐 | `goto` | Navigate | Navigate to a URL, wait for load. With `new_tab = true` open in a new tab & switch to it (replaces `open_tab`). | `url = {payload.host}/login` · `new_tab = true` |
| ✖ | `close_tab` | Close tab | Close the current tab. | — |
| 🔗 | `get_links` | Collect links | Collect links; the **per link** output runs for each match. | `selector = a.product` → `{link}` |

### Interaction
| | Type | Name | Purpose | Example |
|---|---|---|---|---|
| 🖱 | `click` | Click | Click an element (with scroll/retry). For download buttons set `expect_download = true` → waits for the download and saves it. | `selector = a.download, expect_download = true` |
| ⌨ | `send_keys` | Type text | Type text into an input field (fills it). For Enter/Tab/Escape → `press_key`. | `selector = input[name=q], value = {payload.searchword}` |
| ⏳ | `wait_for` | Wait for element | Wait for visibility/presence. | `selector = .result, state = visible` |
| 💤 | `sleep` | Pause | Wait a fixed time. | `seconds = 2` |
| 📋 | `menu_path` | Menu navigation | Click/hover through a hierarchical menu. | `path = File, Export, PDF` |
| ↕ | `scroll` | Scroll | Scroll up/down or to an element; repeated scrolling loads lazily added content. | `to = bottom, times = 3` |
| ⏎ | `press_key` | Press key | Press a (special) key/combination (Enter, Escape, Tab, `Control+A`), global or on an element. | `key = Enter, selector = input[name=q]` |
| ▼ | `select_option` | Select dropdown | Select an entry in a `<select>` by value/label/index. | `selector = select#country, by = label, value = Germany` |
| 👆 | `hover` | Hover | Move the mouse over an element (reveal menu/tooltip). | `selector = .menu-item` |

### Control flow
| | Type | Name | Outputs | Example |
|---|---|---|---|---|
| ❓ | `if_then_else` | If / Then / Else | `then` / `else` | `condition = element_exists, selector = .error` |
| 🔄 | `for_range` | For loop | `loop` / `done` | `start = 1, end = 5` → `{i}` |
| 🔁 | `foreach` | Foreach loop | `item` / `done` | `items = {payload.targets}` |
| 📞 | `call` | Call subnode | 1 | `target = login` (shows the subnode name) |
| ⏸ | `noop` | No-op / breakpoint | 1 | Placeholder |
| ✔ | `assert` | Check / Assert | 1 | `condition = element_exists, selector = .success` (error if not met) |
| 🚪 | `quit` | Quit | 0 | Stops the flow here |

**`if_then_else` and `assert` conditions** (`condition`): `element_exists`, `element_visible`, `element_text`, `page_title`, `page_url`, `page_contains`, `page_matches` (regex), `payload_equals`, `payload_contains`. Invert with `negate = true`; treat the value as a regex with `regex = true`. `assert` aborts the path with an error message if the condition is **not** met (optional `message`).
For `payload_*`/`ctx_*` conditions the **payload key** goes in `selector` and the comparison value in `value` (e.g. `selector = visited`, `value = {payload.link}` → checks whether `visited` contains the link). If `selector` is empty, the `key` field is used instead.

### Data
| | Type | Name | Purpose | Example |
|---|---|---|---|---|
| 📖 | `get_value` | Read value | DOM value (text/attribute) into the payload. | `selector = .price, ctx_key = price` → `{price}` |
| 📦 | `set_payload` | Set payload | Set a key in the payload. | `key = status, value = ok` → `{payload.status}` |
| 🐞 | `debug` | Debug output | Payload/context to the log; optionally **pause**. | `source = payload, pause = true` |
| 📄 | `read_file` | Read file | File into the payload. | `path = data.txt, ctx_key = content` |
| 💾 | `write_file` | Write file | Write a value to a file. | `path = out.txt, value = {payload.result}` |

### Advanced
| | Type | Name | Purpose | Example |
|---|---|---|---|---|
| ⬇ | `download_url` | Download URL | Download a file from a URL. | `url = {payload.host}/file.pdf` |
| 📸 | `screenshot` | Screenshot | Save the page/element as PNG (path → `screenshot_path`). | `selector = .card, path = receipt.png` |
| ƒ | `page_function` | Function | JS function in the page context: without a selector `payload => { … }`, with a selector `(element, payload) => { … }`. Return → `ctx_key` (single value) or object fields merged into the payload. Manipulate the page (show a hint, remove/highlight elements) or read values. (Unifies the former `eval_js`, which remains an alias.) | `code = payload => ({ count: document.querySelectorAll('a').length })` |
| 🔐 | `save_session` | Save session | Write cookies + localStorage to a file. | insert after login; empty path = `session.json` |
| 🔐 | `credential_store` | Vault (credentials) | Marker/anchor for the encrypted credential vault (double-click opens management). Access anywhere via `{secret[name].user/.password/.api}`. | place in Main |
| 🔓 | `use_session` | Use session | **If/else for sessions** (2 outputs `loaded` / `no session`): if a (not-too-old) session file exists, its cookies are loaded into the running browser → output `loaded`; otherwise `no session`. So: with a session navigate directly, otherwise log in + `save_session`. | `max_age_hours = 0` (unlimited) |
| 🎬 | `download_stream` | Capture stream/media | Captures network traffic, detects media URLs (video/audio, HLS `.m3u8`, DASH `.mpd`) → payload (`ctx_key`); downloads direct files via HTTP, segment streams via **ffmpeg**. DRM streams cannot be downloaded. | `wait_ms = 8000, download = true` |
| 🤖 | `captcha_guard` | CAPTCHA guard | Detect a CAPTCHA, auto-click the first checkbox (`auto_click`), wait for the solution. `timeout_s = 0` = no time limit (waits until solved or until "Stop"). | `auto_click = true, timeout_s = 120` |

### AI
| | Type | Name | Purpose | Example |
|---|---|---|---|---|
| 🧠 | `ai_query` | AI query | Sends the page content (text/HTML, optionally a single element) with an instruction to the AI; answer → payload (`ctx_key`). `json = true` forces JSON, `max_chars` limits the amount of text. **Provider/model** are selectable per node (empty = default from settings; uses the API key stored there). Requires a configured AI (Settings → AI). | `prompt = Extract all threads as JSON {title,url}, provider = gemini, json = true, ctx_key = data` |

### Annotation (display only)
| | Type | Name | Purpose |
|---|---|---|---|
| 🏷 | `note` | Note | Text on the canvas: `style = heading` (large heading) or `comment`. Replaces the former `caption`/`label` (kept as aliases). |

---

## Examples

> All examples are real v2 JSON. You can save them 1:1 as `.json` and open them via **📄 Open flow**, or rebuild them in the editor.
>
> **Ready to open:** the examples are also available as separate projects under [`projects/`](projects):
> `example-1-minimal`, `example-2-foreach`, `example-3-if-else`, `example-4-subnode`, `example-5-debug-pause`, `example-6-scraping` (each with `flow.json`).

### Example 1 — Minimal flow: open a page and inspect the payload

`function → goto → debug`

![Example 1](docs/images/example-1.png)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "f1", "type": "function", "tabId": "main", "label": "Start", "x": 80, "y": 40,
      "config": { "payload": "{ \"host\": \"https://example.com\" }" }, "wires": [["g1"]] },

    { "id": "g1", "type": "goto", "tabId": "main", "label": "Home page", "x": 80, "y": 160,
      "config": { "url": "{payload.host}", "wait_ms": "500" }, "wires": [["d1"]] },

    { "id": "d1", "type": "debug", "tabId": "main", "x": 80, "y": 280,
      "config": { "source": "payload", "pause": "false" }, "wires": [[]] }
  ]
}
```

### Example 2 — Iterate over a list (foreach + payload spread)

The `function` node provides a **list of objects**. The `foreach` unpacks each element's fields into the payload (`{payload.host}`, `{payload.name}`); the **item** output (port 0) leads into the loop body, the **done** output (port 1) runs afterwards.

![Example 2](docs/images/example-2.png)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "f1", "type": "function", "tabId": "main", "label": "Devices", "x": 80, "y": 40,
      "config": { "payload": "{ \"targets\": [ {\"name\":\"A\",\"host\":\"10.0.0.1\"}, {\"name\":\"B\",\"host\":\"10.0.0.2\"} ] }" },
      "wires": [["fe"]] },

    { "id": "fe", "type": "foreach", "tabId": "main", "x": 80, "y": 160,
      "config": { "items": "{payload.targets}", "ctx_key": "target" },
      "wires": [["g1"], []] },

    { "id": "g1", "type": "goto", "tabId": "main", "label": "Open device", "x": 80, "y": 300,
      "config": { "url": "https://{payload.host}/" }, "wires": [["d1"]] },

    { "id": "d1", "type": "debug", "tabId": "main", "x": 80, "y": 420,
      "config": { "source": "payload", "label": "{payload.name}" }, "wires": [[]] }
  ]
}
```

> Note: `wires` is a list **per output port**. `foreach` has two ports → `[["g1"], []]` means: port 0 (item) → `g1`, port 1 (done) → nothing.

### Example 3 — Branch with re-merge (if then/else → rejoin)

After the `if`, **both** cases continue to the next step: wire both outputs (then/else) to the follow-up node.

![Example 3](docs/images/example-3.png)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "g1", "type": "goto", "tabId": "main", "x": 80, "y": 40,
      "config": { "url": "https://example.com" }, "wires": [["if1"]] },

    { "id": "if1", "type": "if_then_else", "tabId": "main", "label": "Cookie banner?", "x": 80, "y": 160,
      "config": { "condition": "element_exists", "selector": "#accept" },
      "wires": [["c1"], ["done"]] },

    { "id": "c1", "type": "click", "tabId": "main", "label": "Accept", "x": 320, "y": 280,
      "config": { "selector": "#accept" }, "wires": [["done"]] },

    { "id": "done", "type": "sleep", "tabId": "main", "label": "continue", "x": 80, "y": 400,
      "config": { "seconds": "1" }, "wires": [[]] }
  ]
}
```

Flow: `if1` → **then** (`#accept` exists) clicks and goes to `done`; **else** goes straight to `done`. Both paths converge at `done`.

### Example 4 — Reusable subnode (call)

A `login` subnode is called from the main flow. Create it in the subnodes list (＋), open it, build its content; in Main call it via `call` with `target = login` (or drag the subnode onto the canvas).

![Example 4](docs/images/example-4.png)

```json
{
  "version": 2,
  "tabs": [
    { "id": "main", "label": "Main", "isSubFlow": false },
    { "id": "t_login", "label": "Login", "isSubFlow": true, "name": "login" }
  ],
  "nodes": [
    { "id": "f1", "type": "function", "tabId": "main", "x": 80, "y": 40,
      "config": { "payload": "{ \"host\": \"https://example.com\", \"user\": \"apc\", \"pass\": \"secret\" }" },
      "wires": [["call1"]] },
    { "id": "call1", "type": "call", "tabId": "main", "x": 80, "y": 160,
      "config": { "target": "login" }, "wires": [[]] },

    { "id": "g1", "type": "goto", "tabId": "t_login", "x": 80, "y": 40,
      "config": { "url": "{payload.host}/logon.htm" }, "wires": [["u1"]] },
    { "id": "u1", "type": "send_keys", "tabId": "t_login", "label": "User", "x": 80, "y": 160,
      "config": { "selector": "[name=\"login_username\"]", "value": "{payload.user}" }, "wires": [["p1"]] },
    { "id": "p1", "type": "send_keys", "tabId": "t_login", "label": "Password", "x": 80, "y": 280,
      "config": { "selector": "[name=\"login_password\"]", "value": "{payload.pass}" }, "wires": [["s1"]] },
    { "id": "s1", "type": "click", "tabId": "t_login", "label": "Sign in", "x": 80, "y": 400,
      "config": { "selector": "[name=\"submit\"]" }, "wires": [[]] }
  ]
}
```

### Example 5 — Debugging with a pause

`debug` with `pause = true` writes the payload to the log and **halts**. **⏭ Resume** appears in the toolbar — only on click does it continue. This lets you inspect the payload content at leisure.

Independently, a running flow can be paused at any time with **⏸ Pause** (it stops before the next node) and resumed with **⏭ Resume**. While paused, **👣 Step** runs exactly **one** node and pauses again (single-step debugging). The node to run **next** is **outlined in cyan** — so you can see which node runs on the next step (and adjust its values in the properties panel beforehand; they are applied on execution).

![Example 5](docs/images/example-5.png)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "sp", "type": "set_payload", "tabId": "main", "x": 80, "y": 40,
      "config": { "key": "status", "value": "checked" }, "wires": [["d1"]] },
    { "id": "d1", "type": "debug", "tabId": "main", "label": "Inspection", "x": 80, "y": 160,
      "config": { "source": "both", "pause": "true" }, "wires": [[]] }
  ]
}
```

### Example 6 — Read a value and write it to a file (scraping)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "g1", "type": "goto", "tabId": "main", "x": 80, "y": 40,
      "config": { "url": "https://example.com/product/123" }, "wires": [["v1"]] },
    { "id": "v1", "type": "get_value", "tabId": "main", "label": "Read price", "x": 80, "y": 160,
      "config": { "selector": ".price", "ctx_key": "price", "filter": "trim" }, "wires": [["w1"]] },
    { "id": "w1", "type": "write_file", "tabId": "main", "label": "Save", "x": 80, "y": 280,
      "config": { "path": "prices.txt", "value": "123 = {price}", "append": "true" }, "wires": [[]] }
  ]
}
```

---

## Payload & placeholders

- **One** shared data object (`payload`) flows through the flow.
- **Set**: `function` (initial JSON), `set_payload`, `get_value`, `read_file`, loop variables (`foreach`/`for_range` write their key; `foreach` over objects additionally unpacks all fields).
- **Use**: in (almost) every text field via placeholders:
  - `{host}` **or** `{payload.host}` — both resolve to the same payload value.
- Example: `goto.url = {payload.host}/login`, `sleep.seconds = {payload.seconds}`, `send_keys.value = {payload.user}`.

---

## Credential vault (secrets)

**Plaintext** credentials do not belong in the flow. Instead they are stored **encrypted** inside the flow
and only referenced by name.

- **Storage**: **per flow** — as an encrypted blob directly in the flow (the `.json`'s `credentials` field),
  **AES-256-GCM** with a key derived from your **master password** (PBKDF2). This keeps **flow and passwords
  together**, travelling as one; flow A's passwords **never** end up in flow B. (Previously: one global file
  for all flows — data stored there must be re-entered per flow.)
- **Manage**: toolbar **🔐 Vault** (or double-click a `credential_store` node). Per entry (e.g. `F95`,
  `Pixeldrain`) create the **user / password / API key** fields. Changes are written into the flow and saved
  immediately (provided the flow already has a file path — otherwise on the next save).
- **Use**: via placeholders `{secret[name].user}`, `{secret[name].password}`, `{secret[name].api}` —
  e.g. in **Type text** (`value`), **Navigate** (`url`), **Select dropdown**, **Click** (text).
  In these fields the properties panel also offers a dropdown **"🔐 Insert secret…"** that inserts the matching
  placeholder directly (once the vault is unlocked).
- **Lifecycle**: locked by default. At flow start (when secrets are used) the **master password prompt**
  appears; afterwards the vault is unlocked for the session. On **New/Load** of a flow it is **rebound to the
  flow** (and locked in the process), as well as on **program exit**.
- **Security**: secret values are resolved **only when used** and **never enter the payload** (not even via
  `set_payload`/`function`) — so the **debug node** never shows the value. In **logs/traces** they are
  **masked** (`***`). The protection guards against sharing/repo/logs — not against an attacker with an
  unlocked session or the master password (the app must decrypt the values at runtime).
- Placeholders that arrive **from the page** are never expanded: if scraped text happens to contain `{secret[…]}` and is inserted via `{payload.…}`, it stays literal. Only references written in the node's own configuration resolve — otherwise a prepared page could have the vault type its own secrets into a form.
- The program settings have their **own** protection: the **AI API key** and the **proxy password** in
  `settings.json` are stored **encrypted** (Windows: DPAPI per user; Linux/macOS: AES bound to machine
  and user account), never in clear text. Values written by older versions are migrated automatically on
  the next start. This guards against a config file being passed on (support attachment, backup,
  screenshot) — not against an attacker who already has your user account.

---

## Settings

Via **⚙** in the title bar. Stored in `~/.config/WebExStudio/settings.json`
(on Windows `%AppData%\WebExStudio\settings.json`) and loaded on start. The window
is organized into **Browser**, **Network** and **AI**.

![Settings](docs/images/settings.png)

**Language.** The interface is multilingual (German, English, Français, Русский; shown with the
country flag and the language's own name). The language is switched at
the top of the settings and applies **immediately** (without a restart) — including the node
palette, properties panel, context menu, and the names/descriptions/examples of all nodes.
The stored node `type` and the flow format are unchanged, so flows remain interchangeable
regardless of language. Additional languages can be added with another
`Localization/<code>.json` in the `WebExStudio.Core` project.

**"Browser" tab**

| Setting | Meaning |
|---|---|
| **Browser type** | `chromium` (default), `firefox`, `webkit`. |
| **System browser (channel)** | empty = bundled; `chrome`, `msedge`, `chrome-beta`, `msedge-beta` = installed system browser. `brave` starts Brave as Chromium (the executable path is found automatically; browser type must be `chromium`). |
| **Browser executable path** | empty = automatic; otherwise the path to the browser EXE (`ExecutablePath`). |
| **Playwright driver path** | only if the driver isn't found automatically (sets `PLAYWRIGHT_DRIVER_PATH`). Below it a link to manual/offline installation of the browsers (e.g. behind a corporate proxy). |
| **Default download path** | target folder for browser downloads; empty = `~/Downloads`. Trigger the download button with a `click` node + **`expect_download = true`**: the node waits for the download and saves it there with its **real name** (otherwise Playwright only stores it temporarily with a GUID name and deletes it on close). |
| **Headless** | run the browser without a visible window. |
| **Start maximized** | opens the visible browser window maximized (`--start-maximized`) and lets the page use the full window size (instead of the fixed 1280×720 viewport). Only affects Chromium-based browsers (Chromium/Chrome/Edge/Brave) and not headless mode. Applies to all tabs of the run, including those opened via `open_tab`. |
| **Reuse session** | on start, loads a saved session (cookies + localStorage) from the **session file** (empty = `session.json` in the project folder) into the browser context. **Caution:** if the flow then still runs the login steps, that can interfere (you are already logged in). For flows with login steps it is better to **leave this OFF** and use the **`use_session`** node instead, which branches at runtime (session present → navigate, otherwise → login + `save_session`). |

**"Network" tab (proxy)** — applies **to the browser and to AI requests**:

| Setting | Meaning |
|---|---|
| **Proxy server** | e.g. `http://proxy.company.com:8080`; empty = no proxy / system default. |
| **Exceptions / bypass** | comma-separated hosts without a proxy (`localhost, 127.0.0.1, *.internal`). |
| **User / password** | optional, for authenticated proxies. |

**"AI" tab** — see [AI: flow from a description](#ai-flow-from-a-description).

---

## System tray & update check

**System tray.** Minimising the main window (the `—` button in the title bar) puts WebExStudio
into the system tray instead of the taskbar. The tray icon offers **Show** (also on left-click) and
**Quit**; closing the window with `✕` still quits the app as usual. If the desktop has no tray
support (headless server, some Wayland compositors without a status-notifier), minimising falls
back to the normal behaviour — no error message.

**Update check.** The **About** dialog automatically checks GitHub Releases on open (non-blocking,
proxy-aware — uses the system proxy with default credentials, so it also works behind a corporate
Kerberos/Negotiate proxy). If a newer version is available, an `📥 Open release page` button appears
that opens `github.com/Kroste/WebExStudio/releases/latest` in your browser — no silent auto-install,
you decide when and how to update. A `🔄 Check for updates` button triggers a fresh check on demand.
Errors (offline / proxy issue) are only logged, never shown as an error dialog.

---

## Logging

NLog writes to the user data directory — `~/.config/WebExStudio/logs/`
(on Windows `%AppData%\WebExStudio\logs\`). Deliberately **not** next to the executable:
inside a running AppImage or a system-wide installation that directory is read-only, and the
app would silently log nothing at all.

| File | Content |
|---|---|
| `info.log` | Info level (flow, node start, targets …) — also colored on the console. |
| `debug.log` | Debug level for `WebExStudio.*` (verbose). |
| `error.log` | Errors incl. stack trace. |
| `cli-debug.log` | The same for the `webex` command line runner (no console duplicate). |
| `cli-error.log` | Errors of the command line runner incl. stack trace. |

Secrets (API keys, passwords, tokens) are **masked in every log line** (`***`) — this happens
centrally in the NLog pipeline, not per call site, so a forgotten call site cannot leak a secret.

Unhandled exceptions (including those from UI event handlers) are caught by a global handler: they are logged as **Fatal** to `error.log` and shown in an error dialog — the app keeps running where possible.

During execution, node status and `debug` output also appear live in the **execution log** at the bottom of the app. Per entry:

- **Double-click** → jumps to the corresponding node in the editor (opens its tab, selects it and centers the view).
- **Right-click** → "↪ Jump to node", "📋 Copy line" (to the clipboard) and "💬 Send to AI chat" (sends node type, **node ID**, status and error message as a question into the AI chat — the ID lets the AI find the node unambiguously in the automatically attached flow JSON).
- The message text is selectable and therefore directly copyable.

---

## AI: flow from a description

The toolbar button **🤖 AI flow** lets you generate a complete flow from a natural-language
description. Steps:

1. Enter a description (e.g. "Open example.com, log in, read the heading and write it to a file").
2. The AI is given the **node catalog as a schema** (derived from `NodeCatalog`) and answers with flow JSON.
3. The result is **checked with the `FlowValidator`** before it is loaded. If it is valid, it lands directly
   on the canvas (as an unsaved flow to review). On validation errors these are shown; optionally the flow
   can be opened via **"Load anyway"** for manual correction.

**Providers** (selectable in the **Settings ⚙**, the key is stored **encrypted** in `settings.json`):

| Provider | Default model | Note |
|---|---|---|
| `anthropic` | `claude-sonnet-4-6` | Anthropic Messages API, API key required. |
| `openai` | `gpt-4o` | OpenAI chat completions, API key required. |
| `gemini` | `gemini-2.0-flash` | Google Gemini (Generative Language API), API key required. |
| `perplexity` | `sonar` | Perplexity (OpenAI-compatible), API key required. |
| `ollama` | `llama3.1` | Local instance (default URL `http://localhost:11434`), no key. |

Model and base URL can be overridden per provider. The connection is encapsulated in the
`WebExStudio.AI` project via the `ILlmClient` interface — additional providers can be added there
without changing the generator.

**Auto-detect local AI:** the button **🔍 Detect local AI** (Settings → AI) checks common local
LLM servers on demand and fills in provider, base URL and a detected model automatically —
**Ollama** (`localhost:11434`, e.g. via Pinokio) plus the **OpenAI-compatible** servers
**LM Studio** (`1234`), **llama.cpp** (`8080`) and **Jan** (`1337`). Best-effort across these known
ports; non-standard ports are entered manually.

### AI chat

The toolbar button **💬 AI chat** opens a chat window with the AI (multiple turns, history is
preserved). You can ask questions about WebExStudio or develop a flow iteratively. If an answer
contains a flow, a **"📥 Load into editor"** button appears below it — the flow is (as in the AI flow
dialog) validated first and then loaded. In addition, as soon as the latest answer contains a flow,
a **fixed load bar appears directly above the input field** — so loading stays reachable even with
very long answers (no need to scroll to the end of the message). Chat uses the same provider and
proxy settings.

With **every** message the chat is given the **current state of the flow** (including interim changes
to nodes), so change requests build on the real flow and the returned flow can be loaded directly.

### Explain flow

The toolbar button **🧾 Explain** has the AI summarize the **current flow** in understandable
language (overview, step by step along the connections, possible risks). The explanation appears in
the chat window; the flow is provided to the model in the background, so you can ask follow-up
questions directly.

### Node suggestion

Select a node and click **💡 Suggest**: the AI suggests — based on the whole flow — the **next sensible
node** (type, label, configuration, reasoning). The suggestion is checked against the node catalog; via
**Add** it is created below the selected node and connected automatically from its output.

The checkbox **"Node suggestion"** in the **status bar** (bottom) toggles the feature on/off (the
setting is saved). The status bar also shows the current status, the flow name, the active tab, the
node count and the AI provider.

### AI hints (known issues & fixes)

In the settings **"AI"** tab there is a field **"Known issues & fixes"** — a short, self-maintained
list (one line per hint). If the switch **"Send hints to the AI"** is on, these hints are sent with
**every** AI request (create flow, chat, explain, suggest) in addition to the flow. This lets you
record problems you've solved once and have them considered automatically in the future. Keep hints
short so the AI can use them well.

In the **AI chat**, every answer has a button **"📌 Remember as hint"** — it adds the (shortened) answer
directly to the hint list (editable in the settings afterwards).

---

## Plugins (custom nodes)

Custom node types can be loaded as plugins — without rebuilding the app.

**Write a plugin:**
1. Create a class library (`net10.0`) that references **`WebExStudio.Core`** and **`WebExStudio.Engine`**.
2. For each node implement an `IActionHandler` (`string Type` + `ExecuteAsync(ExecutionContext, FlowNode)`)
   and provide a `NodeDefinition` (metadata for palette/properties).
3. Implement a class with **`INodePlugin`** (parameterless constructor) that returns both as
   `NodePluginNode(Definition, Handler)`.

```csharp
public sealed class MyPlugin : INodePlugin
{
    public IEnumerable<NodePluginNode> CreateNodes() =>
    [
        new(new NodeDefinition { Type = "my_hello", DisplayName = "Hello", Category = "Plugins",
                                 Description = "…", Example = "…",
                                 Properties = [ new() { Key = "text", Label = "Text", Kind = PropertyKind.Text } ] },
            new MyHelloHandler()),
    ];
}
```

Optionally mark the target API version so the loader warns on incompatibility:
```csharp
[assembly: WebExStudioPlugin(PluginApi.Version)]
```

If the node should **branch** itself (multiple outputs, like if_then_else), set
`OutputPorts`/`OutputLabels` in the `NodeDefinition` and **`RoutesOutputs = true`**; the handler then routes
via `ctx.FollowOutput(node, port)`.

**Bundled plugins** (under [`samples/`](samples), also templates):

- [`samples/FileCheckPlugin`](samples/FileCheckPlugin) — node **"File exists?"** (`file_exists`): searches the
  folder (empty = download folder) for a name/pattern and branches **found / not found** — handy to check
  before a download whether the file already exists (an example of a **branching** node, `RoutesOutputs`).
- [`samples/HttpRequestPlugin`](samples/HttpRequestPlugin) — node **"HTTP request"** (`http_request`):
  sends a REST/webhook request **without a browser** (method, headers `Name: value` per line, body).
  Response body → `ctx_key` (default `response`), status code → `status_key` (default `response_status`);
  optionally **fail on status ≥ 400**. `{secret[..]}` is allowed in URL/headers/body and is resolved
  **only when sent**, never logged (an example of correct secret handling in plugins).

Build and copy the DLL(s):
```bash
dotnet build samples/HttpRequestPlugin -c Release
# copy HttpRequestPlugin.dll + HttpRequestPlugin.deps.json to %AppData%/WebExStudio/plugins, restart the app
```

**Loading:** place the compiled DLL into a `plugins/` folder — next to the application **or** under
`%AppData%\WebExStudio\plugins` (Linux/macOS: `~/.config/WebExStudio/plugins`). On start it is loaded in an
**isolated load context** (`AssemblyLoadContext`): shared host assemblies (WebExStudio, System, Avalonia,
NLog) are shared with the app, its own dependencies come via the plugin's `*.deps.json` — so plugin
libraries do not collide with the app's. The nodes appear in the **palette, properties panel, validation**
and are available to the **AI**.

**Manage:** **Settings → "Plugins" tab** shows the discovered plugins with status and allows
**enable/disable** (takes effect after a restart) as well as opening the plugin folder.

> **Security:** plugins are **arbitrary code with full app rights** (browser, files, network) — only load
> trusted plugins; there is no sandbox. An existing node type is not overwritten. Custom property editors do
> not exist (yet) — only the available field types.

---

## Command line (CLI / headless)

Flows can be run **without a GUI** — ideal for `cron`/task scheduling, CI or servers. The
**`WebExStudio.Cli`** project produces the **`webex`** command and uses the **same plugins, the same
credential vault and the same validator/executor** as the app.

Ready-made `webex` binaries are attached to every **GitHub release** as a **separate asset**
(`webex-<version>-linux-x64.tar.gz` or `webex-<version>-win-x64.zip`) — or build it yourself:

```bash
dotnet build WebExStudio.Cli -c Release          # builds the executable "webex"

webex run      -f projects/f95zone/f95zoneV2.json -c '<vault-pw>'   # run (headless by default)
webex validate -f flow.json                       # validate only (no browser)
webex secrets  -f flow.json                       # which {secret[..]} entries does the flow need?
```

**Options for `run`:**

| Option | Effect |
|---|---|
| `-f, --flow <path>` | Path to the flow file (required) |
| `-c, --credential <pw>` | Vault password. Better: the environment variable `WEBEX_VAULT_PW` or interactive input — a password as an argument ends up in the shell history/process list. |
| `--var key=value` | Initial value in the payload context (repeatable) → parametrize the flow |
| `--headful` | Start the browser visibly (otherwise headless) |
| `--browser <name>` | `chromium` (default), `firefox`, `webkit` |
| `--timeout <ms>` | Default timeout per action |
| `--download-dir <d>` | Target folder for downloads |
| `--out <file.json>` | Write a run report (node status, errors) as JSON |

**Exit codes** (for cron/CI): `0` OK · `1` run error (a node failed) · `2` validation/invocation ·
`3` vault (password missing/wrong) · `130` cancelled (Ctrl+C).

The vault is only unlocked if the flow actually uses `{secret[..]}`. Before each run it is validated
as in the GUI (errors abort). AI nodes (`ai_query`) are not active in the CLI.

---

## Importing legacy projects

Old Python WebEX projects (a folder with `actions/*.json`, nested `call`/`then_actions_file` references and `targets.json`) are converted into a single v2 flow.

**In the app:** toolbar **♻ Convert** → choose the old project folder → the converted flow is loaded directly into the editor (review and save afterwards).

**Via the command line:**

```bash
dotnet run --project WebExStudio.UI -- --convert <legacyProjectFolder> <output.json>
# Example:
dotnet run --project WebExStudio.UI -- --convert projects/usv2 projects/usv3/flow.json
```

In this process:
- each referenced `.json` file becomes a **named subnode** (name = path with dots, e.g. `configuration.general.datetime.daylightSavings`);
- `call` / `then_actions_file` / `else_actions_file` become visible **`call` nodes**;
- `if` becomes a node with **then/else outputs**, branches are merged back to the next step;
- `targets.json` lands as a list in the **`function`/Start** node, a **`foreach`** iterates over it → calls the `start` subnode.

You open the result (`projects/usv3/flow.json`) via **📄 Open flow**.

---

## File format (v2)

A flow is **one** JSON file:

```json
{
  "version": 2,
  "tabs": [
    { "id": "main", "label": "Main", "isSubFlow": false },
    { "id": "t1",   "label": "Login", "isSubFlow": true, "name": "login" }
  ],
  "nodes": [
    {
      "id": "n1",
      "type": "goto",
      "tabId": "main",
      "label": "Home page",
      "x": 80, "y": 40,
      "config": { "url": "{payload.host}" },
      "wires": [ ["n2"] ],
      "seqIndex": 0
    }
  ]
}
```

- **`tabs`**: `main` (`isSubFlow=false`) + named subnodes (`isSubFlow=true`, unique `name`).
- **`nodes[].wires`**: `wires[portIndex]` = list of target node IDs at that output. `if`/`foreach` use index 0 and 1.
- **`nodes[].config`**: all node-specific fields as strings (numbers/booleans also as strings).
- **`nodes[].label`**: the freely chosen label (shown on the node).
- Subnode calls: a `call` node with `config.target = <subnode-name>`.

---

## Flow validation

The `FlowValidator` (in `WebExStudio.Core`) checks a flow document for structural and
schematic errors — a safety net for imported and (in the future) automatically generated flows.
It returns a list of findings with severity **Error** or **Warning**;
`IsValid` is `true` as long as there is no error.

**Errors** (the flow won't run reliably like this):

| Code | Meaning |
|---|---|
| `unknown-type` | Node type is not known in the catalog. |
| `missing-required` | A required field is missing (aliases are considered). |
| `dangling-wire` | A connection points to a non-existent node ID. |
| `cross-tab-wire` | A connection leads to a node on a different tab (only allowed via `call`). |
| `wire-invalid-port` | A connection at an output the node type doesn't have. |
| `wire-into-no-input` | A connection leads into a node without an input (e.g. an annotation). |
| `call-target-missing` | A `call` node references an unknown subnode. |
| `duplicate-node-id` | A node ID occurs more than once. |
| `duplicate-subnode-name` | A subnode name assigned more than once (`call` target ambiguous). |
| `unknown-tab` | A node references an unknown tab. |
| `no-main-tab` | There is no main tab. |

**Warnings** (suspicious, possibly intentional): `no-entry-node` (tab without a start point / cycle),
`group-missing-node`, `group-foreign-node`.

**On running:** before each run the flow is validated. If there is an **error**, the run is
**not even started** — the findings appear in the log panel, the first faulty node is marked red and the
view jumps to its tab. **Warnings** are also shown in the log but do not block the run.

The bundled example flows under `projects/` are automatically checked against the validator by
`ExampleFlowsValidateTests`.

---

## Tests & Continuous Integration

### Run tests locally

```bash
dotnet test
```

| Test project | Covers |
|---|---|
| `WebExStudio.Core.Tests` | Serialization (round-trip), `FlowDocument2` helpers, `NodeCatalog`, the legacy converter, **flow validation** (incl. checking the example flows). |
| `WebExStudio.Engine.Tests` | `ExecutionContext` (payload/placeholders), `ActionRegistry`, handlers (browserless) and the **wire execution** (if branch, foreach loop). |
| `WebExStudio.UI.Tests` | `FlowEditorViewModel` logic (nodes/wires/subnodes/tabs/groups, without rendering) and the **validation block before the run**. |
| `WebExStudio.AI.Tests` | The **AI flow generator** (prompt → parse → validate) with a fake client and the provider selection of `LlmClientFactory` — without network. |
| `WebExStudio.Cli.Tests` | Argument parsing of the headless runner `webex` (`Options.Parse`: commands, flags, `--var`, error cases). |

The engine tests run **without a browser** — nodes that require Playwright are bypassed via payload-based conditions. Browser/IO-heavy paths (Playwright handlers, drag & drop, the CLI's `run` command) are not unit-tested; the underlying logic is checked instead (e.g. `Options.Parse`, `SecretReferenceScanner`, `ViewTransform`).

### GitHub Actions

[`.github/workflows/ci.yml`](.github/workflows/ci.yml):

1. **`test`** — builds the solution (Release) and runs `dotnet test` (on every push/PR).
2. **`release`** — runs **only on a `v*` tag or manually** (*workflow_dispatch*) and only when the tests are green — **not** on every push (saves artifact storage). Creates **self-contained single-file builds** for **Linux (`linux-x64`)** and **Windows (`win-x64`)** — one package each for the **GUI** (`WebExStudio-…`) and the **CLI** (`webex-…`) — plus, for Linux, an **AppImage** (`WebExStudio-…-x86_64.AppImage`, built via [`build/make-appimage.sh`](build/make-appimage.sh)).
3. On a **`v*` tag** (e.g. `git tag v1.0.0 && git push --tags`) all packages (GUI + CLI + AppImage, per platform) are additionally attached as **separate assets** to a **GitHub release**. The **AppImage** is the most convenient download for Linux: make it executable and run it (`chmod +x WebExStudio-*.AppImage && ./WebExStudio-*.AppImage`).

---

## Project structure

| Project | Description |
|---|---|
| `WebExStudio.Core` | Data models (`FlowDocument2`, `FlowNode`, `FlowTab`, `NodeCatalog`), serialization (`FlowSerializer2`), the legacy converter (`LegacyImporter`). |
| `WebExStudio.Engine` | Flow executor (wire traversal), Playwright integration, action handlers, tracing. |
| `WebExStudio.UI` | Avalonia desktop app: canvas, node/wire rendering, palette, subnode panel, properties, trace, settings, about. |
| `WebExStudio.Cli` | Headless runner `webex` (`run`/`validate`/`secrets`) — run flows without a GUI (cron/CI). |
| `WebExStudio.AI` | AI connection: node schema export, prompt building, `FlowGenerator` and providers (`ILlmClient`: Anthropic/OpenAI/Ollama). |

Stack: **.NET 10**, **Avalonia 12.1**, **Microsoft.Playwright 1.61**, **NLog 6**, tests with **xunit.v3**.
