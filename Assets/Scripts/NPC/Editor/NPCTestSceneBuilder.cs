// File: NPCTestSceneBuilder.cs
// Folder: Assets/Scripts/NPC/Editor/
// Purpose: One-click editor tool that builds a complete routed-movement test scene.
//          Menu: AGIS/NPC/Build Routed Movement Test Scene
//
// What it creates:
//   Assets/NPC_Test/
//     RouteData_Test.asset          — two patrol routes + sequence [0,1]
//     RoutedMovement.asset          — Routed Movement grouped state (internal graph)
//     PatrolGraph.asset             — template outer graph: Selector → Wander | RoutedMovement
//                                     Toggle npc.use_routes in AGISActorState to switch modes.
//     StealthGraph.asset            — single-node graph for the dedicated Stealth slot;
//                                     its npc.stealth_meter node fills/drains npc.detection_meter.
//
//   Scene objects:
//     Ground                        — 50×50 plane for the agent to walk on
//     A* Pathfinding                — AstarPath with a GridGraph (auto-scanned)
//     WalkTarget                    — empty GO the AIDestinationSetter tracks
//     NPC_Test                      — capsule with all required components wired up;
//                                     runner has 2 slots: Stealth (index 0) + Patrol (index 1)

#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.NPC;
using AGIS.NPC.Routes;
using Pathfinding;

namespace AGIS.NPC.Editor
{
    public static class NPCTestSceneBuilder
    {
        private const string AssetFolder = "Assets/NPC_Test";

        [MenuItem("AGIS/NPC/Build Routed Movement Test Scene")]
        public static void Build()
        {
            // ── Asset folder ──────────────────────────────────────────────────────────
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", "NPC_Test");

            // ── 1. RouteData ──────────────────────────────────────────────────────────
            var routeData = BuildRouteData();
            SaveAsset(routeData, "RouteData_Test.asset");

            // ── 2. Routed Movement grouped state ──────────────────────────────────────
            var routedMovement = NPCRoutedMovementAssetBuilder.BuildAsset();
            SaveAsset(routedMovement, "RoutedMovement.asset");

            // ── 3. Outer state machine graph (patrol logic) ───────────────────────────
            var graphAsset = BuildOuterGraph(routedMovement);
            SaveAsset(graphAsset, "PatrolGraph.asset");

            // ── 4. Stealth slot graph (meter fill/drain node, runs forever) ───────────
            var stealthGraph = BuildStealthGraph();
            SaveAsset(stealthGraph, "StealthGraph.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 5. Scene objects ──────────────────────────────────────────────────────
            CreateGround();
            var astar      = CreateAstarPathfinding();
            var walkTarget = CreateWalkTarget();
            CreateNPC(routeData, graphAsset, routedMovement, stealthGraph, walkTarget);

            // Scan the A* graph now that the ground plane exists.
            if (astar != null)
                astar.Scan();

            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[AGIS] Test scene built. Press Play to watch the NPC patrol its routes.");
        }

        // ── Asset builders ────────────────────────────────────────────────────────────

        private static NPCRouteData BuildRouteData()
        {
            var data = ScriptableObject.CreateInstance<NPCRouteData>();

            // Route 0: left triangle patrol
            var routeA = new NPCRoute { name = "Patrol A" };
            routeA.waypoints.Add(new Vector3(-8f, 0f,  0f));
            routeA.waypoints.Add(new Vector3(-4f, 0f,  6f));
            routeA.waypoints.Add(new Vector3(-4f, 0f, -6f));

            // Route 1: right triangle patrol
            var routeB = new NPCRoute { name = "Patrol B" };
            routeB.waypoints.Add(new Vector3( 4f, 0f, -6f));
            routeB.waypoints.Add(new Vector3( 8f, 0f,  0f));
            routeB.waypoints.Add(new Vector3( 4f, 0f,  6f));

            data.routes.Add(routeA);
            data.routes.Add(routeB);

            // Sequence: A → B → A → B → ...
            data.sequence.Add(0);
            data.sequence.Add(1);

            return data;
        }

        /// <summary>
        /// Builds the stealth slot graph — a single permanent npc.stealth_meter node with no
        /// outgoing edges. The node runs forever, filling/draining npc.detection_meter each tick.
        /// </summary>
        private static AGISStateMachineGraphAsset BuildStealthGraph()
        {
            var asset = ScriptableObject.CreateInstance<AGISStateMachineGraphAsset>();

            var meterNode = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.stealth_meter",
            };

            asset.graph.nodes.Add(meterNode);
            asset.graph.entryNodeId = meterNode.nodeId;
            // No edges — the node is permanent and runs until the slot is disabled.

            return asset;
        }

        /// <summary>
        /// Builds the template enemy outer graph:
        ///
        ///   [Any State] ──(npc.is_dying = true,  priority 20)──────────────────────► [Dying]
        ///   [Any State] ──(npc.is_damaged = true AND npc.is_dead = false, priority 10)──► [TakeDamage]
        ///
        ///         (entry)
        ///   [BehaviorSelector]
        ///     | priority 3: meter ≥ max AND !dead → [Chase]
        ///     | priority 2: meter ≥ 1.0 AND !dead → [Investigate]
        ///     | priority 1: npc.use_routes = true  → [RoutedMovement grouped]
        ///     | priority 0: ConstBool(true)         → [Wander]
        ///
        ///   [Chase]            →  [Investigate]     (npc.has_lost_target timeout=3, priority 1)
        ///   [Chase]            →  [BehaviorSelector] (NOT meter≥0.05, priority 0)
        ///   [Investigate]      →  [Chase]            (meter ≥ max, priority 1)
        ///   [Investigate]      →  [BehaviorSelector] (NOT meter≥1.0, priority 0)
        ///   [Wander]           →  [BehaviorSelector] (npc.use_routes = true, priority 1)
        ///   [Wander]           →  [BehaviorSelector] (meter ≥ 1.0, priority 0)
        ///   [RoutedMovement]   →  [BehaviorSelector] (npc.use_routes = false, priority 1)
        ///   [RoutedMovement]   →  [BehaviorSelector] (meter ≥ 1.0, priority 0)
        ///   [TakeDamage]       →  [BehaviorSelector] (agis.node_complete)
        ///   [Dying]            →  [DeadIdle]         (agis.node_complete)
        ///   [DeadIdle]                                (terminal — no outgoing edges)
        ///
        /// • NPCDetectionMeter fills npc.detection_meter while the player is in the cone.
        /// • Selector priority 3/2 route to Chase/Investigate based on meter value.
        /// • Wander/RoutedMovement return to Selector on detection so priorities re-evaluate.
        /// </summary>
        private static AGISStateMachineGraphAsset BuildOuterGraph(AGISGroupedStateAsset routedMovement)
        {
            var asset = ScriptableObject.CreateInstance<AGISStateMachineGraphAsset>();

            // ── Nodes ─────────────────────────────────────────────────────────────────

            var nodeAnyState = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "agis.any_state",
            };

            var nodeSelector = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.behavior_selector",
            };

            var nodeChase = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.follow_target",
            };
            nodeChase.@params.Set("follow_player",        AGISValue.FromBool(true));
            nodeChase.@params.Set("use_detection_memory", AGISValue.FromBool(true));
            nodeChase.@params.Set("pursuit_range_bonus",  AGISValue.FromFloat(4f));
            nodeChase.@params.Set("pursuit_angle_bonus",  AGISValue.FromFloat(20f));

            var nodeInvestigate = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.investigate",
                // All schema params use their defaults.
            };

            var nodeWander = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.wander",
            };

            var nodeRouted = new AGISNodeInstanceDef
            {
                nodeId       = AGISGuid.New(),
                nodeTypeId   = AGISGroupedNodeType.TYPE_ID,
                groupAssetId = routedMovement.groupAssetId,
            };

            var nodeTakeDamage = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.take_damage",
                // damage_flag_key defaults to "npc.is_damaged" — no override needed.
            };

            var nodeDying = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.dying",
                // animation_trigger="Die", animation_state="Death", death_flag_key="npc.is_dead" — all defaults.
            };

            // Terminal idle — NPC is frozen here after the death animation completes.
            // No outgoing edges; this node is never exited by the state machine.
            var nodeDeadIdle = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.idle",
            };

            asset.graph.nodes.Add(nodeAnyState);
            asset.graph.nodes.Add(nodeSelector);
            asset.graph.nodes.Add(nodeChase);
            asset.graph.nodes.Add(nodeInvestigate);
            asset.graph.nodes.Add(nodeWander);
            asset.graph.nodes.Add(nodeRouted);
            asset.graph.nodes.Add(nodeTakeDamage);
            asset.graph.nodes.Add(nodeDying);
            asset.graph.nodes.Add(nodeDeadIdle);
            asset.graph.entryNodeId = nodeSelector.nodeId;

            // ── Condition helpers ─────────────────────────────────────────────────────

            // Condition: npc.use_routes == true
            var condUseRoutes = new AGISConditionInstanceDef
            {
                conditionId     = AGISGuid.New(),
                conditionTypeId = "npc.actor_state_bool",
            };
            condUseRoutes.@params.Set("key",      AGISValue.FromString("npc.use_routes"));
            condUseRoutes.@params.Set("expected", AGISValue.FromBool(true));

            // Condition: npc.is_dead == false  (guards TakeDamage interrupt while dead)
            var condNotDead = new AGISConditionInstanceDef
            {
                conditionId     = AGISGuid.New(),
                conditionTypeId = "npc.actor_state_bool",
            };
            condNotDead.@params.Set("key",      AGISValue.FromString("npc.is_dead"));
            condNotDead.@params.Set("expected", AGISValue.FromBool(false));

            // Condition: meter >= maxDetection (use_max_detection = true → full alert)
            var condMeterFull = new AGISConditionInstanceDef
            {
                conditionId     = AGISGuid.New(),
                conditionTypeId = "npc.detection_meter_exceeds",
            };
            condMeterFull.@params.Set("use_max_detection", AGISValue.FromBool(true));

            // Condition: meter >= 1.0 (investigate threshold)
            var condMeterInvestigate = new AGISConditionInstanceDef
            {
                conditionId     = AGISGuid.New(),
                conditionTypeId = "npc.detection_meter_exceeds",
            };
            condMeterInvestigate.@params.Set("threshold",         AGISValue.FromFloat(1.0f));
            condMeterInvestigate.@params.Set("use_max_detection", AGISValue.FromBool(false));

            // Condition: meter >= 0.05 (used negated: NPC gives up chase when meter nearly drained)
            var condMeterTiny = new AGISConditionInstanceDef
            {
                conditionId     = AGISGuid.New(),
                conditionTypeId = "npc.detection_meter_exceeds",
            };
            condMeterTiny.@params.Set("threshold",         AGISValue.FromFloat(0.05f));
            condMeterTiny.@params.Set("use_max_detection", AGISValue.FromBool(false));

            // ── Edges ─────────────────────────────────────────────────────────────────

            // AnyState → Dying  (highest priority; npc.is_dying = true on blackboard)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeAnyState.nodeId,
                toNodeId   = nodeDying.nodeId,
                condition  = AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
                {
                    conditionId     = AGISGuid.New(),
                    conditionTypeId = "npc.blackboard_bool",
                    @params         = BuildParamTable("key",      AGISValue.FromString("npc.is_dying"),
                                                     "expected", AGISValue.FromBool(true)),
                }),
                priority = 20,
                policy   = new AGISTransitionPolicy { interruptible = true, cooldownSeconds = 0f },
            });

            // AnyState → TakeDamage  (npc.is_damaged = true AND npc.is_dead = false, 0.5s cooldown)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeAnyState.nodeId,
                toNodeId   = nodeTakeDamage.nodeId,
                condition  = AGISConditionExprDef.And(
                    AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
                    {
                        conditionId     = AGISGuid.New(),
                        conditionTypeId = "npc.blackboard_bool",
                        @params         = BuildParamTable("key",      AGISValue.FromString("npc.is_damaged"),
                                                         "expected", AGISValue.FromBool(true)),
                    }),
                    AGISConditionExprDef.Leaf(condNotDead)
                ),
                priority = 10,
                policy   = new AGISTransitionPolicy { interruptible = true, cooldownSeconds = 0.5f },
            });

            // Selector → Chase  (meter at max AND not dead, highest priority)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeSelector.nodeId,
                toNodeId   = nodeChase.nodeId,
                condition  = AGISConditionExprDef.And(
                    AGISConditionExprDef.Leaf(condMeterFull),
                    AGISConditionExprDef.Leaf(condNotDead)
                ),
                priority = 3,
            });

            // Selector → Investigate  (meter ≥ investigate threshold AND not dead)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeSelector.nodeId,
                toNodeId   = nodeInvestigate.nodeId,
                condition  = AGISConditionExprDef.And(
                    AGISConditionExprDef.Leaf(condMeterInvestigate),
                    AGISConditionExprDef.Leaf(condNotDead)
                ),
                priority = 2,
            });

            // Selector → RoutedMovement  (use_routes = true)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeSelector.nodeId,
                toNodeId   = nodeRouted.nodeId,
                condition  = AGISConditionExprDef.Leaf(condUseRoutes),
                priority   = 1,
            });

            // Selector → Wander  (fallback — always true, lowest priority)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeSelector.nodeId,
                toNodeId   = nodeWander.nodeId,
                condition  = AGISConditionExprDef.True(),
                priority   = 0,
            });

            // Chase → Investigate  (lost target for 3+ seconds)
            var condLostTarget = new AGISConditionInstanceDef
            {
                conditionId     = AGISGuid.New(),
                conditionTypeId = "npc.has_lost_target",
            };
            condLostTarget.@params.Set("timeout", AGISValue.FromFloat(3f));

            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeChase.nodeId,
                toNodeId   = nodeInvestigate.nodeId,
                condition  = AGISConditionExprDef.Leaf(condLostTarget),
                priority   = 1,
            });

            // Chase → Selector  (meter fully drained while in pursuit — NPC gives up)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeChase.nodeId,
                toNodeId   = nodeSelector.nodeId,
                condition  = AGISConditionExprDef.Not(AGISConditionExprDef.Leaf(condMeterTiny)),
                priority   = 0,
            });

            // Investigate → Chase  (meter refilled to max while investigating)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeInvestigate.nodeId,
                toNodeId   = nodeChase.nodeId,
                condition  = AGISConditionExprDef.Leaf(condMeterFull),
                priority   = 1,
            });

            // Investigate → Selector  (meter drained below investigate threshold)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeInvestigate.nodeId,
                toNodeId   = nodeSelector.nodeId,
                condition  = AGISConditionExprDef.Not(AGISConditionExprDef.Leaf(condMeterInvestigate)),
                priority   = 0,
            });

            // Wander → Selector  (when npc.use_routes is flipped to true at runtime)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeWander.nodeId,
                toNodeId   = nodeSelector.nodeId,
                condition  = AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
                {
                    conditionId     = AGISGuid.New(),
                    conditionTypeId = "npc.actor_state_bool",
                    @params         = BuildParamTable("key",      AGISValue.FromString("npc.use_routes"),
                                                     "expected", AGISValue.FromBool(true)),
                }),
                priority = 1,
            });

            // Wander → Selector  (detection meter triggered — let Selector re-route to Investigate/Chase)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeWander.nodeId,
                toNodeId   = nodeSelector.nodeId,
                condition  = AGISConditionExprDef.Leaf(condMeterInvestigate),
                priority   = 0,
            });

            // RoutedMovement → Selector  (when npc.use_routes is flipped to false at runtime)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeRouted.nodeId,
                toNodeId   = nodeSelector.nodeId,
                condition  = AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
                {
                    conditionId     = AGISGuid.New(),
                    conditionTypeId = "npc.actor_state_bool",
                    @params         = BuildParamTable("key",      AGISValue.FromString("npc.use_routes"),
                                                     "expected", AGISValue.FromBool(false)),
                }),
                priority = 1,
            });

            // RoutedMovement → Selector  (detection meter triggered — let Selector re-route)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeRouted.nodeId,
                toNodeId   = nodeSelector.nodeId,
                condition  = AGISConditionExprDef.Leaf(condMeterInvestigate),
                priority   = 0,
            });

            // TakeDamage → Selector  (once animation finishes)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeTakeDamage.nodeId,
                toNodeId   = nodeSelector.nodeId,
                condition  = AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
                {
                    conditionId     = AGISGuid.New(),
                    conditionTypeId = "agis.node_complete",
                }),
                priority = 0,
            });

            // Dying → DeadIdle  (once death animation finishes — terminal)
            asset.graph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeDying.nodeId,
                toNodeId   = nodeDeadIdle.nodeId,
                condition  = AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
                {
                    conditionId     = AGISGuid.New(),
                    conditionTypeId = "agis.node_complete",
                }),
                priority = 0,
            });
            // nodeDeadIdle has no outgoing edges — the state machine stops transitioning here.

            return asset;
        }

        private static AGISParamTable BuildParamTable(
            string key1, AGISValue val1,
            string key2, AGISValue val2)
        {
            var table = new AGISParamTable();
            table.Set(key1, val1);
            table.Set(key2, val2);
            return table;
        }

        // ── Scene object builders ─────────────────────────────────────────────────────

        private static void CreateGround()
        {
            if (GameObject.Find("Ground") != null) return;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position   = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f); // 50×50 units
        }

        private static AstarPath CreateAstarPathfinding()
        {
            if (AstarPath.active != null) return AstarPath.active;

            var go    = new GameObject("A* Pathfinding");
            var astar = go.AddComponent<AstarPath>();

            var graph = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
            if (graph != null)
            {
                graph.SetDimensions(50, 50, 1f);
                graph.center          = new Vector3(0f, 0f, 0f);
                graph.collision.mask  = LayerMask.GetMask("Default");
            }

            return astar;
        }

        private static GameObject CreateWalkTarget()
        {
            var existing = GameObject.Find("WalkTarget");
            if (existing != null) return existing;

            var go = new GameObject("WalkTarget");
            go.transform.position = Vector3.zero;
            return go;
        }

        private static void CreateNPC(
            NPCRouteData               routeData,
            AGISStateMachineGraphAsset  graphAsset,
            AGISGroupedStateAsset       routedMovement,
            AGISStateMachineGraphAsset  stealthGraph,
            GameObject                  walkTarget)
        {
            if (GameObject.Find("NPC_Test") != null)
            {
                Debug.LogWarning("[AGIS] NPC_Test already exists in the scene. Skipping NPC creation.");
                return;
            }

            // Capsule mesh so the NPC is visible.
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "NPC_Test";
            npc.transform.position = new Vector3(0f, 1f, 0f);

            // Rigidbody — freeze all rotations and Y position so the capsule stays upright.
            var rb = npc.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

            // A* Pathfinding components.
            npc.AddComponent<Seeker>();
            var aiPath = npc.AddComponent<AIPath>();
            aiPath.maxSpeed           = 5f;
            aiPath.slowdownDistance   = 1f;
            aiPath.endReachedDistance = 0.5f;
            aiPath.enabled            = false;    // disabled until state machine activates

            var destSetter = npc.AddComponent<AIDestinationSetter>();
            destSetter.target  = walkTarget.transform;
            destSetter.enabled = false;

            // Detection cone — default 60° / 10 unit range on all layers.
            // npc.show_detection_cone is auto-populated false by IAGISPersistentComponent.
            var cone = npc.AddComponent<NPCDetectionCone>();
            cone.range = 10f;
            cone.angle = 60f;
            cone.detectionMask = ~0;

            // Detection meter — fills while the player is in the cone, drains when not.
            // npc.detection_meter and npc.last_known_target_pos are auto-populated in AGISActorState.
            var meter = npc.AddComponent<NPCDetectionMeter>();
            meter.fillRate             = 1.0f;
            meter.drainRate            = 0.4f;
            meter.maxDetection         = 3.0f;
            meter.investigateThreshold = 1.0f;
            meter.targetTag            = "Player";

            // Route data — also declares npc.use_routes via IAGISPersistentComponent.
            var holder = npc.AddComponent<NPCRouteDataHolder>();
            holder.routeData = routeData;

            // AGIS actor runtime (owns the blackboard).
            npc.AddComponent<AGISActorRuntime>();

            // State machine runner.
            var runner = npc.AddComponent<AGISStateMachineRunner>();

            // Wire up via serialized fields using SerializedObject.
            WireRunner(runner, graphAsset, stealthGraph, routedMovement);
        }

        private static void WireRunner(
            AGISStateMachineRunner     runner,
            AGISStateMachineGraphAsset  graphAsset,
            AGISStateMachineGraphAsset  stealthGraph,
            AGISGroupedStateAsset       routedMovement)
        {
            var so = new SerializedObject(runner);

            // autoRegisterTypesFromAssemblies = true  (auto-discovers all IAGISNodeType/Condition)
            so.FindProperty("autoRegisterTypesFromAssemblies").boolValue = true;

            // Debug trace enabled so we can see transitions in the console.
            var traceProp = so.FindProperty("trace");
            if (traceProp != null)
            {
                var enabledProp = traceProp.FindPropertyRelative("enabled");
                if (enabledProp != null) enabledProp.boolValue = true;
            }

            // Two slots:
            //   [0] Stealth — ticks the detection meter every frame (runs before Patrol)
            //   [1] Patrol  — main behavior graph that reads the meter via conditions
            var slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = 2;

            var stealthSlot = slotsProp.GetArrayElementAtIndex(0);
            stealthSlot.FindPropertyRelative("slotName").stringValue = "Stealth";
            stealthSlot.FindPropertyRelative("enabled").boolValue    = true;
            stealthSlot.FindPropertyRelative("graphAsset").objectReferenceValue = stealthGraph;

            var patrolSlot = slotsProp.GetArrayElementAtIndex(1);
            patrolSlot.FindPropertyRelative("slotName").stringValue = "Patrol";
            patrolSlot.FindPropertyRelative("enabled").boolValue    = true;
            patrolSlot.FindPropertyRelative("graphAsset").objectReferenceValue = graphAsset;

            // Register the Routed Movement grouped asset so the runner can resolve it.
            var knownProp = so.FindProperty("knownGroupedAssets");
            knownProp.arraySize = 1;
            knownProp.GetArrayElementAtIndex(0).objectReferenceValue = routedMovement;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static void SaveAsset(Object asset, string fileName)
        {
            string path = Path.Combine(AssetFolder, fileName);
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}

#endif
