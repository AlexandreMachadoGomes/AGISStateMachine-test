// File: AGISNetworkReplicator.cs
// Folder: Assets/Scripts/AX State Machine/Networking/
// Purpose: Server-side MonoBehaviour that detects state machine transitions and flagged
//          ActorState key changes, then serializes and sends them via INetworkTransport.
//
// Usage:
//   1. Place alongside AGISStateMachineRunner on server-side NPC GameObjects.
//   2. Assign a concrete INetworkTransport implementation (e.g. WebSocket wrapper).
//   3. Set actorNetId to a unique string (e.g. the object's network ID).
//
// LateUpdate() runs after the runner's Update() so it sees all transitions that
// happened in the current frame before sending.

using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Networking
{
    [RequireComponent(typeof(AGISStateMachineRunner))]
    public sealed class AGISNetworkReplicator : MonoBehaviour
    {
        [SerializeField] private string actorNetId;
        [SerializeField] private AGISStateMachineRunner _runner;

        // Transport is set via injection — not serialized to avoid UnityObject coupling.
        private INetworkTransport _transport;

        // Per-slot last-known node IDs (filled in Start).
        private AGISGuid[] _lastNodeIds;

        // Snapshot of flagged ActorState key values from the previous LateUpdate.
        private Dictionary<string, AGISValue> _lastFlaggedSnapshot = new Dictionary<string, AGISValue>();

        // Keys to replicate — collected from IAGISReplicatedNodeType on node types in all slots.
        private readonly HashSet<string> _flaggedKeys = new HashSet<string>();

        // ─────────────────────────────────────────────────────────────────────────

        public string ActorNetId
        {
            get => actorNetId;
            set => actorNetId = value;
        }

        /// <summary>Inject the transport. Must be called before the first LateUpdate.</summary>
        public void SetTransport(INetworkTransport transport) => _transport = transport;

        // ─────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (_runner == null)
                _runner = GetComponent<AGISStateMachineRunner>();

            if (_runner == null)
            {
                Debug.LogError($"[AGISNetworkReplicator] No AGISStateMachineRunner found on {name}.", this);
                enabled = false;
                return;
            }

            var slots = _runner.Slots;
            _lastNodeIds = new AGISGuid[slots.Count];
            for (int i = 0; i < slots.Count; i++)
                _lastNodeIds[i] = AGISGuid.Empty;

            // Collect flagged keys from IAGISReplicatedNodeType implementations.
            if (_runner.NodeTypes != null)
            {
                foreach (var nodeType in _runner.NodeTypes.AllTypes)
                {
                    if (nodeType is IAGISReplicatedNodeType rep)
                        foreach (var key in rep.ReplicatedStateKeys)
                            _flaggedKeys.Add(key);
                }
            }

            // Seed snapshot.
            var actorState = GetComponent<AGISActorState>();
            if (actorState != null)
                foreach (var key in _flaggedKeys)
                    _lastFlaggedSnapshot[key] = actorState.Get(key);
        }

        private void LateUpdate()
        {
            if (_runner == null || _transport == null) return;

            var slots      = _runner.Slots;
            var actorState = GetComponent<AGISActorState>();

            // ── Node change detection ─────────────────────────────────────────
            for (int i = 0; i < slots.Count && i < _lastNodeIds.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || !slot.enabled) continue;

                var currentNodeId = slot.CurrentNodeId;
                if (currentNodeId == _lastNodeIds[i]) continue;

                _lastNodeIds[i] = currentNodeId;

                var msg = new NodeChangedMessage
                {
                    actorNetId = actorNetId,
                    slotIndex  = i,
                    nodeId     = currentNodeId.Value,
                };
                _transport.SendToAll(msg.ToJson());
            }

            // ── Flagged ActorState key diff ────────────────────────────────────
            if (actorState != null && _flaggedKeys.Count > 0)
            {
                foreach (var key in _flaggedKeys)
                {
                    var currentVal = actorState.Get(key);

                    bool changed = !_lastFlaggedSnapshot.TryGetValue(key, out var lastVal)
                                || !currentVal.Equals(lastVal);

                    if (!changed) continue;

                    _lastFlaggedSnapshot[key] = currentVal;

                    var msg = new ActorStateChangedMessage
                    {
                        actorNetId = actorNetId,
                        key        = key,
                        valueJson  = JsonUtility.ToJson(currentVal),
                    };
                    _transport.SendToAll(msg.ToJson());
                }
            }
        }
    }
}
