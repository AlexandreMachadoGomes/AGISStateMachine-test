# AGIS ESM — Visual State Machine Editor: Design Guidelines

## 1. Purpose & Scope

This document defines the architecture, UX conventions, and implementation guidelines for the AGIS ESM graph editor — a Unity Editor window that lets designers create, edit, and save `AGISStateMachineGraph` and `AGISGroupedStateAsset` assets visually without writing code.

The editor sits entirely in the **UGC layer** (`AGIS.ESM.UGC`). It reads and writes serialized data structures (`AGISNodeInstanceDef`, `AGISTransitionEdgeDef`, `AGISConditionExprDef`, `AGISParamTable`). It never touches runtime classes.

---

## 2. Technology Recommendation: Unity GraphView

Use **Unity's `GraphView` API** (`UnityEditor.Experimental.GraphView`), the same foundation used by Shader Graph and VFX Graph.

**Provides out of the box:**
- Pan (middle-mouse / alt-drag) and zoom (scroll wheel)
- Node drag, multi-select, marquee selection
- Edge drawing (click port → drag → click port)
- Minimap (`MiniMap` element)
- Searcher integration for node creation

**Known caveats:**
- Marked `Experimental` but stable across Unity 2021–6; Unity themselves ship production tools on it
- No built-in self-loop edge rendering — requires a custom `Edge` subclass (see §9)
- GraphView edges are always `Port → Port`; the AGIS model is `Node → Node` — ports must be synthesised as 1-per-node output / 1-per-node input (not per-slot typed ports)

**Alternative** (fully custom `EditorWindow` + `GUI`): viable but costs ~3× the implementation time with no significant benefit for this use case.

---

## 3. Data Model → Editor Mapping

| UGC type | Editor element |
|---|---|
| `AGISStateMachineGraph` | The open graph document |
| `AGISNodeInstanceDef` | Node card on the canvas |
| `AGISTransitionEdgeDef` | Directed edge (arrow) between two nodes |
| `AGISConditionExprDef` | Condition tree shown in the edge inspector |
| `AGISConditionInstanceDef` (leaf) | Leaf row in the condition tree |
| `AGISParamTable` (on node) | Property rows in the node inspector panel |
| `AGISParamSchema` (from `IAGISNodeType`) | Drives which fields appear and their types |
| `AGISGroupedStateAsset` | Opens in its own editor window (or tab) |
| `AGISNodeVisualDef` | Stores canvas position and collapsed state — already on `AGISNodeInstanceDef.visual` |

The editor must never fabricate new `AGISGuid` values after initial creation. Each node and edge gets its GUID once on creation. Changing type, params, or position never changes the GUID.

---

## 4. Editor Window Structure

```
┌─────────────────────────────────────────────────────────────────┐
│  Toolbar                                                        │
│  [Save]  [Revert]  [Validate]  [Centre View]  [Breadcrumb...]  │
├───────────────────────────────────────┬─────────────────────────┤
│                                       │                         │
│           Canvas (GraphView)          │   Inspector Panel       │
│                                       │   (context-sensitive)   │
│                                       │                         │
│                                       │                         │
│                                       │                         │
└───────────────────────────────────────┴─────────────────────────┘
│  Status bar:  [Graph ID]  [Node count]  [Validation summary]   │
└─────────────────────────────────────────────────────────────────┘
```

- **Canvas** takes ~70% of the window width.
- **Inspector panel** is fixed ~30% on the right. It shows context for the current selection: a node, an edge, or nothing (graph-level properties).
- **Breadcrumb** in the toolbar shows the drill-down path when inside a grouped sub-graph: `PatrolGraph > RoutedMovement`.
- The editor opens when double-clicking an `AGISStateMachineGraphAsset` or `AGISGroupedStateAsset` in the Project window.

---

## 5. Node Visual Design

### 5a. Node card anatomy

```
┌──────────────────────────────┐
│ ● [Display Name]       [Kind]│  ← header (colour-coded by Kind)
├──────────────────────────────┤
│  TypeId: npc.wander          │  ← subtitle (small, muted)
│                              │
│  [param summary lines...]    │  ← collapsed preview (optional)
└──────────────────────────────┘
  ↑ input port (top centre)    ↓ output port (bottom centre)
```

Ports are **not typed** — every node has one input port (top) and one output port (bottom). All edges originate from the output port and terminate at any input port. This matches the AGIS model where edges carry all the logic, not the ports.

### 5b. Header colour by `AGISNodeKind`

| Kind | Header colour | Label shown |
|---|---|---|
| `Normal` | Dark grey (#3a3a3a) | *(none)* |
| `Grouped` | Dark blue (#1a3a5c) | `GROUP` |
| `Parallel` | Dark teal (#1a4a40) | `PARALLEL` |
| `AnyState` | Dark gold (#5c4a00) | `ANY STATE` |

### 5c. Entry node marker

The graph's `entryNodeId` node gets a green left border (4px) and a small `ENTRY` badge below its header. Right-clicking any node offers **Set as Entry**.

### 5d. Collapsed vs expanded

Nodes are expanded by default (showing param summary). A small `▾/▸` toggle in the header collapses to header-only. `AGISNodeVisualDef.collapsed` persists this state.

### 5e. AnyState node special rules

- **No input port** — nothing should ever transition *into* an AnyState node
- Output port present as normal — edges are drawn from it to target nodes
- Canvas label: `ANY STATE` stamped across the header in the gold colour
- Should not be selectable as the entry node (validate and block in the Set as Entry action)
- Recommend placing it in a corner by convention; the editor does not enforce position

---

## 6. Edge Visual Design

### 6a. Normal edges

Standard GraphView `Edge`. Drawn as a bezier curve from source output port to target input port.

**Label on edge:** priority value rendered as a small badge at the midpoint of the curve. Only shown when priority ≠ 0.

**Thickness:**
- Default: 2px
- Selected: 3px + highlight colour

**Colour coding:**

| Condition on edge | Colour |
|---|---|
| `ConstBool(true)` (unconditional) | Pale white (#cccccc) |
| Has a real condition | Cyan (#00bfff) |
| No condition set (null — always false) | Red (#cc3333) — signals authoring error |
| Edge from AnyState node | Gold (#f0c040) |

### 6b. Self-loop edges

GraphView's built-in edge renderer requires two distinct ports at different positions. A self-loop (`fromNodeIndex == toNodeIndex`) must use a **custom `Edge` subclass** that overrides `UpdateEdgeControl` to draw an arc:

- Draw a circular arc emanating from the output port, looping above (or to the right of) the node, and re-entering the input port
- The label badge sits at the top of the arc
- Hit-testing: a thin invisible rect along the arc path for click selection

This is the one case where custom rendering is required. The data model supports self-loops natively; only the renderer needs special-casing.

### 6c. Edge creation flow

1. User drags from a node's **output port** — GraphView begins an edge drag
2. Dropping on any **input port** (including the source node's own input port for a self-loop) creates the edge
3. An `AGISTransitionEdgeDef` is created with a fresh `AGISGuid`, default priority 0, default policy, and `condition = AGISConditionExprDef.False()` (explicit null-is-false, shown red until configured)
4. The new edge is immediately selected and the inspector panel switches to it

### 6d. Edges from AnyState nodes

Edges from an AnyState node are drawn in gold and labelled `GLOBAL` at the midpoint instead of the priority badge. Priority is still editable in the inspector — the badge just reads differently.

---

## 7. Inspector Panel

The panel is divided into sections depending on what is selected.

### 7a. Nothing selected — Graph properties

- Graph ID (read-only display)
- Version field
- Entry node ID (read-only; set via right-click on canvas)

### 7b. Node selected

```
[ Display Name ] (from IAGISNodeType, read-only)
[ Type ID      ] (read-only)
[ Kind         ] (read-only)

── Parameters ──────────────────────────
  Each param from AGISParamSchema rendered as a property row:
  Label     [field appropriate to AGISParamType]   [Reset to default ↺]

  Bool   → Toggle
  Int    → IntField (respects hasMin/hasMax)
  Float  → FloatField (respects hasMin/hasMax)
  String → TextField
  Vector2/3 → Vector2Field / Vector3Field
  Guid   → TextField (hex display)

  Greyed-out rows: param exists in Schema but no override in the node's
  AGISParamTable — shows the schema default value. Editing writes an override.
  Reset button removes the override, restoring schema default.

── Grouped node extras ─────────────────
  [ Group Asset ]  (object field: AGISGroupedStateAsset)
  [ Open Sub-Graph ↗ ]  button

── Parallel node extras ─────────────────
  [ Children ]  (reorderable list of child node definitions)
```

### 7c. Edge selected

```
[ From ] NodeDisplayName  →  [ To ] NodeDisplayName  (read-only)

── Transition ──────────────────────────
  Priority       [ int field ]
  Cooldown (s)   [ float field ]
  Interruptible  [ toggle ]
  Scope ID       [ text field ]  (only relevant for grouped-exit edges)

── Condition ───────────────────────────
  [ Condition Tree Editor — see §8 ]
```

---

## 8. Condition Expression Tree Editor

The condition on an edge is an `AGISConditionExprDef` — a recursive tree of `And`, `Or`, `Not`, `Leaf`, and `ConstBool` nodes. The inspector renders this as an **inline collapsible tree** with add/remove controls.

### 8a. Visual structure

```
  ┌─[AND]──────────────────────────────── [+ Add child] [✕]
  │   ├─ [Leaf] npc.blackboard_bool       [Edit params] [✕]
  │   │     key = "npc.is_damaged"
  │   │     expected = true
  │   └─[NOT]──────────────────────────── [+ Add child] [✕]
  │        └─ [Leaf] npc.actor_state_bool [Edit params] [✕]
  │               key = "npc.use_routes"
  │               expected = true
  └─────────────────────────────────────────────────────────
```

### 8b. Node types and creation

| Type | Button label | Children allowed |
|---|---|---|
| `And` | `AND` | 2+ |
| `Or` | `OR` | 2+ |
| `Not` | `NOT` | exactly 1 |
| `Leaf` | pick via Searcher | 0 (has params) |
| `ConstBool(true)` | `ALWAYS TRUE` | 0 |
| `ConstBool(false)` | `ALWAYS FALSE` | 0 |

The root node has a **Replace Root** dropdown. Each non-root node has a **✕ Remove** button that removes the subtree.

### 8c. Leaf node param editing

When a `Leaf` is expanded, the condition's `AGISParamSchema` (from `IAGISConditionTypeRegistry`) is used to render inline property rows — the same style as the node param editor (§7b). Params are stored in `AGISConditionInstanceDef.@params`.

### 8d. Searcher for condition types

Clicking **Add Leaf** opens a Searcher popup listing all registered `IAGISConditionType` implementations by `DisplayName`, grouped by namespace prefix (`agis.*`, `npc.*`, `user.*`, etc.).

---

## 9. Grouped Node Drill-Down

Double-clicking a Grouped node header opens its internal `AGISGroupedStateAsset` graph:

- The canvas replaces its content with the sub-graph
- The breadcrumb updates: `PatrolGraph  >  RoutedMovement`
- Clicking a breadcrumb segment navigates back up
- The sub-graph is saved independently to its own asset file

The editor maintains a **navigation stack** (list of open asset references + scroll/zoom state) so the user can go back and forth without losing view position.

**Exposed parameter bindings** (`AGISNodeInstanceDef.exposedOverrides`) are shown in the node inspector when a Grouped node is selected in the parent graph — not inside the sub-graph itself.

---

## 10. Right-Click Context Menu

**Canvas background:**
```
Create Node
  ├ Normal...          (opens Searcher for IAGISNodeType)
  ├ Grouped...         (opens Searcher for existing AGISGroupedStateAssets)
  ├ Parallel
  └ Any State
```

**Node:**
```
Set as Entry
Open Sub-Graph          (Grouped only)
Duplicate
Delete
```

**Edge:**
```
Delete
```

---

## 11. Searcher Integration

The node creation Searcher (Unity.Searcher package, already a dependency via Shader Graph) lists all registered `IAGISNodeType` implementations. Items are grouped by the prefix of `TypeId`:

```
agis
  ├ Any State
  ├ Dialogue
  └ Node Complete (condition — shown separately but same searcher pattern)
npc
  ├ Behavior Selector
  ├ Follow Target
  ├ Idle
  ├ Move To Waypoint
  ├ ...
  └ Wander
```

Selecting an item creates the node at the clicked canvas position with all params set to schema defaults and no overrides.

---

## 12. Validation Overlay

Runs `AGISGraphValidator` on the current graph and overlays results directly on the canvas:

| Severity | Visual |
|---|---|
| Error | Red border on the offending node or edge; red dot in status bar |
| Warning | Yellow border; yellow dot in status bar |
| Info | No border; grey dot in status bar |

The **Validate** toolbar button re-runs validation manually. Auto-validation runs on every save. Hovering an error border shows a tooltip with the validation message.

**Common validation errors to surface visually:**
- Entry node not set (red banner across the canvas top)
- Edge condition is null/always-false (red edge colour, warning in inspector)
- AnyState node set as entry
- Grouped node with no group asset assigned
- Leaf condition referencing an unregistered TypeId

---

## 13. Save / Revert / Dirty Tracking

- The editor tracks a **dirty flag** set on any canvas mutation (node move, param change, edge add/remove)
- The window title shows `*` when dirty: `PatrolGraph *`
- **Save** calls `EditorUtility.SetDirty()` + `AssetDatabase.SaveAssets()`
- **Revert** reloads the asset from disk after a confirmation dialog if dirty
- Unity's domain reload (entering Play mode, script compilation) should prompt save if dirty

The editor should **not** auto-save on every keystroke. Save is explicit.

---

## 14. Undo / Redo

Register every mutation with `Undo.RecordObject(graphAsset, "description")` before applying changes. This gives undo/redo for free via Unity's standard Ctrl+Z / Ctrl+Y.

Operations to wrap:
- Create node
- Delete node (also deletes connected edges)
- Move node (on mouse-up, not every drag frame)
- Create edge
- Delete edge
- Any param value change (on field commit, not every keystroke)
- Set entry node

---

## 15. Known Hard Problems

| Problem | Notes |
|---|---|
| Self-loop edge rendering | Custom `Edge` subclass required; hit-testing on arc path needs manual implementation |
| Condition tree undo | The tree is a recursive object graph; must `RecordObject` the asset (not individual tree nodes) before each tree mutation |
| Grouped node param binding UI | `exposedOverrides` on the outer node instance needs to mirror the sub-graph's exposed params — a two-asset join in the inspector |
| AnyState node cannot be entry | Block in UI + validation rule; compiler doesn't enforce it |
| Parallel node child ordering | Children render order matters for execution; the reorderable list in the inspector is the UX for this |
| Unregistered TypeId after code deletion | Nodes whose `nodeTypeId` has no registered type compile with `Type = null`; the editor should show these in a distinct "Unknown" style rather than crashing |
| Large graphs | GraphView handles ~200 nodes comfortably; beyond that consider culling off-screen nodes from the visual tree while keeping them in the data model |

---

## 16. File & Folder Conventions

```
Assets/Scripts/
  State Machine Infrastructure/
    Editor/                        ← new Editor assembly def
      AGISGraphEditorWindow.cs     ← main EditorWindow
      AGISNodeView.cs              ← GraphView node element
      AGISEdgeView.cs              ← GraphView edge element (+ self-loop subclass)
      AGISGraphView.cs             ← GraphView canvas
      AGISInspectorPanel.cs        ← right-panel root
      Inspectors/
        AGISNodeInspector.cs
        AGISEdgeInspector.cs
        AGISConditionTreeEditor.cs
      Searchers/
        AGISNodeSearcher.cs
        AGISConditionSearcher.cs
```

The editor assembly definition must reference `UnityEditor`, `UnityEditor.UIElements`, `UnityEditor.Experimental.GraphView`, and the main AGIS runtime assembly.

---

## 17. Out of Scope (for v1)

- Runtime playback overlay (highlighting the current active node during Play mode) — desirable but a Phase 2 feature
- Graph diffing / merge support
- Undo across domain reloads
- Multi-graph tabbed editing (single graph open at a time is sufficient for v1)
- Localization of display names

---

## 18. Simplified State Authoring (Script Nodes)

### Goal

Creating a new state type currently requires a developer to write a full `IAGISNodeType` class: a TypeId, a DisplayName, a `AGISParamSchema` declaration, a `CreateRuntime` factory, and a nested `Runtime` class implementing `IAGISNodeRuntime`. This is intentional boilerplate for production code, but it is a barrier for users who just want to add a small piece of logic to a graph without understanding the full infrastructure.

The goal of this feature is an authoring mode built into the graph editor where a user writes only the logic they care about — the bodies of `Enter`, `Tick`, and `Exit`, plus a list of variables — and the editor handles everything else automatically.

The end result should feel as close as possible to writing a Unity `MonoBehaviour`: declare a field, write a method body, done.

---

### The Two Tiers

The editor should support two tiers of state creation side by side. Both produce real `IAGISNodeRuntime` instances at runtime; they differ only in authoring experience.

| | Tier 1 — Code Class | Tier 2 — Script Node |
|---|---|---|
| Who uses it | Framework developers, power users | Designers, non-framework users |
| Where authored | External `.cs` file | Inside the graph editor |
| Schema declaration | Manual `AGISParamSchema` | Inferred from declared variables |
| Boilerplate | Full `IAGISNodeType` class | None |
| Reusable across graphs | Yes (auto-discovered) | By default scoped to one graph; can be promoted |
| Debuggable | Full IDE support | Limited (see §18e) |

Tier 1 is not going away. Script Nodes are an addition, not a replacement.

---

### 18a. What the User Writes

The user sees a simplified editor with three sections:

**Variables** — a list of named variables with a type and default value. These automatically become `AGISParamSchema` params, visible in the node's inspector panel like any other param.

```
Variables
  ┌────────────────────────────────────────────────────────┐
  │  Name            Type     Default    Display Name      │
  │  speed           Float    5.0        Move Speed        │
  │  target_tag      String   "Player"   Target Tag        │
  │  stop_on_arrive  Bool     true       Stop On Arrive    │
  │                                   [ + Add Variable ]   │
  └────────────────────────────────────────────────────────┘
```

**Code sections** — three independent code bodies. Each receives a fixed set of pre-injected locals so the user never needs to declare them:

```csharp
// Always available in all three sections:
//   GameObject       actor       — the NPC's root GameObject
//   IAGISBlackboard  blackboard  — the actor's in-memory key-value store
//   AGISActorState   actorState  — the actor's persistent key-value store
//   IAGISNPCPathFinder pathFinder — the pathfinding interface (may be null)
//
// All declared variables are available by name (speed, target_tag, etc.)
// In Tick only: float dt

// ── Enter ────────────────────────────────────────────────
pathFinder?.EnablePathfinding();

// ── Tick ─────────────────────────────────────────────────
if (pathFinder != null && pathFinder.ReachedDestination && stop_on_arrive)
    pathFinder.DisablePathfinding();

// ── Exit ─────────────────────────────────────────────────
pathFinder?.DisablePathfinding();
```

The user never writes a class, a constructor, `GetComponent` calls for the standard services, or schema declarations. All of that is generated.

---

### 18b. What the Editor Generates

When the graph is saved, the editor produces a complete, valid `IAGISNodeType` implementation behind the scenes. Given the example above it generates:

```csharp
// AUTO-GENERATED — do not edit directly. Edit via the Script Node editor.
namespace AGIS.ESM.Runtime.Generated
{
    public sealed class ScriptNode_[GraphId]_[NodeId] : IAGISNodeType
    {
        public string TypeId      => "agis.script.[NodeId]";
        public string DisplayName => "[User-defined name]";
        public AGISNodeKind Kind   => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("speed",         AGISParamType.Float,  AGISValue.FromFloat(5f))
                    { displayName = "Move Speed" },
                new AGISParamSpec("target_tag",    AGISParamType.String, AGISValue.FromString("Player"))
                    { displayName = "Target Tag" },
                new AGISParamSpec("stop_on_arrive",AGISParamType.Bool,   AGISValue.FromBool(true))
                    { displayName = "Stop On Arrive" },
            }
        };

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            float  speed          = args.Params.GetFloat ("speed",          5f);
            string target_tag     = args.Params.GetString("target_tag",     "Player");
            bool   stop_on_arrive = args.Params.GetBool  ("stop_on_arrive", true);
            return new Runtime(args.Ctx, speed, target_tag, stop_on_arrive);
        }

        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly GameObject        actor;
            private readonly IAGISBlackboard   blackboard;
            private readonly AGISActorState    actorState;
            private readonly IAGISNPCPathFinder pathFinder;
            private readonly float             speed;
            private readonly string            target_tag;
            private readonly bool              stop_on_arrive;

            public Runtime(AGISExecutionContext ctx, float speed, string target_tag, bool stop_on_arrive)
            {
                actor          = ctx.Actor;
                blackboard     = ctx.Blackboard;
                actorState     = ctx.Actor?.GetComponent<AGISActorState>();
                pathFinder     = ctx.Actor?.GetComponent<IAGISNPCPathFinder>();
                this.speed          = speed;
                this.target_tag     = target_tag;
                this.stop_on_arrive = stop_on_arrive;
            }

            public void Enter()
            {
                // ── USER CODE ─────────────────────────
                pathFinder?.EnablePathfinding();
            }

            public void Tick(float dt)
            {
                // ── USER CODE ─────────────────────────
                if (pathFinder != null && pathFinder.ReachedDestination && stop_on_arrive)
                    pathFinder.DisablePathfinding();
            }

            public void Exit()
            {
                // ── USER CODE ─────────────────────────
                pathFinder?.DisablePathfinding();
            }
        }
    }
}
```

The generated file lives at `Assets/Scripts/State Machine Infrastructure/Generated/` and is automatically included in the assembly. Unity recompiles it, and the node type is auto-discovered via reflection like any other.

The generated file is clearly marked as auto-generated. Users should not edit it directly — changes belong in the Script Node editor.

---

### 18c. Variable Types Supported

| Editor type | C# type | Param type |
|---|---|---|
| Float | `float` | `AGISParamType.Float` |
| Int | `int` | `AGISParamType.Int` |
| Bool | `bool` | `AGISParamType.Bool` |
| String | `string` | `AGISParamType.String` |
| Vector2 | `Vector2` | `AGISParamType.Vector2` |
| Vector3 | `Vector3` | `AGISParamType.Vector3` |

Variables of these types are read-only inside the code sections (they come from the schema). For mutable runtime state (a timer, a flag that changes during execution), the user uses local variables declared directly in the code body, or reads/writes the blackboard.

---

### 18d. Node Card Appearance

Script Nodes are visually distinct from compiled node types:

- Header colour: dark purple (#3a2a5c)
- Header badge: `SCRIPT`
- A small pencil icon in the corner opens the script editor inline

---

### 18e. Limitations and Known Constraints

| Constraint | Notes |
|---|---|
| No IDE autocomplete inside the editor | The code sections are plain text fields. The user does not get Intellisense. Document the available locals clearly in the UI (a collapsible "Available Variables" help section in the editor). |
| Compile errors surface as Unity console errors | When generated code fails to compile, the node shows an error state in the canvas and the console shows the error with file + line. The user then corrects in the script editor. |
| No access to arbitrary MonoBehaviours | Only `actor`, `blackboard`, `actorState`, and `pathFinder` are pre-injected. For anything else the user writes `actor.GetComponent<MyComponent>()` in the code body manually — which is fine but not hidden. |
| `IAGISNodeSignal` not auto-supported | Script Nodes cannot currently signal completion via `IAGISNodeSignal`. If the user needs `agis.node_complete`, they should write a Tier 1 class instead. This may be added later via a checkbox in the variable section: `[ ] Expose IsComplete signal`. |
| Not suitable for complex persistent state | Script Nodes don't implement `IAGISPersistentNodeType`. If the node needs keys pre-populated in `AGISActorState` at startup, it should be a Tier 1 class. |
| Naming collisions | Generated TypeIds are scoped to the node GUID, so two Script Nodes in different graphs can have the same display name without conflict. |

---

### 18f. Promoting a Script Node to a Code Class

When a Script Node grows complex enough to warrant a proper Tier 1 class, the editor provides an **Export as Code Class** button. It writes the current generated file to `Assets/Scripts/NPC/States/` under a user-chosen name, removes the `AUTO-GENERATED` header, and replaces the Script Node in the graph with a reference to the new TypeId. From that point on the node is a normal Tier 1 class and the script editor is no longer involved.
