// File: INetworkTransport.cs
// Folder: Assets/Scripts/AX State Machine/Networking/
// Purpose: Transport abstraction for AGIS state machine replication messages.
//          Implementations (WebSocket, Mirror, Netcode, etc.) inject this into
//          AGISNetworkReplicator. The state machine layer never imports a socket library.

using System;

namespace AGIS.ESM.Networking
{
    /// <summary>
    /// Minimal transport contract for sending JSON strings to connected clients.
    /// Implement on a MonoBehaviour or plain class and inject into AGISNetworkReplicator.
    /// </summary>
    public interface INetworkTransport
    {
        /// <summary>Broadcast a message to all connected clients.</summary>
        void SendToAll(string json);

        /// <summary>Send a message to a specific client.</summary>
        void SendToClient(int clientId, string json);

        /// <summary>
        /// Raised on the server when a message arrives from any client.
        /// The server may use this to handle client-to-server coordination events.
        /// </summary>
        event Action<string> OnMessageReceived;
    }
}
