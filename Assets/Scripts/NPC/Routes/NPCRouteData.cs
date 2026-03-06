// File: NPCRouteData.cs
// Folder: Assets/Scripts/NPC/Routes/
// Purpose: ScriptableObject that holds a named library of routes and the sequence in which
//          to traverse them. Assign this asset to a NPCRouteDataHolder on the NPC GameObject.
//
// Example setup:
//   routes:
//     [0] "Guard Post A"  — waypoints: SpawnPoint → Door → Window → SpawnPoint
//     [1] "Guard Post B"  — waypoints: CorridorStart → Mid → End
//     [2] "Shortcut"      — waypoints: JunctionA → JunctionB
//   sequence: [0, 1, 2, 1]
//   → NPC walks: Post A → Post B → Shortcut → Post B → (loop: Post A → ...)

using System.Collections.Generic;
using UnityEngine;

namespace AGIS.NPC.Routes
{
    [CreateAssetMenu(menuName = "AGIS/NPC/Route Data", fileName = "AGIS_RouteData")]
    public class NPCRouteData : ScriptableObject
    {
        [Tooltip("All available routes. Each route is a named list of waypoints.")]
        [SerializeField] public List<NPCRoute> routes = new List<NPCRoute>();

        [Tooltip("Ordered sequence of route indices to execute. The NPC traverses routes in this " +
                 "order, looping back to the first entry when the list is exhausted. " +
                 "Example: [0, 1, 2, 1] — plays route 0 then 1 then 2 then 1, then loops.")]
        [SerializeField] public List<int> sequence = new List<int>();

        /// <summary>Returns the route referenced by the sequence entry at <paramref name="sequenceIndex"/>, or null if out of range.</summary>
        public NPCRoute GetRouteAtSequenceIndex(int sequenceIndex)
        {
            if (sequence == null || routes == null) return null;
            if (sequenceIndex < 0 || sequenceIndex >= sequence.Count) return null;
            int routeIndex = sequence[sequenceIndex];
            if (routeIndex < 0 || routeIndex >= routes.Count) return null;
            return routes[routeIndex];
        }

        public int SequenceLength => sequence != null ? sequence.Count : 0;

        /// <summary>
        /// Returns false if the sequence or routes list is empty, or if any sequence entry references
        /// a route index that doesn't exist. Call this in the editor to catch configuration errors early.
        /// </summary>
        public bool Validate(out string error)
        {
            if (routes == null || routes.Count == 0) { error = "No routes defined."; return false; }
            if (sequence == null || sequence.Count == 0) { error = "Sequence list is empty."; return false; }

            for (int i = 0; i < sequence.Count; i++)
            {
                int ri = sequence[i];
                if (ri < 0 || ri >= routes.Count)
                {
                    error = $"Sequence[{i}] = {ri} references a route that does not exist (routes.Count = {routes.Count}).";
                    return false;
                }
                var route = routes[ri];
                if (route == null || route.WaypointCount == 0)
                {
                    error = $"Route {ri} (referenced by sequence[{i}]) has no waypoints.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
