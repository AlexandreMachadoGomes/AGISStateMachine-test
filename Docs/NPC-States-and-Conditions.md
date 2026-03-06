# NPC States and Conditions — Tutorial

This guide is for anyone who wants to understand how the NPC behaviour system works and how to add new behaviours to it. No prior knowledge of this project is assumed, but basic C# and Unity familiarity is expected.

---

## Part 1 — The Big Picture

### What is a state machine?

An NPC's behaviour at any moment can be described as being in a **state** — Wandering, Patrolling, Chasing, Taking Damage, etc. A state machine is the system that decides which state the NPC is in right now and when to switch to a different one.

In this project, every NPC has at least one state machine running on it. The machine is always in exactly one state at a time. Each tick (frame), the machine asks: *should I switch states?* If an outgoing **condition** becomes true, it transitions to a new state.

```
[Wander] ──(player detected)──► [Chase] ──(lost target for 3s)──► [Wander]
```

### What are states and conditions in code?

- A **state** (also called a node) is a C# class that says what the NPC *does* while it is active. It has three moments in its life: `Enter` (just became active), `Tick` (called every frame), and `Exit` (about to switch away).

- A **condition** is a C# class that answers a yes/no question: *is this thing true right now?* Conditions live on the arrows between states. They are evaluated every tick to decide whether a transition should fire.

States and conditions are completely independent of each other. A condition doesn't know which states it connects, and a state doesn't know what conditions are on its outgoing edges.

### How does the system find your code?

You don't need to register anything manually. When the game starts, `AGISStateMachineRunner` scans all loaded assemblies via reflection and discovers every class that implements `IAGISNodeType` (states) or `IAGISConditionType` (conditions) automatically. All you need to do is create the class and put it in the right folder.

---

## Part 2 — Understanding an Existing State: Wander

The best way to learn is to read a real example. Open `Assets/Scripts/NPC/States/NPCWanderNodeType.cs` and follow along.

### The outer class: the type definition

```csharp
public sealed class NPCWanderNodeType : IAGISNodeType
{
    public string TypeId      => "npc.wander";
    public string DisplayName => "NPC Wander";
    public AGISNodeKind Kind   => AGISNodeKind.Normal;
    ...
}
```

The outer class (`NPCWanderNodeType`) is not the thing that actually runs during gameplay. It is more like a **template** or a **factory**. It has three jobs:

1. **TypeId** — a unique string ID used to reference this state in graph assets. Always dot-namespaced (e.g. `npc.wander`, `npc.follow_target`). Pick something that won't clash.
2. **DisplayName** — a human-readable name shown in the editor.
3. **Kind** — almost always `AGISNodeKind.Normal`. Other values exist for special framework nodes (Grouped, Parallel, AnyState) that you won't need to create yourself.

### The schema: design-time parameters

```csharp
public AGISParamSchema Schema { get; } = new AGISParamSchema
{
    specs =
    {
        new AGISParamSpec("wander_radius", AGISParamType.Float, AGISValue.FromFloat(10f))
            { displayName = "Wander Radius", hasMin = true, floatMin = 0.5f },
        new AGISParamSpec("pause_time", AGISParamType.Float, AGISValue.FromFloat(0.5f))
            { displayName = "Pause Time", hasMin = true, floatMin = 0f },
    }
};
```

The schema declares what **parameters** this state has — the knobs a designer can turn per node instance. These are set in the graph editor, not in code. At runtime they are read-only.

Each `AGISParamSpec` needs:
- A key string (used to read the value at runtime)
- A type (`AGISParamType.Float`, `.Bool`, `.Int`, `.String`, `.Vector2`, `.Vector3`)
- A default value (`AGISValue.FromFloat(...)`, `.FromBool(...)`, etc.)

You can optionally add `displayName`, `tooltip`, and min/max clamps.

### The factory: `CreateRuntime`

```csharp
public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
{
    float radius    = args.Params.GetFloat("wander_radius", 10f);
    float pauseTime = args.Params.GetFloat("pause_time", 0.5f);
    return new Runtime(args.Ctx, radius, pauseTime);
}
```

`CreateRuntime` is called once each time the state machine **enters** this node. It reads the params for this specific node instance and creates a `Runtime` object — the thing that will actually run. Think of it as constructing the behaviour with its configured values baked in.

`args.Ctx` is the **execution context** — it gives you access to the actor's `GameObject` and `Blackboard`. Always pass it to your `Runtime` so it can use them.

### The runtime: where behaviour lives

```csharp
private sealed class Runtime : IAGISNodeRuntime
{
    private readonly IAGISNPCPathFinder _pathFinder;
    private readonly float              _radius;
    private readonly float              _pauseTime;

    private float _pauseTimer;
    private bool  _waiting;

    public Runtime(AGISExecutionContext ctx, float radius, float pauseTime)
    {
        _pathFinder = ctx.Actor?.GetComponent<IAGISNPCPathFinder>();
        _radius     = radius;
        _pauseTime  = pauseTime;
        // ...
    }

    public void Enter() { /* called once on entry */ }
    public void Tick(float dt) { /* called every frame */ }
    public void Exit() { /* called once on exit */ }
}
```

The `Runtime` is a **private nested class** inside the type. It is created fresh every time the node is entered, so you can freely use instance variables to track state (`_pauseTimer`, `_waiting`, etc.) without worrying about leftover data from a previous visit.

Notice the constructor resolves `IAGISNPCPathFinder` from the actor. This is an **interface** that wraps whatever pathfinding component is on the NPC (A*, NavMesh, etc.). Always use this interface rather than accessing a specific pathfinding class directly.

### What Wander actually does

```
Enter:
  - Enable pathfinding
  - Create a temporary child GameObject as the wander target
  - Pick a random point within wander_radius and move the target there

Tick (every frame):
  - If waiting: count down the pause timer; when it hits zero, pick a new point
  - If not waiting: check if the NPC has arrived; if yes, start the pause timer

Exit:
  - Destroy the temporary target GameObject
```

The random point is snapped to the nearest A* graph node so the NPC never navigates toward an unreachable position.

---

## Part 3 — Understanding the Routed Patrol

Patrol is more complex than Wander. Rather than one state doing everything, it is split into three small states inside a **Grouped node** (a state that contains its own mini state machine).

The internal graph looks like this:

```
(entry)
[Reset Route] ──► [Move To Waypoint]
                        │
               (arrived at waypoint)
                        ▼
              [Advance Waypoint] ──► [Move To Waypoint]
```

Each state has a single responsibility:

| State | Job |
|---|---|
| `npc.reset_route` | Resets the patrol position to the very beginning (waypoint 0, route 0). Runs once on entry, then immediately yields. |
| `npc.move_to_waypoint` | Reads the current waypoint position and points the pathfinder at it. Waits for an arrival condition to fire. |
| `npc.advance_waypoint` | Calculates the **next** waypoint in the sequence and writes the new indices. Runs once, then immediately yields. |

### Where waypoint positions come from

`NPCRouteDataHolder` is a MonoBehaviour on the NPC that holds a reference to an `NPCRouteData` ScriptableObject. That asset contains:

- A list of **routes**, each being a list of `Vector3` waypoints.
- A **sequence** — an ordered list of route indices that defines which routes to visit and in what order.

So a sequence of `[0, 1]` means: walk all waypoints of route 0, then all waypoints of route 1, then back again.

> **Runtime / multiplayer note:** `NPCRouteData` does not have to come from a project asset. `ScriptableObject.CreateInstance<NPCRouteData>()` works at runtime, so a network layer can receive patrol data from a server and populate the component dynamically:
> ```csharp
> var routeData = ScriptableObject.CreateInstance<NPCRouteData>();
> // fill routeData.routes and routeData.sequence from the downloaded payload
> npc.GetComponent<NPCRouteDataHolder>().routeData = routeData;
> ```
> The NPC should be spawned with `NPCRouteDataHolder` already attached but `routeData = null`, and `npc.use_routes` should only be set to `true` once data has arrived. If routes need to change while the NPC is already patrolling, note that `npc.move_to_waypoint` and `npc.advance_waypoint` resolve `NPCRouteDataHolder` in their constructor (once per node entry) — a mid-patrol route swap will take effect on the next entry into those nodes.

### Ping-pong traversal

When the NPC reaches the end of the sequence it reverses direction instead of restarting, creating a natural back-and-forth patrol:

```
Routes: A = [p0, p1, p2]   B = [p3, p4, p5]   Sequence: [A, B]

→ A.p0 → A.p1 → A.p2 → B.p3 → B.p4 → B.p5
← B.p4 → B.p3 → A.p2 → A.p1 → A.p0
→ A.p1 → A.p2 → ...
```

The current position in the traversal is stored in `AGISActorState` (a key-value store on the NPC that persists across state transitions). This means if the NPC gets interrupted mid-patrol (chases a player, takes damage, etc.), it resumes from exactly where it left off when it returns to patrol.

### The Behavior Selector

At the top level of the enemy graph sits `npc.behavior_selector` — a node with no behaviour at all. It exists as a stable **dispatch point** that the NPC always returns to:

```
[BehaviorSelector]  (entry)
  │ priority 1: npc.use_routes = true  → [RoutedMovement]
  │ priority 0: always true            → [Wander]
```

When multiple outgoing edges are true at the same time, the one with the highest priority wins. Here, patrol always beats wander, but wander is the fallback if patrol is disabled.

To switch the NPC from wandering to patrolling at runtime, set `npc.use_routes = true` in the `AGISActorState` inspector on the NPC GameObject. On the next tick, the selector will transition to patrol.

---

## Part 4 — Creating a New State

Here is how to add a brand new behaviour to the system. We'll create a **"Look At Player"** state as an example — the NPC stops moving and turns to face the player.

### Step 1 — Create the file

Create `Assets/Scripts/NPC/States/NPCLookAtPlayerNodeType.cs`.

Use the namespace `AGIS.NPC.States` to keep it consistent with the rest.

### Step 2 — Write the outer class

```csharp
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.NPC;
using UnityEngine;

namespace AGIS.NPC.States
{
    public sealed class NPCLookAtPlayerNodeType : IAGISNodeType
    {
        public string TypeId      => "npc.look_at_player";
        public string DisplayName => "NPC Look At Player";
        public AGISNodeKind Kind   => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("turn_speed", AGISParamType.Float, AGISValue.FromFloat(5f))
                    { displayName = "Turn Speed",
                      tooltip     = "Degrees per second to rotate toward the player.",
                      hasMin = true, floatMin = 0f },
            }
        };

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            float turnSpeed = args.Params.GetFloat("turn_speed", 5f);
            return new Runtime(args.Ctx, turnSpeed);
        }
```

### Step 3 — Write the runtime

```csharp
        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly AGISExecutionContext _ctx;
            private readonly IAGISNPCPathFinder   _pathFinder;
            private readonly float                _turnSpeed;

            public Runtime(AGISExecutionContext ctx, float turnSpeed)
            {
                _ctx        = ctx;
                _pathFinder = ctx.Actor?.GetComponent<IAGISNPCPathFinder>();
                _turnSpeed  = turnSpeed;
            }

            public void Enter()
            {
                // Stop moving when we enter this state.
                _pathFinder?.DisablePathfinding();
            }

            public void Tick(float dt)
            {
                if (_ctx.Actor == null) return;

                var player = GameObject.FindWithTag("Player");
                if (player == null) return;

                // Rotate toward the player on the Y axis only.
                Vector3 direction = player.transform.position - _ctx.Actor.transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude < 0.001f) return;

                Quaternion targetRotation = Quaternion.LookRotation(direction);
                _ctx.Actor.transform.rotation = Quaternion.RotateTowards(
                    _ctx.Actor.transform.rotation,
                    targetRotation,
                    _turnSpeed * dt
                );
            }

            public void Exit()
            {
                // Nothing to clean up.
            }
        }
    }
}
```

### Step 4 — Use it in a graph

That's all the code needed. Open the state machine graph asset in the editor, add a new node with type `npc.look_at_player`, set the `Turn Speed` param, and connect edges to and from it.

---

### Things to know when writing states

**Use `IAGISNPCPathFinder`, not a specific pathfinder class.**
Always resolve the pathfinder as `ctx.Actor.GetComponent<IAGISNPCPathFinder>()`. This interface works with any concrete pathfinding implementation on the NPC. Never directly reference `AIPath`, `NavMeshAgent`, or similar.

**The runtime is created fresh on every entry.**
A new `Runtime` instance is created each time the state machine enters the node. This means all instance variables start clean. You don't need to manually reset state on `Enter()` — just initialise variables in the constructor or at the top of `Enter()`.

**Guard against missing components.**
Always check `if (_pathFinder == null) return;` in `Enter()` and `Tick()`. The system won't crash if a component is missing, but it also won't tell you loudly — graceful no-ops are the convention.

**Keep `Tick()` cheap.**
`Tick()` runs every frame. Avoid `GetComponent` calls inside `Tick()` — resolve components once in the constructor and cache them.

**`Exit()` must clean up.**
If you create a temporary `GameObject` (like Wander does for its wander target), destroy it in `Exit()`. If you set something to a particular state on `Enter()`, consider whether it needs to be restored on `Exit()`.

---

### Optional: persisting data across state transitions

Sometimes a state needs to remember something even when it is not active — like patrol progress surviving a chase sequence. For this, use `AGISActorState`.

`AGISActorState` is a MonoBehaviour on the NPC that acts as a named key-value store. Unlike the blackboard (which is in-memory only), `AGISActorState` values are visible in the Unity Inspector and survive state transitions.

To declare that your state needs certain keys to exist in `AGISActorState`, also implement `IAGISPersistentNodeType`:

```csharp
public sealed class NPCMyNodeType : IAGISNodeType, IAGISPersistentNodeType
{
    // ...

    public IReadOnlyList<AGISParamSpec> PersistentParams { get; } = new AGISParamSpec[]
    {
        new AGISParamSpec("npc.my.counter", AGISParamType.Int, AGISValue.FromInt(0))
            { displayName = "My Counter" },
    };
}
```

The runner will call `EnsureKey` for each declared param during `Awake()`. If the key is already present (e.g. from a save), the existing value is kept. If it is absent, the default is written.

To read and write it at runtime:

```csharp
var actorState = ctx.Actor?.GetComponent<AGISActorState>();
int count = actorState.GetInt("npc.my.counter");
actorState.Set("npc.my.counter", AGISValue.FromInt(count + 1));
```

---

### Optional: signalling that a state is done

Some states represent a finite action — play an animation, wait a fixed time, complete a task. When the action finishes, the state machine should automatically move on. Use `IAGISNodeSignal` for this.

Implement the interface on your `Runtime`, expose an `IsComplete` property, and set it to `true` when the action is done. Then put an edge from the node with the condition `agis.node_complete` — it fires as soon as `IsComplete` becomes true.

```csharp
private sealed class Runtime : IAGISNodeRuntime, IAGISNodeSignal
{
    public bool IsComplete { get; private set; }

    private float _elapsed;
    private readonly float _duration;

    public Runtime(float duration) { _duration = duration; }

    public void Enter()  { IsComplete = false; _elapsed = 0f; }
    public void Exit()   { IsComplete = false; } // always reset on exit

    public void Tick(float dt)
    {
        _elapsed += dt;
        if (_elapsed >= _duration)
            IsComplete = true;
    }
}
```

Always reset `IsComplete` to `false` in both `Enter()` and `Exit()` so re-entering the state starts a fresh run.

---

## Part 5 — Creating a New Condition

Conditions are simpler than states — they have no lifecycle, just a single `Evaluate` method that returns `true` or `false`.

We'll create a **"NPC Is Facing Player"** condition as an example.

### Step 1 — Create the file

Create `Assets/Scripts/NPC/Conditions/NPCIsFacingPlayerConditionType.cs`.

### Step 2 — Write the class

```csharp
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using UnityEngine;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCIsFacingPlayerConditionType : IAGISConditionType
    {
        public string TypeId      => "npc.is_facing_player";
        public string DisplayName => "NPC Is Facing Player";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("angle_threshold", AGISParamType.Float, AGISValue.FromFloat(15f))
                    { displayName = "Angle Threshold",
                      tooltip     = "How many degrees of error are allowed. 0 = must face exactly.",
                      hasMin = true, floatMin = 0f, hasMax = true, floatMax = 180f },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            // Always guard against a missing actor first.
            if (args.Ctx.Actor == null) return false;

            var player = GameObject.FindWithTag("Player");
            if (player == null) return false;

            float threshold = args.Params.GetFloat("angle_threshold", 15f);

            Vector3 toPlayer = player.transform.position - args.Ctx.Actor.transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude < 0.001f) return false;

            float angle = Vector3.Angle(args.Ctx.Actor.transform.forward, toPlayer);
            return angle <= threshold;
        }
    }
}
```

### Step 3 — Use it in a graph

Add it as a condition on any edge in the graph editor. Set the `Angle Threshold` param per edge as needed.

---

### Things to know when writing conditions

**Conditions must be stateless.**
`Evaluate` can be called many times per tick (once per edge that uses this condition type). Never store data between calls. If you need accumulated data (e.g. how long something has been true), that belongs in `AGISActorState`, written by a state's `Tick()`.

**Always guard against null.**
Check `args.Ctx.Actor == null` before any `GetComponent`. Return `false` when something required is missing — never throw an exception.

**What you have access to:**

| Member | What it gives you |
|---|---|
| `args.Ctx.Actor` | The NPC's root `GameObject`. Use `GetComponent<>()` from here. |
| `args.Ctx.Blackboard` | In-memory key-value store. `TryGet<T>(key, out value)`, `Set<T>(key, value)`, `Remove(key)`. |
| `args.Params` | The params configured on this condition instance. `GetBool`, `GetInt`, `GetFloat`, `GetString`. |
| `args.CurrentRuntime` | The node that is currently active. Cast to `IAGISNodeSignal` to read its completion state. |

**Writing to the blackboard from a condition is acceptable when it makes sense.**
For example, `npc.detects_object` writes the detected `GameObject` to a configurable blackboard key as a side-effect. This is a well-established pattern — the condition both evaluates and captures the result for other states to use.

---

## Part 6 — Quick Reference

### All existing states

| TypeId | What it does |
|---|---|
| `npc.idle` | Stops the NPC completely. Disables pathfinding. |
| `npc.wander` | Roams randomly within a radius. Params: `wander_radius`, `pause_time`. |
| `npc.follow_target` | Chases the player or a blackboard target. Optional lost-target memory. |
| `npc.move_to_waypoint` | Navigates to the current waypoint from `NPCRouteDataHolder`. |
| `npc.advance_waypoint` | Calculates and saves the next waypoint in the ping-pong sequence. |
| `npc.reset_route` | Resets patrol progress to the beginning. |
| `npc.behavior_selector` | Empty dispatch node. Outgoing edges select the next behaviour. |
| `npc.take_damage` | Plays a damage animation via Animator trigger. Signals completion when done. |
| `npc.dying` | Plays a death animation, disables pathfinding, sets `npc.is_dead` in AGISActorState. Signals completion when done. |
| `agis.dialogue` | Dialogue beat. Signals game code via blackboard. Waits for a choice or an "ended" signal. See Part 7. |

### All existing conditions

| TypeId | Returns true when… |
|---|---|
| `npc.has_reached_destination` | Pathfinder has arrived at its current target. |
| `npc.has_arrived_at_waypoint` | NPC is within `arrival_distance` of the current waypoint. |
| `npc.is_moving` | NPC's desired velocity exceeds `threshold`. |
| `npc.is_within_distance` | NPC is within `distance` units of the player or a blackboard target. |
| `npc.detects_object` | `NPCDetectionCone` detects the player, a blackboard target, or any layered object. |
| `npc.has_lost_target` | Target has been out of detection for longer than `timeout` seconds. |
| `npc.actor_state_bool` | A named bool in `AGISActorState` equals `expected`. |
| `npc.blackboard_bool` | A named bool in the blackboard equals `expected`. |
| `npc.on_sequence_index` | The current patrol sequence index matches a comparison against `sequence_index`. |
| `agis.node_complete` | The active node implements `IAGISNodeSignal` and `IsComplete` is true. |
| `agis.has_dialogue_choice` | Any choice has been made on the active dialogue beat (`choice_key >= 0`). |
| `agis.dialogue_option` | The player chose the specific option index (`choice_key == option`). |

### The two data stores

| Store | Where | Persists? | Use for |
|---|---|---|---|
| **Blackboard** | In memory on `AGISActorRuntime` | Until the actor is destroyed | Transient flags, detected objects, temporary signals (e.g. `npc.is_damaged`) |
| **AGISActorState** | Serialized MonoBehaviour on the NPC | Survives state transitions, visible in Inspector | Route progress, persistent flags, anything that should resume correctly (e.g. `npc.use_routes`) |

### Naming conventions

| Thing | Pattern | Example |
|---|---|---|
| State TypeId | `namespace.verb_noun` | `npc.move_to_waypoint` |
| Condition TypeId | `namespace.description` | `npc.has_reached_destination` |
| AGISActorState keys | `namespace.feature.key` | `npc.route.sequence_index` |
| Class names | `[Feature]NodeType` | `NPCWanderNodeType` |
| File location | States → `NPC/States/`, Conditions → `NPC/Conditions/` | |

---

## Part 7 — The Dialogue System

Dialogue is built on the same node/condition model as everything else. The `agis.dialogue` node is just a state that parks the machine and waits for game code to write a choice to the blackboard.

### How a dialogue beat works

```
Enter:  blackboard[choice_key] = -1  (NoChoice — clears any leftover)
        blackboard["agis.dialogue.active_id"] = dialogue_id
        (game code sees active_id change → shows UI)

Tick:   nothing; waits for game code to write an option index

Exit:   blackboard["agis.dialogue.active_id"] removed
        (game code sees key gone → hides UI)
```

Game code integration:
```csharp
// Poll or event-driven — show UI when active_id is set
if (blackboard.TryGet<string>(AGISDialogueConstants.ActiveIdKey, out var id))
    ShowDialogueUI(id);

// When the player picks option 2
blackboard.Set(AGISDialogueConstants.DefaultChoiceKey, 2);

// State machine transitions on the next tick via the matching condition
```

### Outgoing transitions and how they are managed

Every `agis.dialogue` node must have outgoing edges so the machine can move on. There are two patterns:

**No choices (linear beat):**
One outgoing edge with condition `agis.has_dialogue_choice`. This fires as soon as game code writes any value ≥ 0 to the choice key — even just `0` — signalling "the player acknowledged / dialogue ended".

```
[Dialogue] ──(agis.has_dialogue_choice)──► [Next State]
```

**With choices:**
One outgoing edge per option, each with condition `agis.dialogue_option` (param `option = N`). The `agis.has_dialogue_choice` edge is not present.

```
[Dialogue] ──(agis.dialogue_option  option=0)──► [Path A]
           ──(agis.dialogue_option  option=1)──► [Path B]
           ──(agis.dialogue_option  option=2)──► [Path C]
```

### Auto-management of transitions (`AGISDialogueEdgeSync`)

The helper class `AGISDialogueEdgeSync` (`Assets/Scripts/Dialogue/`) keeps a dialogue node's outgoing edges consistent with its intended choice count. It is the single source of truth for building and modifying dialogue transitions — the graph editor should call it rather than manipulating edges directly.

| Method | What it does |
|---|---|
| `EnsureEndedEdge(graph, nodeId, choiceKey)` | Adds the `agis.has_dialogue_choice` edge if the node has no managed edges. No-op otherwise. |
| `AddChoice(graph, nodeId, choiceKey)` | Appends a new `agis.dialogue_option` edge (next index). Removes the ended edge when the first choice is added. |
| `RemoveLastChoice(graph, nodeId, choiceKey)` | Removes the highest-indexed choice edge. Restores the ended edge when back to zero choices. |
| `FindEndedEdge` / `FindChoiceEdges` | Query helpers used by the editor to read current state. |

`AGISStateMachineGraphAsset.OnValidate` calls `EnsureEndedEdge` for every dialogue node automatically, so a freshly added node is always initialised correctly without any manual step.

### Unconnected edges

New edges produced by `AGISDialogueEdgeSync` have `toNodeId = AGISGuid.Empty`. This is the project convention for a **dangling / unconnected transition** — a placeholder that exists in the data but has no target yet.

- `AGISGraphCompiler` silently skips edges where `toNodeId` is not found in the node list, so unconnected edges never cause runtime errors.
- `AGISGraphValidator` reports them as **warnings** (`Graph.EdgeToUnconnected`), not errors, so validation still passes and a validated save is allowed.
- **Graph editor expectation:** detect `!toNodeId.IsValid` on an edge and draw it as a dangling arrow with an open tail, visually distinct from connected edges. Clicking the open tail should allow the user to drag-connect it to a target node.

### The `loop` param

When `loop = true` on a dialogue node, the Tick clears the choice key every frame before outgoing edges are evaluated. This keeps the node active indefinitely regardless of what game code writes — useful for repeating terminal beats ("I have nothing more to say"). Edges with non-dialogue conditions (e.g. a flag) still fire normally.
