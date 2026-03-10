# AGIS ESM — Visual Graph Editor Design Specification
# File: EDITOR_DESIGN.md
# Location: Project root (alongside CLAUDE.md)
# Purpose: Comprehensive visual layout, component hierarchy, and content
#          specification for the AGIS graph editor. Intended for UI/UX design
#          planning. Keep in sync with CLAUDE.md architecture decisions.

---

## Technology Stack

- **Framework**: UIToolkit Runtime (`UnityEngine.UIElements`) — **fully runtime; works in shipped builds**
- **UI Layer**: UXML + USS for all panels, toolbar, and the graph canvas. No `UnityEditor` dependencies anywhere.
- **Entry point**: In-game toggle (e.g. F12 key or a UI button) that shows/hides a `UIDocument`-driven overlay
- **Window type**: Runtime `MonoBehaviour` + `UIDocument` — not an `EditorWindow`. Can be opened mid-game.
- **Registry source**: `AGISStateMachineRunner.NodeTypes` / `ConditionTypes` (already public, already populated at runtime)
- **Save pipeline**: `AGISGraphSerializer.ToJson()` → `AGISContentLibrary.ImportGraph()` → `ApplyGraphToRunner()`
- **Undo**: Custom `AGISEditorHistory` command stack (no `UnityEditor.Undo` — that is editor-only and stripped from builds)

---

## Overall Window Layout

```
+==============================================================================+
| [Tab: PatrolGraph]  [Tab: RoutedMovement]  [+]                               |  <- Tab bar (one tab per open asset)
+==============================================================================+
| [Toolbar strip — full width]                                                 |
+------------------------------------------------------+------------------------+
|                                                      |                        |
|   [Breadcrumb bar — overlaid top-left of canvas]     |                        |
|                                                      |   [Right Panel]        |
|                                                      |   320px wide           |
|              [Graph Canvas]                          |   Slide in/out         |
|              (infinite, pan+zoom)                    |                        |
|                                                      |   Tab A: Node          |
|                                                      |   Tab B: Edge          |
|                                          [MiniMap]   |   Tab C: Graph         |
|                                          (corner)    |   Tab D: Grouped       |
|                                                      |   Tab E: Blackboard    |
+------------------------------------------------------+------------------------+
| [Status Bar — full width]                                                    |
+==============================================================================+
```

---

## 1. TAB BAR

**Location**: Very top of window, above the toolbar.
**Behavior**: One tab per open `AGISStateMachineGraphAsset` or `AGISGroupedStateAsset`.

### Contents per tab
| Element | Detail |
|---|---|
| Asset filename | e.g. "PatrolGraph" (without .asset extension) |
| Unsaved dot | Small orange circle when there are unsaved changes |
| [x] close | Closes the tab (prompts save if unsaved) |
| [+] button | Opens the in-game graph picker overlay (see below) |

### Behavior notes
- Active tab is highlighted with a bottom border accent color
- Tabs are reorderable by drag
- Opening a graph via the picker focuses its tab if already open

### In-game graph picker overlay
Opened by the `[+]` button or `[Import JSON]`. No OS file browser — lists graphs from:
1. Graphs already registered in `AGISContentLibrary` (by key)
2. `AGISStateMachineGraphAsset` objects loadable via `Resources.LoadAll<>()` (if placed under `Resources/`)

```
+--[OPEN GRAPH]----------------------------------+
|  [Search: ________________]                    |
|  [+ New Graph]                                 |
|  ─────────────────────────────────────         |
|  PatrolGraph          (registered)             |
|  RoutedMovement       (registered)             |
|  EnemyBehavior        (Resources)              |
+------------------------------------------------+
```
| Element | Detail |
|---|---|
| Search field | Filter by graph name |
| [+ New Graph] | Creates a blank graph (AnyState only, no entry); prompts for a name |
| Graph rows | Click to open in a new tab or focus existing tab |

### Slot selector
The toolbar includes a **slot dropdown** showing all slots of the connected `AGISStateMachineRunner`
(populated from `runner.Slots`, displaying `slotName`). Changing the slot re-clones `_workingGraph`
from that slot and rebuilds the canvas. The currently active slot is shown in the status bar.

---

## 2. TOOLBAR

**Location**: Full-width strip below the tab bar.
**Layout**: Left group | Center group | Right group

### Left group
| Control | Type | Action |
|---|---|---|
| Asset name / breadcrumb | Label | Shows current asset + depth if drilled into sub-graph |
| Slot dropdown | Dropdown | Lists all slots of the connected runner by `slotName`; switching re-clones `_workingGraph` from that slot |
| [Save] | Button | `AGISGraphSerializer.ToJson()` → write to `Application.persistentDataPath` (or `AGISContentLibrary`) → `runner.ApplyGraphToRunner()` if a runner is connected |
| [Undo] | IconButton | Undo last action; disabled when nothing to undo |
| [Redo] | IconButton | Redo last undone action; disabled when nothing to redo |
| [Validate] | Button | Runs `AGISGraphValidator` and refreshes overlays |

### Center group
| Control | Type | Action |
|---|---|---|
| [+ Add Node] | Button | Opens the Node Search Window (same as spacebar) |
| [Auto-Layout] | Button | Runs a simple left-to-right DAG layout on all nodes |
| [Frame All] | Button | Zooms/pans canvas to fit all nodes |
| [Frame Selected] | Button | Zooms/pans canvas to fit selected nodes; disabled when nothing is selected |

### Right group
| Control | Type | Action |
|---|---|---|
| Zoom % | Label | e.g. "85%" — click to reset to 100% |
| [Snap] | Toggle | Snap-to-grid on drag |
| [Grid] | Toggle | Show/hide background grid |
| [Minimap] | Toggle | Show/hide minimap |
| Separator | --- | --- |
| [Debug] | Toggle | Enable live debug overlay (shows active node/edge; requires a runner to be connected) |

---

## 3. BREADCRUMB BAR

**Location**: Overlaid on top-left corner of the Graph Canvas.
**Visibility**: Only visible when the user has drilled into a sub-graph (Grouped node).

### Contents
```
[<]  PatrolGraph  >  RoutedMovement  >  [current level]
```

| Element | Detail |
|---|---|
| [<] back arrow | Navigate one level up |
| Segment buttons | Each ancestor graph name; click to jump directly to that level |
| Current level | Non-clickable, shows the deepest active graph's display name |

---

## 4. GRAPH CANVAS

**Location**: Main area of the window.
**Technology**: Custom `VisualElement` canvas with manual pan (middle-mouse / alt-drag), scroll-zoom via `WheelEvent`, and rubber-band multi-select. No `GraphView` dependency.

The canvas renders four stacked layers (bottom to top):

### Layer 1 — Background
- Dot grid or line grid (configurable via toggle in toolbar)
- Grid color, spacing, and subdivision defined in `AGISGraph.uss`
- Rubber-band selection rectangle drawn here

### Layer 2 — Edges (transition arrows)
Described in detail in **Section 4B** below.

### Layer 3 — Nodes (state cards)
Described in detail in **Section 4A** below.

### Layer 4 — Overlays (badges, debug highlights)
- Validation error/warning badges
- Debug active-node glow (when debug overlay is on and runner is connected)
- Transition flash effect on recently-fired edges

---

## 4A. NODE CARDS

All node kinds share a common card structure. Differences are noted per kind.

### Common card anatomy
```
+--[HEADER]---------------------------------------------+
| [KIND ICON]  [Display Name]          [BADGES] [v] [x] |
+--[PARAMS]---------------------------------------------+
| param_display_name_1     [input field]            [R] |
| param_display_name_2     [input field]            [R] |
| param_display_name_3     [input field]            [R] |
|                                          [More...] |  <- if > 4 params
+--[PORT]-----------------------------------------------+
| o  OUT                                                 |
+-------------------------------------------------------+
```

| Element | Detail |
|---|---|
| KIND ICON | Icon representing `AGISNodeKind` (see icon table below) |
| Display Name | `IAGISNodeType.DisplayName` — large, readable |
| BADGES | Validation badge (red/yellow), Entry crown (gold star) |
| [v] | Collapse/expand the param body. Reads/writes `visual.collapsed`. Double-clicking the node header is also a valid collapse/expand gesture. |
| [x] | Delete node (hidden for AnyState; confirms if edges exist) |
| param rows | One row per `AGISParamSpec` in type's `Schema.Specs` |
| [R] reset | "Reset to default" — removes the key from `AGISParamTable` |
| OUT port | Single output port; drag from here to create a transition edge |

### Param input field types (per `AGISParamType`)
| AGISParamType | Control |
|---|---|
| Bool | Toggle checkbox |
| Int | Integer field (arrows up/down); respects `intMin`/`intMax` if set |
| Float | Float field with step; respects `floatMin`/`floatMax` if set |
| String | Single-line text field |
| Vector2 | Two float fields side-by-side labeled X Y |
| Vector3 | Three float fields side-by-side labeled X Y Z |
| Guid | Read-only label showing GUID string + [Copy] icon |

**Override highlighting**: If a param value is identical to the schema default, the
field label and value are shown dimmed (grey). If overridden, the label is bold and
the reset [R] button is visible.

**Tooltip**: A small (?) icon appears next to the label when `spec.tooltip` is not empty.
Hover shows the tooltip string.

**Category grouping**: If `spec.category` is set, params are grouped under collapsible
category headers inside the node card.

---

### 4A-i. Normal State Node  (`AGISNodeKind.Normal`)

- **Header color**: Steel blue (#3A7BD5)
- **Icon**: Circle with a dot (state symbol)
- **Entry indicator**: Gold star ★ in top-left corner of header when `nodeId == graph.entryNodeId`
- **Collapsed form**: Shows header only (kind icon + display name + badge + controls)
- **Expanded form**: Full card with all param rows

**Special: Dialogue node** (`nodeTypeId == "agis.dialogue"`)

Inside the param section, after the standard params, a dialogue-specific section appears:
```
+--[DIALOGUE TRANSITIONS]----------------------------+
|  Mode: 0 choices  [+ Add Choice]  [- Remove Last]  |
|                                                    |
|  Ended edge  ->  [unconnected o]                   |
+----------------------------------------------------+
```
| Element | Detail |
|---|---|
| Mode label | "Ended (0 choices)" or "N choices" |
| [+ Add Choice] | Calls `AGISDialogueEdgeSync.AddChoice(graph, node.nodeId, choiceKey)` |
| [- Remove Last] | Calls `AGISDialogueEdgeSync.RemoveLastChoice(graph, node.nodeId, choiceKey)` |
| Edge list | Read-only list of current edges from this node, with their targets |

The `choiceKey` argument is the node's `choice_key` param value (defaulting to
`AGISDialogueConstants.DefaultChoiceKey` if not overridden).

---

### 4A-ii. AnyState Node  (`AGISNodeKind.AnyState`)

- **Header color**: Dark crimson (#8B1A1A)
- **Icon**: Hexagon ⬡ (global / all-encompassing symbol)
- **Cannot be deleted** — [x] is hidden
- **Cannot receive incoming edges** — no input port rendered
- **No params** — param body is hidden
- **One instance only** per graph — the editor ensures this
- **Collapsed always**: shows only header + OUT port; [v] is hidden

```
+--[HEADER]---------------------------------------------+
| [⬡]  ANY STATE                                        |
+--[PORT]-----------------------------------------------+
| o  OUT                                                 |
+-------------------------------------------------------+
```

---

### 4A-iii. Grouped (Macro) Node  (`AGISNodeKind.Grouped`)

- **Header color**: Teal (#1A8B7A)
- **Icon**: Nested squares ⧉
- **Extra header button**: [⊞] "Open Sub-Graph" — drills into internal graph

Body shows two sections:

**Asset section**:
```
+--[ASSET]----------------------------------------------+
| Asset:  [RoutedMovement.asset          v]  [⊞ Open]  |
+-------------------------------------------------------+
```
- Dropdown is populated at runtime from two sources: `AGISContentLibrary` registered grouped assets, and `Resources.LoadAll<AGISGroupedStateAsset>()` (if assets are placed under `Resources/`)
- No `ObjectField` — that is `UnityEditor.UIElements`-only and stripped from builds. Use the same in-game picker overlay pattern as the graph picker (Phase 0-F).

**Exposed Overrides section** (only if asset is assigned and has `exposedParams`):
```
+--[EXPOSED OVERRIDES]----------------------------------+
| wander_radius    [ 5.00 ]                        [R]  |
| detection_range  [ 10.0 ]                        [R]  |
+-------------------------------------------------------+
```
- Fields driven by `AGISGroupedStateAsset.exposedParams` list
- Values stored in `node.exposedOverrides` (`AGISParamTable`)

---

### 4A-iv. Parallel Node  (`AGISNodeKind.Parallel`)

- **Header color**: Purple (#6B3FA0)
- **Icon**: Double vertical bars ⫼
- **Single outgoing edge only** — after the first outgoing edge exists, the OUT port greys out
  and cannot be dragged from (matches Design Constraint #7).

Body shows branch list:
```
+--[BRANCHES]-------------------------------------------+
| Branch A:  [npc.idle          v]                      |
| Branch B:  [npc.wander        v]                      |
| [+ Add Branch]                                        |
+-------------------------------------------------------+
```
- Each branch is a dropdown of node types (creates a `parallelChildren` entry)
- [+ Add Branch] / [- Remove Last] controls

---

### 4A-v. Node Kind Icon Table

| Kind | Icon | Color |
|---|---|---|
| Normal | ● (filled circle) | Steel blue |
| AnyState | ⬡ (hexagon) | Dark crimson |
| Grouped | ⧉ (nested squares) | Teal |
| Parallel | ⫼ (parallel bars) | Purple |
| Entry indicator | ★ (gold star) | Gold — overlay only |

---

### 4A-vi. Node Validation Badges

Badges are rendered as small circles in the top-right corner of the node header,
overlapping the border:

| Badge | Condition |
|---|---|
| Red ⊗ with count | Node has 1+ `Error` validation issues |
| Yellow △ with count | Node has 1+ `Warning` validation issues (no errors) |
| No badge | Node is valid |

Hovering any badge shows a tooltip listing all issues for that node.

---

## 4B. TRANSITION EDGES

### Normal (connected) edge

```
  [NodeA]  ─────────────[LABEL PILL]────────────►  [NodeB]
```

**Visual anatomy**:
| Element | Detail |
|---|---|
| Bezier curve | Smooth S-curve from NodeA's OUT port to NodeB's body |
| Arrowhead | Filled triangle pointing into NodeB |
| Label pill | Centered on the edge curve; always visible |
| Selection | Edge thickens + color brightens when selected |

**Label pill contents**:
```
 P2   AND( meter>=max · !dead )
```
| Element | Detail |
|---|---|
| Priority badge | "P" + number; background color = priority level (see table) |
| Condition summary | Abbreviated text of the condition expression |

**Priority badge colors**:
| Priority | Color |
|---|---|
| 0 | Grey |
| 1–3 | Green |
| 4–9 | Yellow |
| 10–19 | Orange |
| 20+ | Red |

**Clicking the label pill** selects the edge AND opens the Edge/Condition tab in the
right panel.

**Policy indicators** (shown as small icons inside the label pill when non-default):
| Indicator | Condition |
|---|---|
| ⏱ icon + seconds | `cooldownSeconds > 0` |
| ⛔ icon | `interruptible == false` |
| 🔒 scopeId | `scopeId != "Any"` |

---

### Dangling (unconnected) edge

Used when `toNodeId == AGISGuid.Empty`. Common for dialogue choice edges before
the designer connects them to a target.

```
  [NodeA]  ─────────────[LABEL PILL]────────────o
                                                  ^
                                      open circle (draggable)
```

| Element | Detail |
|---|---|
| Curve | Same bezier, but ends in a floating open circle ○ |
| Color tint | Yellow/orange tint on the curve to indicate it needs connecting |
| Label pill | Same as normal edge but with a warning icon prefix ⚠ |
| Drag behavior | User drags the open circle ○ onto a node to connect it |

---

### AnyState edge

Visually identical to a normal edge but with additional styling:
- Curve starts with a dark crimson / red color
- Priority badge uses a larger font size
- A subtle "GLOBAL" prefix in the label pill indicates it is a global interrupt

---

### Edge Validation Badge

Same badge system as nodes:
- Red ⊗ badge on the label pill for errors
- Yellow △ badge for warnings

---

## 5. MINIMAP

**Location**: Bottom-right corner of the canvas. Collapsible via toolbar toggle.

```
+--[MINIMAP]----+
|               |
|  [viewport    |
|   rect]  ...  |
| ..nodes.. ... |
|               |
+---------------+
```

| Element | Detail |
|---|---|
| Viewport rectangle | White outline showing the currently visible portion |
| Node dots | Tiny colored rectangles matching each node's kind color |
| Drag | Drag viewport rect to pan the main canvas |
| Resize handle | Bottom-left corner to resize the minimap |

The minimap is a custom `VisualElement` (see `AGISMinimapElement.cs`). It uses `generateVisualContent` to draw scaled node rectangles and a viewport indicator rectangle. No `GraphView` dependency.

---

## 6. RIGHT PANEL

**Location**: Fixed right side of the window, 320px wide.
**Behavior**: Slides in/out. Collapses to a thin sliver with a ">" chevron to re-expand.

The panel has a **mini tab bar** at its top, switching content based on context:

```
+--[RIGHT PANEL]-------------------------------+
| [Node] [Edge] [Graph] [Grouped] [Blackboard] |  <- context tab bar
+----------------------------------------------+
| [Tab content — see below]                    |
|                                              |
|                                              |
+----------------------------------------------+
```

Tabs auto-switch on canvas selection:
- Select a node → switches to "Node" tab
- Select an edge → switches to "Edge" tab
- Click empty canvas → stays on last tab, or shows "Graph" if nothing was shown

---

### 6A. Tab A — NODE INSPECTOR

Shown when a node is selected on canvas.

```
+--[NODE INSPECTOR]----------------------------+
| [KIND ICON]  NPC Follow Target               |  <- type display name
| TypeId: npc.follow_target                    |  <- small monospace
| ID: a3f2...  [copy]          [★ Set Entry]   |
+--[PARAMS]------------------------------------+
| > General                                    |  <- category group (if any)
|   Follow Player         [x] true        [R] |
|   Target Key            [__________]    [R] |
|   Use Detection Memory  [ ] false       [R] |
| > Advanced                                   |
|   Pursuit Range Bonus   [0.00      ]    [R] |
|   Pursuit Angle Bonus   [0.00      ]    [R] |
+--[PERSISTENT KEYS]---------------------------+
| (info) Auto-populated in AGISActorState:     |
|   npc.target_time_lost  Float  default: 0    |
+--[VALIDATION]--------------------------------+
|   No issues                                  |
+----------------------------------------------+
```

| Section | Contents |
|---|---|
| Header | Kind icon, DisplayName (h2), TypeId (small code), GUID + copy, Set Entry button |
| Params | One row per `AGISParamSpec`. Label = displayName. Input field per type. [R] reset. Category-grouped if `category` is set. |
| Persistent Keys | Read-only list of keys from `IAGISPersistentNodeType.PersistentParams`. Shows key + type + default. Info note. |
| Validation | List of validation issues for this node. Green "No issues" when clean. |

**Special section — Dialogue** (appended when `nodeTypeId == "agis.dialogue"`):
```
+--[DIALOGUE TRANSITIONS]----------------------+
| Mode: Ended (0 choices)                      |
| [+ Add Choice]    [- Remove Last]            |
| ─────────────────────────────────            |
| Ended edge   ->  [unconnected]               |
+----------------------------------------------+
```

**Special section — Grouped** (appended when `Kind == Grouped`):
```
+--[GROUPED ASSET]-----------------------------+
| Asset:  [RoutedMovement.asset   v]           |
| [Open Sub-Graph]                             |
| ─────────────────────────────────            |
| Exposed Overrides:                           |
|   wander_radius    Float  [5.00      ]  [R] |
+--[SCOPES]------------------------------------+
| Scope: "Exit"  ->  [MoveToWaypoint]          |
| Scope: "Any"   ->  (fallback)                |
| [Edit Scopes...]                             |
+----------------------------------------------+
```

---

### 6B. Tab B — EDGE / CONDITION EDITOR

Shown when a transition edge is selected on canvas.

```
+--[EDGE INSPECTOR]----------------------------+
| Transition                                   |
| From: BehaviorSelector -> Chase              |
| ID: b7c1...  [copy]                          |
+--[EDGE SETTINGS]-----------------------------+
| Priority          [ 3          ]             |
| Interruptible     [x]                        |
| Cooldown (sec)    [ 0.00       ]             |
| Scope ID          [ Any        ]             |
+--[CONDITION EXPRESSION]----------------------+
|                                              |
|  [Replace root: True | False | AND | OR]     |
|                                              |
|  AND                           [x]           |
|  |                                           |
|  +-- LEAF                      [x]           |
|  |   Type: npc.detection_meter_exceeds       |
|  |   [Change Type]                           |
|  |   use_max_detection  [x] true        [R] |
|  |                                           |
|  +-- LEAF                      [x]           |
|      Type: npc.actor_state_bool              |
|      [Change Type]                           |
|      key       [ npc.is_dead  ]         [R] |
|      expected  [ ] false               [R] |
|                                              |
|  [+ Add AND child]                           |
+--[VALIDATION]--------------------------------+
|   No issues                                  |
+----------------------------------------------+
```

| Section | Contents |
|---|---|
| Header | "Transition", From → To node names (or "unconnected"), Edge GUID + copy |
| Edge Settings | Priority (int), Interruptible (bool toggle), Cooldown (float), Scope ID (string / dropdown) |
| Condition Expression | Full recursive tree builder (see below) |
| Validation | Validation issues for this edge |

#### Condition Expression Tree — Node Types

Each tree node is rendered as an indented block with connecting lines:

**AND node**:
```
AND                                [x remove]
|  [+ Add Child]  [Wrap in NOT]  [Replace: OR | LEAF | True | False]
+-- [child 0]
+-- [child 1]
```
- Left border color: Blue

**OR node**:
```
OR                                 [x remove]
|  [+ Add Child]  [Wrap in NOT]  [Replace: AND | LEAF | True | False]
+-- [child 0]
+-- [child 1]
```
- Left border color: Orange

**NOT node**:
```
NOT                                [x remove]
|  [Replace child]
+-- [child]
```
- Left border color: Red

**LEAF node**:
```
LEAF                               [x remove]
   Type: npc.actor_state_bool     [Change Type]
   [Wrap in NOT]  [Wrap in AND]  [Wrap in OR]
   ─────────────────────────────────
   key         [ npc.use_routes ]    [R]
   expected    [x] true              [R]
```
- Condition type shown as a colored pill (category-colored)
- [Change Type] opens Condition Type Search Window
- Param fields below the type line (same field types as node params)

**CONST TRUE**:
```
[✓ Always True]                    [x remove]
   [Change to: False | AND | OR | LEAF]
```
- Green background badge

**CONST FALSE**:
```
[✗ Never (False)]                  [x remove]
   [Change to: True | AND | OR | LEAF]
```
- Grey background badge

---

### 6C. Tab C — GRAPH PROPERTIES

Shown when nothing is selected, or accessed via toolbar button.

```
+--[GRAPH PROPERTIES]--------------------------+
| PatrolGraph.asset                            |
| Path: Assets/NPC_Test/PatrolGraph.asset      |
| Graph ID: g9a2...   [copy]                   |
| Version: 1                                   |
| Nodes: 9   Edges: 14                         |
+--[ENTRY NODE]--------------------------------+
| Entry:  [BehaviorSelector  v]                |
+--[VALIDATION REPORT]-------------------------+
| [✓] No errors, no warnings                  |
|  -- or --                                    |
| [x] 2 errors:                                |
|   > Edge: toNodeId not found  [Select]       |
|   > Node: unknown typeId      [Select]       |
| [!] 1 warning:                               |
|   > Edge: dangling (unconnected) [Select]    |
+--[RUNNER REFERENCES]-------------------------+
| (info) Referenced by:                        |
|   NPC_Test / Slot[1]: Patrol                 |
+--[SAVE / IMPORT / EXPORT]--------------------+
| Auto-save  [ ] (save after every change)     |
| [Export JSON]   [Import JSON]                |
+----------------------------------------------+
```

| Section | Contents |
|---|---|
| Info | Asset name, content library key or `persistentDataPath`-relative path, graph GUID, version number, node/edge counts. **Note**: `graphName` field must be added in Phase 10-A if not present on `AGISStateMachineGraph`. |
| Entry Node | Dropdown of all nodes in graph. Sets `graph.entryNodeId`. |
| Validation Report | Full issue list. Each item has a [Select] button that selects the offending node/edge on canvas. |
| Runner References | Shows which `AGISStateMachineRunner` slots in the active scene reference this asset (runtime scan via `FindObjectsByType`). |
| Auto-save | Toggle: save automatically after every command push (1s debounce). |
| [Export JSON] | Serialize working graph to JSON; prompts for filename; writes to `Application.persistentDataPath`. |
| [Import JSON] | Prompts for filename; reads JSON from `Application.persistentDataPath`; replaces `_workingGraph`. |

---

### 6D. Tab D — GROUPED ASSET INSPECTOR

Active when the user has drilled into a `AGISGroupedStateAsset`'s internal graph, **or** when
"Edit Grouped Asset" is selected from the node context menu on a Grouped node.

```
+--[GROUPED ASSET INSPECTOR]-------------------+
| Display Name:  [RoutedMovement     ]         |
| Asset ID: g1b3...   [copy]                   |
+--[EXPOSED PARAMS]----------------------------+
| + [Add Param]                                |
| ─────────────────────────────────            |
| [wander_radius] Float  default: 5.0          |
|    displayName: [Wander Radius    ]          |
|    tooltip:     [How far the NPC..]          |
|    Min: [x]  0.0    Max: [ ]                 |
|    category:    [Movement         ]          |
|    [- Remove]                                |
| ─────────────────────────────────            |
| [detection_range] Float  default: 10.0       |
|    ...                                       |
+--[BINDINGS]----------------------------------+
| wander_radius  ->  targets:                  |
|   [MoveToWaypoint.wander_radius]  [- Remove] |
|   [+ Add Target]                             |
| detection_range  ->  targets:                |
|   [...]                                      |
+--[SCOPES]------------------------------------+
| [+ Add Scope]                                |
| ─────────────────────────────────            |
| Scope ID:    [Exit               ]           |
| displayName: [Waypoint Reached   ]           |
| Nodes:  [MoveToWaypoint] [AdvanceWaypoint]   |
|          [+ Add Node]  [- Remove]            |
| [- Remove Scope]                             |
+----------------------------------------------+
```

| Section | Contents |
|---|---|
| Info | Display name (editable), Asset GUID |
| Exposed Params | Full editor for `List<AGISExposedParamDef>`. Each entry: publicKey, type, defaultValue, displayName, tooltip, hasMin/Max, min/max, step, category. [+Add] / [-Remove] per entry. |
| Bindings | For each `AGISExposedParamBindingDef`: shows which exposed param it represents, and its list of `AGISParamTarget` entries. Each target: node picker (internal nodes) + param key dropdown. [+Add Target] / [-Remove]. |
| Scopes | For each `AGISInternalScopeDef`: scopeId (editable string), displayName, multi-select node list from internal graph. [+Add Scope] / [-Remove Scope]. |

---

### 6E. Tab E — BLACKBOARD (AGISActorState Viewer)

Active whenever a runner is connected. Displays the live `AGISActorState` key-value store for the selected actor slot.

```
+--[BLACKBOARD]--------------------------------+
| ● LIVE  Actor: NPC_Test  Slot: Main          |  <- status row (hidden when no runner)
| No runner connected                          |  <- shown instead when no runner
+--[KEYS]--------------------------------------+
| [Filter...                       ]          |
| ─────────────────────────────────            |
| npc.use_routes         Bool  [☐ false]       |
| npc.show_detection..   Bool  [☐ false]       |
| npc.route.sequence..   Int   [0        ]     |
| npc.route.waypoint..   Int   [0        ]     |
| npc.target_time_lost   Float [0.0      ]     |
| ...                                          |
+----------------------------------------------+
```

| Section | Contents |
|---|---|
| Status row | Actor name + slot name pill; visible only when runner is connected |
| "No runner connected" notice | Shown instead of status row when no runner is active |
| Filter field | Live text filter on key name |
| Key rows | Key name / type / value cell; value is editable when runner is connected, read-only otherwise |

**Key source**: Keys shown in this panel are populated by `AGISStateMachineRunner.Awake()`
via `IAGISPersistentNodeType` scan — node types in slot graphs AND MonoBehaviours on the actor
that implement that interface. Values are read/written via `AGISActorState` typed helpers.

**Implementation note**: `AGISBlackboardPanel.cs` — polls `runner.GetComponent<AGISActorState>()` each frame when runner is connected (not via `Instance.Actor` — `AGISActorState` is a MonoBehaviour component on the runner's GameObject). Uses `AGISParamTable` typed accessors. Writes via `actorState.Set(key, value)`.

---

## 7. STATUS BAR

**Location**: Full-width strip at the very bottom of the window.

```
[ ✓ No issues ]  |  9 nodes  14 edges  |  Assets/NPC_Test/PatrolGraph.asset  |  Saved  |  [● LIVE — NPC_Test / Patrol]
```

| Segment | Content | Color |
|---|---|---|
| Validation | "✓ No issues" or "⊗ N errors, M warnings" | Green / Red |
| Stats | "N nodes, M edges" | Grey |
| Asset path | Content library key or `persistentDataPath`-relative path | Grey |
| Save state | "Saved" or "Unsaved changes ●" | Grey / Orange |
| Live indicator | "● LIVE — [Actor Name] / [Slot Name]" (visible when runner is connected) | Green |

---

## 8. SEARCH WINDOWS (floating overlays)

### 8A. Node Type Search Window

**Trigger**: Spacebar, right-click on canvas > Add Node, or [+ Add Node] toolbar button.

```
+--[ADD NODE]------------------------------------+
|  [Search: ________________]                    |
|  ─────────────────────────────────────────     |
|  NPC / States                                  |
|    ● NPC Follow Target       npc.follow_target |
|    ● NPC Idle                npc.idle          |
|    ● NPC Wander              npc.wander        |
|    ● NPC Move To Waypoint    npc.move_to_w...  |
|  NPC / Conditions (n/a here)                   |
|  System                                        |
|    ⧉ Grouped                 Grouped           |
|    ⫼ Parallel                Parallel          |
|    ⬡ Any State               agis.any_state    |
|  Dialogue                                      |
|    💬 Dialogue               agis.dialogue     |
+------------------------------------------------+
```

| Element | Detail |
|---|---|
| Search field | Fuzzy match against `DisplayName` and `TypeId` |
| Grouped list | Items grouped by namespace prefix (npc., agis., custom.) |
| Row | Kind icon + DisplayName (large) + TypeId (small, right-aligned) |
| Selection | Click → creates node at cursor position; closes window |

Results come from `AGISNodeTypeRegistry.AllTypes` (populated via `RegisterAllFromAssemblies`).

---

### 8B. Condition Type Search Window

**Trigger**: [Change Type] button inside the Condition Expression tree editor.

```
+--[SELECT CONDITION]----------------------------+
|  [Search: ________________]                    |
|  ─────────────────────────────────────────     |
|  NPC                                           |
|    npc.has_reached_destination                 |
|    npc.is_moving                               |
|    npc.detects_object                          |
|    npc.detection_meter_exceeds                 |
|    npc.actor_state_bool                        |
|    npc.blackboard_bool                         |
|  AGIS                                          |
|    agis.node_complete                          |
|    agis.dialogue_option                        |
|    agis.has_dialogue_choice                    |
+------------------------------------------------+
```

Same UX as the node search window, but populated from `AGISConditionTypeRegistry.AllTypes`.
Results show: TypeId (primary) + DisplayName (secondary).

---

## 9. DEBUG OVERLAY

Active when the [Debug] toggle is on and the editor is connected to an `AGISStateMachineRunner` in the scene. Since the editor runs at runtime in a build, the simulation is always active — there is no editor mode distinction to switch into.

### Canvas overlays
| Effect | Trigger | Visual |
|---|---|---|
| Active node glow | `CurrentNodeId` matches node | Pulsing green border glow (CSS animation) |
| Last fired edge flash | Edge was just used for transition | Brief blue flash that fades over 0.5s |
| Paused state | Runner is paused (simulation paused) | Overlay dims entire canvas slightly |

### Live Panel (appended to Status Bar area when runner is connected)
```
● LIVE  |  Actor: NPC_Test  |  Slot: Patrol  |  Active: NPC Follow Target  |  Time in state: 3.2s
```

| Field | Source |
|---|---|
| Actor | `AGISStateMachineRunner.gameObject.name` |
| Slot | `AGISStateMachineSlot.slotName` |
| Active node | `CurrentNodeId` resolved to `DisplayName` |
| Time in state | Stopwatch (float accumulator) reset on each transition by the editor MonoBehaviour |

Polling frequency: every frame via `Update()` on the `AGISGraphEditorWindow` MonoBehaviour.

---

## 10. CONTEXT MENUS

### Right-click on empty canvas
```
Add Node...           (opens Node Search Window)
Paste                 (if clipboard has nodes)
Frame All             (zoom to fit)
Select All            (Ctrl+A)
```

### Right-click on a node
```
Set as Entry Node
Duplicate
Copy
Delete
---
Open Sub-Graph        (Grouped nodes only)
Edit Grouped Asset    (Grouped nodes only)
---
Frame Selection
```

### Right-click on an edge
```
Select Edge
Delete
---
Set Condition to True
Set Condition to False
```

---

## 11. KEYBOARD SHORTCUTS

| Key | Action |
|---|---|
| Space | Open Node Search Window at cursor |
| Delete / Backspace | Delete selected nodes / edges |
| Ctrl+Z | Undo |
| Ctrl+Y / Ctrl+Shift+Z | Redo |
| Ctrl+A | Select all |
| Ctrl+C | Copy selected nodes |
| Ctrl+V | Paste |
| Ctrl+D | Duplicate selected nodes |
| Ctrl+S | Save asset |
| F | Frame All (zoom to fit all nodes) |
| Shift+F | Frame Selected (zoom to fit selected nodes) |
| M | Toggle minimap |
| Escape | Deselect all; close floating windows |
| Enter | Confirm search window selection |

---

## 12. FILE STRUCTURE (implementation)

**Important**: Do NOT place runtime editor code in any folder named `Editor/` — Unity strips those from builds.
The existing `NPC/Editor/` folder contains dev-only scene builder tools (e.g. `NPCTestSceneBuilder.cs`) and is intentionally editor-only. The runtime graph editor lives in `RuntimeEditor/` instead.

```
Assets/Scripts/AX State Machine/
  RuntimeEditor/                           <- Runtime graph editor: MonoBehaviours + VisualElements (included in builds)
    AGISConditionSummary.cs               <- Runtime-safe helper: summarize condition tree to a one-liner string
    AGISEditorHistory.cs                  <- Runtime-safe helper: IEditorCommand + command stack (undo/redo)
    AGISGraphEditorWindow.cs              <- MonoBehaviour host; UIDocument wiring; open/close toggle
    AGISGraphCanvas.cs                    <- Custom VisualElement canvas; pan/zoom/grid; layer stack
    AGISMinimapElement.cs                 <- Custom VisualElement minimap; viewport indicator
    AGISRightPanel.cs                     <- Right panel; 5-tab scaffold; selection listener
    AGISStatusBar.cs                      <- Bottom status bar; error/warning counts; transient messages
    Nodes/
      AGISNodeCardElement.cs              <- Base node card; param rows; ports; drag; collapse
      AGISParamFieldDrawer.cs             <- Reusable: renders one AGISParamSpec input control
    Edges/
      AGISEdgeElement.cs                  <- Bezier edge; label pills; priority badge; dangling stub
    Panels/
      AGISGraphPropertiesPanel.cs         <- Right panel Tab: Graph (save/load/stats)
      AGISNodeInspectorPanel.cs           <- Right panel Tab: Node (type info; params; delete)
      AGISEdgeInspectorPanel.cs           <- Right panel Tab: Edge (condition tree editor)
      AGISBlackboardPanel.cs              <- Right panel Tab: Blackboard (live AGISActorState viewer)
      AGISConditionTreeView.cs            <- Reusable: recursive AGISConditionExprDef builder
    Search/
      AGISNodeSearchWindow.cs             <- Node type fuzzy search popup
      AGISConditionSearchWindow.cs        <- Condition type fuzzy search popup
    USS/
      AGISEditor.uss                      <- Single master stylesheet (all custom properties here)
```

---

## 13. DATA FLOW SUMMARY

```
[AGISStateMachineRunner]  (live, running)
        |
        | clone slot graph on open (serialize → deserialize)
        v
[_workingGraph: AGISStateMachineGraph]  ← editor always mutates this copy
        |
        | builds VisualElements
        v
[AGISGraphCanvas]  (UIToolkit VisualElement)
  |   |   |
  |   |   +──[AGISNodeCardElement] ── drag / param edit ──→ IEditorCommand → AGISEditorHistory
  |   +──────[AGISEdgeElement]    ── edge create/connect ──→ IEditorCommand → AGISEditorHistory
  +──────────[AGISMinimapElement]
        |
        | selection events (EditorSelectionChangedEvent)
        v
[AGISRightPanel]
  |   +── GraphPropertiesPanel ── Save: AGISGraphSerializer.ToJson()
  |                                      → AGISContentLibrary.ImportGraph()
  |                                      → runner.ApplyGraphToRunner()
  |   +── NodeInspectorPanel   ── param field edit → IEditorCommand → history
  |   +── EdgeInspectorPanel   ── condition tree edit → IEditorCommand → history
  +── BlackboardPanel          ── live read/write AGISActorState (when runner is connected)
        |
        | on every mutation
        v
[AGISGraphValidator.ValidateGraph(_workingGraph)]
        |
        v
[node cards / edge elements] ── add/remove .node--error / .node--warning CSS classes
```

---

## 14. DESIGN CONSTRAINTS & RULES

1. **Never call `AGISTransitionEdgeDef` directly for dialogue edges** — always go through `AGISDialogueEdgeSync`.
2. **`visual.position` must be written back** on every node drag end (not every frame).
3. **`visual.collapsed` must be written back** on every collapse/expand toggle.
4. **Every mutation must be pushed to `AGISEditorHistory`** as an `IEditorCommand` before being applied.
5. **Save is explicit** — mutations go to `_workingGraph` only; `Apply` to the live runner only happens when the user presses Save.
6. **AnyState node**: exactly one per graph, always present, never deletable, never has incoming edges.
7. **Parallel node**: exactly one outgoing edge enforced by the editor (grey out the OUT port after first edge).
8. **Dangling edges** (`toNodeId == Empty`): must be rendered, not hidden — they are intentional for dialogue choices.
9. **Condition `null`** on an edge evaluates as FALSE at runtime. The editor should default new edges to `ConstBool(false)` and show a warning prompt to configure the condition.
10. **Param reset** ([R] button): removes the key from `AGISParamTable`, which causes the runtime to use the schema default. This is different from setting the value to the default explicitly.
