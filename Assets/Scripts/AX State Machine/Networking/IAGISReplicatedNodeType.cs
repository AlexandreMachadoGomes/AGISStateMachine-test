// File: IAGISReplicatedNodeType.cs
// Folder: Assets/Scripts/AX State Machine/Networking/
// Purpose: Optional interface on an IAGISNodeType whose AGISActorState keys should be
//          replicated to clients. AGISNetworkReplicator polls these keys after each tick
//          and sends ActorStateChangedMessage when the value has changed.

using System.Collections.Generic;

namespace AGIS.ESM.Networking
{
    /// <summary>
    /// Implement on an IAGISNodeType to declare which AGISActorState keys should be
    /// mirrored to clients by AGISNetworkReplicator.
    /// Non-flagged keys (internal bookkeeping such as route indices) are never sent.
    /// </summary>
    public interface IAGISReplicatedNodeType
    {
        /// <summary>
        /// AGISActorState key names that should be included in replication snapshots.
        /// Example: NPCDyingNodeType returns { "npc.is_dead" }.
        /// </summary>
        IReadOnlyList<string> ReplicatedStateKeys { get; }
    }
}
