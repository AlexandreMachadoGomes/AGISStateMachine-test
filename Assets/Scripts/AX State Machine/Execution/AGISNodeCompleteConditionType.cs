// File: AGISNodeCompleteConditionType.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: True when the currently active node runtime implements IAGISNodeSignal
//          and reports IsComplete == true.
//
// TypeId: "agis.node_complete"
//
// Use this on an outgoing edge from any node that implements IAGISNodeSignal (e.g. NPCTakeDamageNodeType).
// The condition requires no params — it simply polls the active runtime's completion flag.
//
// Because IsComplete is reset to false on Enter(), the edge only fires AFTER the node's
// own work is finished, not on re-entry.

using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISNodeCompleteConditionType : IAGISConditionType
    {
        public string TypeId      => "agis.node_complete";
        public string DisplayName => "Node Complete";

        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            return args.CurrentRuntime is IAGISNodeSignal signal && signal.IsComplete;
        }
    }
}
