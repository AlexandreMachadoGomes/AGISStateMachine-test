// File: AGISContentLibrary.cs
// Folder: Assets/Scripts/AX State Machine/Serialization/
// Purpose: Singleton MonoBehaviour that acts as the in-memory store for all
//          AGIS content downloaded from a remote database.
//
// Lifecycle:
//   1. Your HTTP client downloads a JSON blob for a graph/grouped state/route.
//   2. Call Import*(dbId, json) — the library deserializes and caches the asset.
//   3. Your UI reads GraphIds / GroupedIds / RouteIds to show a list.
//   4. User selects an item → call ApplyGraphToRunner() or get the asset directly.
//
// Setup:
//   Add this component to a DontDestroyOnLoad GameObject once at app start.
//   Access it via AGISContentLibrary.Instance anywhere in code.

using System.Collections.Generic;
using AGIS.ESM.UGC;
using AGIS.NPC.Routes;
using UnityEngine;

namespace AGIS.ESM.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AGISContentLibrary : MonoBehaviour
    {
        public static AGISContentLibrary Instance { get; private set; }

        // Keyed by your database's own string ID (whatever your DB uses — GUID, slug, int as string).
        // These are NOT AGISGuid — they're your backend record identifiers.
        private readonly Dictionary<string, AGISStateMachineGraphAsset> _graphs  = new Dictionary<string, AGISStateMachineGraphAsset>();
        private readonly Dictionary<string, AGISGroupedStateAsset>      _grouped = new Dictionary<string, AGISGroupedStateAsset>();
        private readonly Dictionary<string, NPCRouteData>               _routes  = new Dictionary<string, NPCRouteData>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── Import ────────────────────────────────────────────────────────────────
        // Call these after your HTTP client downloads a JSON record.
        // If a record with the same dbId already exists it is replaced.

        public void ImportGraph(string dbId, string json)
        {
            if (string.IsNullOrEmpty(dbId) || string.IsNullOrEmpty(json)) return;
            _graphs[dbId] = AGISGraphSerializer.GraphFromJson(json);
        }

        public void ImportGrouped(string dbId, string json)
        {
            if (string.IsNullOrEmpty(dbId) || string.IsNullOrEmpty(json)) return;
            _grouped[dbId] = AGISGraphSerializer.GroupedFromJson(json);
        }

        public void ImportRoute(string dbId, string json)
        {
            if (string.IsNullOrEmpty(dbId) || string.IsNullOrEmpty(json)) return;
            _routes[dbId] = AGISGraphSerializer.RouteDataFromJson(json);
        }

        // ── Remove ────────────────────────────────────────────────────────────────

        public bool RemoveGraph(string dbId)   => _graphs.Remove(dbId);
        public bool RemoveGrouped(string dbId) => _grouped.Remove(dbId);
        public bool RemoveRoute(string dbId)   => _routes.Remove(dbId);

        public void Clear()
        {
            _graphs.Clear();
            _grouped.Clear();
            _routes.Clear();
        }

        // ── Query ─────────────────────────────────────────────────────────────────
        // Use these to populate your UI list.

        public IReadOnlyCollection<string> GraphIds   => _graphs.Keys;
        public IReadOnlyCollection<string> GroupedIds => _grouped.Keys;
        public IReadOnlyCollection<string> RouteIds   => _routes.Keys;

        public bool TryGetGraph(string dbId, out AGISStateMachineGraphAsset asset)
            => _graphs.TryGetValue(dbId, out asset);

        public bool TryGetGrouped(string dbId, out AGISGroupedStateAsset asset)
            => _grouped.TryGetValue(dbId, out asset);

        public bool TryGetRoute(string dbId, out NPCRouteData data)
            => _routes.TryGetValue(dbId, out data);

        public bool ContainsGraph(string dbId)   => _graphs.ContainsKey(dbId);
        public bool ContainsGrouped(string dbId) => _grouped.ContainsKey(dbId);
        public bool ContainsRoute(string dbId)   => _routes.ContainsKey(dbId);

        // ── Apply to a runner ─────────────────────────────────────────────────────

        /// <summary>
        /// Assigns a downloaded graph to the specified slot on the runner and rebuilds.
        /// Also registers all currently-loaded grouped assets so the runner can resolve
        /// any Grouped nodes that reference them.
        /// Returns false if the dbId is not found in the library.
        /// </summary>
        public bool ApplyGraphToRunner(string dbId, AGISStateMachineRunner runner, int slotIndex = 0)
        {
            if (runner == null) return false;
            if (!_graphs.TryGetValue(dbId, out var asset)) return false;

            // Register every grouped asset the runner might need to resolve.
            foreach (var grouped in _grouped.Values)
                runner.RegisterGroupedAsset(grouped, rebuildIndex: false);

            runner.SetSlotGraphAsset(slotIndex, asset, rebuild: false);
            runner.RebuildAllSlots();
            return true;
        }

        /// <summary>
        /// Assigns a downloaded route to the NPCRouteDataHolder on the given actor.
        /// Returns false if the dbId is not found or the actor has no NPCRouteDataHolder.
        /// </summary>
        public bool ApplyRouteToActor(string dbId, GameObject actor)
        {
            if (actor == null) return false;
            if (!_routes.TryGetValue(dbId, out var data)) return false;

            var holder = actor.GetComponent<AGIS.NPC.Routes.NPCRouteDataHolder>();
            if (holder == null) return false;

            holder.routeData = data;
            return true;
        }
    }
}
