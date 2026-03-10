# AGIS Graph Editor — UI Component Inventory
# Purpose: Designer reference. Lists every screen region, panel, and individual
#          control (button, dropdown, toggle, text field, etc.) with its purpose,
#          states, and design notes.
# Keep in sync with EDITOR_DESIGN.md (layout spec) and EDITOR_BUILD_PLAN.md (implementation).

---

## How to read this document

Each section describes one **region** of the UI. Inside each region, components are
listed in a table with columns:

| Column | Meaning |
|---|---|
| Component | Name / label of the element |
| Type | Control type (Button, Toggle, Dropdown, TextField, Label, etc.) |
| States | Visual states the control can be in |
| Purpose | What it does when interacted with |

**Control type glossary**:
- **Button** — click to trigger a one-time action
- **Toggle** — two states: on / off (checkbox or pill)
- **Dropdown** — click to open a list, select one item
- **TextField** — single-line text input
- **IntField** — integer number input (with up/down arrows)
- **FloatField** — decimal number input
- **Label** — read-only text
- **IconButton** — button that is only an icon (no label)
- **Pill** — a styled badge/tag; may be clickable
- **Scrollable List** — a scrollable container of repeated rows
- **Tree** — collapsible nested list
- **Canvas** — infinite pan/zoom drawing surface
- **Overlay** — element drawn on top of something else

---

## 1. OVERALL WINDOW STRUCTURE

The editor is a full-screen (or floating) overlay with 5 fixed regions stacked vertically,
plus a right panel docked to the right side.

```
+=========================================================+
|  TAB BAR                                                |  row 1
+=========================================================+
|  TOOLBAR                                                |  row 2
+=========================================================+
|                                              |          |
|   GRAPH CANVAS (infinite)                   |  RIGHT   |  row 3
|                                              |  PANEL   |
|       [BREADCRUMB — overlaid top-left]       |  320px   |
|       [MINIMAP — overlaid bottom-right]      |          |
|                                              |          |
+=========================================================+
|  STATUS BAR                                             |  row 4
+=========================================================+
```

All regions except the canvas have a fixed height (approx):
- Tab bar: 36px
- Toolbar: 40px
- Status bar: 24px
- Right panel: full canvas height, 320px wide

---

## 2. TAB BAR

One tab per open graph. Multiple graphs can be open simultaneously.

| Component | Type | States | Purpose |
|---|---|---|---|
| Graph tab | Button | Default / Active (accent bottom border) / Dirty (orange dot) | Switch the canvas to show this graph |
| Dirty dot | Indicator | Visible when unsaved changes exist | Warns user there are unsaved changes |
| [x] close tab | IconButton | Default / Hover (red) | Close this graph tab; prompts save if dirty |
| [+] new tab | IconButton | Default / Hover | Open the in-game graph picker overlay (lists graphs from AGISContentLibrary + Resources; no OS file browser) |

**Tab label**: graph name (from `AGISStateMachineGraph.graphName` or content library key), e.g. `PatrolGraph`.

**Design notes**:
- Active tab: white text, 2px accent-color underline
- Inactive tab: grey text, no underline
- Dirty state: small filled orange circle (6px) to the left of the graph name
- Tabs are draggable to reorder

---

## 3. TOOLBAR

Full-width strip with three groups: Left | Center | Right.

### 3A. Left Group

| Component | Type | States | Purpose |
|---|---|---|---|
| Slot dropdown | Dropdown | Enabled when runner connected / Disabled (single slot or no runner) | Select which AGISStateMachineSlot to view and edit |
| [Save] | Button | Default / Disabled (if no changes) | `AGISGraphSerializer.ToJson()` → write to `Application.persistentDataPath` → `runner.ApplyGraphToRunner()` if a runner is connected |
| [Undo] | IconButton | Default / Disabled (if nothing to undo) | Undo last action |
| [Redo] | IconButton | Default / Disabled (if nothing to redo) | Redo last undone action |
| [Validate] | Button | Default | Run `AGISGraphValidator` and refresh all node/edge validation overlays |

### 3B. Center Group

| Component | Type | States | Purpose |
|---|---|---|---|
| [+ Add Node] | Button | Default | Open the Node Search Window at canvas center |
| [Auto-Layout] | Button | Default | Arrange all nodes automatically (left-to-right DAG order) |
| [Frame All] | Button | Default | Zoom and pan so all nodes are visible |
| [Frame Selected] | Button | Default / Disabled (if nothing selected) | Zoom and pan to fit selected nodes |

### 3C. Right Group

| Component | Type | States | Purpose |
|---|---|---|---|
| Zoom % | Label + Button | Always visible | Shows current zoom level (e.g. "85%"); click resets to 100% |
| [Snap] | Toggle | On / Off | Snap node positions to grid while dragging |
| [Grid] | Toggle | On / Off | Show or hide the background dot/line grid |
| [Minimap] | Toggle | On / Off | Show or hide the minimap in the canvas corner |
| [Debug] | Toggle | On / Off / Disabled (if no runner connected) | Enable live debug overlay showing active node/edge states |

---

## 4. BREADCRUMB BAR

Overlaid on the top-left of the graph canvas. Only visible when drilled into a sub-graph.

| Component | Type | States | Purpose |
|---|---|---|---|
| [<] back | IconButton | Default | Go up one level (pop the context stack) |
| Ancestor segment | Button | Default / Hover (underline) | Jump directly to that ancestor graph level |
| Current segment | Label | Non-interactive | Shows name of the current (deepest) graph level |
| ">" separator | Label | Non-interactive | Visual separator between breadcrumb segments |

**Example rendering**:
```
[<]  PatrolGraph  >  RoutedMovement  >  InternalReset
```

---

## 5. GRAPH CANVAS

The main interactive area. Infinite, pannable, and zoomable.

### 5A. Canvas Background

| Component | Type | States | Purpose |
|---|---|---|---|
| Grid | Canvas drawing | Visible / Hidden (toolbar toggle) | Visual reference for node placement |
| Rubber-band rect | Overlay | Appears on drag-on-empty | Multi-node selection rectangle |

**Grid design**: minor lines every 20px, major lines every 100px. Two style options: dots or lines (toolbar toggle).

### 5B. Node Card

Every state in the graph appears as a card. All kinds share the same base structure.

#### Node Card — Header (always visible, even when collapsed)

| Component | Type | States | Purpose |
|---|---|---|---|
| Kind icon | Icon | Per node kind (see icon table) | Identifies node kind at a glance |
| Color strip | Background area | Per kind color | Visual grouping |
| Display name | Label | Large, readable | Name of the node type |
| Entry star ★ | Indicator | Visible when this is the entry node | Shows which node the machine starts in |
| Error badge ⊗ | Pill | Visible when node has errors | Red; shows issue count; hover for details |
| Warning badge △ | Pill | Visible when node has warnings (no errors) | Yellow; shows issue count; hover for details |
| [v] collapse | IconButton | Expanded / Collapsed | Toggle show/hide the param body. Double-clicking the node header is also a valid collapse/expand gesture. |
| [x] delete | IconButton | Default / Disabled (Entry) / Hidden (AnyState) | Delete this node (and all its edges). AnyState [x] is not rendered (hidden, not disabled). Entry nodes ARE deletable. |

#### Node Card — Param Body (hidden when collapsed)

One row per parameter defined by the node type:

| Component | Type | States | Purpose |
|---|---|---|---|
| Param label | Label | Normal / Bold (if overridden from default) / Dimmed (if at default) | Parameter display name |
| (?) tooltip icon | IconButton | Visible if tooltip text exists | Hover to see the parameter's description |
| Bool param | Toggle | Checked / Unchecked | True/false parameter |
| Int param | IntField | Default / Invalid range (red border) | Integer number; respects min/max constraints |
| Float param | FloatField | Default / Invalid range (red border) | Decimal number; respects min/max constraints |
| String param | TextField | Default / Focused | Single-line text input |
| Vector2 param | 2x FloatField (X, Y) | Default | Two-component vector |
| Vector3 param | 3x FloatField (X, Y, Z) | Default | Three-component vector |
| Guid param | Label + IconButton | Read-only | Shows GUID string; [copy] icon to clipboard |
| [R] reset | IconButton | Visible (if overridden) / Hidden (if at default) | Reset this param to its schema default |
| Category header | Collapsible Label | Expanded / Collapsed | Groups params under a named collapsible section |
| [More...] | Button | Visible if >4 params | Expands to show all params (collapsed by default for long nodes) |

#### Node Card — Output Port

| Component | Type | States | Purpose |
|---|---|---|---|
| OUT port circle | Drag handle | Default / Hover (highlighted) / Dragging (pulse) | Drag from here to start drawing a new transition edge |

#### Node Card Variants

**AnyState node** — no input port, no param body, no [x] delete, no [v] collapse:
```
[⬡]  ANY STATE
o  OUT
```

**Entry node** — gold ★ in header; normal otherwise.

**Dialogue node** — extra section below params:

| Component | Type | States | Purpose |
|---|---|---|---|
| Mode label | Label | "Ended (0 choices)" / "N choices" | Shows current dialogue edge mode |
| [+ Add Choice] | Button | Default | Add a choice edge — calls `AGISDialogueEdgeSync.AddChoice(graph, node.nodeId, choiceKey)` |
| [- Remove Last] | Button | Default / Disabled (if 0 choices) | Remove last choice edge — calls `AGISDialogueEdgeSync.RemoveLastChoice(graph, node.nodeId, choiceKey)` |
| Edge list | Scrollable List | Read-only rows | Shows each outgoing edge and its connected target node |

**Grouped node** — extra section below params:

| Component | Type | States | Purpose |
|---|---|---|---|
| Asset dropdown | Dropdown | Default / Unassigned (red border) | Pick the grouped state asset this node references |
| [Open Sub-Graph] | Button | Default / Disabled (if no asset) | Drill into the grouped asset's internal graph |
| Exposed override fields | Same as param fields | Default | Override the exposed params defined by the grouped asset |

**Parallel node** — extra branch list section:

| Component | Type | States | Purpose |
|---|---|---|---|
| Branch rows | Scrollable List | Each row is a Dropdown | Pick a node type for each concurrent branch |
| [+ Add Branch] | Button | Default | Add a new branch |
| [- Remove Last] | Button | Default / Disabled (1 branch) | Remove the last branch |

#### Node Kind Color + Icon Table

| Kind | Header color | Icon |
|---|---|---|
| Normal | Steel blue | ● filled circle |
| AnyState | Dark crimson | ⬡ hexagon |
| Grouped | Teal | ⧉ nested squares |
| Parallel | Purple | ⫼ parallel bars |
| Entry (overlay) | — | ★ gold star (top-left overlay on any kind) |

### 5C. Transition Edges

#### Normal (connected) edge

| Component | Type | States | Purpose |
|---|---|---|---|
| Bezier curve | Canvas drawing | Default / Selected (thicker, brighter) / Hovered | The transition arrow from one node to another |
| Arrowhead | Canvas drawing | Matches curve state | Points toward the target node |
| Label pill | Pill (clickable) | Default / Selected / Warning (⚠) | Click to select edge and open Edge Inspector |
| Priority badge | Pill | Color by priority level (see below) | Shows transition priority number |
| Condition summary | Label inside pill | Default / Truncated with ellipsis | Abbreviated view of the condition expression |
| ⏱ cooldown icon | Icon inside pill | Visible if cooldown > 0 | Indicates this edge has a cooldown |
| ⛔ non-interruptible icon | Icon inside pill | Visible if interruptible = false | Indicates this edge cannot be interrupted |
| 🔒 scope icon | Icon inside pill | Visible if scope ≠ "Any" | Indicates this edge is scope-gated |

**Priority badge colors**:
| Priority | Badge color |
|---|---|
| 0 | Grey |
| 1–3 | Green |
| 4–9 | Yellow |
| 10–19 | Orange |
| 20+ | Red |

#### Dangling (unconnected) edge

| Component | Type | States | Purpose |
|---|---|---|---|
| Bezier curve stub | Canvas drawing | Orange/yellow tint | Visual indicator that this edge needs a target |
| Open circle ○ | Drag handle | Default / Hover / Dragging | Drag to connect to a target node |
| Label pill (warning) | Pill | ⚠ prefix | Same as normal pill but with warning prefix |

#### AnyState edge variant

Edges originating from an AnyState node have additional visual treatment:

| Component | Type | States | Purpose |
|---|---|---|---|
| Bezier curve | Canvas drawing | Dark crimson color (#8B1A1A) | Distinguishes global interrupt transitions from node-specific ones |
| Priority badge | Pill | Larger font size than normal edges | Emphasizes global interrupt priority |
| "GLOBAL" prefix | Label inside pill | Always visible on AnyState edges | Indicates this edge evaluates regardless of current active state |

These edges behave identically to normal edges in all other respects (selection, dangling state, condition summary pill, etc.).

#### Edge endpoint drag handles

When an edge is selected, both endpoints become draggable:

| Component | Type | States | Purpose |
|---|---|---|---|
| Source handle | Drag handle | Visible on selection | Drag to re-route the edge source to a different node |
| Target handle | Drag handle | Visible on selection | Drag to re-route the edge target to a different node |

### 5D. Minimap

Located in the bottom-right corner of the canvas. Toggled via toolbar.

| Component | Type | States | Purpose |
|---|---|---|---|
| Background | Panel | Always visible when toggled on | Dark semi-transparent backdrop |
| Node rectangles | Canvas drawing | Color per kind | Tiny colored rects showing all node positions |
| Viewport rect | Canvas drawing (white outline) | Moves as user pans | Shows which part of the graph is currently visible |
| Drag surface | Interactive area | Default / Dragging | Click or drag to pan the main canvas to that position |
| Resize handle | Drag handle | Bottom-left corner | Drag to resize the minimap panel |

---

## 6. RIGHT PANEL

320px fixed-width panel docked to the right. Collapsible.

| Component | Type | States | Purpose |
|---|---|---|---|
| [>] collapse toggle | IconButton | Expanded / Collapsed | Show or hide the panel |
| Tab bar | Row of Buttons | One per tab; active has accent underline | Switch between Node / Edge / Graph / Grouped / Blackboard tabs |

### 6A. Node Inspector Tab

Shown automatically when a node is selected on canvas.

| Component | Type | States | Purpose |
|---|---|---|---|
| Kind icon | Icon | Per kind | Visual identifier |
| Display name | Label (h2) | — | Node type's display name |
| TypeId | Label (monospace, small) | — | Internal type identifier string |
| GUID label | Label (monospace) | — | Unique node instance ID |
| [Copy GUID] | IconButton | Default | Copy GUID to clipboard |
| [★ Set as Entry] | Button | Default / Disabled (if already entry) | Mark this node as the graph's entry node |
| Param fields | (see Section 5B param body) | — | Same param field rows as on the node card |
| Persistent Keys section header | Label | Collapsible | "Auto-populated in AGISActorState:" |
| Persistent key rows | Scrollable List | Read-only | key name / type / default value for each declared persistent key |
| Validation section header | Label | — | "Validation" |
| Validation issue rows | Scrollable List | Visible if issues exist | Each row: severity icon + message |
| "No issues" label | Label | Visible if clean | Green checkmark + text |
| [Delete Node] | Button (destructive) | Default / Disabled (AnyState only) | Delete node + all connected edges; confirms if edges exist. Entry nodes ARE deletable (editor auto-assigns next node as entry). AnyState nodes cannot be deleted. |

**Dialogue extra section** (appears for `agis.dialogue` nodes):

| Component | Type | States | Purpose |
|---|---|---|---|
| Mode label | Label | — | "Ended (0 choices)" or "N choices" |
| [+ Add Choice] | Button | Default | Add a dialogue choice edge |
| [- Remove Last] | Button | Default / Disabled | Remove last choice edge |
| Outgoing edge list | Scrollable List | Read-only | Each row: edge label → target node name or "unconnected" |

**Grouped extra section** (appears for Grouped kind nodes):

| Component | Type | States | Purpose |
|---|---|---|---|
| Asset dropdown | Dropdown | Default / Unassigned | Pick the AGISGroupedStateAsset |
| [Open Sub-Graph] | Button | Default / Disabled | Drill into the asset's internal graph |
| Exposed overrides | Param field rows | Same as standard params | Override values for the asset's exposed params |
| Scope list | Scrollable List | Read-only (edit in Grouped tab) | Shows scope IDs defined by the asset |

### 6B. Edge Inspector Tab

Shown automatically when an edge is selected on canvas.

| Component | Type | States | Purpose |
|---|---|---|---|
| "Transition" heading | Label | — | Section title |
| From node label | Label | — | Display name of the source node |
| → arrow | Label | — | Visual arrow |
| To node label | Label | — | Display name of target node, or "unconnected" |
| GUID label | Label (monospace) | — | Edge instance ID |
| [Copy GUID] | IconButton | — | Copy GUID to clipboard |
| Priority | IntField | Default / Focused | Transition priority (higher fires first) |
| Interruptible | Toggle | Checked / Unchecked | Can this transition fire while target is active |
| Cooldown (sec) | FloatField | Default / Focused | Minimum seconds between fires |
| Scope ID | TextField | Default / "Any" (default) | Scope gating string |
| [Delete Edge] | Button (destructive) | Default | Remove this edge |

**Condition Expression Tree** (the main content of this tab):

The tree is a recursive structure. Each node type in the tree has its own controls:

**Tree root controls**:
| Component | Type | States | Purpose |
|---|---|---|---|
| Root type selector | Row of Buttons | [True] [False] [AND] [OR] [LEAF] | Replace the entire root expression |

**AND node**:
| Component | Type | States | Purpose |
|---|---|---|---|
| "AND" label | Pill (blue left border) | — | Identifies this as an AND combinator |
| [x] remove | IconButton | — | Remove this node (promotes children up or collapses) |
| [+ Add Child] | Button | — | Add a new LEAF child to this AND |
| [Wrap in NOT] | Button | — | Wrap this entire AND inside a NOT |
| [Replace: OR] | Button | — | Replace this AND with an OR (keeping children) |
| Child slots | Tree children | Indented below | Recursive condition nodes |

**OR node**: identical to AND but with orange left border and [Replace: AND] button.

**NOT node**:
| Component | Type | States | Purpose |
|---|---|---|---|
| "NOT" label | Pill (red left border) | — | Identifies this as a NOT inverter |
| [x] remove | IconButton | — | Remove this NOT (promotes child up) |
| Child slot | Single tree child | Indented | The single child expression |

**LEAF node**:
| Component | Type | States | Purpose |
|---|---|---|---|
| "LEAF" label | Pill (condition category color) | — | Identifies this as a condition check |
| [x] remove | IconButton | — | Remove this leaf |
| Type label | Label (TypeId) | — | Current condition type |
| [Change Type] | Button | Default | Open the Condition Type Search Window to pick a different condition |
| [Wrap in NOT] | Button | — | Wrap this leaf in a NOT |
| [Wrap in AND] | Button | — | Wrap this leaf in a new AND |
| [Wrap in OR] | Button | — | Wrap this leaf in a new OR |
| Param fields | Same as node param fields | — | Parameters specific to this condition type |

**CONST TRUE**:
| Component | Type | States | Purpose |
|---|---|---|---|
| [✓ Always True] | Pill (green) | — | Label; shows edge always fires |
| [x] remove | IconButton | — | Remove |
| [Change to: False / AND / OR / LEAF] | Row of Buttons | — | Replace this constant |

**CONST FALSE**: same as TRUE but grey pill and [Change to: True / AND / OR / LEAF].

### 6C. Graph Properties Tab

Shown when nothing is selected, or manually by clicking the "Graph" tab.

| Component | Type | States | Purpose |
|---|---|---|---|
| Asset name | Label (h2) | — | Graph name (`AGISStateMachineGraph.graphName` or content library key) |
| Asset path | Label (small) | — | Content library key or `persistentDataPath`-relative path |
| Graph GUID | Label (monospace) | — | Unique ID of the graph |
| [Copy GUID] | IconButton | — | Copy graph GUID |
| Node count | Label | — | "N nodes" |
| Edge count | Label | — | "M edges" |
| Entry Node dropdown | Dropdown | Default / Unset (red border) | Pick which node is the entry point |
| Validation report section | Collapsible section | Expanded / Collapsed | Shows all errors and warnings |
| [✓ No issues] | Label | Visible if clean | Green |
| Error rows | Scrollable List | Visible if errors exist | Each row: ⊗ icon + message + [Select] button |
| Warning rows | Scrollable List | Visible if warnings exist | Each row: △ icon + message + [Select] button |
| [Select] (per issue) | Button | Default | Pan canvas to the offending node or edge |
| Runner references section | Collapsible section | — | Shows which runner slots use this graph |
| Runner reference rows | Scrollable List | Read-only | "Actor name / Slot name" for each reference |
| [Export JSON] | Button | Default | Serialize graph to JSON and write to `Application.persistentDataPath`; shows a TextField for the filename before confirming |
| [Import JSON] | Button | Default | Shows a TextField for a `persistentDataPath`-relative filename; reads JSON and replaces the working graph |
| Auto-save toggle | Toggle | On / Off | Save automatically after every change (1s debounce) |

### 6D. Grouped Asset Inspector Tab

Only active when the user has drilled into a `AGISGroupedStateAsset`'s internal graph,
or when "Edit Grouped Asset" is selected from the node context menu.

| Component | Type | States | Purpose |
|---|---|---|---|
| Asset display name | TextField | Editable | `AGISGroupedStateAsset` display name |
| Asset GUID | Label (monospace) | Read-only | Unique ID of the grouped asset |
| [Copy GUID] | IconButton | Default | Copy GUID |
| Exposed Params section header | Label | Collapsible | "Exposed Parameters" |
| [+ Add Param] | Button | Default | Add a new `AGISExposedParamDef` |
| Param entry rows | Expandable List rows | Default / Expanded | publicKey / type / defaultValue / displayName / tooltip / hasMin / hasMax / min / max / category — one row per param |
| [- Remove] per row | Button (destructive) | Default | Remove that exposed param |
| Bindings section header | Label | Collapsible | "Bindings" |
| Binding rows | List | Default | Each binding: exposed param → list of internal targets (node + param key) |
| [+ Add Target] per binding | Button | Default | Add a new AGISParamTarget |
| [- Remove] per target | Button (destructive) | Default | Remove a target |
| Scopes section header | Label | Collapsible | "Scopes" |
| [+ Add Scope] | Button | Default | Add a new `AGISInternalScopeDef` |
| Scope rows | Expandable List rows | Default | scopeId (editable) / displayName / node multi-select |
| [- Remove Scope] per row | Button (destructive) | Default | Remove scope |

---

### 6E. Blackboard Tab

Shows the live `AGISActorState` key-value store of the connected actor.

| Component | Type | States | Purpose |
|---|---|---|---|
| Filter field | TextField | Default / Focused | Filter rows by key name |
| [Clear filter] | IconButton | Visible if filter is non-empty | Reset filter |
| Key-value rows | Scrollable List | Normal / Changed (highlight flash) | Each row: key name / type / value |
| Value cell | Editable field per type | Editable (when runner connected) / Read-only (no runner) | Live-read/write actor state values |
| "No runner connected" notice | Label | Visible when no runner is connected | Informs that editing requires a connected runner |

---

## 7. STATUS BAR

Full-width strip at the very bottom of the editor.

| Component | Type | States | Purpose |
|---|---|---|---|
| Validation indicator | Label + Icon | ✓ (green) / ⊗ N errors (red) / △ M warnings (yellow) | Quick health check; click opens Graph tab validation report |
| Stats | Label | — | "N nodes  M edges" |
| Asset path | Label | — | Current graph's project path |
| Save state | Label | "Saved" (grey) / "Unsaved changes ●" (orange) | Dirty state reminder |
| Transient message | Label | Fades out after 2s | One-line feedback: "Saved", "Undo: Move Nodes", etc. |
| Live indicator | Pill (green) | Visible when runner is connected | "● LIVE — Actor / Slot / Active Node / 3.2s" |

---

## 8. NODE TYPE SEARCH WINDOW

Floating popup. Opens centered on cursor or at canvas center.

| Component | Type | States | Purpose |
|---|---|---|---|
| Search field | TextField | Auto-focused on open | Filter node types by name or TypeId |
| Group headers | Label | Non-interactive | "NPC / States", "System", "Dialogue", etc. |
| Node type rows | Selectable List rows | Default / Hovered / Selected | Each row: kind icon + DisplayName + TypeId |
| [Escape] / click-outside | — | — | Close without selecting |
| [Enter] / click row | — | — | Create node of that type at cursor; close window |

---

## 9. CONDITION TYPE SEARCH WINDOW

Floating popup. Opens when [Change Type] is clicked in the Edge Inspector.

| Component | Type | States | Purpose |
|---|---|---|---|
| Search field | TextField | Auto-focused on open | Filter condition types |
| Group headers | Label | Non-interactive | "NPC", "AGIS", etc. |
| Condition type rows | Selectable List rows | Default / Hovered / Selected | Each row: TypeId (primary) + DisplayName (secondary) |
| [Escape] / click-outside | — | — | Close without selecting |
| [Enter] / click row | — | — | Replace the LEAF's condition type; reset params to defaults |

---

## 10. CONTEXT MENUS

Right-click context menus. All implemented as dropdown menus (UIToolkit `DropdownMenu`).

### On empty canvas

| Menu item | Action |
|---|---|
| Add Node... | Open Node Search Window at cursor position |
| Paste | Paste copied nodes (disabled if clipboard empty) |
| Frame All | Zoom to fit all nodes |
| Select All | Select every node on canvas |

### On a node

| Menu item | Action |
|---|---|
| Set as Entry Node | Make this node the graph entry point |
| Duplicate | Clone node with new GUID, offset by (+20, +20) |
| Copy | Copy to clipboard as JSON |
| Delete | Delete node + all connected edges |
| ─── | Separator |
| Open Sub-Graph | Drill into grouped asset (Grouped nodes only) |
| Edit Grouped Asset | Switch right panel to Grouped tab and display asset inspector (Grouped nodes only) |
| ─── | Separator |
| Frame Selection | Pan/zoom canvas to this node |

### On an edge

| Menu item | Action |
|---|---|
| Select Edge | Select edge and open Edge Inspector |
| Delete | Remove edge |
| ─── | Separator |
| Set Condition to True | Replace condition with ConstBool(true) |
| Set Condition to False | Replace condition with ConstBool(false) |

---

## 11. DEBUG OVERLAY

Additional elements active when [Debug] toggle is on and a runner is connected.

| Component | Type | States | Purpose |
|---|---|---|---|
| Active node glow | CSS animation overlay on node card | Pulsing green border | Shows which node is currently active |
| Fired edge flash | Timed overlay on edge | Brief blue flash fading over 0.5s | Shows which transition just fired |
| Canvas dim | Full-canvas semi-transparent overlay | Visible when runner is paused (simulation paused) | Indicates frozen simulation |
| IsComplete badge | Small pill on node card header | Visible when debug on + node implements IAGISNodeSignal | "✓ Complete" (green) or "… Running" (grey) |
| Live bar (in status area) | Label strip | Green; visible when runner connected | "● LIVE  Actor: NPC_Test  Slot: Patrol  Active: Follow Target  3.2s" |

---

## 12. COMPONENT STATE SUMMARY

This table lists every recurring state a component can be in, and its visual treatment.

| State | Visual treatment |
|---|---|
| Default | Normal colors from design palette |
| Hovered | Slight brightness increase or accent color hint |
| Focused / Active | Accent border (e.g. 1–2px blue outline) |
| Selected | Stronger accent border, slightly elevated background |
| Disabled | 50% opacity; no pointer events |
| Dirty (unsaved) | Orange dot or orange text |
| Error | Red border or red background tint |
| Warning | Yellow border or yellow background tint |
| Valid / Clean | Green indicator or green text |
| Live / Playing | Pulsing green glow or green pill |
| Dragging | Cursor changes to grab; element moves with pointer |

---

## 13. IN-GAME GRAPH PICKER OVERLAY

Opened by the `[+]` new tab button or `[Import JSON]`. No OS file browser — lists graphs from
`AGISContentLibrary` and `Resources.LoadAll<AGISStateMachineGraphAsset>()`.

| Component | Type | States | Purpose |
|---|---|---|---|
| Overlay backdrop | Semi-transparent full-screen panel | Visible when open | Dims the editor while picker is open |
| Title label | Label | — | "Open Graph" or "New Graph" depending on trigger |
| Search / filter field | TextField | Auto-focused | Filter graph names by substring |
| [New Graph] button | Button | Default | Create a blank graph; shows a name input inline |
| Graph list rows | Selectable List | Default / Hovered / Selected | One row per graph in AGISContentLibrary + Resources.LoadAll<> |
| Source badge per row | Label (small) | — | "Library" or "Resources" — indicates graph origin |
| [Open] / [Select] | Button | Default / Disabled (nothing selected) | Open selected graph in a new tab |
| [Cancel] / [Escape] | Button / Key | Default | Close overlay without selecting |
| New graph name field | TextField | Visible on [New Graph] | User types the name for the new graph |
| [Confirm New] | Button | Default / Disabled (empty name) | Confirm graph creation, register in AGISContentLibrary, open tab |

---

## 14. POPUP / MODAL RULES

The editor uses no blocking modals. All secondary UI is either:
- A **floating popup** (search windows, context menus) — dismissed by Escape or click-outside
- A **collapsible section** inside the right panel

One exception: **destructive confirmation** (delete a node that has edges, revert unsaved changes).
These are displayed as an inline banner inside the right panel:
```
+-----------------------------------------------+
|  ⚠ This node has 3 connected edges.            |
|  [Confirm Delete]          [Cancel]            |
+-----------------------------------------------+
```

| Component | Type | States | Purpose |
|---|---|---|---|
| Warning icon + message | Label | — | Describes what will be lost |
| [Confirm Delete] | Button (destructive, red) | Default / Hover | Proceed with the action |
| [Cancel] | Button (secondary) | Default | Dismiss and do nothing |

---

## 15. KEYBOARD SHORTCUT REFERENCE (designer note)

Every shortcut should have a visible hint somewhere in the UI (tooltip or label).

| Shortcut | Action |
|---|---|
| Space | Open Node Search Window |
| F | Frame All (or Frame Selected if nodes selected) |
| Shift+F | Frame Selected |
| Delete / Backspace | Delete selected nodes or edges |
| Ctrl+Z | Undo |
| Ctrl+Y / Ctrl+Shift+Z | Redo |
| Ctrl+A | Select all nodes |
| Ctrl+C | Copy selected nodes |
| Ctrl+V | Paste |
| Ctrl+D | Duplicate selected |
| Ctrl+S | Save |
| M | Toggle minimap |
| Escape | Deselect all; close any floating popup |
| Enter | Confirm search window selection |
