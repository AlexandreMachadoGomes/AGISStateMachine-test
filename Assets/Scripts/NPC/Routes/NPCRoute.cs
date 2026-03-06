// File: NPCRoute.cs
// Folder: Assets/Scripts/NPC/Routes/
// Purpose: A single named route — an ordered list of world-space waypoints.
//          Stored as a list inside NPCRouteData.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGIS.NPC.Routes
{
    [Serializable]
    public class NPCRoute
    {
        [SerializeField] public string name = "Route";

        [Tooltip("Ordered world-space positions the NPC visits from first to last.")]
        [SerializeField] public List<Vector3> waypoints = new List<Vector3>();

        public int WaypointCount => waypoints != null ? waypoints.Count : 0;

        public bool TryGetWaypoint(int index, out Vector3 waypoint)
        {
            if (waypoints != null && index >= 0 && index < waypoints.Count)
            {
                waypoint = waypoints[index];
                return true;
            }
            waypoint = Vector3.zero;
            return false;
        }
    }
}
