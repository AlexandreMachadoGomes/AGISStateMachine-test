# AGIS ESM — An GI System: Extensible State Machine
## Project Overview and AI Authoring Proposal

---

# Part 1 — The Project

## What Is AGIS ESM

AGIS ESM (An GI System — Extensible State Machine) is a Unity 6 framework for defining, editing, and executing actor behaviour through parameter-driven state machines. It is designed as a standalone engine-level library: self-contained, runtime-safe, and independent of any specific game.

The system targets game AI — NPCs, enemies, companions, ambient actors — but its architecture is general enough for any stateful runtime behaviour: UI flows, quest progression, dialogue, animation controllers, or procedural systems.

The central design principle is that **behaviour is data**. A state machine in AGIS is a graph stored as a serializable data object. It can be created in an editor, saved as an asset, loaded at runtime, modified live, and re-executed — all without touching any code. Code enters the system only at the definition layer (implementing a state type or condition type), not at the graph-authoring layer.

A reference NPC layer built on top of the framework provides ready-made state and condition types for common game AI tasks (movement, detection, routes, dialogue, stealth). It is a usage example as much as a library — its types follow the same interfaces as anything a developer or AI would author.

---

## 1. Core Architecture

The pipeline has three stages:

> **UGC Definitions → Compiler → Runtime Execution**

UGC (User-Generated Content) is the graph as data: nodes, edges, parameters, condition expressions. The compiler transforms a UGC graph into an optimised runtime structure. The runner drives the Enter / Tick / Exit loop and evaluates transitions on every frame.

### 1.1 The Graph — UGC Definitions

A state machine is represented as an `AGISStateMachineGraph`. This is a plain serializable data object containing:

- **Nodes** — A list of `AGISNodeInstanceDef`. Each node has a stable GUID, a type ID that references a registered state type, and a parameter table holding the designer's per-instance configuration values.
- **Edges** — A list of `AGISTransitionEdgeDef`. Each edge has a stable GUID, a from-node GUID, a to-node GUID, a condition expression tree, a priority integer, and a transition policy (cooldown duration, interruptible flag).
- **Entry** — A single GUID identifying which node is the starting point when the state machine is first started.

Graphs are stored as `AGISStateMachineGraphAsset` (Unity ScriptableObject) and can be assigned to slots on an actor's runner in the Inspector. Because a graph is pure data with no code references, it is fully serializable, diffable, and transferable between sessions without recompilation.

### 1.2 The Compiler

`AGISGraphCompiler` transforms a UGC graph into an `AGISRuntimeGraph` before execution begins. The compilation step:

- Validates all type ID references against the active registries and reports missing types as errors rather than silently failing at runtime
- Resolves all GUID references into direct index-based lookups for zero-cost node and edge access during the tick loop
- Builds per-node sorted adjacency lists — outgoing edges ordered by priority (descending) so the runner always evaluates the highest-priority transition first without sorting on every frame
- Instantiates a runtime object for each node by calling the node type's factory method, producing an `IAGISNodeRuntime` ready to receive Enter / Tick / Exit calls

The compiled `AGISRuntimeGraph` is cached (`AGISRuntimeGraphCache`) so re-entering the same graph after a stop does not recompile. The cache is invalidated when the source asset changes.

### 1.3 The Runner and Instance

`AGISStateMachineRunner` is the MonoBehaviour that lives on an actor GameObject. It hosts one or more named slots, each of which wraps an `AGISStateMachineInstance`. On startup, the runner compiles all assigned graphs, seeds the actor state, and starts each slot at its entry node.

`AGISStateMachineInstance` manages the moment-to-moment execution of a single compiled graph:

- Tracks the currently active node
- Calls `Enter()` on the active node's runtime when a state is entered
- Calls `Tick(dt)` on the active node's runtime every frame
- After Tick, evaluates outgoing transitions in priority order
- When a transition fires: calls `Exit()` on the current runtime, advances to the target node, calls `Enter()` on the new runtime
- Exposes the current node ID and last-fired edge ID for external observation (used by the editor's debug overlay and by the AI monitoring layer)

The instance is deliberately minimal. It does not know about Unity, game objects, or any game-specific concept. It drives a pure data graph with a registry of typed executors.

### 1.4 State Types — The Execution Building Blocks

A state type is the reusable definition of a kind of behaviour. It is registered in `AGISNodeTypeRegistry` under a string type ID (e.g. `"npc.follow_target"`, `"agis.dialogue"`) and a GUID.

Every state type implements two interfaces:

- **`IAGISNodeType`** — The static, design-time face of the type: TypeId / DisplayName / Kind / Schema, and a `CreateRuntime(args)` factory that produces a fresh runtime instance per node activation.
- **`IAGISNodeRuntime`** — The live, per-activation face: `Enter(args)` called once on activation, `Tick(args, dt)` called every frame, `Exit(args)` called once on deactivation.

The args object passed to Enter / Tick / Exit provides the full context the runtime needs: the actor's component references, the `AGISActorState`, the schema parameter values for this specific node instance, and access to any other registered service.

Separating the type (static, shared, one instance per registration) from the runtime (live, one instance per active node) means state types are stateless singletons. All mutable state goes into the runtime instance or into `AGISActorState`.

Optional interfaces extend a state type's capabilities:

- **`IAGISNodeSignal`** — Exposes an `IsComplete` bool. The transition system checks this via `AGISNodeCompleteConditionType` — an outgoing edge fires when the state reports it is done, without the state needing to know about the graph around it.
- **`IAGISPersistentNodeType`** — Declares a list of `AGISActorState` keys the type needs (with types and defaults). The runner calls `EnsureKey` for each on startup. Keys already present are left untouched, enabling resume behaviour across sessions.

### 1.5 Condition Types — Transition Logic

A condition type is the reusable definition of a boolean test. It is registered in `AGISConditionTypeRegistry` and implements `IAGISConditionType`, providing a TypeId, DisplayName, Schema, and an `Evaluate(args) → bool` method.

The args object gives the condition access to the actor state, the schema parameter values configured for this specific condition instance, and the currently active node runtime (for completion checks).

Conditions are stateless. All information they need comes through args. A condition type is never instantiated per-edge — the same singleton evaluates every edge that references it.

### 1.6 Condition Expression Trees

The condition on a transition edge is not a single condition — it is an expression tree. `AGISConditionExprDef` supports five node kinds:

- **And(children)** — True when all children are true
- **Or(children)** — True when any child is true
- **Not(child)** — True when the child is false
- **Leaf(conditionDef)** — Evaluates a single condition type instance with its configured params
- **ConstBool(value)** — A hardcoded true or false

This lets designers compose arbitrarily complex transition logic without writing code. An edge might fire when "(detects player OR alert level > 3) AND NOT already in combat". The expression tree is serialized directly in the edge definition and evaluated in full on every tick.

A null condition on an edge evaluates as false. An edge with `ConstBool(true)` is an unconditional transition — it fires immediately after `Tick()` unless a higher-priority edge fires first.

### 1.7 The Transition System — Per-Tick Evaluation

Every frame, for the currently active node, the instance:

1. Calls `Tick(dt)` on the active node runtime.
2. Iterates all outgoing edges in priority order (highest first).
3. For each edge, checks three gates in sequence:
   - **Scope eligibility** — If the edge exits a Grouped node, it is only eligible when the Grouped node's internal state is inside the declared scope. This prevents transitions from firing when the sub-graph is mid-execution in an ineligible state.
   - **Transition policy** — Cooldown: the edge cannot re-fire until its cooldown period has elapsed since it last fired. Interruptible flag: if false, the edge will not fire while certain categories of action are in progress.
   - **Condition expression** — The full expression tree is evaluated.
4. The first edge that passes all three gates fires: `Exit()` on the current state, `Enter()` on the target state.

Only one transition fires per tick. Priority determines which one wins when multiple conditions are simultaneously true.

### 1.8 AGISActorState — The Actor's Persistent Memory

`AGISActorState` is a serialized MonoBehaviour that lives on the actor alongside the runner. It is a typed key-value store — a blackboard — persisting across all state transitions and all slots for the lifetime of the actor.

Supported value types: Bool, Int, Float, String, Vector2, Vector3, Guid.

Keys are populated at startup through two discovery paths:

1. Every node type in every assigned graph that implements `IAGISPersistentNodeType` declares the keys it needs.
2. Any MonoBehaviour component on the actor that implements `IAGISPersistentNodeType` does the same.

`EnsureKey` adds a key with its default value only if the key is not already present. This means an actor that was saved mid-session resumes with its previous values intact — the startup scan never overwrites live state.

State types read and write the actor state via typed helpers (`GetBool`, `GetInt`, `GetFloat`, `GetString`, `Set`). Condition types read it via the same API. Because all slots share one `AGISActorState`, they communicate through it naturally: one slot writes a key, another reads it to influence its transitions.

### 1.9 Multi-Slot Actors

A single runner hosts any number of named slots. Each slot runs its own compiled graph simultaneously and independently. This allows an actor's concerns to be separated into parallel graphs that do not need to know about each other — for example, a stealth awareness graph tracking detection state, a behaviour graph handling patrol and combat, and a dialogue graph managing conversation state, all running concurrently.

All slots share `AGISActorState` as their communication channel. One slot writes a key; another reads it in its transition conditions. No slot needs a direct reference to another. Slots are compiled and started independently — one slot failing validation does not prevent others from running.

### 1.10 Hierarchical Nodes — Grouped States

A Grouped node encapsulates a complete sub-graph (stored as an `AGISGroupedStateAsset`) as a single node in a parent graph. From the parent graph's perspective it is an ordinary node with Enter / Tick / Exit. Internally it runs its own `AGISStateMachineInstance`.

**Parameter promotion:** Any parameter from any node inside the sub-graph can be promoted to the Grouped node's outer inspector. The designer sees and tunes only the promoted parameters; the internal structure is encapsulated. This makes Grouped nodes the primary reuse mechanism — author a behaviour once, configure it differently per actor.

**Scope gating:** Edges exiting a Grouped node carry a `scopeId`. The edge is only eligible to fire when the Grouped node's internal instance is currently active inside that scope. This allows the parent graph to express "exit this macro behaviour only when it has reached a specific internal checkpoint", not just when any tick happens.

### 1.11 Parallel Nodes

A Parallel node runs multiple branches simultaneously within a single slot. Each branch is a small independent graph. On every tick, all branches tick. The Parallel node exits when one of its branches signals completion (`IAGISNodeSignal.IsComplete = true`) or when an outgoing transition in the parent graph fires.

Parallel nodes are useful for behaviours that combine concurrent concerns — for example, playing a reaction animation while also scanning the environment — without dedicating a separate slot to each concern.

### 1.12 The Validator

`AGISGraphValidator` checks a UGC graph before execution and produces an `AGISGraphValidationReport` listing all findings with two severity levels:

- **Error** — Graph cannot execute correctly. Examples: no entry node, a node references a type that is not registered, an edge points to a node that does not exist in the graph, a required parameter is missing.
- **Warning** — Graph will execute but something is suspicious. Examples: an edge points to a dangling (unconnected) node, a parameter value is outside the recommended range, a node type reports its own custom warnings via its schema.

The visual editor runs validation on demand and overlays error/warning indicators directly on affected nodes and edges. The runner runs validation automatically at startup and logs all findings; a graph with errors is not started.

---

## 2. The Visual Graph Editor

The visual editor is an in-game overlay built entirely with UIToolkit Runtime (`UnityEngine.UIElements`). It has no dependency on UnityEditor and can run in implemented projects, making it suitable for modding tools, live debugging, or in-game content authoring. The editor is opened at runtime by pressing a configurable key (default: F) and renders as a full-screen overlay over the game view.

### 2.1 Layout

The editor is divided into five areas:

- **Tab bar** — One tab per open slot. Switching tabs loads that slot's graph.
- **Toolbar** — Save / Undo / Redo / Validate on the left. Add Node / Auto-Layout / Frame All / Frame Selected in the centre. Zoom display, Snap, Grid, Minimap, and Debug toggles on the right.
- **Canvas** — The main authoring surface. Pan with middle mouse or Alt+drag, zoom with the scroll wheel. Displays node cards and transition edges drawn as bezier curves, with a minimap overlay and a breadcrumb bar for sub-graph drill-down.
- **Right panel** — A context-sensitive inspector with four tabs: Node Inspector (type info, param fields, validation issues), Edge / Condition editor (condition expression tree for the selected transition), Graph Properties (graph name, entry node, save / revert), and Grouped Asset inspector (exposed parameter bindings).
- **Status bar** — Live mode indicator, dirty flag, node count, and last message.

### 2.2 Node Cards

Each node is rendered as a card showing its type display name, type ID, kind colour, a gold star on the entry node, an output port handle for dragging edges, and an [x] delete button. Collapsed cards show only the header.

Node kind colours:

- **Normal** — Steel blue (#3A7BD5)
- **Grouped** — Teal (#1A8B7A)
- **Parallel** — Purple (#6B3FA0)
- **AnyState** — Dark crimson (#8B1A1A)
- **Entry** — Gold star overlay (any kind)

### 2.3 Creating Content in the Editor

- **Add node** — Spacebar or the [+ Node] toolbar button. Opens a fuzzy search window listing all registered state types grouped by category. New nodes are placed below previous ones so they never stack.
- **Set entry** — Right-click any node → Set as Entry. The first node added to an empty graph is automatically set as entry.
- **Create edge** — Drag from a node's output port to another node. Dropping on empty canvas opens the node search window, then auto-creates the edge to the newly placed node.
- **Select edge** — Click the pill label on any transition. The right panel switches to the Edge / Condition tab.
- **Edit condition** — With an edge selected, build an expression tree (And / Or / Not / Leaf / ConstBool) in the right panel using any registered condition type.
- **Delete** — Select a node or edge and press Delete.
- **Undo / Redo** — Ctrl+Z / Ctrl+Y. Full named command history.
- **Save** — Ctrl+S or the Save toolbar button. Writes back to the graph asset on disk.
- **Validate** — Runs `AGISGraphValidator`. Error and warning indicators appear as coloured overlays directly on affected nodes and edges.

### 2.4 Type Authoring — Code Editor Panel

Beyond editing graph structure, the visual editor supports authoring entirely new state and condition types from within the same UI. A dedicated panel provides a display name and type ID field for the new type, a parameter schema builder (add, remove, and configure schema params with type and default value), three code text areas for the Enter, Tick, and Exit bodies, a private fields and helpers section for internal variables and utility methods, and a Compile button with inline error and warning output.

On compile, the editor assembles a complete C# class from the panel inputs, wrapping the body code in the correct `IAGISNodeType` / `IAGISNodeRuntime` scaffolding, and passes it to the runtime Roslyn compiler (`Microsoft.CodeAnalysis.CSharp`). The resulting type is instantiated and registered under its GUID, and is immediately available in the node search window like any built-in type. The source code is stored in the content library against the GUID and recompiled automatically on the next session startup.

This compilation path is the same one the AI authoring layer uses. A type authored by a designer in this panel and a type generated by the AI are registered identically and are indistinguishable to the rest of the system.

### 2.5 Scene Setup Tool

A Unity Editor menu item (AGIS → Setup Runtime Editor Test Scene) creates a ready-to-use test scene: PanelSettings asset, AGIS Editor GameObject with UIDocument and `AGISGraphEditorWindow`, USS stylesheet wired, and the first `AGISStateMachineRunner` in the scene connected automatically.

---

## 3. Content Identity — GUIDs

Every piece of content in AGIS is assigned a GUID at creation time. This is its permanent, globally unique identifier. Graphs reference other content by GUID. The registries look up types by GUID. The serialized assets store GUIDs, not names.

Human-readable names are display metadata only. They can be changed without breaking any graph. This identity model is the foundation for the library and marketplace systems described in Part 2.

---

# Part 2 — The AI Authoring Layer

## 4. Overview

The AI authoring layer gives an AI system the same authoring capabilities a designer has through the visual editor — constructing graphs, configuring transitions, generating and compiling new state and condition types — but exercised programmatically, autonomously, and at runtime without human intervention.

The core principle is full authoring parity: everything the editor exposes to a designer, the AI can do through the same underlying pipeline. This is not a simplified scripting layer or a restricted template system — it is the same data structures, the same compiler, the same registry, driven by a different author.

---

## 5. What the AI Can Create and Edit

### 5.1 State Machines and Graphs

The AI can construct a complete state machine from scratch, or modify an existing one, using the same data structures the editor reads and writes:

- **Create new graphs** — define nodes, connect them with transitions, assign priorities, configure condition expressions
- **Edit existing graphs** — add, remove, or rewire states and transitions at runtime without stopping the simulation
- **Configure conditions** — compose expression trees (And / Or / Not) from any available condition type
- **Tune parameters** — set every exposed schema value on every node and condition
- **Assemble Grouped States** — build reusable macro behaviours with promoted parameters, identical to those a designer would author in the editor

All of this is pure data manipulation. The AI produces a graph definition that the compiler and runner process identically to one authored by hand.

### 5.2 New State Types — Custom Logic Injection

Beyond editing graph structure, the AI can define entirely new kinds of states. Each state type provides three execution hooks:

- **`Enter(args)`** — Initialise movement, trigger animations, cache references, set up blackboard values
- **`Tick(args, dt)`** — Update logic every frame: poll sensors, drive behaviour, accumulate timers, respond to world state
- **`Exit(args)`** — Clean up: reset flags, notify other systems, persist results

The AI generates the full implementation of these hooks as C# code. The code is compiled at runtime into a real assembly, then registered in the node type registry under a GUID. From that point, the new state type is indistinguishable from a built-in one — it appears in the editor type picker, it can be placed in graphs, and it executes through the same Enter / Tick / Exit loop.

An AI-authored state type can include:

- **Private variables** — internal state that persists across `Tick()` calls for the lifetime of the active state (timers, counters, cached references, flags)
- **Support functions** — private helper methods, exactly as a developer would write them
- **Full Unity API access** — movement, animation, physics, audio, component queries — anything available to a developer is available to the AI
- **Full AGIS API access** — pathfinder interface, blackboard read/write, actor state keys, detection queries, route data
- **Optional interfaces** — completion signal (`IAGISNodeSignal`), persistent key declarations (`IAGISPersistentNodeType`)

### 5.3 New Condition Types — Custom Transition Logic

The same capability applies to conditions. The AI can define new condition types that evaluate arbitrary logic — accessing any game world data, sensor output, or blackboard value — to produce a true/false result used by the transition system. Once registered, an AI-authored condition is composable with And / Or / Not like any built-in condition, and appears in the editor's condition picker.

---

## 6. How It Fits the Existing Architecture

The AI authoring layer requires no changes to the core pipeline. The flow is:

1. AI generates state or condition implementation code (C#)
2. Runtime compiler produces a new assembly
3. New type is registered in the appropriate registry under its GUID
4. AI constructs or modifies a graph referencing that GUID
5. `AGISGraphCompiler` processes the graph
6. `AGISStateMachineRunner` executes it identically to any other graph

The compiler, runner, and transition evaluator are unaware of where a type came from. The registry is the only integration point.

---

## 7. Relationship to the Visual Editor

The visual editor and the AI authoring layer share the same data model and compose naturally. A designer can open an AI-authored graph in the editor, inspect it, and modify it. An AI can take a designer-authored graph, extend it, and hand it back. Grouped states authored by either source are reusable by both, and parameters promoted to a Grouped State's outer layer are tunable by either. There is no special format, no conversion step, and no information loss when crossing between the two authoring modes.

Both the designer and the AI have full authoring capability across the entire system:

- Create and edit graph structure
- Add, remove, and rewire transitions
- Configure condition expression trees
- Tune node and condition parameters
- Build and reuse Grouped States with promoted parameters
- Define new state types with custom Enter / Tick / Exit logic
- Define new condition types with custom evaluation logic
- Add private variables and helper functions to custom types

---

## 8. Content Library and Identity System

### 8.1 GUIDs as the Primary Identifier

Every piece of content — state type, condition type, grouped state, state machine — carries a permanent GUID. Graphs reference content by GUID, not by name. Names are display metadata that can be renamed or translated without breaking any reference. The backend stores GUIDs as primary keys.

### 8.2 Two-Tier Content Model

Not all content needs to be fully loaded at all times.

**Tier 1 — Preview** (always available locally): A lightweight metadata record containing everything needed to display the content in the editor and reason about it: GUID, display name, description, category, author, version, parameter schema, optional icon. The full catalogue of previews loads on startup and is small.

**Tier 2 — Implementation** (downloaded on demand): The actual content — C# source for AI-authored types, or asset data for grouped states and state machines. Downloaded once, cached locally, then registered in the runtime registry. After the first fetch there is no network round-trip.

### 8.3 When an Implementation Is Downloaded

An implementation is fetched exactly once and then cached locally. The three triggers are:

- **Project startup** — The project declares a manifest of required GUIDs. The system batch-downloads anything not already cached before gameplay begins.
- **Visual editor** — When a designer places a type for the first time, the implementation is fetched if not cached. The editor shows a loading state and proceeds once ready.
- **AI authoring** — When the AI builds or extends a graph, the system resolves all referenced implementations before compiling.

### 8.4 The Content Library

The content library is the single in-memory store that the editor, the AI, and the runtime all read from. It maintains two stores:

**Preview store** (always populated): state type previews, condition type previews, Grouped State previews, State Machine previews. The editor and AI query this for type lists, schema inspection, and reasoning about available content.

**Implementation cache** (populated on demand): registered state types (compiled, live in registry), registered condition types, Grouped State assets, State Machine graph assets. The runtime only ever touches this side.

### 8.5 The Project Manifest

Each project carries a manifest — a flat list of GUIDs for every type, grouped state, and graph it requires. At startup, the content system compares the manifest against the local cache and downloads anything missing before the first scene loads. Content added dynamically by the AI during play — types authored or fetched at runtime that were not anticipated at build time — is downloaded on demand.

### 8.6 Foundation for a Content Marketplace

The two-tier model and GUID identity system are designed to support a marketplace where users can publish, discover, and download states, conditions, grouped states, and full state machine templates. The marketplace layer sits entirely outside the state machine — it manages publishing, discovery, and distribution. The state machine only ever sees the result: a preview added to the local library and an implementation fetched when needed. A community-authored state downloaded from the marketplace is handled identically to a built-in one.

---

## 9. AI Authoring Use Cases

**Generative content** — An AI generates unique behaviour graphs for each enemy encounter, producing variety that would be impractical to author manually for every variant.

**Adaptive behaviour** — An AI monitors gameplay and modifies a live state machine in response — adding states, extending transitions, and adjusting conditions based on what it observes the player doing.

**Assisted authoring** — A designer describes intent in natural language. The AI translates it into a concrete graph with states, transitions, and conditions, which the designer then reviews and adjusts in the visual editor. Human and AI iterate together.

**Behaviour libraries** — An AI authors and packages reusable Grouped States. Designers drop them into their graphs and tune the exposed parameters. The available library grows over time without requiring developer involvement for every new behaviour.

**Procedural NPC systems** — At world generation time, an AI constructs tailored behaviour graphs for NPCs based on their role, environment, and relationships. Each NPC's behaviour fits its specific context rather than being selected from a fixed template set.

**Community content** — A designer publishes a complex attack pattern as a Grouped State. Other users add it to their library, inspect its parameters in the editor, and place it in their own graphs. The implementation downloads the first time it is needed.
