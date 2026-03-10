# AGIS ESM — Editor Docs vs. Codebase Audit Findings
# File: EDITOR_AUDIT_FINDINGS.md
# Created: 2026-03-09
# Purpose: Cross-reference of all 94 C# scripts against EDITOR_DESIGN.md,
#          EDITOR_BUILD_PLAN.md, and EDITOR_UI_COMPONENTS.md.
#          Every discrepancy is tagged and a suggested fix is provided.

Tags:
  [MISSING-DOC]  Script/type/interface exists in code but not mentioned in any editor doc
  [WRONG-DOC]    A doc describes something incorrectly (wrong name, wrong API, wrong behavior)
  [STALE-DOC]    Doc references something that doesn't exist in the codebase
  [AMBIGUOUS]    Code and docs both exist but are hard to reconcile without more context

Docs checked:
  A = EDITOR_DESIGN.md
  B = EDITOR_BUILD_PLAN.md
  C = EDITOR_UI_COMPONENTS.md

---

## BATCH 1 — Compilation + Definitions (17 files)

---

### B1-1  [WRONG-DOC]  `runner.NodeTypes.All` — property name is wrong

**Affected docs:** B (Phase 8-B), B (Phase 12-E)
**File:** `Compilation/AGISNodeTypeRegistry.cs`, `Compilation/AGISConditionTypeRegistry.cs`

**What the code has:**
```csharp
// AGISNodeTypeRegistry
public IEnumerable<IAGISNodeType> AllTypes { get; }

// AGISConditionTypeRegistry
public IEnumerable<IAGISConditionType> AllTypes { get; }
```

**What the docs say:**
- Phase 8-B: "Populate list from `runner.NodeTypes.All` (or the editor's cached registry)."
- Phase 12-E: "populated from `runner.ConditionTypes.All`"
- EDITOR_DESIGN.md Section 8A correctly says `AGISNodeTypeRegistry.AllTypes` ✓

**Suggested fix for EDITOR_BUILD_PLAN.md:**
- Phase 8-B: change `runner.NodeTypes.All` → `runner.NodeTypes.AllTypes`
- Phase 12-E: change `runner.ConditionTypes.All` → `runner.ConditionTypes.AllTypes`

---

### B1-2  [WRONG-DOC]  `AGISStateMachineGraph.graphName` — field does not exist

**Affected docs:** B (Phase 10-A), C (Section 6C), A (Section 6C)
**File:** `Definitions/AGISGraphDefs.cs`

**What the code has:**
`AGISStateMachineGraph` fields: `graphId`, `version`, `entryNodeId`, `nodes`, `edges`.
There is **no `graphName` field**.

**What the docs say:**
- Phase 10-A: "Graph name field (TextField) — edits `AGISStateMachineGraph.graphName` *(add this field if missing)*"
  → Phase 10-A already acknowledges this with the "(add this field if missing)" caveat; no change needed there.
- EDITOR_UI_COMPONENTS.md Section 6C: "Asset name | Label (h2) | — | Graph name (`AGISStateMachineGraph.graphName` or content library key)"
  → No caveat given; implies the field already exists.
- EDITOR_DESIGN.md Section 6C: same reference, same issue.

**Suggested fix:**
In C Section 6C and A Section 6C, change:
> "Graph name (`AGISStateMachineGraph.graphName` or content library key)"

to:
> "Graph name (`AGISStateMachineGraph.graphName` — **field must be added in Phase 10-A** — or content library key as fallback)"

---

### B1-3  [MISSING-DOC]  `AGISParamResolver.IAGISParamAccessor` — interface not mentioned

**Affected docs:** A, B, C (none mention this interface by name)
**File:** `Compilation/AGISParamResolver.cs`

**What the code has:**
```csharp
public interface IAGISParamAccessor {
    AGISValue Get(string key, AGISValue fallback = default);
    bool GetBool(string key, bool fallback = default);
    int GetInt(string key, int fallback = default);
    float GetFloat(string key, float fallback = default);
    string GetString(string key, string fallback = "");
    Vector2 GetVector2(string key, Vector2 fallback = default);
    Vector3 GetVector3(string key, Vector3 fallback = default);
    AGISGuid GetGuid(string key, AGISGuid fallback = default);
}
```
Also: `static class AGISParamResolver` with `BuildAccessor()`, `TryResolve()`, `ResolveOrFallback()`, `Validate()`.
And: `readonly struct AGISResolvedParams : IAGISParamAccessor`.

**Impact for editor:** The Node Inspector param field drawer (Phase 4 / 11) needs `IAGISParamAccessor` and `AGISParamResolver.BuildAccessor()` to resolve current param values for display. The editor docs describe what params do but never name the accessor interface used to read them.

**Suggested fix:** Add a "Key runtime types" section to EDITOR_BUILD_PLAN.md Phase 4 noting that param values are read via `AGISParamResolver.BuildAccessor(schema, table)` returning an `IAGISParamAccessor`.

---

### B1-4  [MISSING-DOC]  `AGISGraphValidationReport.AGISValidationSeverity` — enum values not specified

**Affected docs:** B (Phase 15-A, 15-E)
**File:** `Compilation/AGISGraphValidationReport.cs`

**What the code has:**
```csharp
public enum AGISValidationSeverity { Info, Warning, Error }
```
`AGISGraphValidationReport.HasErrors` — true when any `Error`-severity issue exists (not `Warning`).
`AGISValidationIssue` has: `Severity`, `Code`, `Message`, `Path`, `NodeId`, `EdgeId`, `GroupAssetId`.

**What the docs say:**
Phase 15-E: "Save is blocked when any `ValidationSeverity.Error` exists." — correct intent but wrong enum name (`ValidationSeverity` vs. `AGISValidationSeverity`).
Phase 15-A: "Store results in a list `_validationResults`" — accurate enough.

**Suggested fix for Phase 15-E:** Change `ValidationSeverity.Error` → `AGISValidationSeverity.Error`. Also note `AGISGraphValidationReport.HasErrors` as the check property rather than LINQ on the list.

---

### B1-5  [AMBIGUOUS]  `AGISConditionExprDef` — `ExprKind.ConstBool` used for both True and False

**Affected docs:** A (Section 6B), B (Phase 12-D), C (Section 6B)
**File:** `Definitions/AGISConditionDefs.cs`

**What the code has:**
`ExprKind { And, Or, Not, Leaf, ConstBool }` — only one ConstBool kind with a `constValue` bool field.

**What the docs say:**
The docs consistently describe "CONST TRUE" and "CONST FALSE" as separate tree node types with different UI treatments (green vs. grey). The underlying data model uses `ExprKind.ConstBool` with `constValue = true/false`.

**Resolution:** No doc change required — the distinction is UI-only. The editor renders one ConstBool in two ways based on `constValue`. Just note in implementation that the backing kind for both True and False is `ExprKind.ConstBool`, not two separate enum values.

---

## BATCH 2 — Execution (19 files)

---

### B2-1  [STALE-DOC]  Phase 11-F says `IAGISNodeComponentRequirements` doesn't exist — it does

**Affected docs:** B (Phase 11-F)
**File:** `Execution/IAGISNodeComponentRequirements.cs`

**What the code has:**
```csharp
// Namespace: AGIS.ESM.Runtime
public interface IAGISNodeComponentRequirements {
    IReadOnlyList<Type> GetRequiredComponents(IAGISParamAccessor resolvedParams);
}
```
Already implemented by: `NPCDyingNodeType`, `NPCFollowTargetNodeType`, `NPCMoveToWaypointNodeType`,
`NPCWanderNodeType`, `NPCInvestigateNodeType`, `NPCTakeDamageNodeType`.
Also: `AGISActorComponentFixer.EnsureComponents(GameObject, IReadOnlyList<Type>)` exists to enforce them.

**What the docs say:**
Phase 11-F: "*(Reserved) — "Required components" display; deferred until `IAGISNodeComponentRequirements` is added to the ESM core architecture.*"

**Suggested fix for Phase 11-F:**
Replace the entire step with:
```
- [ ] **11-F** **Required components display** — when the selected node's type implements
  `IAGISNodeComponentRequirements`, display the list returned by
  `nodeType.GetRequiredComponents(resolvedParams)` in the Node Inspector as a read-only
  info section ("Requires on actor: IAGISNPCPathFinder, NPCDetectionCone"). Use
  `AGISActorComponentFixer.EnsureComponents(actor, requiredTypes)` if the editor
  offers an "Auto-add components" button.
```

---

### B2-2  [WRONG-DOC]  Phase 22-B uses `runner.GetSlot()` — no such method exists

**Affected docs:** B (Phase 22-B)
**File:** `Execution/AGISStateMachineRunner.cs`

**What the code has:**
```csharp
public IReadOnlyList<AGISStateMachineSlot> Slots { get; }
```
There is **no `GetSlot(int)` method**. Access is via `runner.Slots[slotIndex]`.

**What the docs say:**
Phase 22-B: "read `runner.GetSlot(slotIndex).CurrentNodeId`"

**Suggested fix:**
Change to `runner.Slots[slotIndex].CurrentNodeId` (note: `CurrentNodeId` itself must be added to `AGISStateMachineSlot` per Phase 0-A first).

---

### B2-3  [WRONG-DOC]  Phase 0-A gap: `AGISStateMachineSlot` has neither `CurrentNodeId` nor `Instance` nor `LastTransitionEdgeId`

**Affected docs:** B (Phase 0-A)
**File:** `Execution/AGISStateMachineSlot.cs`, `Execution/AGISStateMachineInstance.cs`

**What the code has:**
`AGISStateMachineSlot` public members (current): `slotName`, `enabled`, `graphAsset`,
`tickHzOverride`, `maxTransitionsPerTickOverride`, `GetGraphDef()`, `GetTickHz()`, `GetMaxTransitionsPerTick()`.
**None of `CurrentNodeId`, `Instance`, or `LastTransitionEdgeId` exist on the slot.**

`AGISStateMachineInstance` DOES have `CurrentNodeId` and `CurrentNodeIndex` as read-only properties.

**What the docs say:**
Phase 0-A correctly lists all three as needing to be added — this finding **confirms** Phase 0-A is accurate and complete. No doc change needed; this is a verification that the gap is real.

**Additional note for Phase 0-A:** The `Instance` property should be typed as:
```csharp
public AGISStateMachineInstance Instance => instance; // (where instance is the private field)
```
This allows `runner.Slots[slotIndex].Instance?.CurrentNodeId` patterns in the editor.

---

### B2-4  [MISSING-DOC]  `IAGISPersistentComponent` — backwards-compat alias not mentioned

**Affected docs:** A, B, C (none mention this)
**File:** `Execution/IAGISPersistentComponent.cs`

**What the code has:**
```csharp
// Namespace: AGIS.ESM.Runtime
public interface IAGISPersistentComponent : IAGISPersistentNodeType { }
// XML doc: "Backwards-compatibility alias — implement IAGISPersistentNodeType instead."
```

**Impact:** Low — the editor code should use `IAGISPersistentNodeType` throughout (per existing docs). But during the `IAGISPersistentNodeType` component scan in `AGISStateMachineRunner`, components implementing `IAGISPersistentComponent` will also be caught (since it inherits `IAGISPersistentNodeType`). No editor bug, but worth a note.

**Suggested fix:** Add a footnote to EDITOR_BUILD_PLAN.md Phase 0 or Phase 13: "`IAGISPersistentComponent` is a backwards-compat alias for `IAGISPersistentNodeType`; both are found by the runner's component scan."

---

### B2-5  [MISSING-DOC]  `AGISDebugTrace` — serializable debug logging class not mentioned

**Affected docs:** A, B, C (none mention this)
**File:** `Execution/AGISDebugTrace.cs`

**What the code has:**
```csharp
[Serializable] public sealed class AGISDebugTrace {
    public bool enabled;   // master switch (default: false)
    public bool info;      // info logging level (default: false)
    public bool warnings;  // warning logging level (default: true)
    public bool errors;    // error logging level (default: true)
    public string prefix;  // log prefix (default: "[AGIS_ESM]")
    public void Info(string msg);
    public void Warn(string msg);
    public void Error(string msg);
}
```
`AGISStateMachineRunner` has a `trace` inspector field of this type. It is passed through to `AGISNodeRuntimeFactory` and `AGISStateMachineInstance`.

**Impact for editor:** The editor's `AGISGraphEditorWindow` or `AGISConditionSummary` may want to log using a similar pattern. Low priority but the pattern exists.

**Suggested fix:** No doc change strictly required; however, if the editor creates its own logging helper in Phase 0-B/0-C, it should follow the same `AGISDebugTrace` pattern (or reuse the runner's `trace` field).

---

### B2-6  [MISSING-DOC]  `AGISGraphFingerprint` — graph hash utility not mentioned

**Affected docs:** A, B, C (none mention this)
**File:** `Execution/AGISGraphFingerprint.cs`

**What the code has:**
```csharp
public static class AGISGraphFingerprint {
    public static ulong Compute(AGISStateMachineGraph graph);
    // FNV-1a 64-bit; returns 0UL if graph is null.
}
```
Used internally by `AGISRuntimeGraphCache` for cache invalidation.

**Impact for editor:** If the editor's save/apply pipeline needs to detect whether `_workingGraph` has changed since last save, `AGISGraphFingerprint.Compute()` is available. Currently the editor docs use `AGISEditorHistory.CanUndo` as the dirty signal instead — both approaches are valid.

**Suggested fix:** Low priority. Add a note to EDITOR_BUILD_PLAN.md Phase 14-E footnote: "Alternatively, `AGISGraphFingerprint.Compute(_workingGraph)` can be used to detect mutations without the history stack."

---

### B2-7  [MISSING-DOC]  `AGISActorComponentFixer` — component-enforcer utility not mentioned

**Affected docs:** A, B, C (none mention this)
**File:** `Execution/AGISActorComponentFixer.cs`

**What the code has:**
```csharp
public static class AGISActorComponentFixer {
    public static void EnsureComponents(GameObject actor, IReadOnlyList<Type> requiredTypes);
    // Adds any missing components to actor. Logs each addition.
}
```

**Impact for editor:** Directly supports Phase 11-F (now that `IAGISNodeComponentRequirements` is confirmed to exist). The Node Inspector can show required components AND offer an "Auto-add" button that calls `AGISActorComponentFixer.EnsureComponents(runner.gameObject, requiredTypes)`.

**Suggested fix:** Reference in Phase 11-F (see B2-1 above).

---

### B2-8  [MISSING-DOC]  `IAGISInterruptibility` — runtime interface on node runtimes not mentioned

**Affected docs:** A, B, C (none mention this)
**File:** `Execution/AGISTransitionPolicyRuntime.cs`

**What the code has:**
```csharp
public interface IAGISInterruptibility {
    bool IsInterruptible { get; }
}
```
Used by `AGISTransitionPolicyRuntime.CanFire()` to check runtime interruptibility in addition to the static `policy.interruptible` field.

**Impact for editor:** Low. The Edge Inspector shows `interruptible` as a toggle on the policy, which is the static design-time setting. The runtime `IAGISInterruptibility` on node runtimes is separate and does not need to be edited. No doc change needed.

---

### B2-9  [AMBIGUOUS]  `AGISStateMachineRunner` public save API — `SetSlotGraphAsset` vs `ApplyGraphToRunner`

**Affected docs:** B (Phase 14-C step 4), A (Technology Stack)
**File:** `Execution/AGISStateMachineRunner.cs`

**What the code has:**
```csharp
public bool SetSlotGraphAsset(int slotIndex, AGISStateMachineGraphAsset graphAsset, bool rebuild = true);
public void RebuildAllSlots();
public void RebuildSlot(AGISStateMachineSlot slot);
```

**What `AGISContentLibrary.ApplyGraphToRunner()` does:**
It calls `runner.SetSlotGraphAsset(slotIndex, asset)` and then `runner.RebuildAllSlots()` internally. So Phase 14-C step 4 (`AGISContentLibrary.Instance.ApplyGraphToRunner(_dbId, runner, slotIndex)`) is a valid shorthand.

**No doc change needed** — both paths work; `ApplyGraphToRunner` is the recommended one. This is noted for implementation awareness.

---

## BATCH 3 — Hierarchical + Serialization (10 files)

---

### B3-1  [WRONG-DOC]  Phase 1-B says clone graph via "serialize → deserialize" — `AGISGraphClone` exists

**Affected docs:** B (Phase 1-B)
**File:** `Hierarchical/AGISGraphClone.cs`

**What the code has:**
```csharp
public static class AGISGraphClone {
    public static AGISStateMachineGraph CloneGraph(AGISStateMachineGraph src);
    public static AGISNodeInstanceDef CloneNode(AGISNodeInstanceDef src);
    public static AGISTransitionEdgeDef CloneEdge(AGISTransitionEdgeDef src);
    public static AGISTransitionPolicy ClonePolicy(AGISTransitionPolicy src);
    public static AGISConditionExprDef CloneConditionExpr(AGISConditionExprDef src);
    public static AGISConditionInstanceDef CloneConditionLeaf(AGISConditionInstanceDef src);
    public static AGISParamTable CloneParamTable(AGISParamTable src);
}
```
`AGISGroupedNodeRuntime` already uses `AGISGraphClone.CloneGraph()` internally for deep-copying.

**What the docs say:**
Phase 1-B: "Clone the current slot's graph *(deep-copy via serialize → deserialize)*"

**Impact:** The serialize→deserialize approach (`ToJson` + `GraphFromJson`) works but is slower than `AGISGraphClone.CloneGraph()`. The correct high-performance path is to call `AGISGraphClone.CloneGraph(runner.Slots[slotIndex].GetGraphDef())`.

**Suggested fix for Phase 1-B:**
Change: "*(deep-copy via serialize → deserialize)*"
To: "*(deep-copy via `AGISGraphClone.CloneGraph(slot.GetGraphDef())` — DO NOT use serialize→deserialize; `AGISGraphClone` is faster and already used by the grouped node runtime)*"

---

### B3-2  [MISSING-DOC]  `AGISBindingChangeDetector` — param fingerprinting utility not mentioned

**Affected docs:** A, B, C (none mention this)
**File:** `Hierarchical/AGISBindingChangeDetector.cs`

**What the code has:**
```csharp
public static class AGISBindingChangeDetector {
    public static ulong FingerprintParamTable(AGISParamTable table);
    // FNV-1a 64-bit; used by AGISGroupedNodeRuntime to detect exposedOverrides changes.
}
```

**Impact for editor:** Low — internal to grouped node runtime. Not needed directly by the editor.

---

### B3-3  [MISSING-DOC]  `AGISParamTargetApplier` — binding application utility not mentioned

**Affected docs:** A, B, C (none mention this)
**File:** `Hierarchical/AGISParamTargetApplier.cs`

**What the code has:**
```csharp
public sealed class AGISParamTargetApplier {
    public bool ApplyToTarget(AGISStateMachineGraph internalGraphClone, AGISParamTarget target, in AGISValue value);
}
```

**Impact for editor:** Low — internal to grouped node runtime. Not needed directly by the editor.

---

### B3-4  [MISSING-DOC]  `AGISGroupedParamBinder` — binding orchestration class not mentioned

**Affected docs:** A, B, C (none mention this)
**File:** `Hierarchical/AGISGroupedParamBinder.cs`

**What the code has:**
```csharp
public sealed class AGISGroupedParamBinder {
    public void ApplyBindings(AGISGroupedStateAsset groupAsset, AGISNodeInstanceDef groupedNodeInstanceDef, AGISStateMachineGraph internalGraphClone);
}
```

**Impact for editor:** Low — internal to grouped node runtime. Not needed directly by the editor unless the Grouped Asset Inspector (Tab D) offers a live "preview bindings" feature.

---

### B3-5  [AMBIGUOUS]  `AGISGroupedStateAsset.internalEntryNodeId` — legacy field still present

**Affected docs:** A (Section 6D), B (Phase 17), C (Section 6D)
**File:** `Definitions/AGISGroupedStateAsset.cs`

**What the code has:**
```csharp
[Obsolete] public AGISGuid internalEntryNodeId; // LEGACY; use internalGraph.entryNodeId
public AGISGuid EntryNodeId { get; } // returns internalGraph.entryNodeId
```
`OnValidate()` syncs the two fields.

**What the docs say:**
The editor docs consistently reference `internalGraph.entryNodeId` — which is correct. However, they never mention that `internalEntryNodeId` exists and that both must be kept in sync.

**Suggested fix:** Low priority. Add a note to Phase 17-E: "Do NOT write `internalEntryNodeId` directly — use `internalGraph.entryNodeId` and let `OnValidate` (or editor code) sync the legacy field."

---

### B3-6  [MISSING-DOC]  `AGISContentLibrary` — `ImportGrouped`, `ImportRoute`, `TryGetGrouped`, `TryGetRoute`, `RemoveGraph`, `Clear` etc. not described

**Affected docs:** A (Technology Stack), B (Phase 14), C (Section 6C)
**File:** `Serialization/AGISContentLibrary.cs`

**What the code has (full public API):**
- `ImportGraph(string dbId, string json)` — documented ✓
- `ImportGrouped(string dbId, string json)` — **not mentioned**
- `ImportRoute(string dbId, string json)` — **not mentioned**
- `RemoveGraph(string dbId)`, `RemoveGrouped(string dbId)`, `RemoveRoute(string dbId)` — **not mentioned**
- `Clear()` — **not mentioned**
- `TryGetGraph(string dbId, out AGISStateMachineGraphAsset)` — **not mentioned**
- `TryGetGrouped(string dbId, out AGISGroupedStateAsset)` — **not mentioned**
- `TryGetRoute(string dbId, out NPCRouteData)` — **not mentioned**
- `ApplyGraphToRunner(string dbId, AGISStateMachineRunner runner, int slotIndex = 0)` — documented ✓
- `ApplyRouteToActor(string dbId, GameObject actor)` — **not mentioned**
- `GraphIds`, `GroupedIds`, `RouteIds` (IReadOnlyCollection<string>) — **not mentioned**

**Impact for editor:** The editor's "Import JSON" (Phase 10-G) and "Export JSON" (Phase 10-F) flows use `AGISContentLibrary.ImportGraph`. These are documented. The other methods are less relevant to the editor but should be noted for completeness.

**Suggested fix:** No urgent doc change. Optional: add a "Full `AGISContentLibrary` API" reference block to EDITOR_BUILD_PLAN.md Phase 14.

---

### B3-7  [AMBIGUOUS]  `AGISGraphSerializer.ToJson` — second param (`prettyPrint`) not shown in docs

**Affected docs:** B (Phase 14-C step 2, Phase 0-B)
**File:** `Serialization/AGISGraphSerializer.cs`

**What the code has:**
```csharp
public static string ToJson(AGISStateMachineGraph graph, bool prettyPrint = false);
public static AGISStateMachineGraphAsset GraphFromJson(string json);
```

**What the docs say:**
Phase 14-C step 2: "`json = AGISGraphSerializer.ToJson(_workingGraph)`" — correct, `prettyPrint` defaults to false.
Phase 10-F: Export → "`System.IO.File.WriteAllText`" — docs don't specify `prettyPrint = true` for export, but human-readable JSON is preferable for files.

**Suggested fix for Phase 10-F:** Note that the Export JSON flow should call `AGISGraphSerializer.ToJson(_workingGraph, prettyPrint: true)` for readability.

---

## BATCH 4 — Dialogue (5 files)

---

### B4-1  [WRONG-DOC]  `AGISDialogueEdgeSync.EnsureEndedEdge` — wrong parameter types and missing `choiceKey`

**Affected docs:** B (Phase 0-H), B (Phase 1-B)
**File:** `Dialogue/AGISDialogueEdgeSync.cs`

**What the code has:**
```csharp
public static void EnsureEndedEdge(AGISStateMachineGraph graph, AGISGuid nodeId, string choiceKey);
```

**What the docs say:**
- Phase 0-H: "call `AGISDialogueEdgeSync.EnsureEndedEdge(graph, node)` for every `agis.dialogue` node"
- Phase 1-B: "call `AGISDialogueEdgeSync.EnsureEndedEdge(_workingGraph, node)` for every node whose `typeId == "agis.dialogue"`"

**Two errors:**
1. The second parameter is `AGISGuid nodeId`, not the full `AGISNodeInstanceDef node`. Must pass `node.nodeId`.
2. A third parameter `string choiceKey` is required. The default choice key value is `AGISDialogueConstants.DefaultChoiceKey` = `"agis.dialogue.choice"`.

**Suggested fix for Phase 0-H and Phase 1-B:**
Change all occurrences to:
```
AGISDialogueEdgeSync.EnsureEndedEdge(_workingGraph, node.nodeId, AGISDialogueConstants.DefaultChoiceKey)
```
(Note: the actual `choiceKey` should come from the node's `params.Get("choice_key")` override if set, or fall back to `DefaultChoiceKey`.)

---

### B4-2  [WRONG-DOC]  `AGISDialogueEdgeSync.FindEndedEdge` / `FindChoiceEdges` — wrong parameter type

**Affected docs:** B (Phase 5-F)
**File:** `Dialogue/AGISDialogueEdgeSync.cs`

**What the code has:**
```csharp
public static AGISTransitionEdgeDef FindEndedEdge(AGISStateMachineGraph graph, AGISGuid nodeId);
public static List<(AGISTransitionEdgeDef edge, int option)> FindChoiceEdges(AGISStateMachineGraph graph, AGISGuid nodeId);
```

**What the docs say:**
Phase 5-F: "use `AGISDialogueEdgeSync.FindEndedEdge(_workingGraph, node)` ... and `AGISDialogueEdgeSync.FindChoiceEdges(_workingGraph, node)`"

**Error:** Second parameter is `AGISGuid nodeId`, not `AGISNodeInstanceDef node`. Must pass `node.nodeId`.

Also note: `FindChoiceEdges` returns `List<(AGISTransitionEdgeDef edge, int option)>` — a list of named tuples, not just edges. The editor can use `.edge` and `.option` fields from each tuple.

**Suggested fix for Phase 5-F:**
Change to `AGISDialogueEdgeSync.FindEndedEdge(_workingGraph, node.nodeId)` and `AGISDialogueEdgeSync.FindChoiceEdges(_workingGraph, node.nodeId)`.

---

### B4-3  [WRONG-DOC]  `AGISDialogueEdgeSync.AddChoice` / `RemoveLastChoice` — missing `choiceKey` param

**Affected docs:** A (Section 4A-i), C (Section 5B Node Card Variants)
**File:** `Dialogue/AGISDialogueEdgeSync.cs`

**What the code has:**
```csharp
public static void AddChoice(AGISStateMachineGraph graph, AGISGuid nodeId, string choiceKey);
public static void RemoveLastChoice(AGISStateMachineGraph graph, AGISGuid nodeId, string choiceKey);
```

**What the docs say:**
EDITOR_DESIGN.md Section 4A-i: "[+ Add Choice] Calls `AGISDialogueEdgeSync.AddChoice()`"
EDITOR_UI_COMPONENTS.md Section 5B: "[+ Add Choice] Add a choice edge (calls AGISDialogueEdgeSync)"

These omit the third `choiceKey` parameter. When calling from the editor, the key must be read from the node's params:
```csharp
var choiceKey = node.@params.TryGet("choice_key", out var v) ? v.AsString() : AGISDialogueConstants.DefaultChoiceKey;
AGISDialogueEdgeSync.AddChoice(_workingGraph, node.nodeId, choiceKey);
```

**Suggested fix:** Update all call-site descriptions in EDITOR_DESIGN.md and EDITOR_UI_COMPONENTS.md to reflect the three-parameter signature and the need to resolve `choiceKey` from the node's params.

---

### B4-4  [MISSING-DOC]  `AGISDialogueNodeType` — `loop (Bool, false)` schema param not mentioned

**Affected docs:** A (Section 4A-i, 6A), B (Phase 5-F), C (Section 5B), CLAUDE.md
**File:** `Dialogue/AGISDialogueNodeType.cs`

**What the code has:**
Schema params:
- `dialogue_id (String, "")` — documented ✓
- `choice_key (String, "agis.dialogue.choice")` — documented ✓
- `loop (Bool, false)` — **NOT documented anywhere**

**Behavior of `loop`:** When `true`, the dialogue node clears any existing choice each tick, preventing outgoing `agis.dialogue_option` edges from firing. This keeps the node active indefinitely for repeating terminal beats.

**Suggested fix:** Add `loop (Bool, false)` to the dialogue node schema table in EDITOR_DESIGN.md Section 4A-i and CLAUDE.md's dialogue section. Note the "suppress outgoing transitions while looping" behavior.

---

### B4-5  [MISSING-DOC]  `AGISDialogueEdgeSync` — `IsEndedEdge` and `TryGetChoiceOption` helpers undocumented

**Affected docs:** A, B, C (none mention these)
**File:** `Dialogue/AGISDialogueEdgeSync.cs`

**What the code has:**
```csharp
public static bool IsEndedEdge(AGISTransitionEdgeDef edge);
public static bool TryGetChoiceOption(AGISTransitionEdgeDef edge, out int option);
```

**Impact for editor:** These are useful for the Edge Inspector (Phase 12) to determine whether a selected edge is a managed dialogue edge and what kind (ended vs. choice). The editor can use them to show/hide dialogue-specific controls.

**Suggested fix:** Mention in EDITOR_BUILD_PLAN.md Phase 12-G: "Use `AGISDialogueEdgeSync.IsEndedEdge(edge)` and `AGISDialogueEdgeSync.TryGetChoiceOption(edge, out option)` to identify managed dialogue edges and route deletion through `AGISDialogueEdgeSync` instead of direct graph mutation."

---

## BATCH 5 — NPC Core + Routes (9 files)

---

### B5-1  [MISSING-DOC]  `NPCDetectionMeter` declares `npc.last_known_target_pos` — not in CLAUDE.md key table

**Affected docs:** CLAUDE.md (AGISActorState keys table), A, B, C (none mention this key)
**File:** `NPC/NPCDetectionMeter.cs`

**What the code has:**
`NPCDetectionMeter` implements `IAGISPersistentNodeType` and declares:
- `npc.detection_meter (Float, 0)` — current suspicion level [0, maxDetection]
- `npc.last_known_target_pos (Vector3, zero)` — last world-space position where target was detected

CLAUDE.md's key table lists `npc.detection_meter` implicitly (via `NPCDetectionMeterConditionType` usage) but **does not list `npc.last_known_target_pos`** at all.

**Also note:** `NPCStealthMeterNodeType` (batch 6) declares the **same two keys** as its PersistentParams, meaning these keys are registered by both the `NPCDetectionMeter` component and the `NPCStealthMeterNodeType` node type. `AGISActorState.EnsureKey` only adds if absent, so no conflict — but it's redundant.

**Suggested fix:** Add to CLAUDE.md AGISActorState key table:
| `npc.detection_meter` | Float | 0 | `NPCDetectionMeter` + `NPCStealthMeterNodeType` |
| `npc.last_known_target_pos` | Vector3 | zero | `NPCDetectionMeter` + `NPCStealthMeterNodeType` |

---

### B5-2  [MISSING-DOC]  `AGISEnemyTemplateData` has a `stealthGraph` field (slot 0) — not in CLAUDE.md

**Affected docs:** CLAUDE.md, A, B, C
**File:** `NPC/AGISEnemyTemplateData.cs`

**What the code has:**
```csharp
public AGISStateMachineGraphAsset stealthGraph;   // slot index 0, slot name "Stealth"
public AGISStateMachineGraphAsset patrolGraph;    // slot index 1, slot name "Patrol"
public AGISGroupedStateAsset[] knownGroupedAssets;
public NPCRouteData routeData;
// + detection meter config fields, detection cone config
```
The template wires TWO `AGISStateMachineSlot`s: a "Stealth" slot (slot 0) running `NPCStealthMeterNodeType` forever, and a "Patrol" slot (slot 1) running the patrol/wander behavior graph.

**What CLAUDE.md says:**
The template enemy graph description shows only a single graph structure (the outer BehaviorSelector → [RoutedMovement | Wander]) without mentioning the Stealth slot.

**Suggested fix for CLAUDE.md:** Add a "Template enemy — two slots" note:
- Slot 0 "Stealth": `stealthGraph` — single-node graph running `NPCStealthMeterNodeType` permanently; fills/drains detection meter
- Slot 1 "Patrol": `patrolGraph` — outer behavior selector graph (BehaviorSelector → [RoutedMovement | Wander])

---

### B5-3  [MISSING-DOC]  `NPCDetectionMeter` is a config-holder only — tick logic is in `NPCStealthMeterNodeType`

**Affected docs:** CLAUDE.md
**File:** `NPC/NPCDetectionMeter.cs`

**What the code has:**
`NPCDetectionMeter` is a config holder with fields (`fillRate`, `drainRate`, `maxDetection`, `investigateThreshold`, `targetTag`) and no `Update()`/`Tick()` logic. All tick logic lives in `NPCStealthMeterNodeType` (see Batch 6).

**What CLAUDE.md says:**
"`NPCDetectionMeter.cs` — config holder for detection meter; declares persistent keys" — **CORRECT** ✓
The architecture note is right. No change needed.

---

## BATCH 6 — NPC States (11 files)

---

### B6-1  [MISSING-DOC]  `NPCInvestigateNodeType` (`npc.investigate`) — not in CLAUDE.md or any editor doc

**Affected docs:** CLAUDE.md, A, B, C
**File:** `NPC/States/NPCInvestigateNodeType.cs`

**What the code has:**
```
TypeId:       "npc.investigate"
DisplayName:  "NPC Investigate"
Kind:         Normal
Schema:       look_radius (Float, 2.0)    [reserved for future use]
              look_count (Int, 3)         [number of random Y-rotation targets]
              look_duration (Float, 1.5)  [seconds per look direction]
              rotation_speed (Float, 90)  [degrees/sec]
              arrival_distance (Float, 1.0)
Interfaces:   IAGISNodeType, IAGISNodeComponentRequirements
Runtime:      implements IAGISNodeSignal (IsComplete)
Required:     IAGISNPCPathFinder
Behavior:     Moves to npc.last_known_target_pos, then performs look_count random
              Y-rotation looks for look_duration each. Signals IsComplete when done.
```

**Suggested fix:**
1. Add to CLAUDE.md `States/` directory listing.
2. Add to CLAUDE.md AGISActorState key table note: `npc.last_known_target_pos` is READ (not written) by this node.
3. Add to EDITOR_DESIGN.md Section 8A node search window list.
4. Add to EDITOR_DESIGN.md Section 8B condition search window if any companion condition uses it.

---

### B6-2  [MISSING-DOC]  `NPCStealthMeterNodeType` (`npc.stealth_meter`) — not in CLAUDE.md or any editor doc

**Affected docs:** CLAUDE.md, A, B, C
**File:** `NPC/States/NPCStealthMeterNodeType.cs`

**What the code has:**
```
TypeId:       "npc.stealth_meter"
DisplayName:  "NPC Stealth Meter"
Kind:         Normal
Schema:       (no params — all config on NPCDetectionMeter component)
PersistentParams:
              npc.detection_meter (Float, 0)
              npc.last_known_target_pos (Vector3, zero)
Interfaces:   IAGISNodeType, IAGISPersistentNodeType
Behavior:     Runs forever as sole node in the "Stealth" slot. Each Tick: if
              NPCDetectionCone detects the target tag → fill meter by fillRate*dt;
              otherwise drain by drainRate*dt. Clamps to [0, maxDetection].
              Writes last known position while target is detected.
```

**Suggested fix:**
1. Add to CLAUDE.md under `States/` directory listing.
2. Add a "Stealth slot" section to CLAUDE.md's Template Enemy Graph description.
3. Add to EDITOR_DESIGN.md Section 8A node search window list.

---

### B6-3  [WRONG-DOC]  `NPCTakeDamageNodeType` schema — CLAUDE.md has wrong params

**Affected docs:** CLAUDE.md
**File:** `NPC/States/NPCTakeDamageNodeType.cs`

**What the code HAS:**
```
Schema: damage_flag_key (String, "npc.is_damaged")
```
Runtime: UCC ability-based (`#if OPSIVE_UCC`). Clears `damage_flag_key` blackboard entry on Enter, starts Die UCC ability, polls completion, implements `IAGISNodeSignal`.

**What CLAUDE.md says:**
"Params: `animation_trigger (String, "TakeDamage")`, `animation_state (String, "TakeDamage")`, `layer (Int, 0)`. ... fires an Animator trigger on Enter, then polls `GetCurrentAnimatorStateInfo` in Tick."

**The implementation changed from Animator-based to UCC ability-based.** The three animation params (`animation_trigger`, `animation_state`, `layer`) no longer exist. The Animator polling logic is gone.

**Suggested fix for CLAUDE.md:** Replace the `NPCTakeDamageNodeType` description with:
"Params: `damage_flag_key (String, "npc.is_damaged")` — blackboard key cleared on Enter. UCC ability-based (`#if OPSIVE_UCC`); fires TakeDamage UCC ability, polls completion via `IAGISNodeSignal`. Shell (NoOp) otherwise."

---

### B6-4  [MISSING-DOC]  `NPCDyingNodeType` schema and persistent params — not described in CLAUDE.md

**Affected docs:** CLAUDE.md
**File:** `NPC/States/NPCDyingNodeType.cs`

**What the code has:**
```
Schema:       death_flag_key (String, "npc.is_dead")   — written true on Enter
              damage_flag_key (String, "npc.is_damaged") — cleared on Enter
PersistentParams:
              npc.is_dead (Bool, false)
Interfaces:   IAGISNodeType, IAGISPersistentNodeType, IAGISNodeComponentRequirements
Required:     UltimateCharacterLocomotion (when OPSIVE_UCC defined)
```

**What CLAUDE.md says:**
Only "UCC ability-based (#if OPSIVE_UCC); shell (NoOp) otherwise" — no schema, no persistent params.

**Two keys missing from CLAUDE.md's AGISActorState key table:**
- `npc.is_dead (Bool, false)` — owner: `NPCDyingNodeType`
- `npc.is_damaged (Bool, false)` — NOT declared by any PersistentParams (it is a blackboard key used transiently by `NPCTakeDamageNodeType` and `NPCDyingNodeType`)

**Suggested fix for CLAUDE.md:**
Add `npc.is_dead` to the AGISActorState key table.
Add schema details to `NPCDyingNodeType.cs` entry in the States/ directory listing.

---

### B6-5  [AMBIGUOUS]  `NPCFollowTargetNodeType` schema — `use_detection_memory`, `pursuit_range_bonus`, `pursuit_angle_bonus` not surfaced in editor docs

**Affected docs:** A (Section 6A Node Inspector mockup shows these), B, C
**File:** `NPC/States/NPCFollowTargetNodeType.cs`

**What the code has:**
Full schema: `follow_player (Bool, true)`, `target_key (String, "")`, `use_detection_memory (Bool, false)`, `pursuit_range_bonus (Float, 0)`, `pursuit_angle_bonus (Float, 0)`.

**What the docs say:**
EDITOR_DESIGN.md Section 6A Node Inspector mockup correctly shows all five params ✓.
No further doc issue — this finding **confirms** the mockup is accurate.

---

## BATCH 7 — NPC Conditions (10 files)

---

### B7-1  [MISSING-DOC]  `NPCHasLostTargetConditionType` (`npc.has_lost_target`) — not mentioned anywhere

**Affected docs:** CLAUDE.md, A, B, C
**File:** `NPC/Conditions/NPCHasLostTargetConditionType.cs`

**What the code has:**
```
TypeId:       "npc.has_lost_target"
DisplayName:  "NPC Has Lost Target"
Schema:       timeout (Float, 3.0)
Behavior:     True when npc.target_time_lost >= timeout.
              Reads npc.target_time_lost (Float) from AGISActorState.
              Requires NPCFollowTargetNodeType with use_detection_memory = true to write the key.
```

**Suggested fix:**
1. Add to CLAUDE.md `Conditions/` directory listing.
2. Add to EDITOR_DESIGN.md Section 8B condition search window list.
3. Add to CLAUDE.md conditions description.

---

### B7-2  [MISSING-DOC]  `NPCDetectionMeterConditionType` (`npc.detection_meter_exceeds`) — partially mentioned in editor docs but not CLAUDE.md

**Affected docs:** CLAUDE.md (missing), A (Section 8B mentions TypeId), B (Phase 6 condition inspector example uses it)
**File:** `NPC/Conditions/NPCDetectionMeterConditionType.cs`

**What the code has:**
```
TypeId:       "npc.detection_meter_exceeds"
DisplayName:  "NPC Detection Meter Exceeds"
Schema:       threshold (Float, 1.0)
              use_max_detection (Bool, false)
Behavior:     True when npc.detection_meter >= threshold.
              If use_max_detection = true, reads maxDetection from NPCDetectionMeter component
              instead of the threshold param.
```

**What CLAUDE.md says:** Does not list `NPCDetectionMeterConditionType` in the Conditions section.

**Suggested fix for CLAUDE.md:** Add entry:
"NPCDetectionMeterConditionType.cs — condition: true when `npc.detection_meter` ≥ threshold (or `NPCDetectionMeter.maxDetection` when `use_max_detection = true`)."

---

### B7-3  [MISSING-DOC]  `NPCIsMovingConditionType` — has `threshold` param not documented in CLAUDE.md

**Affected docs:** CLAUDE.md
**File:** `NPC/Conditions/NPCIsMovingConditionType.cs`

**What the code has:**
`Schema: threshold (Float, 0.1)` — minimum speed in units/sec.

**What CLAUDE.md says:**
Lists `NPCIsMovingConditionType.cs` but gives no param details.

Minor finding — doc expansion optional.

---

### B7-4  [MISSING-DOC]  `NPCHasArrivedAtWaypointConditionType` — has `arrival_distance` param not documented

**Affected docs:** CLAUDE.md
**File:** `NPC/Conditions/NPCHasArrivedAtWaypointConditionType.cs`

**What the code has:**
`Schema: arrival_distance (Float, 0.5)` — radius around waypoint.

Minor finding — doc expansion optional.

---

### B7-5  [MISSING-DOC]  `NPCOnSequenceIndexConditionType` — `comparison` param (equal/gt/lt) not documented

**Affected docs:** CLAUDE.md
**File:** `NPC/Conditions/NPCOnSequenceIndexConditionType.cs`

**What the code has:**
```
Schema: sequence_index (Int, 0)
        comparison (Int, 0)   // 0 = Equal, 1 = GreaterThan, 2 = LessThan
```

Minor finding — doc expansion optional.

---

## BATCH 8 — NPC Editor Tools (2 files)

---

### B8-1  Confirmed: Both editor tools are correctly editor-only

**Files:** `NPC/Editor/NPCRoutedMovementAssetBuilder.cs`, `NPC/Editor/NPCTestSceneBuilder.cs`

Both use `#if UNITY_EDITOR`, `using UnityEditor;`, and `using UnityEditor.SceneManagement;`.
Neither uses `using UnityEngine.UIElements;`.
CLAUDE.md describes them correctly.
**No doc changes needed.**

---

### B8-2  [MISSING-DOC]  `NPCTestSceneBuilder` creates a `StealthGraph.asset` — not mentioned in CLAUDE.md test scene description

**Affected docs:** CLAUDE.md (NPC Test Scene Builder tool description)
**File:** `NPC/Editor/NPCTestSceneBuilder.cs`

**What the code does:**
Creates `StealthGraph.asset` (single-node `npc.stealth_meter` graph) in addition to `PatrolGraph.asset`, and wires two runner slots: Slot 0 = Stealth, Slot 1 = Patrol.

**What CLAUDE.md says:**
"creates `Assets/NPC_Test/` assets + full scene" — no mention of StealthGraph or two-slot configuration.

**Suggested fix for CLAUDE.md:** Update the Build Routed Movement Test Scene description to note that the scene uses two runner slots: a Stealth slot (slot 0, `StealthGraph.asset`) and a Patrol slot (slot 1, `PatrolGraph.asset`).

---

## SUMMARY OF ALL FINDINGS

### High Priority (breaks implementation if not corrected)

| ID | Tag | Short Description |
|---|---|---|
| B2-1 | [STALE-DOC] | Phase 11-F: `IAGISNodeComponentRequirements` already exists; step should not be "deferred" |
| B1-1 | [WRONG-DOC] | Phases 8-B, 12-E: `runner.NodeTypes.All` → should be `runner.NodeTypes.AllTypes` |
| B2-2 | [WRONG-DOC] | Phase 22-B: `runner.GetSlot()` doesn't exist; should be `runner.Slots[slotIndex]` |
| B4-1 | [WRONG-DOC] | Phases 0-H, 1-B: `EnsureEndedEdge(graph, node)` → correct call is `(graph, node.nodeId, choiceKey)` |
| B4-2 | [WRONG-DOC] | Phase 5-F: `FindEndedEdge(graph, node)` → correct call is `(graph, node.nodeId)` |
| B4-3 | [WRONG-DOC] | EDITOR_DESIGN Section 4A-i: `AddChoice()` / `RemoveLastChoice()` → both require `choiceKey` 3rd param |
| B3-1 | [WRONG-DOC] | Phase 1-B: clone via "serialize → deserialize" → use `AGISGraphClone.CloneGraph()` instead |
| B6-3 | [WRONG-DOC] | CLAUDE.md: `NPCTakeDamageNodeType` schema has wrong params (Animator-based, now UCC ability-based) |

### Medium Priority (accurate code understanding needed)

| ID | Tag | Short Description |
|---|---|---|
| B2-3 | [WRONG-DOC] | Phase 0-A: confirms `CurrentNodeId`/`Instance`/`LastTransitionEdgeId` still missing from slot — gap is real |
| B1-2 | [WRONG-DOC] | EDITOR_COMPONENTS/EDITOR_DESIGN Section 6C: `graphName` implied to exist — it doesn't yet |
| B4-4 | [MISSING-DOC] | `AGISDialogueNodeType` has undocumented `loop (Bool, false)` param |
| B6-1 | [MISSING-DOC] | `NPCInvestigateNodeType` (`npc.investigate`) completely absent from all docs |
| B6-2 | [MISSING-DOC] | `NPCStealthMeterNodeType` (`npc.stealth_meter`) completely absent from all docs |
| B7-1 | [MISSING-DOC] | `NPCHasLostTargetConditionType` (`npc.has_lost_target`) completely absent from all docs |
| B5-1 | [MISSING-DOC] | `npc.last_known_target_pos` key missing from CLAUDE.md state key table |
| B5-2 | [MISSING-DOC] | `AGISEnemyTemplateData.stealthGraph` / two-slot architecture missing from CLAUDE.md |
| B6-4 | [MISSING-DOC] | `NPCDyingNodeType` schema + `npc.is_dead` persistent key missing from CLAUDE.md |

### Lower Priority (informational / minor corrections)

| ID | Tag | Short Description |
|---|---|---|
| B2-4 | [MISSING-DOC] | `IAGISPersistentComponent` backwards-compat alias exists; mention in a footnote |
| B2-5 | [MISSING-DOC] | `AGISDebugTrace` class not mentioned in editor docs |
| B2-6 | [MISSING-DOC] | `AGISGraphFingerprint.Compute()` not mentioned in editor docs |
| B2-7 | [MISSING-DOC] | `AGISActorComponentFixer.EnsureComponents()` not mentioned (now relevant post B2-1 fix) |
| B2-8 | [MISSING-DOC] | `IAGISInterruptibility` interface not mentioned (low editor relevance) |
| B3-2 | [MISSING-DOC] | `AGISBindingChangeDetector.FingerprintParamTable()` not mentioned (internal) |
| B3-3 | [MISSING-DOC] | `AGISParamTargetApplier.ApplyToTarget()` not mentioned (internal) |
| B3-4 | [MISSING-DOC] | `AGISGroupedParamBinder.ApplyBindings()` not mentioned (internal) |
| B3-5 | [AMBIGUOUS] | `AGISGroupedStateAsset.internalEntryNodeId` legacy field; use `internalGraph.entryNodeId` |
| B3-6 | [MISSING-DOC] | `AGISContentLibrary` has many undocumented methods (`ImportGrouped`, `TryGetGraph`, etc.) |
| B3-7 | [AMBIGUOUS] | `AGISGraphSerializer.ToJson` `prettyPrint` param — use `true` for Export JSON |
| B4-5 | [MISSING-DOC] | `AGISDialogueEdgeSync.IsEndedEdge()` and `TryGetChoiceOption()` helpers not mentioned |
| B1-3 | [MISSING-DOC] | `IAGISParamAccessor` and `AGISParamResolver.BuildAccessor()` not mentioned by name |
| B1-4 | [WRONG-DOC] | Phase 15-E: `ValidationSeverity.Error` → correct name `AGISValidationSeverity.Error` |
| B1-5 | [AMBIGUOUS] | ConstBool True/False are same `ExprKind.ConstBool` with `constValue` field — UI-only distinction |
| B2-9 | [AMBIGUOUS] | `AGISStateMachineRunner.SetSlotGraphAsset()` + `RebuildAllSlots()` vs `ApplyGraphToRunner()` |
| B7-2 | [MISSING-DOC] | `NPCDetectionMeterConditionType` in editor docs but not CLAUDE.md |
| B7-3 | [MISSING-DOC] | `NPCIsMovingConditionType.threshold` param undocumented (minor) |
| B7-4 | [MISSING-DOC] | `NPCHasArrivedAtWaypointConditionType.arrival_distance` param undocumented (minor) |
| B7-5 | [MISSING-DOC] | `NPCOnSequenceIndexConditionType.comparison` param undocumented (minor) |
| B6-5 | [AMBIGUOUS] | `NPCFollowTargetNodeType` full schema confirmed correct in EDITOR_DESIGN mockup ✓ |
| B8-1 | (confirmed ✓) | Editor tools correctly editor-only; no changes needed |
| B8-2 | [MISSING-DOC] | `NPCTestSceneBuilder` creates two slots (Stealth + Patrol) — not described in CLAUDE.md |

---

## CONFIRMED CORRECT (no changes needed)

The following were flagged for investigation but are accurately documented:

- `AGISGuid.Empty` — static property, used correctly everywhere ✓
- `AGISNodeKind` enum values (Normal, Grouped, Parallel, AnyState) — correct in all docs ✓
- `AGISNodeVisualDef.position` and `.collapsed` — correctly referenced ✓
- `AGISTransitionEdgeDef.scopeId` defaults to `"Any"` — correct ✓
- `AGISGroupedNodeType.TypeId = "Grouped"` and `AGISParallelNodeType.TypeId = "Parallel"` — correct ✓
- `AGISAnyStateNodeType.TypeId = "agis.any_state"` — correct ✓
- `AGISStateMachineRunner.NodeTypes` and `.ConditionTypes` are public properties — correct ✓
- `AGISStateMachineRunner.Slots` is `IReadOnlyList<AGISStateMachineSlot>` — correct ✓
- `AGISStateMachineInstance.CurrentNodeId` exists as property — correct ✓
- `AGISContentLibrary.Instance` singleton pattern — correct ✓
- `AGISContentLibrary.ApplyGraphToRunner(dbId, runner, slotIndex)` signature — correct ✓
- `AGISGraphSerializer.ToJson(graph)` and `.GraphFromJson(json)` method names — correct ✓
- `AGISDialogueConstants.DefaultChoiceKey`, `ActiveIdKey`, `NoChoice` — correct ✓
- `NPCDetectionCone` declared persistent key `npc.show_detection_cone` — correct ✓
- `NPCRouteDataHolder` declared persistent key `npc.use_routes` — correct ✓
- `NPCMoveToWaypointNodeType` persistent params (4 route keys) — correct ✓
- `NPCFollowTargetNodeType` persistent param `npc.target_time_lost` — correct ✓
- Both NPC/Editor tools use `#if UNITY_EDITOR` and `using UnityEditor` — correct ✓
- `AGISConditionExprDef.ExprKind` values (And, Or, Not, Leaf, ConstBool) — correct ✓
- `AGISParamSpec` fields (`displayName`, `tooltip`, `category`, `hasMin`, `hasMax`) — correct ✓
- Priority badge color ranges (0=Grey, 1–3=Green, 4–9=Yellow, 10–19=Orange, 20+=Red) — correct ✓
- `AGISTransitionPolicy.interruptible` and `.cooldownSeconds` — correct ✓

---

## DOC-VS-DOC CONTRADICTIONS

Cross-referencing EDITOR_DESIGN.md (A), EDITOR_BUILD_PLAN.md (B), and EDITOR_UI_COMPONENTS.md (C)
against each other — where two docs disagree on the same feature.

| ID | Conflict | Docs involved | Resolution |
|---|---|---|---|
| D1 | A has [Validate] in toolbar but no Undo/Redo; C has Undo/Redo in 3A but no [Validate] | A §2 Left, C §3A | Both docs need both; add [Validate] to C §3A and [Undo]/[Redo] to A §2 Left |
| D2 | [Frame Selected] listed in C §3B but absent from A §2 Center | A §2, C §3B | Add [Frame Selected] to A §2 Center group |
| D3 | `F` shortcut: "Frame selection (or all if nothing selected)" in A §11; "Frame All" in B Phase 2-E; "Frame All" + separate Shift+F in B Phase 2-F and C §15 | A §11, B §2-E/F, C §15 | Align to: F = Frame All, Shift+F = Frame Selected; A §11 needs splitting |
| D4 | Node search trigger: Spacebar (A §8A, B §20) vs double-click (B §8-A) — internal BUILD_PLAN contradiction | B §8-A, B §20 | Both triggers valid; fix B §8-A to list both Spacebar and double-click |
| D5 | `M` (minimap toggle) shortcut present in B §20 and C §15 but absent from A §11 | A §11, B §20, C §15 | Add `M` = Toggle minimap to A §11 |
| D6 | AnyState [x] delete button: "hidden" (A §4A-ii: "[x] is hidden") vs "disabled" (C §5B: "[x] delete: Disabled (Entry, AnyState)") | A §4A-ii, C §5B | Align to A: AnyState [x] is hidden (not rendered), not disabled |
| D7 | Entry node delete: "Entry CAN be deleted" (B Phase 5-A and 11-G) vs "[x] delete: Disabled (Entry, AnyState)" (C §5B) | B §5-A/11-G, C §5B | Align to B: Entry IS deletable; remove Entry from C §5B disabled condition |
| D8 | Tab D (Grouped Asset Inspector) activation: drill-in only (A §6D) vs also from context menu "Edit Grouped Asset" (C §6D, §10 node menu) | A §6D, C §6D/§10 | Both valid; add context menu activation path to A §6D |
| D9 | Auto-save toggle: present in B Phase 14-D and C §6C but absent from A §6C | A §6C, B §14-D, C §6C | Add auto-save toggle to A §6C |
| D10 | Status bar Live indicator: A §7 shows full format ("● LIVE — Actor / Slot / Active Node / 3.2s"); C §7 shows shorter format without time; content differs slightly | A §7, C §7 | Align both to A §7's fuller format |
| D11 | Export/Import JSON buttons: present in B Phase 10-F/G and C §6C but absent from A §6C | A §6C, B §10-F/G, C §6C | Add [Export JSON] and [Import JSON] to A §6C |
| D12 | Node collapse gesture: [v] button (A §4A, C §5B) vs double-click header (B Phase 3-E) | A §4A, B §3-E, C §5B | Both should exist; each doc to acknowledge both the [v] button and double-click as valid collapse gestures |
| D13 | AnyState edge variant described in A §4B ("GLOBAL" prefix, dark crimson color) but absent from C §5C | A §4B, C §5C | Add AnyState edge variant sub-section to C §5C |

---

## ARCHITECTURAL SENSE REVIEW

Analysis of whether doc-described API calls match actual runtime architecture.

| ID | Severity | Finding | Suggested Fix |
|---|---|---|---|
| A1 | High | EDITOR_DESIGN §6E: implementation note says `runner.Slots[_activeSlotIndex].Instance?.Actor?.ActorState` — but `AGISActorRuntime` (the `Actor` property type) does not directly hold `ActorState`; AGISActorState is a MonoBehaviour component on the actor GameObject. The correct access path is `runner.GetComponent<AGISActorState>()` (or via the actor's GameObject) | Fix §6E implementation note: replace `instance?.Actor?.ActorState` with `runner.GetComponent<AGISActorState>()` |
| A5 | High | Phase 22-B: `runner.GetSlot(slotIndex)` — a method named `GetSlot()` does not exist. `AGISStateMachineRunner` exposes `runner.Slots` as `IReadOnlyList<AGISStateMachineSlot>` (indexed by position). This is the same as B2-2 but confirmed at the architectural level. | Fix Phase 22-B: `runner.GetSlot(slotIndex)` → `runner.Slots[slotIndex]` |
| A9 | Medium | Grouped asset edits (Phase 17-E) are described as mutating the grouped asset's `internalGraph` directly with changes propagating automatically — but `AGISGroupedStateAsset` is a ScriptableObject (Unity asset). At runtime in a build, changes to it are not automatically serialized. The editor must have a separate `ImportGrouped` save path (analogous to `AGISContentLibrary.ImportGraph`) to persist grouped asset edits. This step is missing from Phase 17. | Add a Phase 17-E note: edits to grouped asset internal graph need their own serialization step (either a new `ImportGrouped` method or writing back to `AGISContentLibrary` under the asset's own key) |
| A7 | Low | Phase 17-E: "The grouped asset is a reference type; changes propagate automatically" — this is misleading because the runtime runs on a *cloned* working graph, not the live asset. Edits only apply to the live machine after the next Save + Apply cycle triggers a re-compile and Enter. Should add a comment noting this deferred-propagation behavior. | Add note to Phase 17-E: edits to grouped internal graph take effect on next Save → ApplyGraphToRunner cycle (not immediately) |
| A11 | Low | `AGISContentLibrary.Instance` singleton null-behavior: multiple phases assume it is present, but no phase explicitly checks for null before use (Phase 14-A does add auto-create logic, but later phases call it directly). | Verify at implementation: all callers of `AGISContentLibrary.Instance` should guard against null or rely on Phase 14-A's auto-create guarantee |
| A13 | Low | `AGISConditionSummary` (Phase 0-B) is a new file — not yet in the codebase. The recursive tree walk it implements must handle all `AGISConditionExprKind` values (And, Or, Not, Leaf, ConstBool) and call `conditionRegistry.TryGet(typeId)` for Leaf nodes to get the display name. The plan does not specify the walk algorithm; implementors should note this. | Document in Phase 0-B: use a recursive switch on `expr.Kind`; for Leaf, call `conditionRegistry.TryGet(expr.TypeId, out var type)` to get `type.DisplayName` for the summary string |
