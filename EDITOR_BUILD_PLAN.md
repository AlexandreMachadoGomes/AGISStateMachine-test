# AGIS ESM Runtime Graph Editor — Build Plan

**Technology**: UIToolkit Runtime (`UnityEngine.UIElements`) — works in shipped builds.
**Save pipeline**: `AGISGraphSerializer` (JsonUtility) → `AGISContentLibrary.ImportGraph()` → `runner.ApplyGraphToRunner()`.
**Registry source**: borrow `runner.NodeTypes` / `runner.ConditionTypes` (already public, already populated in Awake).

Each phase has checkboxes for implementation steps, a Notes field for findings during implementation,
and a Guidelines Added field for post-implementation annotations that feed back into later phases.

---

## Phase 0 — Pre-work: Infrastructure Gaps

These are minimal one-off changes to existing files before any editor code is written.

### Steps

- [ ] **0-A** `AGISStateMachineSlot.cs` — add one-line property:
  ```csharp
  public AGISGuid CurrentNodeId => instance?.CurrentNodeId ?? AGISGuid.Empty;
  ```
  Also expose `public AGISStateMachineInstance Instance => instance;` so the editor can read
  `instance.CurrentNodeId` (add `public AGISGuid CurrentNodeId` property to `AGISStateMachineInstance` if needed).
  Also expose `public AGISGuid LastTransitionEdgeId` on `AGISStateMachineSlot`
  (or `AGISStateMachineInstance`) so the debug overlay can highlight the edge that
  most recently fired (Phase 22-C).

- [ ] **0-B** Create `Assets/Scripts/AX State Machine/RuntimeEditor/AGISConditionSummary.cs`
  — pure runtime class (no `using UnityEditor`).
  Public API: `static string Summarize(AGISConditionExprDef expr, AGISConditionTypeRegistry registry)`.
  Returns a human-readable one-liner for edge label pills, e.g. `"DetectsObject AND NOT IsMoving"`.

- [ ] **0-C** Create `Assets/Scripts/AX State Machine/RuntimeEditor/AGISEditorHistory.cs`
  — custom command/undo stack (~100 lines).
  Interface: `IEditorCommand { void Do(); void Undo(); }`.
  `AGISEditorHistory`: `Push(cmd)`, `Undo()`, `Redo()`, `bool CanUndo`, `bool CanRedo`, `Clear()`.

- [ ] **0-E** **Slot selector UI**: `AGISStateMachineRunner` holds multiple slots. Phase 1-A wires to slot 0 by default.
  Add a slot-picker dropdown to the editor window (populated from `runner.Slots`, showing `slotName` per entry).
  Changing the selected slot re-clones `_workingGraph` from that slot and rebuilds the canvas.

- [ ] **0-F** **In-game graph picker**: The `[+]` tab button and `[Import JSON]` cannot use an OS file browser in a
  runtime build. Define the picker as an in-editor overlay list populated from two sources:
  1. Graphs already registered in `AGISContentLibrary` (by key)
  2. Graphs loadable via `Resources.LoadAll<AGISStateMachineGraphAsset>()` (if graphs are placed under `Resources/`)
  The picker shows graph names in a scrollable list with a filter field. Selecting one opens it in a new tab.

- [ ] **0-G** **New Graph creation**: Add a `[New Graph]` button to the `[+]` picker overlay (Phase 0-F).
  Creates a blank `AGISStateMachineGraph` (one AnyState node, no entry set), registers it in `AGISContentLibrary`
  under a user-supplied name, and opens it in a new tab.

- [ ] **0-H** **Dialogue `EnsureEndedEdge` at runtime**: `AGISStateMachineGraphAsset.OnValidate` normally calls
  `AGISDialogueEdgeSync.EnsureEndedEdge` — but `OnValidate` is an editor-only callback and does not fire at runtime.
  The editor must call `AGISDialogueEdgeSync.EnsureEndedEdge(graph, node.nodeId, choiceKey)` explicitly for every
  `agis.dialogue` node immediately after cloning the graph in `_workingGraph` (Phase 1-B). This ensures dialogue
  nodes always have a valid initial edge layout before any editing begins.
  The `choiceKey` argument is the node's `choice_key` param value (defaulting to
  `AGISDialogueConstants.DefaultChoiceKey` if not set).

- [ ] **0-D** Create `Assets/link.xml` — IL2CPP type preservation for reflection-based auto-registration:
  ```xml
  <linker>
    <assembly fullname="Assembly-CSharp" preserve="all"/>
  </linker>
  ```

### Notes
<!-- Fill in during implementation -->

### Guidelines Added
<!-- Lessons learned, decisions made, things to carry forward -->

---

## Phase 1 — Window Scaffold & Entry Point

Create the root MonoBehaviour that hosts the editor window and wires it to a runner.

### Steps

- [ ] **1-A** Create `Assets/Scripts/AX State Machine/RuntimeEditor/AGISGraphEditorWindow.cs`
  — MonoBehaviour (or UIDocument driver). Attach to a Canvas/UIDocument in the scene.
  Fields: `AGISStateMachineRunner targetRunner`, `int _activeSlotIndex` (set via slot-picker dropdown, Phase 0-E).

- [ ] **1-B** On `Start()`: grab `runner.NodeTypes` and `runner.ConditionTypes` and store as editor fields.
  Clone the current slot's graph (deep-copy via `AGISGraphClone.CloneGraph(slot.GetGraphDef())`) into a local
  `AGISStateMachineGraph _workingGraph`. Do NOT use serialize → deserialize for cloning.
  After cloning, iterate all nodes and call
  `AGISDialogueEdgeSync.EnsureEndedEdge(_workingGraph, node.nodeId, choiceKey)` for every
  node whose `typeId == "agis.dialogue"` (Phase 0-H). The editor always mutates `_workingGraph`; it never touches
  the live running graph until Save is pressed.

- [ ] **1-C** Bootstrap the UIDocument: load the root UXML (`AGISEditorRoot.uxml`). Add stylesheet (`AGISEditor.uss`).

- [ ] **1-D** Wire the right panel tab bar buttons (Node | Edge | Graph | Grouped | Blackboard) to show/hide panels.
  Tab state persists across open/close in `PlayerPrefs` under key `"agis.editor.last_tab"`.

- [ ] **1-E** Add a togglable editor open/close button in the scene (or trigger via a keyboard shortcut, e.g. F12).

### Notes

### Guidelines Added

---

## Phase 2 — Graph Canvas Core (Pan / Zoom / Grid)

Build the scrollable, zoomable canvas that all node cards and edges live on.

### Steps

- [ ] **2-A** Create `AGISGraphCanvas.cs` — a `VisualElement` subclass registered in USS as `.agis-canvas`.
  Internal layers (bottom to top): `_gridLayer`, `_edgeLayer`, `_nodeLayer`, `_overlayLayer`.

- [ ] **2-B** Implement pan: register `PointerDownEvent`, `PointerMoveEvent`, `PointerUpEvent` on the canvas background.
  Middle-mouse or Alt+left-drag pans. Translate `_gridLayer`, `_edgeLayer`, `_nodeLayer` together.

- [ ] **2-C** Implement zoom: register `WheelEvent`. Scale around pointer position (transform pivot trick).
  Clamp zoom to `[0.2, 2.0]`. Store current transform as `Matrix4x4 _viewTransform`.

- [ ] **2-D** Grid rendering: override `generateVisualContent` on `_gridLayer` using `MeshGenerationContext`.
  Draw minor lines every 20px, major lines every 100px (colors from USS custom properties).

- [ ] **2-E** `Frame All` — calculate bounding rect of all nodes, set pan+zoom so they fit in the viewport.
  Bind to keyboard shortcut `F` and a toolbar button.

- [ ] **2-F** `Frame Selected` — same but only selected nodes. Shortcut: `Shift+F`.

### Notes

### Guidelines Added

---

## Phase 3 — Node Cards: Structure & Drag

Render one visual card per `AGISNodeInstanceDef`. Support selection and drag.

### Steps

- [ ] **3-A** Create `AGISNodeCardElement.cs` — `VisualElement` subclass.
  Anatomy: `.node-header` (color strip + kind badge + display name) | `.node-body` (param fields) | `.node-footer` (port anchors).
  Position from `nodeDef.visual.position`; write back on drag.

- [ ] **3-B** Node kind color strip (from USS variables, so they're easy to retheme).
  Colors match EDITOR_DESIGN.md Section 4A-v exactly:
  - Normal: `--color-normal` (#3A7BD5 steel blue)
  - AnyState: `--color-any` (#8B1A1A dark crimson)
  - Grouped: `--color-grouped` (#1A8B7A teal)
  - Parallel: `--color-parallel` (#6B3FA0 purple)
  - Entry indicator overlay: `--color-entry` (gold) — overlay only, not a separate node kind
  - Exit node (grouped internal graph only): `--color-exit` (#8B1A1A, same as AnyState or distinct red)

- [ ] **3-C** Drag: `PointerDownEvent` on the header starts drag. `PointerMoveEvent` on canvas moves all selected cards.
  `PointerUpEvent` commits positions to `nodeDef.visual.position` and pushes a `MoveNodesCommand` to `AGISEditorHistory`.

- [ ] **3-D** Selection: click selects one node (clears others). Shift+click toggles. Rubber-band (drag on empty canvas) selects rect.
  Selected cards get `.node--selected` USS class (border highlight).

- [ ] **3-E** Collapse/expand: double-click header toggles `nodeDef.visual.collapsed`. Collapsed cards show only the header.

- [ ] **3-F** Ports: small circle anchors on left (input) and right (output) of each card. Ports are `VisualElement` with
  `.node-port--input` / `.node-port--output`. Their world-space centers are queried when drawing edges.

### Notes

### Guidelines Added

---

## Phase 4 — Node Cards: Param Field Drawer

Draw the correct input control for each `AGISParamSpec` in the node's schema.

### Steps

- [ ] **4-A** Create `AGISParamFieldDrawer.cs` — factory: given an `AGISParamSpec` and the current `AGISValue`,
  returns a `VisualElement` with a label and an appropriate control.

- [ ] **4-B** Type → control mapping:
  - `Bool` → `Toggle`
  - `Int` → `IntegerField`
  - `Float` → `FloatField` (respect `hasMin`/`hasMax`)
  - `String` → `TextField`
  - `Vector2` → two `FloatField` inline
  - `Vector3` → three `FloatField` inline
  - `Guid` → read-only `Label` with a [Copy] `Button` beside it (no editing — GUIDs are system-assigned; per EDITOR_DESIGN.md)

- [ ] **4-C** Each control registers `ChangeEvent<T>`. On change: clone the node's `AGISParamTable`, set the new value,
  push `ChangeNodeParamCommand` to history.

- [ ] **4-D** Collapsed cards: hide the body (`display: none`) but keep params in memory — no data loss on collapse.

- [ ] **4-E** Validation inline: after each param change, run `AGISGraphValidator.ValidateNode()` (if such a method exists)
  or a quick schema check (required fields, range). Show a red border + tooltip on invalid fields.

### Notes

### Guidelines Added

---

## Phase 5 — Node Cards: Type Variants

Handle Entry, Exit, Any, Grouped, and Parallel node cards specially.

### Steps

- [ ] **5-A** **Entry node**: The entry node is NOT a special kind — it is any normal node whose ID matches
  `graph.entryNodeId`. It shows a gold ★ overlay on its header. It CAN be deleted as long as at least one
  other node exists (the editor auto-assigns the next available node as entry, or prompts the user to pick one).
  Only `AnyState` is permanently undeletable.

- [ ] **5-B** **Exit node** (grouped internal graphs only): Inside a `AGISGroupedStateAsset`'s internal graph,
  an Exit node marks a terminal point that triggers a scope transition back to the parent graph. No output port.
  Header shows "EXIT" badge. Cannot be deleted (it is the exit anchor for the grouped asset's scope system).
  This node kind does NOT appear in top-level graphs.

- [ ] **5-C** **Any node**: dashed border. Tooltip explains it evaluates regardless of current state.

- [ ] **5-D** **Grouped node**: show a "Drill In" button in the footer. On click: push current graph context to the
  breadcrumb stack and load the grouped asset's `internalGraph` into the canvas (Phase 17).

- [ ] **5-E** **Parallel node**: show branch count badge. Each branch gets a sub-port visually.

- [ ] **5-F** **Dialogue node** (`agis.dialogue`): show a choice-count badge. Footer has +/- buttons that call
  `AGISDialogueEdgeSync.AddChoice(_workingGraph, node.nodeId, choiceKey)` /
  `RemoveLastChoice(_workingGraph, node.nodeId, choiceKey)`. NEVER manipulate dialogue edges directly.
  To populate the node card's edge list, use `AGISDialogueEdgeSync.FindEndedEdge(_workingGraph, node.nodeId)`
  (for the "Dialogue Ended" transition) and `AGISDialogueEdgeSync.FindChoiceEdges(_workingGraph, node.nodeId)`
  (for option edges). These are the canonical query helpers — do not iterate all edges and filter manually.

### Notes

### Guidelines Added

---

## Phase 6 — Edge Drawing (Static)

Render edges as bezier curves between node output and input ports.

### Steps

- [ ] **6-A** Create `AGISEdgeElement.cs` — `VisualElement` with `generateVisualContent` override.
  Reads `fromNodeId`, `toNodeId`, queries the port world positions from the node card registry, draws a cubic bezier.

- [ ] **6-B** Control points: output port tangent goes right (+X), input port tangent goes left (-X), magnitude = 80px.
  This gives smooth S-curves between nodes.

- [ ] **6-C** Edge color by condition state:
  - Default: `--color-edge` (mid-grey)
  - Selected: `--color-edge-selected` (white)
  - Invalid (dangling): `--color-edge-dangling` (orange dashed)

- [ ] **6-D** Priority badge: render a small pill at the midpoint of each edge showing `priority` value.
  Pill uses `.edge-priority-badge` USS class.

- [ ] **6-E** Condition summary pill: render a second pill below the priority badge showing
  `AGISConditionSummary.Summarize(edge.condition, registry)`. Truncate at 40 chars with ellipsis.

- [ ] **6-F** Dangling edges (`toNodeId == AGISGuid.Empty`): draw a short stub with an open arrow tip instead of a bezier.
  These are valid UX state — the user needs to drag-connect them.

- [ ] **6-G** When canvas pans/zooms or any node moves, mark all edges dirty and regenerate. Use `MarkDirtyRepaint()`.

### Notes

### Guidelines Added

---

## Phase 7 — Edge Interaction

Allow creating, deleting, and reconnecting edges by dragging.

### Steps

- [ ] **7-A** **Create edge**: hover an output port → cursor changes to crosshair. `PointerDownEvent` starts a drag-to-connect.
  A temporary `AGISEdgeDraftElement` follows the pointer. On `PointerUpEvent` over an input port: create the edge
  (`AddEdgeCommand` pushed to history). On release elsewhere: cancel.

- [ ] **7-B** **Reconnect edge**: clicking an existing edge selects it. Dragging either endpoint tears it off and enters
  drag-to-connect mode for that endpoint.

- [ ] **7-C** **Delete edge**: `Delete` key when edge selected. Confirm if it is the last edge leaving a node that has no
  other outgoing edges. `RemoveEdgeCommand` pushed to history.
  If the edge being deleted belongs to a dialogue node (i.e. source node typeId == "agis.dialogue"),
  route the deletion through `AGISDialogueEdgeSync` instead of direct graph mutation.
  Same guard applies to Phase 12-G (Delete edge button in Edge Inspector).

- [ ] **7-D** **Edge selection**: click on the bezier curve within ~8px selects the edge. Selection highlights the curve
  and shows the right panel's Edge tab (Phase 11).

- [ ] **7-E** **Connection validation**: disallow connecting output → same node's input (self-loop guard), disallow
  multiple edges from the same source port to the same target port (duplicate guard).

- [ ] **7-F** **Dialogue edge guard**: when dragging from or to a dialogue node, intercept and route through
  `AGISDialogueEdgeSync` instead of direct edge creation.

### Notes

### Guidelines Added

---

## Phase 8 — Node Search Window

Popup that lets the user pick a node type to add.

### Steps

- [ ] **8-A** Create `AGISNodeSearchWindow.cs` — `VisualElement` popup anchored to pointer position.
  Triggered by **Spacebar** or **double-clicking empty canvas**, or right-click → "Add Node".

- [ ] **8-B** Populate list from `runner.NodeTypes.AllTypes` (or the editor's cached registry).
  Group by namespace prefix: `npc.*`, `agis.*`, etc. Ungrouped entries at top.

- [ ] **8-C** `TextField` search box at top — fuzzy filters by `DisplayName` or `TypeId`. Auto-focused on open.

- [ ] **8-D** On selection: create a new `AGISNodeInstanceDef` with a fresh `AGISGuid`, default params, and
  `visual.position` = canvas-space pointer position. Push `AddNodeCommand` to history.

- [ ] **8-E** Close on `Escape`, on click outside the popup, or on selection.

- [ ] **8-F** Keyboard navigation: Up/Down arrow keys move selection; Enter confirms.

### Notes

### Guidelines Added

---

## Phase 9 — Right Panel Scaffold

Build the collapsible right panel with its five tabs (Node | Edge | Graph | Grouped | Blackboard).

### Steps

- [ ] **9-A** Create `AGISRightPanel.cs` — `VisualElement`. Width: 320px, collapsible via a toggle arrow button.
  Collapse state stored in `PlayerPrefs` under `"agis.editor.right_panel_open"`.

- [ ] **9-B** Tab bar: Node | Edge | Graph | Grouped | Blackboard. Each tab is a `Button` with `.tab-button` and `.tab-button--active` classes.
  Tab switching shows/hides the corresponding content `VisualElement`.

- [ ] **9-C** Each tab's content area is a `ScrollView` to handle overflow.

- [ ] **9-D** The panel listens to the canvas selection event (custom `EditorSelectionChangedEvent`). When a node is
  selected: switch to Node tab. When an edge is selected: switch to Edge tab. When nothing is selected: stay on Graph tab.

### Notes

### Guidelines Added

---

## Phase 10 — Right Panel Tab A: Graph Properties

Displays and edits top-level graph metadata.

### Steps

- [ ] **10-A** Graph name field (`TextField`) — edits `AGISStateMachineGraph.graphName` (add this field if missing).

- [ ] **10-B** Slot index indicator (read-only label) showing which runner slot is open.

- [ ] **10-C** Node count, edge count — read-only stat labels, updated on any graph mutation.

- [ ] **10-D** Save button — serializes `_workingGraph` via `AGISGraphSerializer.ToJson()`, calls
  `AGISContentLibrary.Instance.ImportGraph(dbId, json)` then `ApplyGraphToRunner(dbId, runner, slotIndex)`.

- [ ] **10-E** Revert button — discards `_workingGraph`, re-clones from the runner's live slot graph.
  Requires confirmation dialog if history stack is non-empty.

- [ ] **10-F** Export JSON button — shows an inline `TextField` for a filename, then writes to
  `Path.Combine(Application.persistentDataPath, filename + ".json")` via `System.IO.File.WriteAllText`.
  Note: `System.IO.File` is unavailable on WebGL; guard with `#if !UNITY_WEBGL` if WebGL support is needed.

- [ ] **10-G** Import JSON button — shows an inline `TextField` for a filename, reads from
  `Path.Combine(Application.persistentDataPath, filename + ".json")` via `System.IO.File.ReadAllText`,
  calls `AGISGraphSerializer.GraphFromJson()`, replaces `_workingGraph`. Same WebGL caveat applies.

### Notes

### Guidelines Added

---

## Phase 11 — Right Panel Tab B: Node Inspector

Shows selected node's type info and param fields.

### Steps

- [ ] **11-A** Display node `TypeId`, `DisplayName`, node kind badge.

- [ ] **11-B** Display node GUID (read-only, copyable).

- [ ] **11-C** "Is Entry" checkbox — marks node as graph entry point. Only one entry node allowed; show error if another
  already exists.

- [ ] **11-D** Param fields section — reuse `AGISParamFieldDrawer` (Phase 4) for all schema params.

- [ ] **11-E** Persistent params section (read-only at design time) — list keys declared by `IAGISPersistentNodeType`,
  show type and default value.

- [ ] **11-F** **Required components display**: `IAGISNodeComponentRequirements` EXISTS in the codebase.
  Node types that implement it declare which MonoBehaviour components the actor must have at runtime.
  Use `AGISActorComponentFixer.EnsureComponents(actor, nodeType)` to auto-add missing components.
  In the Node Inspector: show a read-only list of required component types declared by the selected
  node's type. If the connected runner's actor is missing any, show a yellow warning + [Fix] button
  that calls `EnsureComponents`.

- [ ] **11-G** Delete node button — pushes `RemoveNodeCommand` (removes node + all connected edges).
  Disabled for: AnyState nodes (permanently protected) and grouped-internal Exit nodes (anchor for scope system).
  Entry nodes ARE deletable — the editor auto-assigns the next available node as entry, or prompts the user.

### Notes

### Guidelines Added

---

## Phase 12 — Right Panel Tab C: Edge Inspector

Shows selected edge's transition properties and condition tree editor.

### Steps

- [ ] **12-A** Display from-node → to-node as labels with arrows.

- [ ] **12-B** Priority field (`IntegerField`) — edits `AGISTransitionEdgeDef.priority`.

- [ ] **12-C** Policy fields: cooldown (`FloatField`), interruptible (`Toggle`).

- [ ] **12-D** Condition tree editor — recursive `VisualElement` tree mirroring `AGISConditionExprDef`:
  - `And` / `Or` / `Not`: show operator label + children indented below, with Add Child / Remove buttons.
  - `Leaf`: dropdown for condition type (`TypeId`) + param fields for the condition's schema.
  - `ConstBool`: checkbox.
  - Root-level Add button inserts a default `ConstBool(false)` node.

- [ ] **12-E** Condition type dropdown — populated from `runner.ConditionTypes.AllTypes`. Changing type resets params to defaults.

- [ ] **12-F** Condition summary preview — live-updates as tree changes, shows what `AGISConditionSummary.Summarize()` produces.

- [ ] **12-G** Delete edge button — same as delete in Phase 7.

### Notes

### Guidelines Added

---

## Phase 13 — Right Panel Tab D: Blackboard Viewer

Read/write the live `AGISActorState` (runtime debug + design aid).

### Steps

- [ ] **13-A** List all keys in `runner`'s actor `AGISActorState`. Columns: Key | Type | Value.

- [ ] **13-B** Value cells are editable when a runner is connected (change `AGISActorState.Set(key, value)` live).
  Read-only when no runner is connected.

- [ ] **13-C** Filter text field — hides rows not matching the filter string.

- [ ] **13-D** Refresh rate: update every 0.25s via `schedule.Execute(...).Every(250)`.

- [ ] **13-E** Highlight rows whose values changed in the last refresh cycle (flash animation via USS transitions).

### Notes

### Guidelines Added

---

## Phase 14 — Save / Load Pipeline

Wire the complete round-trip from editing to running graph.

### Steps

- [ ] **14-A** Ensure `AGISContentLibrary` singleton exists in the scene when the editor opens.
  If not found: auto-create a `DontDestroyOnLoad` GameObject and add the component.

- [ ] **14-B** Assign a stable `_dbId` for the editor's working graph: `"agis.editor.slot{N}"` where N = slot index.

- [ ] **14-C** Save flow:
  1. Validate `_workingGraph` (run `AGISGraphValidator.Validate()`; block save on errors).
  2. `json = AGISGraphSerializer.ToJson(_workingGraph)`.
  3. `AGISContentLibrary.Instance.ImportGraph(_dbId, json)`.
  4. `AGISContentLibrary.Instance.ApplyGraphToRunner(_dbId, runner, slotIndex)`.
  5. Clear `AGISEditorHistory` (clean state after save).
  6. Show "Saved" status in the status bar for 2 seconds.

- [ ] **14-D** Auto-save option (toggle in Graph tab): triggers save after every command push, debounced 1s.

- [ ] **14-E** Dirty indicator: show asterisk `*` in the window title when `AGISEditorHistory.CanUndo` is true (unsaved changes).

### Notes

### Guidelines Added

---

## Phase 15 — Validation Overlay

Show graph errors and warnings without blocking the editor.

### Steps

- [ ] **15-A** After every graph mutation, run `AGISGraphValidator.Validate(_workingGraph)` async on next frame.
  Store results in a list `_validationResults`.

- [ ] **15-B** Nodes with errors: add `.node--error` USS class (red glow). Nodes with warnings: `.node--warning` (yellow glow).
  Edges with warnings (dangling, `Graph.EdgeToUnconnected`): already handled by dashed style in Phase 6.

- [ ] **15-C** Status bar shows error/warning counts (Phase 21). Click to open Validation panel.

- [ ] **15-D** Validation panel (collapsible list below the canvas or in a drawer): each result row shows
  icon | severity | message | "Go To" button (centers canvas on the offending node/edge).

- [ ] **15-E** Save is blocked when any `AGISValidationSeverity.Error` exists. Warnings allow save.

### Notes

### Guidelines Added

---

## Phase 16 — Undo / Redo Stack

Connect `AGISEditorHistory` to all mutating actions.

### Steps

- [ ] **16-A** All commands from Phases 3–15 that mutate `_workingGraph` must be `IEditorCommand` implementations.
  Review and confirm every mutation goes through `AGISEditorHistory.Push()`.

- [ ] **16-B** Keyboard: `Ctrl+Z` → `history.Undo()`, `Ctrl+Y` / `Ctrl+Shift+Z` → `history.Redo()`.

- [ ] **16-C** Toolbar buttons: Undo and Redo with icons. Disable when `!CanUndo` / `!CanRedo`.

- [ ] **16-D** After Undo/Redo: rebuild affected node cards and edges from `_workingGraph`. Use a full canvas rebuild
  (`RebuildCanvas()`) for simplicity; optimize to partial rebuild later if perf is needed.

- [ ] **16-E** History limit: cap at 100 commands. Oldest commands are dropped when limit is exceeded.

### Notes

### Guidelines Added

---

## Phase 17 — Breadcrumb & Grouped Node Drill-Down

Allow navigating into a Grouped node's internal graph.

### Steps

- [ ] **17-A** Create `AGISGraphContextStack` — stack of `(AGISStateMachineGraph graph, string label)`.
  Root entry = the slot's graph, label = runner name or graph name.

- [ ] **17-B** Breadcrumb bar above the canvas: renders each stack entry as a clickable label separated by `>`.
  Clicking a breadcrumb pops back to that context level.

- [ ] **17-C** Grouped node "Drill In" (Phase 5-D): push current graph + label onto stack. Load the grouped asset's
  `internalGraph` into the canvas. Update breadcrumb.

- [ ] **17-D** On breadcrumb pop: restore previous `_workingGraph` reference and rebuild canvas.

- [ ] **17-E** Edits inside a grouped graph mutate the grouped asset's `internalGraph` directly via the working
  context. **Note**: changes take effect in the live machine only after the next Save → `ApplyGraphToRunner` cycle
  triggers a re-compile and Enter; they do NOT propagate immediately.
  **Note**: grouped asset edits need their own serialization step (an `ImportGrouped` save path or equivalent)
  to persist the internal graph changes — the root graph Save pipeline covers the outer graph only.

- [ ] **17-F** Exposed param bindings for a Grouped node: show in Node Inspector tab when a Grouped node is selected.
  Let the user add/remove bindings (outer param → inner param name).

### Notes

### Guidelines Added

---

## Phase 18 — Minimap

Small overview panel showing the full graph at reduced scale.

### Steps

- [ ] **18-A** Create `AGISMinimapElement.cs` — fixed-size `VisualElement` (200 × 150px) in the bottom-right corner
  of the canvas. Toggle visibility with `M` key or a toolbar button.

- [ ] **18-B** Render node rectangles scaled to fit the full graph bounding box inside the minimap.
  Colors match node kind colors (Phase 3-B). No labels at this scale.

- [ ] **18-C** Viewport indicator: draw a white rectangle showing which portion of the graph is currently visible.

- [ ] **18-D** Click or drag inside the minimap pans the main canvas to that position.

- [ ] **18-E** Update every frame (or on graph change + pan/zoom). Use `schedule.Execute(...).Every(100)`.

### Notes

### Guidelines Added

---

## Phase 19 — Context Menus

Right-click menus on nodes, edges, and empty canvas.

### Steps

- [ ] **19-A** Register `ContextClickEvent` on the canvas root. Determine hit target (node, edge, or background).

- [ ] **19-B** **Node context menu**: Add Node (opens search), Delete Node, Duplicate Node, Set As Entry,
  Collapse/Expand, Frame Node (pan to it).

- [ ] **19-C** **Edge context menu**: Delete Edge, Select Source Node, Select Target Node.

- [ ] **19-D** **Canvas background context menu**: Add Node (opens search at pointer), Frame All, Frame Selected,
  Paste (if clipboard has copied node data).

- [ ] **19-E** **Copy node**: `Ctrl+C` on selected nodes serializes them to a JSON clipboard string.
  **Paste**: `Ctrl+V` deserializes, assigns new GUIDs, offsets positions by (+20, +20), adds to `_workingGraph`.

- [ ] **19-F** Implement context menu using UIToolkit `DropdownMenu` (runtime-safe; available in `UnityEngine.UIElements`).

### Notes

### Guidelines Added

---

## Phase 20 — Keyboard Shortcuts

Full keyboard navigation and power-user bindings.

### Steps

- [ ] **20-A** Shortcut table (register all via `RegisterCallback<KeyDownEvent>` on the canvas):

  | Key | Action |
  |---|---|
  | `F` | Frame All |
  | `Shift+F` | Frame Selected |
  | `Delete` / `Backspace` | Delete selected nodes/edges |
  | `Ctrl+Z` | Undo |
  | `Ctrl+Y` / `Ctrl+Shift+Z` | Redo |
  | `Ctrl+S` | Save |
  | `Ctrl+C` | Copy selected nodes |
  | `Ctrl+V` | Paste |
  | `Ctrl+D` | Duplicate selected nodes |
  | `Ctrl+A` | Select all nodes |
  | `Escape` | Deselect all / close popup |
  | `M` | Toggle minimap |
  | `F12` / assigned key | Toggle editor window open/close |
  | `Double-click canvas` | Open node search window |

- [ ] **20-B** Ensure shortcuts don't fire when focus is inside a text field.
  Check `focusedElement is TextField` before acting.

- [ ] **20-C** Display shortcut hints in tooltips of toolbar buttons.

### Notes

### Guidelines Added

---

## Phase 21 — Status Bar

Thin bar at the bottom of the editor window showing live state.

### Steps

- [ ] **21-A** Create `AGISStatusBar.cs` — `VisualElement` docked to the bottom. Height: 24px.

- [ ] **21-B** Left section: current graph name | slot index | dirty indicator (`*`).

- [ ] **21-C** Center section: error count (red) | warning count (yellow). Click opens Validation panel (Phase 15).

- [ ] **21-D** Right section: selected node count | canvas zoom level (e.g. `87%`).

- [ ] **21-E** Transient messages (e.g. "Saved", "Undo: Move Nodes") shown in center, fade out after 2 seconds.
  Use `schedule.Execute()` to clear after timeout.

### Notes

### Guidelines Added

---

## Phase 22 — Live Debug Overlay

Visualize the running state machine. Since the editor runs in a build, the simulation is always active — there is no editor mode distinction to switch into. The overlay is available whenever a runner is connected and the [Debug] toggle is on.

### Steps

- [ ] **22-A** When [Debug] toggle is on and a runner is connected: enable debug overlay layer (`_debugLayer`
  above `_nodeLayer`). Disable it when the toggle is off or the runner reference is cleared.

- [ ] **22-B** Each tick: read `runner.Slots[slotIndex].CurrentNodeId`.
  Highlight the active node card with `.node--active` USS class (green glow pulse animation).

- [ ] **22-C** Highlight the last-taken transition edge (requires tracking `_lastTransitionEdgeId` — add a
  `public AGISGuid LastTransitionEdgeId` to `AGISStateMachineSlot` or `AGISStateMachineInstance`).

- [ ] **22-D** Refresh rate: every 100ms via `schedule.Execute(...).Every(100)`. This keeps CPU overhead negligible.

- [ ] **22-E** Debug overlay disables when the runner reference is cleared or the toggle is turned off.

- [ ] **22-F** Blackboard tab (Phase 13) shows live values with auto-refresh when a runner is connected.

### Notes

### Guidelines Added

---

## Phase 23 — USS Styling Pass

Polish all visuals to a cohesive dark-theme aesthetic.

### Steps

- [ ] **23-A** Create `Assets/Scripts/AX State Machine/RuntimeEditor/USS/AGISEditor.uss` as the single stylesheet.
  All editor elements reference this file.

- [ ] **23-B** Color palette (USS custom properties on `:root`):
  ```css
  --color-bg: #1a1a1a;
  --color-panel: #242424;
  --color-surface: #2e2e2e;
  --color-border: #3a3a3a;
  --color-accent: #4a9eff;
  --color-entry: #e8b84b;
  --color-exit: #e85b5b;
  --color-normal: #3A7BD5;
  --color-any: #8B1A1A;
  --color-grouped: #1A8B7A;
  --color-parallel: #6B3FA0;
  --color-edge: #6a6a6a;
  --color-edge-selected: #ffffff;
  --color-edge-dangling: #e8903a;
  --color-error: #e85b5b;
  --color-warning: #e8b84b;
  --color-active: #4adc8a;
  ```

- [ ] **23-C** Node card corner radius: 8px. Header: 8px top only. Body: 0. Footer: 8px bottom only.

- [ ] **23-D** Animate `.node--active` with a CSS pulse: `border-color` oscillates from `--color-active` to transparent
  over 1s infinite. Use UIToolkit's `transition` support.

- [ ] **23-E** Tab buttons: underline style, not box style. Active tab: white text + 2px bottom border in `--color-accent`.

- [ ] **23-F** Typography: all labels `font-size: 12px` unless heading (14px bold). Port labels `font-size: 10px`.

- [ ] **23-G** Scrollbars: minimal / thin (8px wide) with `--color-border` track.

### Notes

### Guidelines Added

---

## Phase 24 — IL2CPP & Build Verification

Confirm the editor and the reflection-based type registration survive a stripped build.

### Steps

- [ ] **24-A** Verify `Assets/link.xml` (Phase 0-D) preserves `Assembly-CSharp` fully.
  If the project later gains `.asmdef` files, add their assembly names to `link.xml` too.

- [ ] **24-B** Build for a target platform (e.g. Android or Standalone IL2CPP). Run the build and open the editor
  window in-game. Confirm all node types and condition types appear in search and dropdowns.

- [ ] **24-C** Confirm `AGISGraphSerializer` round-trips correctly in the build: save a graph, reload, verify
  node count and param values are identical.

- [ ] **24-D** Confirm undo/redo, save, and live debug overlay work correctly in the build (not just in the Editor).

- [ ] **24-E** Profile: open a graph with 50+ nodes. Confirm canvas pan/zoom is above 60 FPS. Profile edge regeneration;
  optimize `MarkDirtyRepaint()` calls if needed (batch per frame instead of per-mutation).

- [ ] **24-F** Document any remaining known limitations or future work in a `<!-- KNOWN ISSUES -->` comment block
  at the bottom of this file.

### Notes

### Guidelines Added

---

<!-- KNOWN ISSUES -->
<!--
  - None yet. Add as discovered during implementation.
-->
