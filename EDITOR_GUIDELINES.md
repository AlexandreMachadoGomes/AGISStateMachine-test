# AGIS Runtime Editor — Architecture Guidelines

This document captures the architectural decisions and constraints that should guide the
implementation of the AGIS runtime editor. It is written now (before the editor exists)
so that decisions made during the design of the DB-driven pipeline are not lost.

---

## Vision

All NPC data is authored and stored in a backend database. A runtime editor UI reads that
data, allows editing, and applies it to live NPCs — entirely at runtime, with no
ScriptableObjects or prefabs required.

```
DB (JSON records)
    → AGISContentLibrary (in-memory cache, singleton)
    → Editor UI (reads, presents, allows editing)
    → "Compile & Apply" action (configures live NPC via AGISEnemyConfigurator or successor)
```

`AGISEnemyConfigurator` is the **current stopgap** for this final step. Its
`Configure()` / `Inject()` / `RebuildAllSlots()` logic is correct and reusable — only
the input changes from a `ScriptableObject` to a plain deserialized data class.

---

## Data That Lives in the DB

Every category below maps to a DB record type. Each must be serializable to/from JSON.

| Record type | Contents | Current stand-in |
|---|---|---|
| **State machine graph** | Nodes, edges, conditions, transition policies, per-node params | `AGISStateMachineGraphAsset` (ScriptableObject) |
| **Sub-graph (macro)** | Grouped state internal graph + param bindings | `AGISGroupedStateAsset` (ScriptableObject) |
| **Route data** | Named routes (each: ordered `Vector3` waypoints) + sequence list | `NPCRouteData` (ScriptableObject) |
| **Detection config** | `shape` (Sphere/Cone), `range`, `angle`, `detectionMask` | Fields on `NPCDetectionCone` (MonoBehaviour) |
| **Stealth meter config** | `fillRate`, `drainRate`, `maxDetection`, `investigateThreshold`, `targetTag` | Fields on `NPCDetectionMeter` (MonoBehaviour) |
| **NPC actor profile** | Which graphs go into which slots, route id, detection config, meter config | `AGISEnemyTemplateData` (ScriptableObject) |
| **Initial blackboard overrides** | Per-actor-type starting values for persistent keys | Hardcoded defaults in `IAGISPersistentNodeType.PersistentParams` |

---

## AGISContentLibrary — The In-Memory Store

`AGISContentLibrary` (singleton MonoBehaviour, `DontDestroyOnLoad`) is already the
correct home for DB-fetched data. It currently handles graphs, grouped assets, and
routes. It must be extended to also store:

- **Detection config records** (plain serializable C# class, keyed by DB id)
- **Stealth meter config records** (plain serializable C# class, keyed by DB id)
- **NPC actor profile records** (plain serializable C# class, keyed by DB id)

All records are imported as JSON via `Import*(dbId, json)` methods, following the
existing pattern. The editor UI queries the library — it never talks to the DB directly
after the initial load.

---

## Components vs. Data — The Core Rule

Several MonoBehaviours currently act as data holders (they exist only to hold config that
gets injected at spawn time). This pattern is not suitable for a DB-driven system because
it requires `AddComponent` + manual field assignment at runtime, which is no better than
reading from the source directly.

### Components that hold data but shouldn't need to

| Component | Problem | Resolution |
|---|---|---|
| `NPCRouteDataHolder` | Holds `NPCRouteData` reference | Route data comes from `AGISContentLibrary`; node types query a route service by ID stored on the blackboard |
| `NPCDetectionMeter` | Pure config holder; tick logic is in `NPCStealthMeterNodeType` | Config comes from the actor profile record; the component can be removed or reduced to just declaring persistent keys |
| `NPCDetectionCone` | Config fields (`range`, `angle`, `shape`, `mask`) + runtime behavior | Keep the component for runtime behavior (`IsDetected`, `DetectAll`, pursuit override); inject config from the actor profile record at spawn |

### Components that are correctly components

`NPCDetectionCone` runtime behavior, `AGISActorRuntime`, `AGISActorState`,
`AGISStateMachineRunner` — these hold live runtime state and belong on the actor.

---

## The NPC Actor Profile (Replacing AGISEnemyTemplateData)

`AGISEnemyTemplateData` is a ScriptableObject that bundles all per-NPC-type config. In
the DB-driven system it becomes a plain serializable C# class:

```csharp
[System.Serializable]
public sealed class AGISNPCProfileData
{
    public string profileId;           // DB record id
    public string displayName;

    // Slots: index → graph DB id
    public AGISSlotAssignment[] slots;

    // Referenced sub-graphs (by DB id) needed to resolve grouped nodes
    public string[] groupedAssetIds;

    // Route
    public string routeDataId;         // key into AGISContentLibrary routes

    // Detection
    public string detectionConfigId;   // key into detection config records

    // Stealth meter
    public string meterConfigId;       // key into meter config records

    // Optional: per-profile initial blackboard value overrides
    public AGISParamOverride[] initialBlackboardValues;
}
```

The editor creates and edits these records. The "Compile & Apply" action resolves all
referenced ids from `AGISContentLibrary` and calls the equivalent of
`AGISEnemyConfigurator.UpgradeEnemy`.

---

## ScriptableObject Wrappers — Future Removal

`AGISStateMachineGraphAsset` and `AGISGroupedStateAsset` are Unity ScriptableObjects that
wrap plain serializable structs (`AGISStateMachineGraph`, etc.). `AGISContentLibrary`
currently stores these wrapper objects after deserialization, created via
`ScriptableObject.CreateInstance`.

ScriptableObjects created at runtime have Unity-specific lifecycle implications (they live
in managed memory, appear in the profiler, must be destroyed explicitly). For a fully
runtime pipeline, consider storing the inner plain structs directly and bypassing the
ScriptableObject wrapper:

- `AGISContentLibrary` stores `AGISStateMachineGraph` (plain struct), not
  `AGISStateMachineGraphAsset`
- `AGISGraphCompiler` already accepts the plain graph — no change needed there
- `AGISStateMachineRunner.SetSlotGraphAsset` would need a companion overload that accepts
  the plain graph directly

This is not urgent. The current wrapper approach works. Revisit when the editor layer is
being built, before locking in the content library's public API.

---

## The "Compile & Apply" Action

This is what the editor triggers when the user hits "Apply to NPC" (or equivalent). It
replaces `AGISEnemyConfigurator.UpgradeEnemy`. Steps:

1. Resolve all record ids from `AGISContentLibrary` (graphs, grouped assets, route,
   detection config, meter config)
2. Call `AGISEnemyConfigurator.Configure(actor)` to ensure all required components exist
3. Push resolved config into components (detection cone fields, meter fields)
4. Push route id into a route service (not into `NPCRouteDataHolder` — see above)
5. Assign graphs to runner slots
6. Apply initial blackboard overrides (if any) to `AGISActorState` via `EnsureKey`
7. Call `runner.RebuildAllSlots()`

Steps 2–7 are essentially `AGISEnemyConfigurator.Inject` + `RebuildAllSlots`, extended to
cover detection/meter config and blackboard overrides. Refactor rather than replace.

---

## Initial Blackboard Value Overrides

Currently, persistent key defaults are hardcoded in `IAGISPersistentNodeType.PersistentParams`
(e.g. `npc.use_routes` defaults to `false`). These defaults are correct as system
defaults. However, a specific NPC profile may need to start with different values (e.g.
routes enabled by default for a patrol enemy).

The actor profile record should support an optional list of `(key, value)` overrides.
During "Compile & Apply", after `Configure()` runs (which calls `EnsureKey` for all
declared params), the overrides are applied via `AGISActorState.Set(key, value)` — this
overwrites the default without breaking the resume behavior for keys that are already set.

---

## What AGISEnemyConfigurator Is and Is Not

**Is:** A correct, working stopgap that wires up a live NPC from a template. Its
component-setup logic (`Configure`) and injection logic (`Inject`) are reusable.

**Is not:** The final architecture. It takes a `ScriptableObject` as input, which will
be replaced by a plain data class resolved from `AGISContentLibrary`.

Do not build the editor on top of `AGISEnemyConfigurator` as a permanent dependency.
Instead, treat its internals as a reference implementation for what the editor's
"Compile & Apply" step must do.
