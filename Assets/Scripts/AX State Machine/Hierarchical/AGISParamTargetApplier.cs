// File: AGISParamTargetApplier.cs
// Folder: Assets/Scripts/AX State Machine/Hierarchical/
// Purpose: Apply an exposed param value to a target inside a (cloned) grouped internal graph.
// Targets are stable refs: (internalNodeId,paramKey) or (internalEdgeId,internalConditionId,conditionParamKey).
// Canvas alignment: stable bindings; 1-to-many allowed; no shared asset mutation.

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISParamTargetApplier
    {
        private readonly AGISNodeTypeRegistry _nodeTypes;
        private readonly AGISConditionTypeRegistry _conditionTypes;
        private readonly AGISDebugTrace _trace;

        public AGISParamTargetApplier(AGISNodeTypeRegistry nodeTypes, AGISConditionTypeRegistry conditionTypes, AGISDebugTrace trace = null)
        {
            _nodeTypes = nodeTypes ?? throw new ArgumentNullException(nameof(nodeTypes));
            _conditionTypes = conditionTypes ?? throw new ArgumentNullException(nameof(conditionTypes));
            _trace = trace;
        }

        public bool ApplyToTarget(AGISStateMachineGraph internalGraphClone, AGISParamTarget target, in AGISValue value)
        {
            if (internalGraphClone == null || target == null)
                return false;

            switch (target.kind)
            {
                case AGISParamTarget.TargetKind.InternalNodeParam:
                    return ApplyToNodeParam(internalGraphClone, target.internalNodeId, target.paramKey, value);

                case AGISParamTarget.TargetKind.InternalEdgeConditionParam:
                    return ApplyToConditionParam(internalGraphClone, target.internalEdgeId, target.internalConditionId, target.conditionParamKey, value);

                default:
                    _trace?.Warn("Unknown AGISParamTarget kind.");
                    return false;
            }
        }

        private bool ApplyToNodeParam(AGISStateMachineGraph g, AGISGuid nodeId, string paramKey, in AGISValue value)
        {
            if (!nodeId.IsValid || string.IsNullOrEmpty(paramKey))
                return false;

            var node = FindNodeByIdIncludingParallelChildren(g, nodeId);
            if (node == null)
            {
                _trace?.Warn($"Binding target nodeId not found: {nodeId}");
                return false;
            }

            if (!string.IsNullOrEmpty(node.nodeTypeId) && _nodeTypes.TryGet(node.nodeTypeId, out var nodeType) && nodeType?.Schema != null)
            {
                if (nodeType.Schema.TryGetSpec(paramKey, out var spec))
                {
                    if (spec != null && spec.type != value.Type)
                    {
                        _trace?.Warn($"Binding type mismatch on node '{node.nodeTypeId}' param '{paramKey}': expected {spec.type}, got {value.Type}. Skipping.");
                        return false;
                    }
                }
            }

            node.@params ??= new AGISParamTable();
            node.@params.Set(paramKey, value);
            return true;
        }

        private bool ApplyToConditionParam(AGISStateMachineGraph g, AGISGuid edgeId, AGISGuid conditionId, string conditionParamKey, in AGISValue value)
        {
            if (!edgeId.IsValid || string.IsNullOrEmpty(conditionParamKey))
                return false;

            var edge = FindEdgeById(g, edgeId);
            if (edge == null)
            {
                _trace?.Warn($"Binding target edgeId not found: {edgeId}");
                return false;
            }

            if (edge.condition == null)
            {
                _trace?.Warn($"Binding target edge has null condition expr: {edgeId}");
                return false;
            }

            var leaf = FindLeafByConditionId(edge.condition, conditionId);
            if (leaf == null)
            {
                if (!conditionId.IsValid)
                {
                    var single = FindSingleLeaf(edge.condition);
                    if (single != null)
                        leaf = single;
                }

                if (leaf == null)
                {
                    _trace?.Warn($"Binding target condition leaf not found on edge {edgeId}. conditionId={conditionId}");
                    return false;
                }
            }

            if (string.IsNullOrEmpty(leaf.conditionTypeId))
                return false;

            if (_conditionTypes.TryGet(leaf.conditionTypeId, out var condType) && condType?.Schema != null)
            {
                if (condType.Schema.TryGetSpec(conditionParamKey, out var spec))
                {
                    if (spec != null && spec.type != value.Type)
                    {
                        _trace?.Warn($"Binding type mismatch on condition '{leaf.conditionTypeId}' param '{conditionParamKey}': expected {spec.type}, got {value.Type}. Skipping.");
                        return false;
                    }
                }
            }

            leaf.@params ??= new AGISParamTable();
            leaf.@params.Set(conditionParamKey, value);
            return true;
        }

        private static AGISNodeInstanceDef FindNodeByIdIncludingParallelChildren(AGISStateMachineGraph g, AGISGuid nodeId)
        {
            if (g?.nodes == null) return null;

            for (int i = 0; i < g.nodes.Count; i++)
            {
                var found = FindNodeRecursive(g.nodes[i], nodeId);
                if (found != null) return found;
            }
            return null;
        }

        private static AGISNodeInstanceDef FindNodeRecursive(AGISNodeInstanceDef node, AGISGuid nodeId)
        {
            if (node == null) return null;
            if (node.nodeId == nodeId) return node;

            if (node.parallelChildren != null)
            {
                for (int i = 0; i < node.parallelChildren.Count; i++)
                {
                    var found = FindNodeRecursive(node.parallelChildren[i], nodeId);
                    if (found != null) return found;
                }
            }

            return null;
        }

        private static AGISTransitionEdgeDef FindEdgeById(AGISStateMachineGraph g, AGISGuid edgeId)
        {
            if (g?.edges == null) return null;
            for (int i = 0; i < g.edges.Count; i++)
            {
                var e = g.edges[i];
                if (e != null && e.edgeId == edgeId)
                    return e;
            }
            return null;
        }

        private static AGISConditionInstanceDef FindLeafByConditionId(AGISConditionExprDef expr, AGISGuid conditionId)
        {
            if (expr == null) return null;

            switch (expr.kind)
            {
                case AGISConditionExprDef.ExprKind.Leaf:
                    if (expr.leaf != null && conditionId.IsValid && expr.leaf.conditionId == conditionId)
                        return expr.leaf;
                    return null;

                case AGISConditionExprDef.ExprKind.Not:
                    return FindLeafByConditionId(expr.child, conditionId);

                case AGISConditionExprDef.ExprKind.And:
                case AGISConditionExprDef.ExprKind.Or:
                    if (expr.children == null) return null;
                    for (int i = 0; i < expr.children.Count; i++)
                    {
                        var found = FindLeafByConditionId(expr.children[i], conditionId);
                        if (found != null) return found;
                    }
                    return null;

                default:
                    return null;
            }
        }

        private static AGISConditionInstanceDef FindSingleLeaf(AGISConditionExprDef expr)
        {
            int count = 0;
            AGISConditionInstanceDef single = null;
            CollectLeaves(expr, ref count, ref single);
            return count == 1 ? single : null;
        }

        private static void CollectLeaves(AGISConditionExprDef expr, ref int count, ref AGISConditionInstanceDef single)
        {
            if (expr == null) return;

            switch (expr.kind)
            {
                case AGISConditionExprDef.ExprKind.Leaf:
                    if (expr.leaf != null)
                    {
                        count++;
                        if (count == 1) single = expr.leaf;
                    }
                    return;

                case AGISConditionExprDef.ExprKind.Not:
                    CollectLeaves(expr.child, ref count, ref single);
                    return;

                case AGISConditionExprDef.ExprKind.And:
                case AGISConditionExprDef.ExprKind.Or:
                    if (expr.children == null) return;
                    for (int i = 0; i < expr.children.Count; i++)
                        CollectLeaves(expr.children[i], ref count, ref single);
                    return;

                default:
                    return;
            }
        }
    }
}
