// File: IAGISNodeSignal.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Optional interface a node runtime can implement to signal that it has finished
//          its own work and is ready for the state machine to transition away.
//
// Usage:
//   1. Implement IAGISNodeSignal on a IAGISNodeRuntime.
//   2. Set IsComplete = true when the node's work is done (e.g. animation ended).
//   3. Add an outgoing edge with the condition "agis.node_complete" (AGISNodeCompleteConditionType).
//
// Notes:
//   - IsComplete is checked by AGISNodeCompleteConditionType via AGISConditionEvalArgs.CurrentRuntime.
//   - Reset IsComplete to false in Enter() so re-entering the node starts fresh.
//   - The signal is NOT persisted — it exists only while the node is the active runtime.

namespace AGIS.ESM.Runtime
{
    public interface IAGISNodeSignal
    {
        /// <summary>
        /// True when the node has completed its internal work and is ready to transition out.
        /// Set to true from inside the runtime (e.g. when an animation finishes).
        /// Must be reset to false in Enter() to support re-entry.
        /// </summary>
        bool IsComplete { get; }
    }
}
