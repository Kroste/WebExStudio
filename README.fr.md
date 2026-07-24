# WebExStudio

**Application de bureau visuelle pour créer et exécuter des automatisations web** — les déroulements du navigateur sont modélisés sous forme de graphe de nœuds (dans le style de Node-RED) et exécutés directement via [Playwright](https://playwright.dev/dotnet/). Interface : [Avalonia UI](https://avaloniaui.net/).

> **Idée & concept :** Lars Oste (initiateur). **Programmation & réalisation :** Claude (Anthropic) — développé avec Claude Code. Lars fournit les idées et les exigences ; l'IA se charge de la réalisation technique.

![Fenêtre principale](docs/images/main-window.png)

---

## Sommaire

- [Que peut faire WebExStudio ?](#que-peut-faire-webexstudio-)
- [Démarrage rapide](#démarrage-rapide)
- [L'interface](#linterface)
- [Concepts clés](#concepts-clés)
- [Utilisation (souris & clavier)](#utilisation-souris--clavier)
- [Référence des nœuds](#référence-des-nœuds)
- [Exemples](#exemples)
- [Payload & espaces réservés](#payload--espaces-réservés)
- [Coffre d'identifiants (secrets)](#coffre-didentifiants-secrets)
- [Plugins (nœuds personnalisés)](#plugins-nœuds-personnalisés)
- [Ligne de commande (CLI / sans interface)](#ligne-de-commande-cli--sans-interface)
- [Paramètres (Navigateur / Réseau / IA)](#paramètres)
- [Zone de notification & vérification des mises à jour](#zone-de-notification--vérification-des-mises-à-jour)
- [Journalisation](#journalisation)
- [IA : flux à partir d'une description](#ia--flux-à-partir-dune-description)
- [Importer des projets hérités](#importer-des-projets-hérités)
- [Format de fichier (v2)](#format-de-fichier-v2)
- [Validation des flux](#validation-des-flux)
- [Tests & Intégration continue](#tests--intégration-continue)
- [Structure du projet](#structure-du-projet)

---

## Que peut faire WebExStudio ?

- **Éditeur de flux visuel** : placer les nœuds par glisser-déposer, les relier par des connexions (wires).
- **Vraies branches & boucles** : `if`/`foreach`/`for_range`/`get_links` ont de vrais ports de sortie (p. ex. `then`/`else`) — tout le déroulement est câblé visiblement, rien n'est caché dans des onglets invisibles.
- **Sous-nœuds** : sous-programmes nommés et réutilisables (comme des fonctions), appelés par un nœud `call`.
- **Flux de données payload** : un objet de données partagé circule à travers le flux ; les espaces réservés `{key}` / `{payload.key}` sont substitués partout.
- **Exécution en direct** : pendant l'exécution, la vue suit automatiquement le nœud actif (y compris dans les sous-nœuds) et le met en évidence.
- **Nœud debug avec pause** : afficher le payload dans le journal et, en option, mettre le flux en pause pour inspecter.
- **Libellés personnalisés** : chaque nœud reçoit un nom d'affichage librement choisi ; en plus, des nœuds purement commentaire/titre (`label`/`caption`).
- **Navigateur librement sélectionnable** : Chromium/Firefox/WebKit, navigateur système (Chrome/Edge), chemin d'exécutable et de pilote personnalisés.
- **Import hérité** : les anciens projets Python WebEX (`actions/*.json` imbriqués) sont convertis en un seul flux v2.

---

## Démarrage rapide

### Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Navigateurs Playwright (à installer une fois, voir ci-dessous)

### Lancer

```bash
dotnet run --project WebExStudio.UI
```

### Installer les navigateurs Playwright (une fois)

```bash
dotnet build WebExStudio.Engine
pwsh WebExStudio.Engine/bin/Debug/net10.0/playwright.ps1 install
```

Vous pouvez aussi utiliser un navigateur système déjà installé (p. ex. Google Chrome) dans les [paramètres](#paramètres).

### Construire

```bash
dotnet build          # toute la solution (WebExStudio.slnx)
```

---

## L'interface

![Zones de l'UI](docs/images/ui-overview.png)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  🌐 WebExStudio                                          ⚙  ❓  ℹ  —  ▢  ✕     │  ← Barre de titre (boutons personnalisés)
├──────────────────────────────────────────────────────────────────────────────┤
│ ✨ Nouveau  📄 Ouvrir 🕘  💾 Enregistrer  ♻ Convertir  ↶ ↷ │ ▶ Exécuter ⏹ ⏸ 👣 ⏭ │ 🔎 🪄 ▦ ⊞ 🔍 │ ← Barre d'outils
├───────────────┬──────────────────────────────────────────────┬───────────────┤
│ Palette nœuds │  [ Main ] [ login ✕ ] [ submit ✕ ]           │ Propriétés    │
│  Rechercher…  │                                              │               │
│  ▸ Début      │     ┌───────────────┐                        │ Libellé       │
│  ▸ Navigation │     │ 🚀 Function    │                        │ [__________]  │
│  ▸ Interaction│     └──────●────────┘                        │ Champ 1 …     │
│  ▸ …          │            │  (wire)                          │ Champ 2 …     │
│───────────────│     ┌──────●────────┐                        │               │
│ Sous-nœuds    │     │ 🌐 Goto        │                        │ ℹ Description │
│  • login      │     │  Page de login │ ← libellé               │   Exemple     │
│  • submit     │     └───────────────┘                        │               │
│  ＋ ✎ 🗑       │                                              │               │
├───────────────┴──────────────────────────────────────────────┴───────────────┤
│ Journal d'exécution   [Défilement auto] [🗑 Effacer]                            │ ← Panneau de trace
│ 13:20:07  RUNNING  goto                                                         │
│ 13:20:08  SUCCESS  goto                                                         │
└──────────────────────────────────────────────────────────────────────────────┘
```

| Zone | Rôle |
|---|---|
| **Barre de titre** | Barre de fenêtre personnalisée (sans bordure) : ⚙ Paramètres, ❓ Aide, ℹ À propos, — Réduire, ▢ Agrandir/Restaurer, ✕ Fermer. Double-clic = agrandir. |
| **Barre d'outils** | Nouveau/ouvrir/enregistrer, fonctions IA, Exécuter/Arrêter/Pause/Continuer, Ajuster/Réinitialiser la vue. |
| **Palette de nœuds** (en haut à gauche) | Tous les types de nœuds par catégorie, avec recherche. **Clic = aperçu** à droite (propriétés, indices, exemple — non inséré) ; **glisser = déposer dans le flux** et l'y modifier. |
| **Sous-nœuds** (en bas à gauche) | Liste de tous les sous-nœuds nommés. Double-clic les ouvre en onglet ; ＋ nouveau, ✎ renommer, 🗑 supprimer ; les glisser sur la zone de travail crée un nœud `call`. |
| **Barre d'onglets** | Onglets ouverts (Main + sous-nœuds ouverts). Chaque onglet sauf Main a un ✕. |
| **Zone de travail** | Le graphe de flux : nœuds + connexions. Zoom/déplacement, menu clic droit. |
| **Propriétés** (à droite) | Champs du nœud sélectionné + libellé + description/exemple. Au **clic sur un nœud de la palette**, un **aperçu en lecture seule** apparaît ici (propriétés + indices/exemple) — il ne devient modifiable qu'une fois le nœud dans le flux. |
| **Journal d'exécution** (en bas) | Trace en direct par nœud (Running/Success/Error/Skipped), y compris les sorties debug. |

---

## Concepts clés

### Flux, onglets & sous-nœuds
- Un **flux** est un unique document JSON (version 2) avec plusieurs **onglets** et tous les **nœuds**.
- L'onglet **Main** est le point d'entrée. L'exécution commence à tous les nœuds sans connexion entrante (nœuds d'entrée).
- Les **sous-nœuds** sont des onglets nommés et réutilisables (p. ex. `login`, `configuration.general.identification`). Ils sont appelés **par leur nom** via un nœud **`call`** — le nœud `call` affiche directement le nom du sous-nœud. **Un double-clic sur un nœud `call`** dans le flux ouvre le sous-nœud référencé en onglet.

### Nœuds, ports & connexions
- Chaque nœud a (généralement) **une entrée en haut** et des **sorties en bas**.
- **Les nœuds de contrôle ont plusieurs sorties** :
  - `if_then_else` → `then` / `else`
  - `foreach` → `élément` (par élément) / `terminé`
  - `for_range` → `boucle` / `terminé`
  - `get_links` → `par lien` / `terminé`
- Vous tirez une **connexion (wire)** du port de sortie d'un nœud vers le port d'entrée d'un autre. L'exécution suit les connexions.

```
        ┌───────────────┐
        │ ❓ Si/Alors/Sinon│
        └──●(then)──●(else)┐
           │          │
   ┌───────▼──┐   ┌────▼─────┐
   │ 🖱 Cliquer│   │ 🚪 Quitter│
   └──────────┘   └──────────┘
```

### Payload (flux de données)
- Il existe **un** stockage de données partagé : le **payload**. Il circule à travers le flux ; chaque nœud peut lire/écrire.
- Il est défini p. ex. par le nœud **`function`/Début** (valeurs initiales en JSON) ou via **`set_payload`**.
- Les **espaces réservés** dans les champs sont substitués : `{key}` **et** `{payload.key}` se résolvent tous deux depuis le payload.

### Annotations
- **`caption`** (grand titre) et **`label`** (texte de commentaire) sont de purs nœuds d'affichage sans fonction — ils sont ignorés à l'exécution.
- De plus, **chaque** nœud a un **libellé** facultatif qui apparaît sous le titre de type (p. ex. un nœud clic libellé « Bouton de connexion »).

---

## Utilisation (souris & clavier)

| Action | Comment |
|---|---|
| **Ajouter un nœud** | Clic droit sur une zone vide → menu ; **ou** glisser depuis la palette sur la zone de travail (cliquer dans la palette n'affiche que l'aperçu). |
| **Insérer un appel de sous-nœud** | Glisser un sous-nœud depuis la liste (en bas à gauche) sur la zone de travail → crée un nœud `call` avec la cible définie. |
| **Connecter (wire)** | Tirer du **port de sortie** (bas) vers le **port d'entrée** (haut) d'un autre nœud. Avec plusieurs sorties, saisir le bon port (then/else …). |
| **Supprimer une connexion** | Cliquer sur la connexion (devient rouge) → `Suppr`/`Retour arrière` ; **ou** clic droit sur la connexion → « Supprimer la connexion ». |
| **Déplacer un nœud** | Glisser un nœud avec le bouton gauche ; **ou** sélectionner le(s) nœud(s) et les déplacer avec les **flèches** (un pas de grille ; **Maj+flèche** = fin, 1 px). |
| **Aligner sur la grille** | Barre d'outils **▦ Aligner** — arrondit tous les nœuds de l'onglet actif sur la grille. |
| **Supprimer un nœud** | Sélectionner le nœud → `Suppr` ; **ou** clic droit → « Supprimer le nœud ». |
| **Exécuter le flux** | Barre d'outils **▶ Exécuter** **ou** la touche **F5**. |
| **Annuler / Rétablir** | **Ctrl+Z** / **Ctrl+Y** ; **ou** barre d'outils **↶ / ↷**. |
| **Copier / Coller / Dupliquer** | **Ctrl+C** / **Ctrl+V** (entre onglets aussi) / **Ctrl+D**. Les connexions au sein de la sélection sont conservées. |
| **Rechercher un nœud / y accéder** | **Ctrl+F** **ou** barre d'outils **🔎 Rechercher** → saisir, Entrée saute au nœud (y compris dans les sous-nœuds). |
| **Disposition automatique** | Barre d'outils **🪄 Disposition** organise les nœuds de l'onglet de haut en bas. |
| **Récemment ouverts** | Barre d'outils **🕘** affiche les flux récemment ouverts/enregistrés. |
| **Sélection multiple** | **Ctrl**+clic sur plusieurs nœuds ; **ou** tracer un **rectangle de sélection (lasso)** sur une zone vide. |
| **Grouper** | Sélectionner ≥ 2 nœuds → clic droit sur une zone vide → **« 📦 Grouper »**. |
| **Groupe → sous-nœud** | Clic droit sur l'**en-tête du groupe** → **« 📦 Configurer le sous-nœud »** ; **ou** directement depuis la sélection : clic droit sur une zone vide → **« 📦 Sous-nœud depuis la sélection »**. Saisir un nom + un libellé — les nœuds passent dans un nouvel onglet de sous-nœud, un nœud `call` reste à leur place, les connexions externes sont recâblées automatiquement. (En-tête de groupe : double-clic = renommer, glisser = déplacer, clic droit → « Dégrouper ».) |
| **Attribuer un libellé** | Sélectionner le nœud → le champ **« Libellé »** en haut du panneau de propriétés. |
| **Marqueur d'erreur** | Les nœuds avec une erreur de validation affichent un **⚠** en haut à droite — l'infobulle nomme le problème (validation en direct à chaque modification). |
| **Aide / guide rapide** | Barre de titre **❓** ; **ou** fenêtre À propos (**ℹ**) → **« 📖 Aide / Guide rapide »**. La fenêtre d'aide affiche **cette README** (rendue, intégrée) — ainsi docs et aide restent toujours synchrones. Elle est **librement redimensionnable**, et les **exemples** JSON se chargent directement via le bouton **« 📥 Charger dans le flux »**. |
| **Convertir un ancien flux** | Barre d'outils **♻ Convertir** → choisir un dossier de projet (flux Python) → il est converti au nouveau format et chargé (alternative à la CLI `--convert`). |
| **Déplacer (pan)** | Molette = vertical, **Maj**+molette = horizontal ; **ou** bouton du milieu / **Alt**+glisser gauche. |
| **Zoom** | **Ctrl**+molette. |
| **Réinitialiser / ajuster la vue** | Barre d'outils **🔍 Réinitialiser la vue** / **⊞ Ajuster**. |
| **Agrandir / plein écran** | Barre de titre **☐** ou double-clic sur la barre de titre ; **plein écran** avec **F11**. (L'agrandissement des fenêtres sans bordure peut mal fonctionner selon le gestionnaire de fenêtres Linux/compositeur Wayland. La fenêtre a la classe fixe **`WebExStudio`** — sous KDE par exemple, vous pouvez créer une règle de fenêtre « forcer l'agrandissement ».) |
| **Créer/renommer/supprimer un sous-nœud** | Panneau Sous-nœuds : **＋ / ✎ / 🗑**. |
| **Ouvrir un sous-nœud** | Double-clic dans le panneau Sous-nœuds **ou** double-clic sur le nœud `call` dans le flux → ouvre en onglet. |
| **Fermer un onglet** | **✕** sur l'onglet (Main reste toujours ouvert). |

---

## Référence des nœuds

> Pour le nœud sélectionné, une description **et** un exemple apparaissent à droite dans le panneau de propriétés.

> **Gestion des erreurs (chaque nœud) :** dans le panneau de propriétés, sous *« Gestion des erreurs »*, on peut définir, par
> nœud, des **nouvelles tentatives en cas d'erreur** (`retry`, 0 = désactivé) et un **délai entre les tentatives**
> (`retry_delay_ms`). Si le nœud échoue, il est réessayé jusqu'à `retry` fois (avec la pause) ; ce n'est qu'ensuite
> que le chemin est considéré en erreur. L'annulation (Arrêter) et `quit` ne sont jamais réessayés — pratique
> pour les pages/réseaux instables (p. ex. un `goto` ou `get_value` capricieux).

### Début
| | Type | Nom | Rôle | Exemple |
|---|---|---|---|---|
| 🚀 | `function` | Entrée | Point d'entrée/de départ ; définit le payload initial (JSON). | `payload = {"host":"https://example.com"}` → `{payload.host}` |

### Navigation
| | Type | Nom | Rôle | Exemple |
|---|---|---|---|---|
| 🌐 | `goto` | Naviguer | Naviguer vers une URL, attendre le chargement. Avec `new_tab = true`, ouvrir dans un nouvel onglet & y basculer (remplace `open_tab`). | `url = {payload.host}/login` · `new_tab = true` |
| ✖ | `close_tab` | Fermer l'onglet | Fermer l'onglet actuel. | — |
| 🔗 | `get_links` | Collecter les liens | Collecter des liens ; la sortie **par lien** s'exécute pour chaque résultat. | `selector = a.product` → `{link}` |

### Interaction
| | Type | Nom | Rôle | Exemple |
|---|---|---|---|---|
| 🖱 | `click` | Cliquer | Cliquer sur un élément (avec défilement/réessai). Pour les boutons de téléchargement `expect_download = true` → attend le téléchargement et l'enregistre. | `selector = a.download, expect_download = true` |
| ⌨ | `send_keys` | Saisir du texte | Saisir du texte dans un champ (le remplit). Pour Entrée/Tab/Échap → `press_key`. | `selector = input[name=q], value = {payload.searchword}` |
| ⏳ | `wait_for` | Attendre un élément | Attendre la visibilité/présence. | `selector = .result, state = visible` |
| 💤 | `sleep` | Pause | Attendre une durée fixe. | `seconds = 2` |
| 📋 | `menu_path` | Navigation par menu | Parcourir un menu hiérarchique (clic/survol). | `path = Fichier, Exporter, PDF` |
| ↕ | `scroll` | Faire défiler | Défiler vers le haut/bas ou jusqu'à un élément ; défiler plusieurs fois charge le contenu « lazy » différé. | `to = bottom, times = 3` |
| ⏎ | `press_key` | Appuyer sur une touche | Appuyer sur une touche (spéciale)/combinaison (Entrée, Échap, Tab, `Control+A`), global ou sur un élément. | `key = Enter, selector = input[name=q]` |
| ▼ | `select_option` | Choisir dans une liste | Sélectionner une entrée dans `<select>` par valeur/label/index. | `selector = select#pays, by = label, value = Allemagne` |
| 👆 | `hover` | Survoler (hover) | Déplacer la souris sur un élément (afficher menu/infobulle). | `selector = .menu-item` |

### Flux de contrôle
| | Type | Nom | Sorties | Exemple |
|---|---|---|---|---|
| ❓ | `if_then_else` | Si / Alors / Sinon | `then` / `else` | `condition = element_exists, selector = .error` |
| 🔄 | `for_range` | Boucle For | `boucle` / `terminé` | `start = 1, end = 5` → `{i}` |
| 🔁 | `foreach` | Boucle Foreach | `élément` / `terminé` | `items = {payload.targets}` |
| 📞 | `call` | Appeler un sous-nœud | 1 | `target = login` (affiche le nom du sous-nœud) |
| ⏸ | `noop` | No-op / point d'arrêt | 1 | Espace réservé |
| ✔ | `assert` | Vérifier / Assert | 1 | `condition = element_exists, selector = .success` (erreur si non remplie) |
| 🚪 | `quit` | Quitter | 0 | Arrête le flux ici |

**Conditions `if_then_else` et `assert`** (`condition`) : `element_exists`, `element_visible`, `element_text`, `page_title`, `page_url`, `page_contains`, `page_matches` (regex), `payload_equals`, `payload_contains`. Inverser avec `negate = true` ; traiter la valeur comme regex avec `regex = true`. `assert` interrompt le chemin avec un message d'erreur si la condition n'est **pas** remplie (`message` facultatif).
Pour les conditions `payload_*`/`ctx_*`, la **clé du payload** va dans `selector` et la valeur de comparaison dans `value` (p. ex. `selector = visited`, `value = {payload.link}` → vérifie si `visited` contient le lien). Si `selector` est vide, le champ `key` est utilisé à la place.

### Données
| | Type | Nom | Rôle | Exemple |
|---|---|---|---|---|
| 📖 | `get_value` | Lire une valeur | Valeur du DOM (texte/attribut) dans le payload. | `selector = .price, ctx_key = prix` → `{prix}` |
| 📦 | `set_payload` | Définir le payload | Définir une clé dans le payload. | `key = status, value = ok` → `{payload.status}` |
| 🐞 | `debug` | Sortie de débogage | Payload/contexte dans le journal ; en option **mise en pause**. | `source = payload, pause = true` |
| 📄 | `read_file` | Lire un fichier | Fichier dans le payload. | `path = data.txt, ctx_key = contenu` |
| 💾 | `write_file` | Écrire un fichier | Écrire une valeur dans un fichier. | `path = out.txt, value = {payload.result}` |

### Avancé
| | Type | Nom | Rôle | Exemple |
|---|---|---|---|---|
| ⬇ | `download_url` | Télécharger une URL | Télécharger un fichier depuis une URL. | `url = {payload.host}/file.pdf` |
| 📸 | `screenshot` | Capture d'écran | Enregistrer la page/un élément en PNG (chemin → `screenshot_path`). | `selector = .card, path = recu.png` |
| ƒ | `page_function` | Function | Fonction JS dans le contexte de la page : sans sélecteur `payload => { … }`, avec sélecteur `(element, payload) => { … }`. Retour → `ctx_key` (valeur unique) ou champs d'objet fusionnés dans le payload. Manipuler la page (afficher un indice, supprimer/surligner des éléments) ou lire des valeurs. (Unifie l'ancien `eval_js`, conservé comme alias.) | `code = payload => ({ count: document.querySelectorAll('a').length })` |
| 🔐 | `save_session` | Enregistrer la session | Écrire cookies + localStorage dans un fichier. | insérer après la connexion ; chemin vide = `session.json` |
| 🔐 | `credential_store` | Coffre (identifiants) | Marqueur/ancre pour le coffre d'identifiants chiffré (un double-clic ouvre la gestion). Accès partout via `{secret[name].user/.password/.api}`. | placer dans Main |
| 🔓 | `use_session` | Utiliser la session | **Si/Sinon pour les sessions** (2 sorties `chargée` / `aucune session`) : si un fichier de session (pas trop ancien) existe, ses cookies sont chargés dans le navigateur en cours → sortie `chargée` ; sinon `aucune session`. Ainsi : avec session naviguer directement, sinon connexion + `save_session`. | `max_age_hours = 0` (illimité) |
| 🎬 | `download_stream` | Capturer un flux/média | Capture le trafic réseau, détecte les URL de médias (vidéo/audio, HLS `.m3u8`, DASH `.mpd`) → payload (`ctx_key`) ; télécharge les fichiers directs par HTTP, les flux segmentés via **ffmpeg**. Les flux DRM ne sont pas téléchargeables. | `wait_ms = 8000, download = true` |
| 🤖 | `captcha_guard` | Protection CAPTCHA | Détecter un CAPTCHA, cliquer automatiquement sur la première case (`auto_click`), attendre la résolution. `timeout_s = 0` = pas de limite (attend jusqu'à résolution ou « Arrêter »). | `auto_click = true, timeout_s = 120` |

### IA
| | Type | Nom | Rôle | Exemple |
|---|---|---|---|---|
| 🧠 | `ai_query` | Requête IA | Envoie le contenu de la page (texte/HTML, en option un seul élément) avec une instruction à l'IA ; réponse → payload (`ctx_key`). `json = true` impose du JSON, `max_chars` limite la quantité de texte. **Fournisseur/modèle** sélectionnables par nœud (vide = par défaut des paramètres ; utilise la clé API qui y est enregistrée). Nécessite une IA configurée (Paramètres → IA). | `prompt = Extrais tous les threads en JSON {titre,url}, provider = gemini, json = true, ctx_key = data` |

### Annotation (affichage seul)
| | Type | Nom | Rôle |
|---|---|---|---|
| 🏷 | `note` | Note | Texte sur la zone de travail : `style = heading` (grand titre) ou `comment`. Remplace les anciens `caption`/`label` (conservés comme alias). |

---

## Exemples

> Tous les exemples sont du vrai JSON v2. Vous pouvez les enregistrer tels quels en `.json` et les ouvrir via **📄 Ouvrir un flux**, ou les reconstruire dans l'éditeur.
>
> **Prêts à ouvrir :** les exemples sont aussi disponibles en projets séparés sous [`projects/`](projects) :
> `example-1-minimal`, `example-2-foreach`, `example-3-if-else`, `example-4-subnode`, `example-5-debug-pause`, `example-6-scraping` (chacun avec `flow.json`).

### Exemple 1 — Flux minimal : ouvrir une page et inspecter le payload

`function → goto → debug`

![Exemple 1](docs/images/example-1.png)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "f1", "type": "function", "tabId": "main", "label": "Début", "x": 80, "y": 40,
      "config": { "payload": "{ \"host\": \"https://example.com\" }" }, "wires": [["g1"]] },

    { "id": "g1", "type": "goto", "tabId": "main", "label": "Page d'accueil", "x": 80, "y": 160,
      "config": { "url": "{payload.host}", "wait_ms": "500" }, "wires": [["d1"]] },

    { "id": "d1", "type": "debug", "tabId": "main", "x": 80, "y": 280,
      "config": { "source": "payload", "pause": "false" }, "wires": [[]] }
  ]
}
```

### Exemple 2 — Itérer sur une liste (foreach + diffusion du payload)

Le nœud `function` fournit une **liste d'objets**. Le `foreach` décompresse pour chaque élément ses champs dans le payload (`{payload.host}`, `{payload.name}`) ; la sortie **élément** (port 0) mène au corps de la boucle, la sortie **terminé** (port 1) s'exécute ensuite.

![Exemple 2](docs/images/example-2.png)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "f1", "type": "function", "tabId": "main", "label": "Appareils", "x": 80, "y": 40,
      "config": { "payload": "{ \"targets\": [ {\"name\":\"A\",\"host\":\"10.0.0.1\"}, {\"name\":\"B\",\"host\":\"10.0.0.2\"} ] }" },
      "wires": [["fe"]] },

    { "id": "fe", "type": "foreach", "tabId": "main", "x": 80, "y": 160,
      "config": { "items": "{payload.targets}", "ctx_key": "target" },
      "wires": [["g1"], []] },

    { "id": "g1", "type": "goto", "tabId": "main", "label": "Ouvrir l'appareil", "x": 80, "y": 300,
      "config": { "url": "https://{payload.host}/" }, "wires": [["d1"]] },

    { "id": "d1", "type": "debug", "tabId": "main", "x": 80, "y": 420,
      "config": { "source": "payload", "label": "{payload.name}" }, "wires": [[]] }
  ]
}
```

> Remarque : `wires` est une liste **par port de sortie**. `foreach` a deux ports → `[["g1"], []]` signifie : port 0 (élément) → `g1`, port 1 (terminé) → rien.

### Exemple 3 — Branche avec re-fusion (if then/else → rejoin)

Après le `if`, on continue dans **les deux** cas vers l'étape suivante : câbler les deux sorties (then/else) vers le nœud suivant.

![Exemple 3](docs/images/example-3.png)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "g1", "type": "goto", "tabId": "main", "x": 80, "y": 40,
      "config": { "url": "https://example.com" }, "wires": [["if1"]] },

    { "id": "if1", "type": "if_then_else", "tabId": "main", "label": "Bannière cookies ?", "x": 80, "y": 160,
      "config": { "condition": "element_exists", "selector": "#accept" },
      "wires": [["c1"], ["done"]] },

    { "id": "c1", "type": "click", "tabId": "main", "label": "Accepter", "x": 320, "y": 280,
      "config": { "selector": "#accept" }, "wires": [["done"]] },

    { "id": "done", "type": "sleep", "tabId": "main", "label": "continuer", "x": 80, "y": 400,
      "config": { "seconds": "1" }, "wires": [[]] }
  ]
}
```

Déroulement : `if1` → **then** (`#accept` existe) clique et va à `done` ; **else** va directement à `done`. Les deux chemins convergent en `done`.

### Exemple 4 — Sous-nœud réutilisable (call)

Un sous-nœud `login` est appelé depuis le flux principal. Le créer dans la liste des sous-nœuds (＋), l'ouvrir, construire son contenu ; dans Main, l'appeler via `call` avec `target = login` (ou glisser le sous-nœud sur la zone de travail).

![Exemple 4](docs/images/example-4.png)

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
    { "id": "u1", "type": "send_keys", "tabId": "t_login", "label": "Utilisateur", "x": 80, "y": 160,
      "config": { "selector": "[name=\"login_username\"]", "value": "{payload.user}" }, "wires": [["p1"]] },
    { "id": "p1", "type": "send_keys", "tabId": "t_login", "label": "Mot de passe", "x": 80, "y": 280,
      "config": { "selector": "[name=\"login_password\"]", "value": "{payload.pass}" }, "wires": [["s1"]] },
    { "id": "s1", "type": "click", "tabId": "t_login", "label": "Se connecter", "x": 80, "y": 400,
      "config": { "selector": "[name=\"submit\"]" }, "wires": [[]] }
  ]
}
```

### Exemple 5 — Débogage avec une pause

`debug` avec `pause = true` écrit le payload dans le journal et **fait une halte**. **⏭ Continuer** apparaît dans la barre d'outils — ce n'est qu'au clic que l'exécution reprend. Vous pouvez ainsi inspecter le contenu du payload à loisir.

Indépendamment, un flux en cours peut être mis en pause à tout moment avec **⏸ Pause** (il s'arrête avant le nœud suivant) et repris avec **⏭ Continuer**. En pause, **👣 Pas à pas** exécute exactement **un** nœud puis se met de nouveau en pause (débogage pas à pas). Le nœud à exécuter **ensuite** est **encadré en cyan** — vous voyez ainsi quel nœud s'exécutera à l'étape suivante (et pouvez ajuster ses valeurs dans le panneau de propriétés avant ; elles sont appliquées à l'exécution).

![Exemple 5](docs/images/example-5.png)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "sp", "type": "set_payload", "tabId": "main", "x": 80, "y": 40,
      "config": { "key": "status", "value": "vérifié" }, "wires": [["d1"]] },
    { "id": "d1", "type": "debug", "tabId": "main", "label": "Inspection", "x": 80, "y": 160,
      "config": { "source": "both", "pause": "true" }, "wires": [[]] }
  ]
}
```

### Exemple 6 — Lire une valeur et l'écrire dans un fichier (scraping)

```json
{
  "version": 2,
  "tabs": [{ "id": "main", "label": "Main", "isSubFlow": false }],
  "nodes": [
    { "id": "g1", "type": "goto", "tabId": "main", "x": 80, "y": 40,
      "config": { "url": "https://example.com/produit/123" }, "wires": [["v1"]] },
    { "id": "v1", "type": "get_value", "tabId": "main", "label": "Lire le prix", "x": 80, "y": 160,
      "config": { "selector": ".price", "ctx_key": "prix", "filter": "trim" }, "wires": [["w1"]] },
    { "id": "w1", "type": "write_file", "tabId": "main", "label": "Enregistrer", "x": 80, "y": 280,
      "config": { "path": "prix.txt", "value": "123 = {prix}", "append": "true" }, "wires": [[]] }
  ]
}
```

---

## Payload & espaces réservés

- **Un** objet de données partagé (`payload`) circule à travers le flux.
- **Définir** : `function` (JSON initial), `set_payload`, `get_value`, `read_file`, variables de boucle (`foreach`/`for_range` écrivent leur clé ; `foreach` sur des objets décompresse en plus tous les champs).
- **Utiliser** : dans (presque) chaque champ texte via les espaces réservés :
  - `{host}` **ou** `{payload.host}` — les deux résolvent la même valeur du payload.
- Exemple : `goto.url = {payload.host}/login`, `sleep.seconds = {payload.seconds}`, `send_keys.value = {payload.user}`.

---

## Coffre d'identifiants (secrets)

Les identifiants **en clair** n'ont pas leur place dans le flux. Ils sont stockés **chiffrés** à l'intérieur du flux
et référencés uniquement par leur nom.

- **Stockage** : **par flux** — sous forme de blob chiffré directement dans le flux (champ `credentials` du `.json`),
  **AES-256-GCM** avec une clé dérivée de votre **mot de passe maître** (PBKDF2). Ainsi **flux et mots de passe restent
  ensemble** et voyagent comme un tout ; les mots de passe du flux A n'aboutissent **jamais** dans le flux B. (Auparavant :
  un fichier global pour tous les flux — les données qui y étaient stockées doivent être ressaisies par flux.)
- **Gérer** : barre d'outils **🔐 Coffre** (ou double-clic sur un nœud `credential_store`). Par entrée (p. ex. `F95`,
  `Pixeldrain`), créer les champs **utilisateur / mot de passe / clé API**. Les modifications sont écrites dans le flux et
  enregistrées immédiatement (si le flux a déjà un chemin de fichier — sinon au prochain enregistrement).
- **Utiliser** : via les espaces réservés `{secret[name].user}`, `{secret[name].password}`, `{secret[name].api}` —
  p. ex. dans **Saisir du texte** (`value`), **Naviguer** (`url`), **Choisir dans une liste**, **Cliquer** (texte).
  Dans ces champs, le panneau de propriétés propose en plus un menu **« 🔐 Insérer un secret… »** qui insère directement
  l'espace réservé adéquat (une fois le coffre déverrouillé).
- **Cycle de vie** : **verrouillé** par défaut. Au démarrage du flux (si des secrets sont utilisés), la **demande de mot de
  passe maître** apparaît ; ensuite le coffre est déverrouillé pour la session. Lors de **Nouveau/Charger** d'un flux, il est
  **réassocié au flux** (et verrouillé au passage), de même qu'à la **fermeture du programme**.
- **Sécurité** : les valeurs des secrets sont résolues **uniquement lors de leur utilisation** et **n'entrent jamais dans le
  payload** (pas même via `set_payload`/`function`) — le **nœud debug** n'affiche donc jamais la valeur. Dans les
  **journaux/traces**, elles sont **masquées** (`***`). La protection vaut contre le partage/dépôt/journaux — pas contre un
  attaquant disposant d'une session déverrouillée ou du mot de passe maître (l'appli doit déchiffrer les valeurs à l'exécution).
- Les paramètres du programme (clé IA, etc. dans `settings.json`) ne sont pas concernés.

---

## Paramètres

Via **⚙** dans la barre de titre. Enregistrés dans `~/.config/WebExStudio/settings.json`
(sous Windows `%AppData%\WebExStudio\settings.json`) et chargés au démarrage. La fenêtre
est organisée en **Navigateur**, **Réseau** et **IA**.

![Paramètres](docs/images/settings.png)

**Langue.** L'interface est multilingue (Deutsch, English, Français, Русский ; avec le drapeau du pays et le
nom propre de la langue). La langue se change en haut des paramètres et s'applique **immédiatement** (sans
redémarrage) — y compris la palette de nœuds, le panneau de propriétés, le menu contextuel et les
noms/descriptions/exemples de tous les nœuds. Le `type` de nœud enregistré et le format de flux restent
inchangés, de sorte que les flux restent interchangeables quelle que soit la langue. D'autres langues
peuvent être ajoutées avec un fichier `Localization/<code>.json` supplémentaire dans le projet `WebExStudio.Core`.

**Onglet « Navigateur »**

| Paramètre | Signification |
|---|---|
| **Type de navigateur** | `chromium` (par défaut), `firefox`, `webkit`. |
| **Navigateur système (canal)** | vide = intégré ; `chrome`, `msedge`, `chrome-beta`, `msedge-beta` = navigateur système installé. `brave` lance Brave en Chromium (le chemin de l'exécutable est trouvé automatiquement ; le type de navigateur doit être `chromium`). |
| **Chemin de l'exécutable du navigateur** | vide = automatique ; sinon le chemin de l'EXE du navigateur (`ExecutablePath`). |
| **Chemin du pilote Playwright** | uniquement si le pilote n'est pas trouvé automatiquement (définit `PLAYWRIGHT_DRIVER_PATH`). En dessous, un lien vers l'installation manuelle/hors ligne des navigateurs (p. ex. derrière un proxy d'entreprise). |
| **Chemin de téléchargement par défaut** | dossier cible pour les téléchargements du navigateur ; vide = `~/Downloads`. Déclencher le bouton de téléchargement avec un nœud `click` + **`expect_download = true`** : le nœud attend le téléchargement et l'enregistre là sous son **vrai nom** (sinon Playwright ne le stocke que temporairement avec un nom GUID et le supprime à la fermeture). |
| **Sans interface** | exécuter le navigateur sans fenêtre visible. |
| **Démarrer agrandi** | ouvre la fenêtre visible du navigateur agrandie (`--start-maximized`) et laisse la page utiliser toute la taille de la fenêtre (au lieu du viewport fixe 1280×720). N'affecte que les navigateurs basés sur Chromium (Chromium/Chrome/Edge/Brave) et pas le mode sans interface. S'applique à tous les onglets de l'exécution, y compris ceux ouverts via `open_tab`. |
| **Réutiliser la session** | au démarrage, charge une session enregistrée (cookies + localStorage) depuis le **fichier de session** (vide = `session.json` dans le dossier du projet) dans le contexte du navigateur. **Attention :** si le flux exécute ensuite quand même les étapes de connexion, cela peut gêner (vous êtes déjà connecté). Pour les flux avec étapes de connexion, mieux vaut **laisser cette option DÉSACTIVÉE** et utiliser plutôt le nœud **`use_session`**, qui se ramifie à l'exécution (session présente → naviguer, sinon → connexion + `save_session`). |

**Onglet « Réseau » (proxy)** — s'applique **au navigateur et aux requêtes IA** :

| Paramètre | Signification |
|---|---|
| **Serveur proxy** | p. ex. `http://proxy.entreprise.com:8080` ; vide = pas de proxy / par défaut du système. |
| **Exceptions / contournement** | hôtes sans proxy séparés par des virgules (`localhost, 127.0.0.1, *.interne`). |
| **Utilisateur / mot de passe** | facultatif, pour les proxys authentifiés. |

**Onglet « IA »** — voir [IA : flux à partir d'une description](#ia--flux-à-partir-dune-description).

---

## Zone de notification & vérification des mises à jour

**Zone de notification (system tray).** Réduire la fenêtre principale (via le bouton `—` dans la
barre de titre) place WebExStudio dans la zone de notification au lieu de la barre des tâches.
L'icône propose **Afficher** (aussi par clic gauche) et **Quitter** ; fermer avec `✕` met fin à
l'application comme d'habitude. Si l'environnement de bureau n'offre pas de zone de notification
(serveur sans interface, certains compositeurs Wayland sans Status-Notifier), la réduction retombe
sur le comportement standard — sans message d'erreur.

**Vérification des mises à jour.** La fenêtre **À propos** vérifie automatiquement GitHub Releases
à l'ouverture (non bloquant, compatible proxy — utilise le proxy système avec les identifiants par
défaut, fonctionne donc aussi derrière un proxy d'entreprise Kerberos/Negotiate). Si une nouvelle
version est disponible, le bouton `📥 Ouvrir la page du release` apparaît et ouvre
`github.com/Kroste/WebExStudio/releases/latest` dans le navigateur — pas d'installation silencieuse,
vous décidez quand et comment mettre à jour. Le bouton `🔄 Vérifier les mises à jour` déclenche une
nouvelle vérification sur demande. Les erreurs (hors ligne ou proxy) sont uniquement journalisées,
jamais affichées comme boîte de dialogue.

---

## Journalisation

NLog écrit dans `WebExStudio.UI/bin/<Config>/net10.0/logs/` :

| Fichier | Contenu |
|---|---|
| `info.log` | Niveau info (déroulement, démarrage de nœud, targets …) — aussi en couleur sur la console. |
| `debug.log` | Niveau debug pour `WebExStudio.*` (détaillé). |
| `error.log` | Erreurs avec pile d'appels. |

Les exceptions non gérées (y compris celles des gestionnaires d'événements de l'interface) sont interceptées par un gestionnaire global : elles sont journalisées en **Fatal** dans `error.log` et affichées dans une boîte de dialogue d'erreur — l'application continue de fonctionner dans la mesure du possible.

Pendant l'exécution, le statut des nœuds et les sorties `debug` apparaissent aussi en direct dans le **journal d'exécution** en bas de l'appli. Par entrée :

- **Double-clic** → saute au nœud correspondant dans l'éditeur (ouvre son onglet, le sélectionne et centre la vue).
- **Clic droit** → « ↪ Aller au nœud », « 📋 Copier la ligne » (dans le presse-papiers) et « 💬 Envoyer au chat IA » (transmet le type de nœud, l'**ID de nœud**, le statut et le message d'erreur comme question dans le chat IA — l'ID permet à l'IA de retrouver le nœud sans ambiguïté dans le JSON de flux joint automatiquement).
- Le texte du message est sélectionnable et donc directement copiable.

---

## IA : flux à partir d'une description

Le bouton de barre d'outils **🤖 Flux IA** permet de générer un flux complet à partir d'une description
en langage naturel. Étapes :

1. Saisir une description (p. ex. « Ouvre example.com, connecte-toi, lis le titre et écris-le dans un fichier »).
2. L'IA reçoit le **catalogue de nœuds comme schéma** (dérivé de `NodeCatalog`) et répond avec un JSON de flux.
3. Le résultat est **vérifié avec le `FlowValidator`** avant le chargement. S'il est valide, il atterrit directement
   sur la zone de travail (en flux non enregistré à vérifier). En cas d'erreurs de validation, elles sont affichées ;
   en option, le flux peut être ouvert via **« Charger quand même »** pour une correction manuelle.

**Fournisseurs** (sélectionnables dans les **Paramètres ⚙**, la clé est enregistrée dans `settings.json`) :

| Fournisseur | Modèle par défaut | Remarque |
|---|---|---|
| `anthropic` | `claude-sonnet-4-6` | Anthropic Messages API, clé API requise. |
| `openai` | `gpt-4o` | OpenAI chat completions, clé API requise. |
| `gemini` | `gemini-2.0-flash` | Google Gemini (Generative Language API), clé API requise. |
| `perplexity` | `sonar` | Perplexity (compatible OpenAI), clé API requise. |
| `ollama` | `llama3.1` | Instance locale (URL par défaut `http://localhost:11434`), pas de clé. |

Le modèle et l'URL de base sont surchargés par fournisseur. La connexion est encapsulée dans le projet
`WebExStudio.AI` via l'interface `ILlmClient` — d'autres fournisseurs peuvent y être ajoutés sans modifier
le générateur.

**Détecter l'IA locale automatiquement :** le bouton **🔍 Détecter l'IA locale** (Paramètres → IA) vérifie à la
demande les serveurs LLM locaux courants et renseigne automatiquement le fournisseur, l'URL de base et un modèle
détecté — **Ollama** (`localhost:11434`, p. ex. via Pinokio) ainsi que les serveurs **compatibles OpenAI**
**LM Studio** (`1234`), **llama.cpp** (`8080`) et **Jan** (`1337`). Au mieux via ces ports connus ; les ports
différents se saisissent manuellement.

### Chat IA

Le bouton de barre d'outils **💬 Chat IA** ouvre une fenêtre de chat avec l'IA (plusieurs tours, l'historique
est conservé). Vous pouvez poser des questions sur WebExStudio ou développer un flux de façon itérative. Si une
réponse contient un flux, un bouton **« 📥 Charger dans l'éditeur »** apparaît en dessous — le flux est (comme
dans le dialogue Flux IA) d'abord validé puis chargé. De plus, dès que la dernière réponse contient un flux,
une **barre de chargement fixe apparaît juste au-dessus du champ de saisie** — le chargement reste accessible même
avec de très longues réponses (sans avoir à dérouler jusqu'à la fin du message). Le chat utilise les mêmes
paramètres de fournisseur et de proxy.

À **chaque** message, le chat reçoit l'**état actuel du flux** (y compris les modifications intermédiaires des
nœuds), de sorte que les demandes de modification s'appuient sur le flux réel et que le flux retourné peut être
chargé directement.

### Expliquer un flux

Le bouton de barre d'outils **🧾 Expliquer** fait résumer le **flux actuel** par l'IA en langage compréhensible
(vue d'ensemble, étape par étape le long des connexions, risques possibles). L'explication apparaît dans la
fenêtre de chat ; le flux est fourni au modèle en arrière-plan, de sorte que vous pouvez poser des questions
de suivi directement.

### Suggestion de nœud

Sélectionnez un nœud et cliquez sur **💡 Suggérer** : l'IA suggère — sur la base de tout le flux — le **prochain
nœud pertinent** (type, libellé, configuration, justification). La suggestion est vérifiée contre le catalogue
de nœuds ; via **Ajouter**, elle est créée sous le nœud sélectionné et connectée automatiquement depuis sa sortie.

La case **« Suggestion de nœud »** dans la **barre d'état** (en bas) active/désactive la fonction (le réglage est
enregistré). La barre d'état affiche aussi le statut actuel, le nom du flux, l'onglet actif, le nombre de nœuds
et le fournisseur IA.

### Conseils IA (problèmes connus & solutions)

Dans l'onglet **« IA »** des paramètres, il y a un champ **« Problèmes connus & correctifs »** — une courte liste
auto-entretenue (un conseil par ligne). Si l'interrupteur **« Envoyer les conseils à l'IA »** est activé, ces
conseils sont joints à **chaque** requête IA (créer un flux, chat, expliquer, suggérer) en plus du flux. Cela
permet de consigner les problèmes résolus une fois et de les prendre en compte automatiquement à l'avenir. Gardez
les conseils courts pour que l'IA puisse bien les utiliser.

Dans le **chat IA**, chaque réponse a un bouton **« 📌 Mémoriser comme conseil »** — il ajoute la réponse
(abrégée) directement à la liste des conseils (modifiable ensuite dans les paramètres).

---

## Plugins (nœuds personnalisés)

Des types de nœuds personnalisés peuvent être chargés comme plugins — sans reconstruire l'appli.

**Écrire un plugin :**
1. Créer une bibliothèque de classes (`net10.0`) référençant **`WebExStudio.Core`** et **`WebExStudio.Engine`**.
2. Pour chaque nœud, implémenter un `IActionHandler` (`string Type` + `ExecuteAsync(ExecutionContext, FlowNode)`)
   et fournir une `NodeDefinition` (métadonnées pour palette/propriétés).
3. Implémenter une classe avec **`INodePlugin`** (constructeur sans paramètre) qui renvoie les deux sous forme de
   `NodePluginNode(Definition, Handler)`.

```csharp
public sealed class MyPlugin : INodePlugin
{
    public IEnumerable<NodePluginNode> CreateNodes() =>
    [
        new(new NodeDefinition { Type = "my_hello", DisplayName = "Bonjour", Category = "Plugins",
                                 Description = "…", Example = "…",
                                 Properties = [ new() { Key = "text", Label = "Texte", Kind = PropertyKind.Text } ] },
            new MyHelloHandler()),
    ];
}
```

En option, marquer la version d'API cible pour que le chargeur avertisse en cas d'incompatibilité :
```csharp
[assembly: WebExStudioPlugin(PluginApi.Version)]
```

Si le nœud doit **se ramifier** lui-même (plusieurs sorties, comme if_then_else), définir
`OutputPorts`/`OutputLabels` dans la `NodeDefinition` et **`RoutesOutputs = true`** ; le handler route alors
via `ctx.FollowOutput(node, port)`.

**Plugins fournis** (sous [`samples/`](samples), aussi des modèles) :

- [`samples/FileCheckPlugin`](samples/FileCheckPlugin) — nœud **« Fichier présent ? »** (`file_exists`) : recherche dans le
  dossier (vide = dossier de téléchargement) un nom/motif et se ramifie **trouvé / non trouvé** — pratique pour vérifier
  avant un téléchargement si le fichier existe déjà (exemple de nœud **à ramification**, `RoutesOutputs`).
- [`samples/HttpRequestPlugin`](samples/HttpRequestPlugin) — nœud **« Requête HTTP »** (`http_request`) :
  envoie une requête REST/webhook **sans navigateur** (méthode, en-têtes `Nom: valeur` par ligne, corps).
  Corps de réponse → `ctx_key` (par défaut `response`), code d'état → `status_key` (par défaut `response_status`) ;
  en option **échouer si le statut ≥ 400**. `{secret[..]}` est autorisé dans l'URL/les en-têtes/le corps et est
  résolu **uniquement à l'envoi**, jamais journalisé (exemple de gestion correcte des secrets dans les plugins).

Construire et copier la/les DLL :
```bash
dotnet build samples/HttpRequestPlugin -c Release
# copier HttpRequestPlugin.dll + HttpRequestPlugin.deps.json dans %AppData%/WebExStudio/plugins, redémarrer l'appli
```

**Chargement :** placer la DLL compilée dans un dossier `plugins/` — à côté de l'application **ou** sous
`%AppData%\WebExStudio\plugins` (Linux/macOS : `~/.config/WebExStudio/plugins`). Au démarrage, elle est chargée dans un
**contexte de chargement isolé** (`AssemblyLoadContext`) : les assemblys hôtes communs (WebExStudio, System, Avalonia,
NLog) sont partagés avec l'appli, ses propres dépendances proviennent du `*.deps.json` du plugin — ainsi les
bibliothèques de plugins n'entrent pas en collision avec celles de l'appli. Les nœuds apparaissent dans la **palette,
le panneau de propriétés, la validation** et sont disponibles pour l'**IA**.

**Gérer :** **Paramètres → onglet « Plugins »** affiche les plugins détectés avec leur statut et permet de les
**activer/désactiver** (effet après redémarrage) ainsi que d'ouvrir le dossier des plugins.

> **Sécurité :** les plugins sont du **code arbitraire avec les pleins droits de l'appli** (navigateur, fichiers,
> réseau) — ne chargez que des plugins de confiance ; il n'y a pas de bac à sable. Un type de nœud existant n'est pas
> écrasé. Il n'existe pas (encore) d'éditeurs de propriétés personnalisés — seulement les types de champs disponibles.

---

## Ligne de commande (CLI / sans interface)

Les flux peuvent être exécutés **sans interface graphique** — idéal pour `cron`/planification de tâches, CI ou
serveurs. Le projet **`WebExStudio.Cli`** produit la commande **`webex`** et utilise les **mêmes plugins, le même
coffre d'identifiants et le même validateur/exécuteur** que l'appli.

Des binaires `webex` prêts à l'emploi sont joints à chaque **release GitHub** comme **asset distinct**
(`webex-<version>-linux-x64.tar.gz` ou `webex-<version>-win-x64.zip`) — ou à construire soi-même :

```bash
dotnet build WebExStudio.Cli -c Release          # construit l'exécutable "webex"

webex run      -f projects/f95zone/f95zoneV2.json -c '<mdp-coffre>'   # exécuter (sans interface par défaut)
webex validate -f flow.json                       # valider seulement (pas de navigateur)
webex secrets  -f flow.json                       # de quelles entrées {secret[..]} le flux a-t-il besoin ?
```

**Options pour `run` :**

| Option | Effet |
|---|---|
| `-f, --flow <chemin>` | Chemin du fichier de flux (obligatoire) |
| `-c, --credential <mdp>` | Mot de passe du coffre. Mieux : la variable d'environnement `WEBEX_VAULT_PW` ou la saisie interactive — un mot de passe en argument se retrouve dans l'historique du shell/la liste des processus. |
| `--var key=value` | Valeur initiale dans le contexte payload (répétable) → paramétrer le flux |
| `--headful` | Démarrer le navigateur visiblement (sinon sans interface) |
| `--browser <nom>` | `chromium` (par défaut), `firefox`, `webkit` |
| `--timeout <ms>` | Délai par défaut par action |
| `--download-dir <d>` | Dossier cible pour les téléchargements |
| `--out <fichier.json>` | Écrire un rapport d'exécution (statut des nœuds, erreurs) en JSON |

**Codes de sortie** (pour cron/CI) : `0` OK · `1` erreur d'exécution (un nœud a échoué) · `2` validation/invocation ·
`3` coffre (mot de passe manquant/incorrect) · `130` annulé (Ctrl+C).

Le coffre n'est déverrouillé que si le flux utilise réellement `{secret[..]}`. Avant chaque exécution, il est validé
comme dans l'interface (les erreurs interrompent). Les nœuds IA (`ai_query`) ne sont pas actifs en CLI.

---

## Importer des projets hérités

Les anciens projets Python WebEX (dossier avec `actions/*.json`, références `call`/`then_actions_file` imbriquées et `targets.json`) sont convertis en un seul flux v2.

**Dans l'appli :** barre d'outils **♻ Convertir** → choisir l'ancien dossier de projet → le flux converti est chargé directement dans l'éditeur (à vérifier et enregistrer ensuite).

**Via la ligne de commande :**

```bash
dotnet run --project WebExStudio.UI -- --convert <dossierProjetHérité> <sortie.json>
# Exemple :
dotnet run --project WebExStudio.UI -- --convert projects/usv2 projects/usv3/flow.json
```

Dans ce processus :
- chaque fichier `.json` référencé devient un **sous-nœud nommé** (nom = chemin avec des points, p. ex. `configuration.general.datetime.daylightSavings`) ;
- `call` / `then_actions_file` / `else_actions_file` deviennent des **nœuds `call`** visibles ;
- `if` devient un nœud avec **sorties then/else**, les branches sont refusionnées vers l'étape suivante ;
- `targets.json` atterrit comme liste dans le nœud **`function`/Début**, un **`foreach`** itère dessus → appelle le sous-nœud `start`.

Vous ouvrez le résultat (`projects/usv3/flow.json`) via **📄 Ouvrir un flux**.

---

## Format de fichier (v2)

Un flux est **un** fichier JSON :

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
      "label": "Page d'accueil",
      "x": 80, "y": 40,
      "config": { "url": "{payload.host}" },
      "wires": [ ["n2"] ],
      "seqIndex": 0
    }
  ]
}
```

- **`tabs`** : `main` (`isSubFlow=false`) + sous-nœuds nommés (`isSubFlow=true`, `name` unique).
- **`nodes[].wires`** : `wires[portIndex]` = liste des ID de nœuds cibles à cette sortie. `if`/`foreach` utilisent les index 0 et 1.
- **`nodes[].config`** : tous les champs spécifiques au nœud sous forme de chaînes (nombres/booléens aussi en chaînes).
- **`nodes[].label`** : le libellé librement choisi (affiché sur le nœud).
- Appels de sous-nœud : un nœud `call` avec `config.target = <nom-du-sous-nœud>`.

---

## Validation des flux

Le `FlowValidator` (dans `WebExStudio.Core`) vérifie un document de flux pour les erreurs structurelles et
schématiques — un filet de sécurité pour les flux importés et (à l'avenir) générés automatiquement.
Il renvoie une liste de constats de gravité **Error** ou **Warning** ;
`IsValid` vaut `true` tant qu'il n'y a pas d'erreur.

**Erreurs** (le flux ne s'exécute pas de façon fiable ainsi) :

| Code | Signification |
|---|---|
| `unknown-type` | Le type de nœud n'est pas connu dans le catalogue. |
| `missing-required` | Un champ obligatoire manque (les alias sont pris en compte). |
| `dangling-wire` | Une connexion pointe vers un ID de nœud inexistant. |
| `cross-tab-wire` | Une connexion mène à un nœud d'un autre onglet (autorisé seulement via `call`). |
| `wire-invalid-port` | Une connexion à une sortie que le type de nœud n'a pas. |
| `wire-into-no-input` | Une connexion mène à un nœud sans entrée (p. ex. une annotation). |
| `call-target-missing` | Un nœud `call` référence un sous-nœud inconnu. |
| `duplicate-node-id` | Un ID de nœud apparaît plusieurs fois. |
| `duplicate-subnode-name` | Un nom de sous-nœud attribué plusieurs fois (cible `call` ambiguë). |
| `unknown-tab` | Un nœud référence un onglet inconnu. |
| `no-main-tab` | Il n'y a pas d'onglet principal. |

**Avertissements** (suspects, peut-être intentionnels) : `no-entry-node` (onglet sans point de départ / cycle),
`group-missing-node`, `group-foreign-node`.

**À l'exécution :** avant chaque exécution, le flux est validé. S'il y a une **erreur**, l'exécution
**n'est même pas lancée** — les constats apparaissent dans le panneau de journal, le premier nœud fautif est marqué
en rouge et la vue saute à son onglet. Les **avertissements** sont également affichés dans le journal mais ne
bloquent pas l'exécution.

Les flux d'exemple fournis sous `projects/` sont vérifiés automatiquement contre le validateur par
`ExampleFlowsValidateTests`.

---

## Tests & Intégration continue

### Exécuter les tests localement

```bash
dotnet test
```

| Projet de test | Couvre |
|---|---|
| `WebExStudio.Core.Tests` | Sérialisation (aller-retour), aides `FlowDocument2`, `NodeCatalog`, le convertisseur hérité, **validation des flux** (y compris la vérification des flux d'exemple). |
| `WebExStudio.Engine.Tests` | `ExecutionContext` (payload/espaces réservés), `ActionRegistry`, handlers (sans navigateur) et l'**exécution des connexions** (branche if, boucle foreach). |
| `WebExStudio.UI.Tests` | Logique de `FlowEditorViewModel` (nœuds/connexions/sous-nœuds/onglets/groupes, sans rendu) et le **blocage de validation avant l'exécution**. |
| `WebExStudio.AI.Tests` | Le **générateur de flux IA** (prompt → analyse → validation) avec un client fictif et la sélection de fournisseur de `LlmClientFactory` — sans réseau. |
| `WebExStudio.Cli.Tests` | Analyse des arguments du runner sans interface `webex` (`Options.Parse` : commandes, drapeaux, `--var`, cas d'erreur). |

Les tests du moteur s'exécutent **sans navigateur** — les nœuds nécessitant Playwright sont contournés via des conditions basées sur le payload. Les chemins lourds en navigateur/E-S (handlers Playwright, glisser-déposer, la commande `run` de la CLI) ne sont pas testés unitairement ; la logique sous-jacente est vérifiée à la place (p. ex. `Options.Parse`, `SecretReferenceScanner`, `ViewTransform`).

### GitHub Actions

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) :

1. **`test`** — construit la solution (Release) et exécute `dotnet test` (à chaque push/PR).
2. **`release`** — s'exécute **uniquement sur un tag `v*` ou manuellement** (*workflow_dispatch*) et seulement si les tests sont au vert — **pas** à chaque push (économise le stockage d'artefacts). Crée des **builds autonomes en fichier unique** pour **Linux (`linux-x64`)** et **Windows (`win-x64`)** — un paquet pour la **GUI** (`WebExStudio-…`) et un pour la **CLI** (`webex-…`) — plus, pour Linux, un **AppImage** (`WebExStudio-…-x86_64.AppImage`, construit via [`build/make-appimage.sh`](build/make-appimage.sh)).
3. Sur un **tag `v*`** (p. ex. `git tag v1.0.0 && git push --tags`), tous les paquets (GUI + CLI + AppImage, par plateforme) sont en plus joints comme **assets distincts** à une **release GitHub**. L'**AppImage** est le téléchargement le plus pratique pour Linux : le rendre exécutable et le lancer (`chmod +x WebExStudio-*.AppImage && ./WebExStudio-*.AppImage`).

---

## Structure du projet

| Projet | Description |
|---|---|
| `WebExStudio.Core` | Modèles de données (`FlowDocument2`, `FlowNode`, `FlowTab`, `NodeCatalog`), sérialisation (`FlowSerializer2`), le convertisseur hérité (`LegacyImporter`). |
| `WebExStudio.Engine` | Exécuteur de flux (parcours des connexions), intégration Playwright, handlers d'action, traçage. |
| `WebExStudio.UI` | Application de bureau Avalonia : zone de travail, rendu des nœuds/connexions, palette, panneau de sous-nœuds, propriétés, trace, paramètres, à propos. |
| `WebExStudio.Cli` | Runner sans interface `webex` (`run`/`validate`/`secrets`) — exécuter des flux sans GUI (cron/CI). |
| `WebExStudio.AI` | Connexion IA : export du schéma de nœuds, construction du prompt, `FlowGenerator` et fournisseurs (`ILlmClient` : Anthropic/OpenAI/Ollama). |

Technique : **.NET 10**, **Avalonia 12.0**, **Microsoft.Playwright 1.52**, **NLog 6**.
