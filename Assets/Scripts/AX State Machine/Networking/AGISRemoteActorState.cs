// File: AGISRemoteActorState.cs
// Folder: Assets/Scripts/AX State Machine/Networking/
// Purpose: Client-side MonoBehaviour on NPC proxies (visual-only, no runner).
//          Receives and applies NodeChangedMessage / ActorStateChangedMessage from the server.
//          Visual/audio systems subscribe to OnNodeChanged for playback cues.

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Networking
{
    public sealed class AGISRemoteActorState : MonoBehaviour
    {
        [SerializeField] private string actorNetId;

        // Current node IDs per slot (key = slotIndex).
        private readonly Dictionary<int, AGISGuid> _currentNodeIds = new Dictionary<int, AGISGuid>();

        // Mirrored ActorState values from the server.
        private readonly Dictionary<string, AGISValue> _mirroredState = new Dictionary<string, AGISValue>();

        // ── Events ────────────────────────────────────────────────────────────────────

        /// <summary>Raised when the server reports a node transition for this actor.</summary>
        public event Action<int, AGISGuid> OnNodeChanged; // slotIndex, nodeId

        /// <summary>Raised when a flagged ActorState key changes on the server.</summary>
        public event Action<string, AGISValue> OnStateKeyChanged; // key, value

        // ── Public API ────────────────────────────────────────────────────────────────

        public string ActorNetId
        {
            get => actorNetId;
            set => actorNetId = value;
        }

        public bool TryGetNodeId(int slotIndex, out AGISGuid nodeId)
            => _currentNodeIds.TryGetValue(slotIndex, out nodeId);

        public bool TryGetValue(string key, out AGISValue value)
            => _mirroredState.TryGetValue(key, out value);

        /// <summary>
        /// Feed raw JSON messages from the transport into this component.
        /// Call this from your client-side message dispatcher.
        /// </summary>
        public void HandleMessage(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            string msgType = AGISNetworkMessageRouter.PeekType(json);
            if (msgType == null) return;

            if (msgType == nameof(AGISNetworkMessageType.NodeChanged))
            {
                var msg = NodeChangedMessage.FromJson(json);
                if (msg == null || msg.actorNetId != actorNetId) return;

                var nodeGuid = new AGISGuid(msg.nodeId ?? string.Empty);
                _currentNodeIds[msg.slotIndex] = nodeGuid;
                OnNodeChanged?.Invoke(msg.slotIndex, nodeGuid);
            }
            else if (msgType == nameof(AGISNetworkMessageType.ActorStateChanged))
            {
                var msg = ActorStateChangedMessage.FromJson(json);
                if (msg == null || msg.actorNetId != actorNetId) return;

                var value = JsonUtility.FromJson<AGISValue>(msg.valueJson);
                _mirroredState[msg.key] = value;
                OnStateKeyChanged?.Invoke(msg.key, value);
            }
        }
    }
}
