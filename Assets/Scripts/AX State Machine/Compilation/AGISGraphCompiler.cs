// File: AGISGraphCompiler.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Compile UGC graph defs into AGISRuntimeGraph (adjacency + ordered edges).

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISGraphCompiler
    {
        private readonly AGISNodeTypeRegistry _nodeTypes;

        public AGISGraphCompiler(AGISNodeTypeRegistry nodeTypes)
        {
            _nodeTypes = nodeTypes ?? throw new ArgumentNullException(nameof(nodeTypes));
        }

        public AGISRuntimeGraph Compile(AGISStateMachineGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            graph.nodes ??= new List<AGISNodeInstanceDef>();
            graph.edges ??= new List<AGISTransitionEdgeDef>();

            var nodeIndexById = new Dictionary<AGISGuid, int>();
            var compiledNodesTmp = new List<AGISCompiledNode>(graph.nodes.Count);

            for (int i = 0; i < graph.nodes.Count; i++)
            {
                var n = graph.nodes[i];
                if (n == null || !n.nodeId.IsValid)
                    continue;

                if (nodeIndexById.ContainsKey(n.nodeId))
                    continue;

                nodeIndexById.Add(n.nodeId, compiledNodesTmp.Count);

                _nodeTypes.TryGet(n.nodeTypeId, out var nodeType);

                compiledNodesTmp.Add(new AGISCompiledNode(n.nodeId, n, nodeType, Array.Empty<int>()));
            }

            // Collect AnyState node indices for edge routing
            var anyStateNodeIndices = new HashSet<int>();
            for (int i = 0; i < compiledNodesTmp.Count; i++)
                if (compiledNodesTmp[i].Type?.Kind == AGISNodeKind.AnyState)
                    anyStateNodeIndices.Add(i);

            var compiledEdgesTmp = new List<AGISCompiledEdge>(graph.edges.Count);
            var outgoing = new Dictionary<int, List<int>>();
            var anyStateEdgeIndicesTmp = new List<int>();

            for (int i = 0; i < graph.edges.Count; i++)
            {
                var e = graph.edges[i];
                if (e == null || !e.edgeId.IsValid)
                    continue;

                if (!nodeIndexById.TryGetValue(e.fromNodeId, out var fromIndex))
                    continue;
                if (!nodeIndexById.TryGetValue(e.toNodeId, out var toIndex))
                    continue;

                e.condition?.EnsureLeafIds();

                var compiledEdge = new AGISCompiledEdge(
                    e.edgeId,
                    fromIndex,
                    toIndex,
                    e.priority,
                    e.policy,
                    e.condition,
                    e.scopeId
                );

                int edgeIndex = compiledEdgesTmp.Count;
                compiledEdgesTmp.Add(compiledEdge);

                if (anyStateNodeIndices.Contains(fromIndex))
                {
                    anyStateEdgeIndicesTmp.Add(edgeIndex);   // global pool
                }
                else
                {
                    if (!outgoing.TryGetValue(fromIndex, out var list))
                    {
                        list = new List<int>();
                        outgoing[fromIndex] = list;
                    }
                    list.Add(edgeIndex);
                }
            }

            // Sort outgoing edges by priority DESC; tie-break by EdgeId string.
            for (int n = 0; n < compiledNodesTmp.Count; n++)
            {
                if (!outgoing.TryGetValue(n, out var list) || list.Count == 0)
                {
                    var old = compiledNodesTmp[n];
                    compiledNodesTmp[n] = new AGISCompiledNode(old.NodeId, old.Def, old.Type, Array.Empty<int>());
                    continue;
                }

                list.Sort((a, b) =>
                {
                    var ea = compiledEdgesTmp[a];
                    var eb = compiledEdgesTmp[b];

                    int pr = eb.Priority.CompareTo(ea.Priority);
                    if (pr != 0) return pr;

                    return eb.EdgeId.ToString().CompareTo(ea.EdgeId.ToString());
                });

                var arr = list.ToArray();
                var old2 = compiledNodesTmp[n];
                compiledNodesTmp[n] = new AGISCompiledNode(old2.NodeId, old2.Def, old2.Type, arr);
            }

            // Sort AnyState edge pool by priority DESC; tie-break by EdgeId string.
            anyStateEdgeIndicesTmp.Sort((a, b) =>
            {
                var ea = compiledEdgesTmp[a];
                var eb = compiledEdgesTmp[b];
                int pr = eb.Priority.CompareTo(ea.Priority);
                if (pr != 0) return pr;
                return eb.EdgeId.ToString().CompareTo(ea.EdgeId.ToString());
            });

            int entryIndex = -1;
            if (graph.entryNodeId.IsValid && nodeIndexById.TryGetValue(graph.entryNodeId, out var idx))
                entryIndex = idx;

            return new AGISRuntimeGraph(graph.graphId, compiledNodesTmp.ToArray(), compiledEdgesTmp.ToArray(), entryIndex, anyStateEdgeIndicesTmp.ToArray());
        }
    }
}
