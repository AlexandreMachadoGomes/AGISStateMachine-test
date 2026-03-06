// File: AGISAnyStateNodeType.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Virtual "Any State" node type. Edges drawn FROM this node become global interrupt
//          transitions evaluated every tick before node-specific edges.
//          This node is never the active runtime — it exists only as a graph-time source.

using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISAnyStateNodeType : IAGISNodeType
    {
        public string TypeId      => "agis.any_state";
        public string DisplayName => "Any State";
        public AGISNodeKind Kind   => AGISNodeKind.AnyState;
        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        // Never actually called — AnyState node is never the active runtime.
        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args) => null;
    }
}
