// File: NPCRoutedMovementAssetBuilder.cs
// Folder: Assets/Scripts/NPC/Editor/
// Purpose: Editor utility that generates the "Routed Movement" AGISGroupedStateAsset with
//          its internal graph pre-wired. Run once via the menu then assign the resulting
//          .asset to any Grouped node in your state machine graphs.
//
// Generated internal graph:
//
//   (entry)
//   [Reset Route]
//        | ConstBool(true)
//        ▼
//   [Move To Waypoint] ◄────────────────────────┐
//        | npc.has_arrived_at_waypoint           |
//        ▼                                       |
//   [Advance Waypoint] ─── ConstBool(true) ──────┘
//
// The loop is infinite — the NPC traverses every route in the sequence and wraps back
// to the first entry when the sequence is exhausted.
//
// To build a "resume" variant (skip the reset), simply delete the Reset Route node and
// set Move To Waypoint as the entry node in the inspector.

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using AGIS.ESM.UGC;

namespace AGIS.NPC.Editor
{
    public static class NPCRoutedMovementAssetBuilder
    {
        [MenuItem("AGIS/NPC/Create Routed Movement Asset")]
        public static void CreateAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                title:       "Save Routed Movement Asset",
                defaultName: "RoutedMovement",
                extension:   "asset",
                message:     "Choose where to save the Routed Movement grouped state asset.");

            if (string.IsNullOrEmpty(path))
                return;

            var asset = BuildAsset();

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;

            Debug.Log($"[AGIS] Routed Movement asset created at {path}. " +
                      "Add it to the AGISStateMachineRunner's Known Grouped Assets list.");
        }

        /// <summary>
        /// Builds and returns the asset without saving it to disk.
        /// Useful for tests or programmatic setup.
        /// </summary>
        public static AGISGroupedStateAsset BuildAsset()
        {
            var asset = ScriptableObject.CreateInstance<AGISGroupedStateAsset>();
            asset.displayName = "Routed Movement";

            // ── Nodes ─────────────────────────────────────────────────────────────────

            var nodeReset = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.reset_route",
            };

            var nodeMove = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.move_to_waypoint",
            };

            var nodeAdvance = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = "npc.advance_waypoint",
            };

            asset.internalGraph.nodes.Add(nodeReset);
            asset.internalGraph.nodes.Add(nodeMove);
            asset.internalGraph.nodes.Add(nodeAdvance);

            // Entry is the reset node so the patrol always starts clean.
            asset.internalGraph.entryNodeId = nodeReset.nodeId;

            // ── Edges ─────────────────────────────────────────────────────────────────

            // Reset → MoveToWaypoint (unconditional)
            asset.internalGraph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeReset.nodeId,
                toNodeId   = nodeMove.nodeId,
                condition  = AGISConditionExprDef.True(),
                priority   = 0,
            });

            // MoveToWaypoint → AdvanceWaypoint (arrived at waypoint)
            asset.internalGraph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeMove.nodeId,
                toNodeId   = nodeAdvance.nodeId,
                condition  = AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
                {
                    conditionId     = AGISGuid.New(),
                    conditionTypeId = "npc.has_arrived_at_waypoint",
                }),
                priority = 0,
            });

            // AdvanceWaypoint → MoveToWaypoint (unconditional — loops forever)
            asset.internalGraph.edges.Add(new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = nodeAdvance.nodeId,
                toNodeId   = nodeMove.nodeId,
                condition  = AGISConditionExprDef.True(),
                priority   = 0,
            });

            return asset;
        }
    }
}

#endif
