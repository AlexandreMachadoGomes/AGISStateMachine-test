# AGIS ESM — Dialogue Editor: Design Guidelines

## 1. Purpose & Scope

This document defines the architecture, UX conventions, and data design for the AGIS Dialogue Editor — a friendlier authoring tool that sits **on top of** the existing state machine infrastructure. It allows designers (and non-technical users of the framework) to author branching dialogue trees without touching the raw graph editor.

The editor is a higher-level abstraction. Under the hood it writes to an `AGISStateMachineGraphAsset` using the same `agis.dialogue` node types and `agis.dialogue_option` edge conditions. From the state machine's perspective, a dialogue tree built here is indistinguishable from one built by hand in the graph editor.

---

## 2. New Asset: `AGISDialogueTreeAsset`

A single ScriptableObject that owns everything for one dialogue tree:

```
AGISDialogueTreeAsset
  ├── beats : List<AGISDialogueBeatDef>     ← content + structure
  └── graphAsset : AGISStateMachineGraphAsset  ← generated graph (sub-asset)
```

The `graphAsset` is stored as a **sub-asset** (nested inside the `.asset` file). Designers never interact with it directly — it is generated and owned by the dialogue editor. The `AGISStateMachineRunner` references the tree asset, which exposes the graph through a property.

### `AGISDialogueBeatDef` (serializable class, not a ScriptableObject)

```csharp
[Serializable]
public class AGISDialogueBeatDef
{
    public AGISGuid    beatId;          // stable identity — never changes
    public string      speaker;         // character name shown in the UI
    public string      text;            // the spoken line
    public bool        loop;            // maps to AGISDialogueNodeType loop param
    public string      dialogueId;      // maps to AGISDialogueNodeType dialogue_id param
                                        // auto-set to beatId.ToString() on creation

    public List<AGISDialogueChoiceDef> choices = new();
    public List<AGISDialogueExitDef>   exits   = new(); // non-choice exits (see §7)

    // Editor-only layout
    public Vector2     canvasPosition;
    public bool        isEntry;
}

[Serializable]
public class AGISDialogueChoiceDef
{
    public AGISGuid targetBeatId;   // AGISGuid.Empty = not yet connected
    public string   label;          // choice text shown to the player
}

[Serializable]
public class AGISDialogueExitDef
{
    // A raw AGISTransitionEdgeDef ID that was manually authored in the graph editor.
    // The dialogue editor leaves these alone and shows them read-only.
    public AGISGuid edgeId;
    public string   description;    // human label for display in the dialogue editor
}
```

### Why separate content from the graph?

- The graph (`AGISStateMachineGraph`) is a runtime structure — it has no place for speaker names or choice labels.
- Keeping content in `AGISDialogueBeatDef` makes localization straightforward: the content layer can be swapped out for a localization backend without touching the graph.
- The graph is fully regenerated from `AGISDialogueBeatDef` data on save — it is never the source of truth.

---

## 3. Editor Window: `AGISDialogueEditorWindow`

Opens by double-clicking an `AGISDialogueTreeAsset` in the Project window.

```
┌──────────────────────────────────────────────────────────────────────────┐
│  [← Back]  My Dialogue Tree *         [+ New Beat]  [Save]  [Validate]  │
├────────────────────────────────────────────────┬─────────────────────────┤
│                                                │                         │
│             Beat Canvas                        │   Beat Inspector        │
│         (scrollable, zoomable)                 │   (selected beat)       │
│                                                │                         │
│                                                │                         │
└────────────────────────────────────────────────┴─────────────────────────┘
│  [Beat count: 5]  [Unconnected choices: 1 ⚠]  [Validation: OK ✓]        │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Beat Card (Canvas Element)

Each `AGISDialogueBeatDef` is rendered as a card on the canvas.

```
┌─────────────────────────────────────────────────────┐
│ ENTRY ●  Guard Captain                    [⋮ menu]  │  ← header
├─────────────────────────────────────────────────────┤
│ "Halt! State your business."                        │  ← text (truncated, full in inspector)
├─────────────────────────────────────────────────────┤
│ Choices                                             │
│  [0] "I'm a merchant."          ──────────────────► │  ← connected (line to target card)
│  [1] "None of your business."   ──────────────────► │
│  [2] "..."                       [ Create Beat → ]  │  ← unconnected (orange button)
│                           [ + Add Choice ]          │
├─────────────────────────────────────────────────────┤
│ ◎ Loop    ⚑ 1 advanced exit                        │  ← footer flags
└─────────────────────────────────────────────────────┘
```

### Header

- Left: `ENTRY ●` badge if this beat is the entry point. Only one beat can be entry.
- Centre: speaker name (editable inline on double-click, or via inspector).
- Right: `⋮` menu → Set as Entry / Duplicate / Delete.

### Text preview

First ~60 characters of the dialogue text. Truncated with `…`. Click to open full text in inspector.

### Choices section

Each `AGISDialogueChoiceDef` is one row:
- Index badge `[0]`, `[1]`, etc. (read-only, matches option index)
- Choice label text (editable inline)
- **If connected:** an arrow line drawn to the target card on the canvas. The row right side shows the target beat's speaker name as a dim label.
- **If not connected:** an orange **[ Create Beat → ]** button. Clicking it creates a new blank beat, positions it to the right of the current card, and wires the choice to it. Alternatively a **[ Connect → ]** dropdown lists existing beats to link to instead.
- **[ + Add Choice ]** button appends a new unconnected choice row.

### Footer flags

- `◎ Loop` — shown (filled circle) when `loop = true`.
- `⚑ N advanced exits` — shown when the beat has manually authored non-choice exits. Clicking opens the inspector's Advanced Exits tab.

### Connecting choices visually

Lines from the right edge of a choice row to the input anchor (top-left) of the target card. Lines are:
- White for a normal connected choice
- Orange/dashed for an unconnected choice slot
- Animated (moving dash) when the edge is selected

---

## 5. Beat Inspector Panel

Shown on the right when a beat card is selected.

```
── Identity ────────────────────────────────────────
  Beat ID      greet_01  (read-only)
  Dialogue ID  greet_01  (read-only, same as beatId by default)

── Content ─────────────────────────────────────────
  Speaker  [ Guard Captain          ]
  Text     [ Halt! State your       ]
           [ business.              ]  (multiline, auto-grows)

── Behaviour ────────────────────────────────────────
  Loop     [✓]   (when checked: this beat repeats until a non-choice
                   condition exits it or an AnyState interrupt fires)
  Entry    [ Set as Entry ]  (button; disabled if already entry)

── Choices ──────────────────────────────────────────
  [0]  Label  [ I'm a merchant.       ]   Target [ Beat: merchant_path ▾ ]
  [1]  Label  [ None of your business.]   Target [ Beat: hostile_path   ▾ ]
  [2]  Label  [ ...                   ]   Target [ — unconnected —      ]
                                              [ Create Connected Beat ]
                                              [ Connect to Existing…  ]
       [ + Add Choice ]   [ ↑ ][ ↓ ] reorder   [ ✕ ] remove last

── Advanced Exits ───────────────────────────────────
  (see §7)
```

### Choice reordering

Reordering choices renumbers their indices (0, 1, 2…). Since `agis.dialogue_option` conditions store the index as a param, regenerating the graph re-stamps the correct index on each edge automatically. The `label` field on `AGISDialogueChoiceDef` is the player-facing text — the actual option index is always positional.

---

## 6. Auto-Wiring Logic

The dialogue editor is the **sole writer** of the generated `AGISStateMachineGraphAsset`. On every save it performs a full regeneration:

```
foreach beat in beats:
    create AGISNodeInstanceDef (nodeTypeId = "agis.dialogue")
    set params: dialogue_id = beat.beatId, loop = beat.loop, choice_key = default

set entryNodeId = beats.First(b => b.isEntry).nodeId

foreach beat in beats:
    foreach choice at index i:
        if choice.targetBeatId is valid:
            create AGISTransitionEdgeDef
                fromNodeId = beat's node
                toNodeId   = target beat's node
                priority   = 0
                condition  = AGISConditionExprDef.Leaf(
                    agis.dialogue_option  option=i  choice_key=default
                )

    foreach exit in beat.exits:
        copy the raw AGISTransitionEdgeDef as-is (do not overwrite)
```

**Idempotent:** running the generator twice with the same data produces an identical graph. The generator matches existing `AGISNodeInstanceDef` by `beatId` so node GUIDs are stable across saves (important for `AGISRuntimeGraphCache`).

**Detect existing connections:** the **[ Create Beat → ]** button is only shown when `targetBeatId == AGISGuid.Empty`. Once connected the button is replaced by the target label. This satisfies the requirement to only auto-create when no connection exists.

---

## 7. Advanced Exits (Non-Choice Transitions)

Some beats need exits that aren't player choices — for example an AnyState interrupt (take damage), a timer, or a proximity check. These are beyond the friendly layer and fall back to the raw edge editor.

In the Beat Inspector, the **Advanced Exits** section shows a list of edges that originate from this beat's underlying node but were NOT created by the dialogue editor (i.e., they have no matching `AGISDialogueChoiceDef`). Each is shown as:

```
⚑  [condition summary]  →  [target beat / node name]   [ Edit in Graph Editor ↗ ]
```

Clicking **Edit in Graph Editor ↗** opens the raw `AGISGraphEditorWindow` (from `VisualEditor-Guidelines.md`) focused on that edge. Changes made there are preserved by the generator (exits are copied as-is, not regenerated).

The dialogue editor **never deletes** edges it did not create.

---

## 8. Canvas Layout

### Auto-layout

When a new beat is created via **[ Create Beat → ]**, it is positioned automatically:
- Horizontally: `sourceBeat.canvasPosition.x + cardWidth + gapX`
- Vertically: `sourceBeat.canvasPosition.y + (choiceIndex * verticalStride)`

This produces a natural left-to-right tree layout for simple linear or branching dialogues without requiring the user to arrange cards.

### Manual layout

Cards are draggable. Position is persisted in `AGISDialogueBeatDef.canvasPosition`. Manual layout overrides auto-layout for that card.

### Entry beat positioning

The entry beat is always visually marked and conventionally placed at the left of the canvas. It is not pinned — the user can move it.

---

## 9. Toolbar Actions

| Button | Behaviour |
|---|---|
| `+ New Beat` | Creates a blank beat at the canvas centre. Not connected to anything. |
| `Save` | Regenerates the `AGISStateMachineGraphAsset` sub-asset and calls `AssetDatabase.SaveAssets()`. |
| `Validate` | Checks: all choices connected, entry beat set, no orphan beats (beats with no incoming choice and not entry), no duplicate speaker+text combinations (warning). |
| `← Back` | Returns to the Project window. Prompts save if dirty. |
| `Auto-Layout` (optional) | Re-runs the auto-layout algorithm on all cards. Useful after heavily manual edits. |

---

## 10. Validation Rules

| Rule | Severity |
|---|---|
| No entry beat set | Error |
| Beat has no text | Warning |
| Beat has choices but all are unconnected | Warning |
| Beat has no choices, is not looping, and has no advanced exits | Warning (dead end) |
| Orphan beat (no incoming connections, not entry) | Warning |
| Circular reference with no exit (loop=false on all beats in cycle) | Warning |
| Two beats with identical `dialogueId` | Error (should never happen; beatId is the source) |

Validation results are shown inline as coloured card borders (red = error, yellow = warning) and summarised in the status bar.

---

## 11. Runtime Integration for Game Code

The game UI only needs to watch two blackboard keys, same as the raw dialogue node (no changes to the runtime):

```csharp
// Poll each frame (or subscribe to a blackboard change event if you add one):
if (blackboard.TryGet<string>("agis.dialogue.active_id", out var beatId))
{
    // Look up the AGISDialogueTreeAsset to get speaker + text + choice labels
    var beat = dialogueTree.GetBeat(beatId);
    ShowUI(beat.speaker, beat.text, beat.choices.Select(c => c.label));
}
else
{
    HideUI();
}

// When player selects choice index 1:
blackboard.Set("agis.dialogue.choice", 1);
```

`AGISDialogueTreeAsset` exposes a `GetBeat(string beatId)` lookup method. Game code holds a reference to the asset (assigned in the inspector on a MonoBehaviour) and uses it purely as a content database — no state machine knowledge required.

---

## 12. Localization Considerations (Future)

For v1, text is stored directly in `AGISDialogueBeatDef`. When localization is needed:

- Replace `speaker` and `text` string fields with a `LocalizationKey` struct (string table + key)
- Choice `label` similarly becomes a localization key
- The dialogue editor gains a locale preview dropdown
- The runtime lookup method (`GetBeat`) returns localized strings for the active locale

This is a content-layer change only — the state machine graph, conditions, and runtime are unaffected.

---

## 13. File & Folder Conventions

```
Assets/Scripts/
  Dialogue/
    AGISDialogueTreeAsset.cs         ← ScriptableObject (runtime + editor)
    AGISDialogueBeatDef.cs           ← serializable content + structure
    Editor/
      AGISDialogueEditorWindow.cs    ← EditorWindow
      AGISDialogueBeatCard.cs        ← VisualElement: card on canvas
      AGISDialogueInspector.cs       ← right-panel inspector
      AGISDialogueGenerator.cs       ← regenerates AGISStateMachineGraphAsset from beats
      AGISDialogueValidator.cs       ← validation rules
```

The `Editor/` folder requires its own assembly definition referencing `UnityEditor`, `UnityEditor.UIElements`, and the main AGIS Dialogue assembly.

---

## 14. Relationship to the Raw Graph Editor

The dialogue editor and the raw graph editor (`AGISGraphEditorWindow`) coexist:

| | Dialogue Editor | Raw Graph Editor |
|---|---|---|
| Opens from | `AGISDialogueTreeAsset` | `AGISStateMachineGraphAsset` |
| Edits | Beat content + choice wiring | Full graph (any node/edge type) |
| Generates graph | Yes (on save) | No (is the graph) |
| Suitable for | Dialogue authors, narrative designers | Framework developers, power users |

The generated `AGISStateMachineGraphAsset` sub-asset **can** be opened in the raw graph editor for advanced edits (AnyState interrupts, extra conditions). The dialogue editor will preserve those edits via the Advanced Exits mechanism (§7) on the next save.

Opening the sub-asset directly and editing dialogue nodes there is discouraged — changes to node params (dialogue_id, loop) would be overwritten on the next dialogue editor save, since those params are regenerated from `AGISDialogueBeatDef`.
