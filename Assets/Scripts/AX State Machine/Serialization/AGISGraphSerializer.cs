// File: AGISGraphSerializer.cs
// Folder: Assets/Scripts/AX State Machine/Serialization/
// Purpose: JSON serialization / deserialization for all AGIS data types.
//          Uses Unity's built-in JsonUtility — no external packages required.
//
// JsonUtility respects [SerializeField] on private fields, so all internal
// union fields (AGISValue._type, AGISGuid._value, etc.) and the recursive
// AGISConditionExprDef tree all round-trip correctly out of the box.
//
// Usage:
//   // Serialize
//   string json = AGISGraphSerializer.ToJson(myGraph);
//   string json = AGISGraphSerializer.ToJson(myGroupedAsset);
//   string json = AGISGraphSerializer.ToJson(myRouteData);
//
//   // Deserialize (returns runtime-only ScriptableObject instances — not saved to disk)
//   AGISStateMachineGraphAsset asset  = AGISGraphSerializer.GraphFromJson(json);
//   AGISGroupedStateAsset      grouped = AGISGraphSerializer.GroupedFromJson(json);
//   NPCRouteData               routes  = AGISGraphSerializer.RouteDataFromJson(json);

using AGIS.ESM.UGC;
using AGIS.NPC.Routes;
using UnityEngine;

namespace AGIS.ESM.Runtime
{
    public static class AGISGraphSerializer
    {
        // ── State machine graphs ──────────────────────────────────────────────────
        // Serializes the inner AGISStateMachineGraph (the data), not the SO wrapper.
        // Store the result as a string/text column in your database.

        public static string ToJson(AGISStateMachineGraph graph, bool prettyPrint = false)
        {
            return JsonUtility.ToJson(graph, prettyPrint);
        }

        /// <summary>
        /// Deserializes a graph JSON string and returns a runtime-only
        /// AGISStateMachineGraphAsset (not backed by a disk asset).
        /// Pass the result directly to AGISStateMachineRunner.SetSlotGraphAsset().
        /// </summary>
        public static AGISStateMachineGraphAsset GraphFromJson(string json)
        {
            var graph = JsonUtility.FromJson<AGISStateMachineGraph>(json);
            var asset = ScriptableObject.CreateInstance<AGISStateMachineGraphAsset>();
            asset.graph = graph;
            return asset;
        }

        // ── Grouped states ────────────────────────────────────────────────────────
        // AGISGroupedStateAsset holds all its own data fields (internalGraph, scopes,
        // exposedParams, bindings) so we serialize the SO directly.

        public static string ToJson(AGISGroupedStateAsset grouped, bool prettyPrint = false)
        {
            return JsonUtility.ToJson(grouped, prettyPrint);
        }

        /// <summary>
        /// Deserializes a grouped state JSON string and returns a runtime-only
        /// AGISGroupedStateAsset. Register with runner.RegisterGroupedAsset() before
        /// calling RebuildAllSlots() on any graph that references it.
        /// </summary>
        public static AGISGroupedStateAsset GroupedFromJson(string json)
        {
            var instance = ScriptableObject.CreateInstance<AGISGroupedStateAsset>();
            JsonUtility.FromJsonOverwrite(json, instance);
            return instance;
        }

        // ── Routes ────────────────────────────────────────────────────────────────

        public static string ToJson(NPCRouteData routeData, bool prettyPrint = false)
        {
            return JsonUtility.ToJson(routeData, prettyPrint);
        }

        /// <summary>
        /// Deserializes a route data JSON string and returns a runtime-only NPCRouteData.
        /// Assign to NPCRouteDataHolder.routeData on the actor.
        /// </summary>
        public static NPCRouteData RouteDataFromJson(string json)
        {
            var instance = ScriptableObject.CreateInstance<NPCRouteData>();
            JsonUtility.FromJsonOverwrite(json, instance);
            return instance;
        }
    }
}
