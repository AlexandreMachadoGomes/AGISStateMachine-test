# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**AGIS ESM** (An GI System - Extensible State Machine) is a Unity 6 (6000.0.58f2) state machine framework written in C#. It is a standalone engine-style library — not a game — providing a parameter-driven, hierarchical state machine system for game AI and actor behavior.

## Build & Development

**Open in Unity Editor**: Open the project root in Unity 6. Unity automatically compiles C# scripts in `Assets/Scripts/`.

**Build via MSBuild** (outside Unity):
```
msbuild "AGISStateMachine test.sln" /p:Configuration=Debug
```

**Run Tests**: Unity Test Framework is installed (`com.unity.test-framework 1.5.1`).
- In Unity Editor: Window > General > Test Runner
- Command line: `unity.exe -runTests -testPlatform editmode -projectPath <path>`

No custom tests exist yet in the project — the framework is in development.

## Code Architecture

### Namespaces

- `AGIS.ESM.UGC` — User-Generated Content: serializable graph definitions (what designers configure)
- `AGIS.ESM.Runtime` — Runtime execution: compiled graphs, state machine runners, factories
- `AGIS.NPC` — NPC building blocks: pathfinding, routes, detection, states, conditions

### Source layout

```
Assets/Scripts/
  AX State Machine/
    Definitions/    ← UGC serializable data structures (AGISStateMachineGraph, AGISConditionDefs, AGISParamTable, AGISGuid, AGISGroupedStateAsset)
    Compilation/    ← Compiler, registries, validator, evaluator, runtime graph
    Execution/      ← Runner, instance, actor, blackboard, factory, slots, cache, built-ins
    Hierarchical/   ← Grouped + parallel node types and runtimes
    Serialization/  ← AGISGraphSerializer (JsonUtility round-trip) + AGISContentLibrary (in-memory DB-driven store)
  NPC/
    IAGISNPCPathFinder.cs          ← pathfinder interface
    NPCUCCPathFinder.cs            ← IAGISNPCPathFinder bridge to AStarAIAgentMovement (UCC + A* integration)
    NPCDetectionCone.cs            ← sphere / cone detection + gizmo
    NPCDetectionMeter.cs           ← config holder for detection meter; declares persistent keys
    AGISEnemyTemplateData.cs       ← ScriptableObject: bundles graphs, route, detection config
    AGISEnemyConfigurator.cs       ← static runtime API: UpgradeEnemy(go, template) one-shot; or Configure(go) + Inject(go, template) two-step
    Routes/
      NPCRoute.cs
      NPCRouteData.cs              ← ScriptableObject: routes list + sequence
      NPCRouteDataHolder.cs        ← MonoBehaviour: data accessor, declares npc.use_routes
    States/                        ← IAGISNodeType implementations
      NPCIdleNodeType.cs
      NPCFollowTargetNodeType.cs
      NPCWanderNodeType.cs
      NPCMoveToWaypointNodeType.cs
      NPCAdvanceWaypointNodeType.cs
      NPCResetRouteNodeType.cs
      NPCBehaviorSelectorNodeType.cs
      NPCTakeDamageNodeType.cs     ← UCC ability-based (#if OPSIVE_UCC); shell (NoOp) otherwise
      NPCDyingNodeType.cs          ← UCC ability-based (#if OPSIVE_UCC); shell (NoOp) otherwise
    Conditions/                    ← IAGISConditionType implementations
      NPCHasReachedDestinationConditionType.cs
      NPCHasArrivedAtWaypointConditionType.cs
      NPCIsMovingConditionType.cs
      NPCIsWithinDistanceConditionType.cs
      NPCDetectsObjectConditionType.cs
      AGISActorStateBoolConditionType.cs
      BlackboardBoolConditionType.cs
      NPCOnSequenceIndexConditionType.cs
    Editor/
      NPCRoutedMovementAssetBuilder.cs   ← builds RoutedMovement grouped asset
      NPCTestSceneBuilder.cs             ← one-click scene builder (menu: AGIS/NPC/...)
```

### Core ESM Pipeline

```
UGC Definitions (ScriptableObjects)
    → AGISGraphCompiler → AGISRuntimeGraph (optimized adjacency lists)
    → AGISStateMachineInstance (Enter/Tick/Exit loop + transition evaluation)
```

**`Definitions/` — Core Definitions (`AGIS.ESM.UGC`)**:
Serializable data structures: `AGISStateMachineGraph` (nodes + edges), `AGISConditionDefs` (expression trees: And/Or/Not/Leaf/ConstBool), `AGISParamTable` (typed parameters: Bool/Int/Float/String/Vector2/Vector3/Guid).

**`Compilation/` — Compilation & Registries (`AGIS.ESM.Runtime`)**:
`AGISGraphCompiler` transforms UGC graphs into `AGISRuntimeGraph` (flat arrays with sorted adjacency lists). `AGISNodeTypeRegistry` and `AGISConditionTypeRegistry` hold code-provided implementations discovered via reflection. `AGISTransitionEvaluator` evaluates condition expression trees against a `AGISParamTable`.

**`Execution/` — Execution & Lifecycle (`AGIS.ESM.Runtime`)**:
`AGISStateMachineRunner` (MonoBehaviour) hosts multiple `AGISStateMachineSlot`s per actor. Each slot wraps an `AGISStateMachineInstance` which drives the Enter/Tick/Exit loop and transition evaluation. `AGISActorRuntime` provides per-actor context including `AGISBlackboardRuntime` (generic key-value state). `AGISRuntimeGraphCache` caches compiled `AGISRuntimeGraph` objects to avoid recompilation.

**`Hierarchical/` — Advanced Features (`AGIS.ESM.Runtime`)**:
`AGISGroupedNodeType`/`AGISGroupedNodeRuntime` — hierarchical/macro nodes with their own internal graph and exposed parameter bindings. `AGISParallelNodeType`/`AGISParallelNodeRuntime` — concurrent branch execution. Scope gating (only on edges exiting Grouped nodes) via `IAGISGroupedScopeRuntime.IsScopeActive(scopeId)`.

### Key Interfaces

```csharp
// Implement to create a custom node type
interface IAGISNodeType      // schema + factory
interface IAGISNodeRuntime   // Enter() / Tick(float dt) / Exit()

// Implement to create a custom condition type
interface IAGISConditionType // schema + Evaluate(params, ctx) → bool

// Implement on a node type OR MonoBehaviour to declare persistent AGISActorState keys
interface IAGISPersistentNodeType   // PersistentParams: IReadOnlyList<AGISParamSpec>
```

Register node/condition implementations in `AGISNodeTypeRegistry` / `AGISConditionTypeRegistry` (auto-discovered via reflection when `autoRegisterTypesFromAssemblies = true`).

### Parameter System — Two Separate Concerns

**Schema params** (`AGISParamSchema` on a type's `Schema` property): static design-time configuration set per node/condition instance in the graph. Read-only at runtime via `args.Params.GetFloat(...)` etc.

**Persistent params** (`IAGISPersistentNodeType.PersistentParams`): mutable runtime state stored in `AGISActorState`. The runner calls `EnsureKey` for each declared param at startup (only adds if absent — resume behaviour). Two discovery sources:
1. Node types in the slot graphs that implement `IAGISPersistentNodeType`
2. MonoBehaviour components on the actor that implement `IAGISPersistentNodeType`

### Transition Evaluation Per Tick

Each tick: `CurrentNode.Tick(dt)` → iterate outgoing edges (sorted by priority, descending) → for each edge check scope eligibility → check `AGISTransitionPolicyRuntime` (cooldown / interruptible) → evaluate condition expression tree → on first true edge, call `Exit()` on current node, then `Enter()` on target node.

A null condition on an edge evaluates as **false**. Use `ConstBool(true)` for unconditional transitions.

### ScriptableObject Assets

`AGISStateMachineGraphAsset` wraps `AGISStateMachineGraph` as a Unity ScriptableObject. `AGISGroupedStateAsset` wraps reusable sub-graphs (macro nodes).

---

## NPC System

### Pathfinding abstraction

`IAGISNPCPathFinder` — interface used by all NPC node types and conditions. Exposes `ReachedDestination`, `DesiredVelocity`, `IsPathfindingActive`, `Enable/DisablePathfinding()`, `SetWalkTarget()`, `SetWalkTargetTransform()`, `ClearWalkTarget()`, `SnapToGraph()`.

`NPCUCCPathFinder` — concrete MonoBehaviour implementing `IAGISNPCPathFinder`. Bridges to `AStarAIAgentMovement` (the official A*/UCC integration ability). Requires `UltimateCharacterLocomotion` with `AStarAIAgentMovement` in its ability list, and an `IAstarAI` (AIPath/RichAI) on the same GameObject. `AGISEnemyConfigurator.Configure()` adds `Seeker`, `AIPath`, and `NPCUCCPathFinder` — **not** `AIDestinationSetter` (would conflict with the ability's own destination management).

### AGISActorState

Serialized MonoBehaviour on the actor. Key-value store (`AGISParamTable`) that persists across state transitions. Populated automatically by `AGISStateMachineRunner.Awake()` via `IAGISPersistentNodeType` scan. `EnsureKey` never overwrites existing values (resume behaviour). Typed helpers: `GetBool/GetInt/GetFloat/GetString`, `Set`.

### Detection

`NPCDetectionCone` — MonoBehaviour with `shape` (`Sphere` | `Cone`), `detectionMask`, `range`, `angle`. Implements `IAGISPersistentNodeType` declaring `npc.show_detection_cone (Bool, false)`. API: `IsDetected(Transform)`, `DetectAll()`, `DetectClosest()`. Gizmo: blue wire sphere (Sphere) or golden arc (Cone) drawn when `npc.show_detection_cone = true`.

`NPCDetectsObjectConditionType` (`npc.detects_object`) — condition using `NPCDetectionCone`. Three modes: player / blackboard key / any object in mask. Optionally writes detected `GameObject` to blackboard via `detected_key`.

### Route system

`NPCRouteData` — ScriptableObject: list of `NPCRoute` (each a list of `Vector3` waypoints) plus an integer `sequence` list that defines traversal order.

`NPCRouteDataHolder` — MonoBehaviour: data accessor. Implements `IAGISPersistentNodeType` declaring `npc.use_routes (Bool, false)`.

**Ping-pong traversal**: `NPCAdvanceWaypointNodeType` uses `npc.route.sequence_direction` (`+1` = forward, `-1` = reverse). When the sequence end is reached the direction flips — the NPC retraces its route in reverse, then flips back. `NPCResetRouteNodeType` resets direction to `+1`.

### AGISActorState keys in use

| Key | Type | Default | Owner |
|---|---|---|---|
| `npc.use_routes` | Bool | false | `NPCRouteDataHolder` |
| `npc.show_detection_cone` | Bool | false | `NPCDetectionCone` |
| `npc.route.sequence_index` | Int | 0 | `NPCMoveToWaypointNodeType` |
| `npc.route.waypoint_index` | Int | 0 | `NPCMoveToWaypointNodeType` |
| `npc.route.sequence_direction` | Int | 1 | `NPCMoveToWaypointNodeType` |
| `npc.route.route_name` | String | "" | `NPCMoveToWaypointNodeType` |
| `npc.target_time_lost` | Float | 0 | `NPCFollowTargetNodeType` |

### Template enemy graph (built by NPCTestSceneBuilder)

```
(entry) [BehaviorSelector]  npc.behavior_selector
  │ priority 1: npc.use_routes = true
  ▼
[RoutedMovement]  — Grouped state (internal: Reset→MoveToWaypoint↔AdvanceWaypoint, ping-pong)
  │ npc.use_routes = false  → back to Selector

[BehaviorSelector]
  │ priority 0: ConstBool(true)
  ▼
[Wander]  npc.wander
  │ npc.use_routes = true  → back to Selector
```

Toggle `npc.use_routes` in the AGISActorState inspector at runtime to switch modes.

### Node completion signal

`IAGISNodeSignal` — optional interface a node runtime implements to expose `bool IsComplete`. The state machine does NOT act on it directly; instead, an outgoing edge uses `AGISNodeCompleteConditionType` (`agis.node_complete`) to fire when `IsComplete = true`.

`AGISNodeCompleteConditionType` (`agis.node_complete`) — no params. Returns true when `AGISConditionEvalArgs.CurrentRuntime` implements `IAGISNodeSignal` and `IsComplete == true`. The evaluator now forwards the active runtime into every condition eval (as `AGISConditionEvalArgs.CurrentRuntime`), so this check is zero-overhead for all other condition types.

`NPCTakeDamageNodeType` (`npc.take_damage`) — fires an Animator trigger on Enter, then polls `GetCurrentAnimatorStateInfo` in Tick. Implements `IAGISNodeSignal`. Params: `animation_trigger (String, "TakeDamage")`, `animation_state (String, "TakeDamage")`, `layer (Int, 0)`. If no Animator exists on the actor, `IsComplete` is set true immediately on the first Tick. `IsComplete` is reset to false on both Enter and Exit.

### Dialogue system

`AGISDialogueNodeType` (`agis.dialogue`) — blackboard-based dialogue beat. On Enter: writes `choice_key = -1` (NoChoice) and `active_id = dialogue_id`. On Exit: removes `active_id`. Game code reads `agis.dialogue.active_id` to know which beat is active and writes an int index to `choice_key` when the player chooses.

`AGISDialogueOptionConditionType` (`agis.dialogue_option`) — true when `blackboard[choice_key] == option`. Params: `option (Int, 0)`, `choice_key (String, "agis.dialogue.choice")`.

`AGISHasDialogueChoiceConditionType` (`agis.has_dialogue_choice`) — true when `blackboard[choice_key] >= 0` (any choice made). Param: `choice_key`.

Constants in `AGISDialogueConstants`: `DefaultChoiceKey = "agis.dialogue.choice"`, `ActiveIdKey = "agis.dialogue.active_id"`, `NoChoice = -1`.

**Transition auto-management (`AGISDialogueEdgeSync`, `Assets/Scripts/Dialogue/`):**
Every `agis.dialogue` node must have exactly one of two outgoing transition layouts:
- **0 choices:** one `agis.has_dialogue_choice` edge (the "Dialogue Ended" transition)
- **N choices:** N `agis.dialogue_option` edges (option 0 … N-1), no ended edge

`AGISDialogueEdgeSync` is the single source of truth for building and modifying these edges. The graph editor must call it (not manipulate edges directly):
- `EnsureEndedEdge` — called by `AGISStateMachineGraphAsset.OnValidate` automatically; adds the ended edge when a dialogue node has no managed edges
- `AddChoice` / `RemoveLastChoice` — called by the graph editor's choice management UI
- `FindEndedEdge` / `FindChoiceEdges` — query helpers for the editor to read current state

**Unconnected edges:** All edges produced by `AGISDialogueEdgeSync` start with `toNodeId = AGISGuid.Empty` (the project convention for a dangling/unconnected transition). `AGISGraphCompiler` silently skips them. `AGISGraphValidator` reports them as warnings (`Graph.EdgeToUnconnected`), not errors. The graph editor should draw `!toNodeId.IsValid` edges as dangling arrows with an open tail that can be drag-connected to a target node.

### Editor tools (menu: AGIS/NPC/…)

- **Build Routed Movement Test Scene** — creates `Assets/NPC_Test/` assets + full scene (Ground, A* grid, WalkTarget, NPC_Test capsule with all components wired)
- **Create Routed Movement Asset** — saves a `RoutedMovement.asset` grouped state to a chosen path
