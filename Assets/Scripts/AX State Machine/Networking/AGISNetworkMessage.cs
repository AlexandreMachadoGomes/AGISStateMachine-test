// File: AGISNetworkMessage.cs
// Folder: Assets/Scripts/AX State Machine/Networking/
// Purpose: Plain serializable message structs for AGIS state machine replication.
//          Serialized as JSON via JsonUtility for transport-agnostic delivery.

using System;
using UnityEngine;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Networking
{
    public enum AGISNetworkMessageType
    {
        NodeChanged       = 0,
        ActorStateChanged = 1,
        TeamEvent         = 2,
    }

    /// <summary>
    /// Sent whenever a state machine slot transitions to a new node on the server.
    /// ~50 bytes. Clients use this to mirror the current node for visuals/audio.
    /// </summary>
    [Serializable]
    public class NodeChangedMessage
    {
        public string msgType     = nameof(AGISNetworkMessageType.NodeChanged);
        public string actorNetId;
        public int    slotIndex;
        public string nodeId;   // AGISGuid.Value

        public string ToJson() => JsonUtility.ToJson(this);

        public static NodeChangedMessage FromJson(string json) => JsonUtility.FromJson<NodeChangedMessage>(json);
    }

    /// <summary>
    /// Sent when a flagged ActorState key changes on the server.
    /// Only keys declared via IAGISReplicatedNodeType are sent.
    /// </summary>
    [Serializable]
    public class ActorStateChangedMessage
    {
        public string msgType   = nameof(AGISNetworkMessageType.ActorStateChanged);
        public string actorNetId;
        public string key;
        public string valueJson; // AGISValue serialized via JsonUtility

        public string ToJson() => JsonUtility.ToJson(this);

        public static ActorStateChangedMessage FromJson(string json) => JsonUtility.FromJson<ActorStateChangedMessage>(json);
    }

    /// <summary>
    /// Sent by AGISActorEventBus for cross-actor coordination events.
    /// Scope string: "actor:{id}", "group:{id}", or "broadcast".
    /// </summary>
    [Serializable]
    public class TeamEventMessage
    {
        public string msgType      = nameof(AGISNetworkMessageType.TeamEvent);
        public string sourceActorId;
        public string eventType;
        public string scope;       // "actor:{id}", "group:{id}", or "broadcast"
        public string[] payloadKeys;
        public string[] payloadValues; // JSON-serialized AGISValues (parallel arrays)

        public string ToJson() => JsonUtility.ToJson(this);

        public static TeamEventMessage FromJson(string json) => JsonUtility.FromJson<TeamEventMessage>(json);
    }

    /// <summary>
    /// Utility to determine message type from raw JSON without full deserialization.
    /// </summary>
    public static class AGISNetworkMessageRouter
    {
        [Serializable]
        private class TypeProbe { public string msgType; }

        public static string PeekType(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return JsonUtility.FromJson<TypeProbe>(json)?.msgType;
        }
    }
}
