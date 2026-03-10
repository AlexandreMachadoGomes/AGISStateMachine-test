AGIS AI Authoring Layer
AGIS_ESM — AI-Driven State Machine Creation


OVERVIEW

The AGIS Enhanced State Machine already provides a powerful, data-driven architecture for defining NPC and actor behaviour through a visual editor. The next layer extends this further: rather than a human user building and editing state machines through the UI, an AI system would be able to perform the same operations — constructing graphs, defining transitions, creating entirely new states and conditions, and injecting custom logic into them — all at runtime, using the same underlying hooks the editor itself uses.

The core principle is full authoring parity: anything a designer can do through the visual editor, the AI can do programmatically. This is not a simplified scripting layer or a restricted template system — it is the same pipeline, driven by a different author.


1. WHAT THE AI CAN CREATE AND EDIT

1.1 State Machines and Graphs

The AI can construct a complete state machine from scratch, or modify an existing one, using the same data structures the editor reads and writes:

   • Create new graphs — define nodes, connect them with transitions, assign priorities
   • Edit existing graphs — add, remove, or rewire states and transitions at runtime
   • Configure transition conditions — compose condition expression trees (And / Or / Not) using any built-in or custom condition types
   • Set and modify parameters — tune every exposed value on every node and condition
   • Assemble Grouped States — build reusable macro behaviours with promoted parameters, identical to those a designer would author in the editor

All of this is pure data manipulation. The AI produces a graph definition that the runtime compiles and executes identically to one authored by hand.


1.2 New State Types — Custom Logic Injection

Beyond editing graph structure, the AI can define entirely new kinds of states that do not exist in the built-in library. This is the key capability that separates this system from a conventional scripting layer.

Each state type in AGIS has three execution hooks:

   • Enter() — runs once when the state becomes active (initialise movement, trigger animations, cache references, set up variables)
   • Tick(dt) — runs every frame while the state is active (update logic, poll sensors, drive behaviour, accumulate timers)
   • Exit() — runs once when the state is leaving (clean up, reset flags, notify other systems)

The AI can generate the full implementation of these hooks as code, which is compiled and registered at runtime. From that point, the new state type is available to any graph — it behaves identically to a built-in state, with no distinction at the engine level.

A new state type created by the AI can include:

   • Private variables — internal state that persists across Tick() calls for the duration the state is active (timers, counters, cached references, flags)
   • Support functions — private helper methods that structure the logic internally, exactly as a developer would write them
   • Full Unity API access — movement, animation, physics, audio, component queries — anything available to a developer is available to the AI-authored state
   • Full AGIS API access — pathfinder interface, blackboard read/write, actor state keys, detection queries, route data
   • Optional capability interfaces — the AI can elect to implement the same optional contracts a developer would, such as exposing a completion signal (so outgoing transitions can fire when the state declares it is done) or declaring persistent actor-state keys that survive across state transitions


1.3 New Condition Types — Custom Transition Logic

The same capability applies to conditions. The AI can define new condition types that evaluate arbitrary logic to produce a true/false result, which the transition system then uses normally. A new AI-authored condition has access to:

   • All actor state and blackboard data
   • Sensor and detection queries
   • Game world queries (distances, tags, physics overlaps)
   • Any custom parameters defined in its schema

Once registered, the condition is available to any transition in any graph, composable with And / Or / Not like any built-in condition.


2. HOW IT FITS THE EXISTING ARCHITECTURE

Each state type, condition type, grouped state, and state machine in AGIS is identified by a GUID — a globally unique identifier assigned at creation time and never changed. The runtime looks up everything by GUID through a central registry. This registry is the single integration point for all content, whether built-in, designer-authored, or AI-generated:

   1. AI generates code
   2. Compiler produces a real assembly at runtime
   3. New type is registered in the registry under its GUID
   4. AI constructs a graph referencing that GUID
   5. Runtime compiles and executes the graph identically to any built-in graph

No part of the execution pipeline — compiler, runner, transition evaluator — changes. The AI is simply a new kind of author feeding into the same front door the editor uses.


3. RELATIONSHIP TO THE VISUAL EDITOR

The visual editor and the AI authoring layer share the same underlying data model. This means they compose naturally:

   • A designer can open an AI-authored graph in the editor, inspect it, and modify it
   • An AI can take a designer-authored graph, extend it, and hand it back
   • Grouped states authored by either source are reusable by both
   • Parameters promoted to the outer layer of a Grouped State are tunable by either

There is no special format, no conversion step, and no loss of information when crossing between the two authoring modes.


4. SCOPE OF AI AUTHORING CAPABILITY

The table below outlines which capabilities are available to each authoring mode:

Capability                                    Designer (UI Editor)    AI Authoring Layer
─────────────────────────────────────────────────────────────────────────────────────────
Create / edit graph structure                       Yes                     Yes
Add / remove / rewire transitions                   Yes                     Yes
Configure condition expressions                     Yes                     Yes
Tune node and condition parameters                  Yes                     Yes
Build and reuse Grouped States                      Yes                     Yes
Promote parameters to outer layer                   Yes                     Yes
Define new state types with custom logic            No                      Yes
Define new condition types                          No                      Yes
Inject Enter / Tick / Exit behaviour                No                      Yes
Add private variables and helper functions          No                      Yes

The bottom four rows represent the unique contribution of the AI authoring layer. Above that line, the two authoring modes are equivalent.


5. CONTENT IDENTITY AND THE LIBRARY SYSTEM

5.1 GUIDs as the Primary Identifier

Every piece of content in AGIS — every state type, condition type, grouped state, and state machine — is assigned a GUID at creation time. This GUID is permanent and globally unique. It is what graphs reference internally, what the runtime registry looks up, and what the backend database stores as its primary key.

Human-readable names (e.g. "Patrol State", "Detects Player") are display metadata only. They can be renamed, translated, or versioned without breaking any graph that references the type, because all internal references use the GUID.


5.2 Two-Tier Content Model: Previews and Implementations

Not all content needs to be fully loaded at all times. The library distinguishes between two tiers:

Tier 1 — Preview (always available locally)

A lightweight metadata record for each known piece of content. It contains everything needed to display the content in the editor and understand what it does, but none of the actual implementation:

   • GUID
   • Display name and description
   • Content category (State / Condition / Grouped State / State Machine)
   • Author and version
   • Parameter schema — the list of configurable parameters and their types, so the editor can render the inspector panel and the AI can reason about what a type accepts
   • Thumbnail or icon reference (optional)

The full list of previews for all content a user has added to their library is kept locally at all times. It is small — a preview record is a short data object — and is loaded on startup. This is what populates the editor's content browser, the AI's catalogue of available types, and the type picker search windows.

Tier 2 — Implementation (downloaded on demand)

The actual content: source code for AI-authored states and conditions, or full asset data for grouped states and state machines. This is only downloaded when genuinely needed, and cached locally once retrieved.


5.3 When an Implementation Is Downloaded

The system downloads an implementation exactly once per installation, triggered by the first moment it is actually needed:

   • Game startup — the game declares a manifest of every GUID it requires. The system checks which of those are already cached locally and batch-downloads any that are not, before gameplay begins. This is the primary load path for shipped content.

   • Visual editor — when a designer places a type onto the canvas for the first time, the system fetches its implementation if not already present. The editor shows a lightweight loading state during the fetch and proceeds once ready.

   • AI authoring — when the AI constructs or extends a graph referencing a GUID, the system resolves the implementation before compiling the graph. If not cached, it is downloaded inline.

In all three cases, once the implementation is local it is registered in the runtime registry and available immediately. Subsequent uses hit the local cache with no network round-trip.


5.4 The Content Library

The content library is the single in-memory store that the editor, the AI, and the runtime all read from. It manages both tiers:

   Preview store (always populated)
      ○ State type previews
      ○ Condition type previews
      ○ Grouped State previews
      ○ State Machine previews

   Implementation cache (populated on demand)
      ○ Registered state types (compiled, live in registry)
      ○ Registered condition types (compiled, live in registry)
      ○ Grouped State assets
      ○ State Machine graph assets

The editor and AI query the preview store to show type lists, inspect schemas, and reason about available content. They trigger an implementation fetch only when a type is about to be used. The runtime only ever touches the implementation cache — it never needs previews.


5.5 The Game Manifest

Each game carries a manifest: a flat list of GUIDs for every state type, condition type, grouped state, and state machine graph it uses. The manifest is compact and ships with the game build.

At startup, the content system reads the manifest, compares it against the local cache, and downloads anything missing before the first scene loads. This guarantees all required implementations are present before gameplay begins, with no mid-session downloads for content the game already knows it needs.

Content added dynamically by the AI during play — types the AI authors or fetches from the library that were not in the original manifest — is downloaded on demand, since the game could not have anticipated them at build time.


5.6 Foundation for a Content Marketplace

The two-tier model and GUID identity system are designed from the start to support a content marketplace where users can publish, discover, and download states, conditions, grouped states, and full state machine templates authored by other users.

The marketplace layer sits entirely outside the state machine: it manages publishing, discovery, and distribution. The state machine only ever sees the result — a preview record added to the local library, and an implementation fetched when needed. From the state machine's perspective, a community-authored state downloaded from the marketplace is handled identically to a built-in one.


6. USE CASES

Generative content — an AI generates unique enemy behaviour graphs for each encounter, producing variety without manual authoring of every variant.

Adaptive behaviour — an AI monitors gameplay and extends or modifies a live state machine in response, adding new states or transition conditions based on what it observes.

Assisted authoring — a designer describes intent in natural language; the AI translates it into a concrete graph with states, transitions, and conditions, which the designer then reviews and adjusts in the editor.

Behaviour libraries — an AI authors and packages reusable Grouped States that designers can drop into their graphs and tune via exposed parameters, expanding the available building blocks over time without developer involvement.

Procedural NPC systems — at world generation time, an AI constructs tailored behaviour graphs for NPCs based on their role, environment, and relationships, producing behaviour that fits the specific context rather than selecting from a fixed set of templates.

Community content — a designer publishes a complex attack pattern as a Grouped State. Other users add it to their library, see its preview and parameters in the editor, and use it in their own graphs. The implementation is only downloaded the first time they place it on a canvas or their game starts with it in the manifest.
