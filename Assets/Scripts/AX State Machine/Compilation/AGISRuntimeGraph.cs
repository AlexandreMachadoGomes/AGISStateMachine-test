// File: AGISRuntimeGraph.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Compiled, runtime-friendly adjacency representation of a UGC graph (fast outgoing edge iteration).

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISRuntimeGraph
    {
        public readonly AGISGuid GraphId;

        public readonly AGISCompiledNode[] Nodes;
        public readonly AGISCompiledEdge[] Edges;

        private readonly Dictionary<AGISGuid, int> _nodeIndexById;

        public readonly int EntryNodeIndex;

        /// <summary>Indices into Edges[] for edges originating from AnyState nodes, sorted by priority DESC.</summary>
        public readonly int[] AnyStateEdgeIndices;

        public AGISRuntimeGraph(AGISGuid graphId, AGISCompiledNode[] nodes, AGISCompiledEdge[] edges, int entryNodeIndex, int[] anyStateEdgeIndices = null)
        {
            GraphId = graphId;
            Nodes = nodes ?? Array.Empty<AGISCompiledNode>();
            Edges = edges ?? Array.Empty<AGISCompiledEdge>();
            EntryNodeIndex = entryNodeIndex;
            AnyStateEdgeIndices = anyStateEdgeIndices ?? Array.Empty<int>();

            _nodeIndexById = new Dictionary<AGISGuid, int>();
            for (int i = 0; i < Nodes.Length; i++)
                _nodeIndexById[Nodes[i].NodeId] = i;
        }

        public bool TryGetNodeIndex(AGISGuid nodeId, out int index) => _nodeIndexById.TryGetValue(nodeId, out index);
    }

    public readonly struct AGISCompiledNode
    {
        public readonly AGISGuid NodeId;
        public readonly AGISNodeInstanceDef Def;
        public readonly IAGISNodeType Type;

        /// <summary>Outgoing edge indices into AGISRuntimeGraph.Edges, sorted by priority (desc).</summary>
        public readonly int[] OutgoingEdges;

        public AGISCompiledNode(AGISGuid nodeId, AGISNodeInstanceDef def, IAGISNodeType type, int[] outgoingEdges)
        {
            NodeId = nodeId;
            Def = def;
            Type = type;
            OutgoingEdges = outgoingEdges ?? Array.Empty<int>();
        }
    }

    public readonly struct AGISCompiledEdge
    {
        public readonly AGISGuid EdgeId;
        public readonly int FromNodeIndex;
        public readonly int ToNodeIndex;
        public readonly int Priority;
        public readonly AGISTransitionPolicy Policy;

        /// <summary>Full condition expression tree (null treated as FALSE by evaluator).</summary>
        public readonly AGISConditionExprDef Condition;

        /// <summary>For edges exiting a Grouped node: scope gating id (default "Any").</summary>
        public readonly string ScopeId;

        public AGISCompiledEdge(AGISGuid edgeId, int fromIndex, int toIndex, int priority, AGISTransitionPolicy policy, AGISConditionExprDef condition, string scopeId)
        {
            EdgeId = edgeId;
            FromNodeIndex = fromIndex;
            ToNodeIndex = toIndex;
            Priority = priority;
            Policy = policy;
            Condition = condition;
            ScopeId = scopeId;
        }
    }
}
