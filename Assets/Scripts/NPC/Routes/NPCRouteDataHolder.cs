// File: NPCRouteDataHolder.cs
// Folder: Assets/Scripts/NPC/Routes/
// Purpose: Holds a reference to the NPCRouteData asset and provides read-only data access.
//          Runtime state (SequenceIndex, WaypointIndex) lives in AGISActorState.
//
// Implements IAGISPersistentComponent so AGISStateMachineRunner pre-populates
// npc.use_routes in AGISActorState at startup — no graph node required for this key.

using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC.Params;

namespace AGIS.NPC.Routes
{
    public sealed class NPCRouteDataHolder : MonoBehaviour, IAGISPersistentNodeType
    {
        // ── IAGISPersistentComponent ───────────────────────────────────────────────────

        public IReadOnlyList<AGISParamSpec> PersistentParams { get; } = new AGISParamSpec[]
        {
            new AGISParamSpec("npc.use_routes", AGISParamType.Bool, AGISValue.FromBool(false))
                { displayName = "Use Routes",
                  tooltip     = "When true the NPC follows defined patrol routes. " +
                                "When false it wanders randomly. Set this flag to switch modes at runtime." },
        };

        [Tooltip("The route data asset that defines all routes and their traversal sequence.")]
        public NPCRouteData routeData;

        public bool HasData => routeData != null && routeData.SequenceLength > 0;

        /// <summary>
        /// Returns the world-space position for the waypoint at the given sequence and
        /// waypoint indices. Returns false if the data or indices are invalid.
        /// </summary>
        public bool TryGetWaypoint(int sequenceIndex, int waypointIndex, out Vector3 waypoint)
        {
            var route = routeData?.GetRouteAtSequenceIndex(sequenceIndex);
            if (route == null)
            {
                waypoint = Vector3.zero;
                return false;
            }
            return route.TryGetWaypoint(waypointIndex, out waypoint);
        }

        /// <summary>Convenience: route name for the given sequence index.</summary>
        public string GetRouteName(int sequenceIndex)
            => routeData?.GetRouteAtSequenceIndex(sequenceIndex)?.name ?? "<none>";

        /// <summary>Waypoint count for the route at the given sequence index.</summary>
        public int GetWaypointCount(int sequenceIndex)
            => routeData?.GetRouteAtSequenceIndex(sequenceIndex)?.WaypointCount ?? 0;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (routeData?.routes == null) return;

            var palette = new Color[]
            {
                Color.cyan, Color.yellow, Color.green, Color.magenta,
                new Color(1f, 0.5f, 0f), Color.white, Color.red
            };

            for (int r = 0; r < routeData.routes.Count; r++)
            {
                var route = routeData.routes[r];
                if (route?.waypoints == null || route.WaypointCount == 0) continue;

                Gizmos.color = palette[r % palette.Length];
                for (int w = 0; w < route.waypoints.Count; w++)
                {
                    Gizmos.DrawSphere(route.waypoints[w], 0.25f);
                    if (w > 0)
                        Gizmos.DrawLine(route.waypoints[w - 1], route.waypoints[w]);
                }
                UnityEditor.Handles.Label(route.waypoints[0], $"[{r}] {route.name}");
            }

            // Highlight the current active waypoint using AGISActorState if available.
            var actorState = GetComponent<AGIS.ESM.Runtime.AGISActorState>();
            int seqIdx = actorState?.GetInt("npc.route.sequence_index") ?? 0;
            int wpIdx  = actorState?.GetInt("npc.route.waypoint_index") ?? 0;

            if (TryGetWaypoint(seqIdx, wpIdx, out var current))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(current, 0.5f);
            }
        }
#endif
    }
}
