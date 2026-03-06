// File: AGISTransitionEvaluator.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Evaluate edge condition expression trees using ConditionTypeRegistry + ParamResolver.
// Notes: null expression == FALSE (as per your rule). Use ConstBool(True) for unconditional edges.

using System;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISTransitionEvaluator
    {
        private readonly AGISConditionTypeRegistry _conditionTypes;

        public AGISTransitionEvaluator(AGISConditionTypeRegistry conditionTypes)
        {
            _conditionTypes = conditionTypes ?? throw new ArgumentNullException(nameof(conditionTypes));
        }

        /// <summary>
        /// Evaluate a condition expression tree.
        /// Pass <paramref name="currentRuntime"/> so conditions like AGISNodeCompleteConditionType
        /// can inspect node-local state without requiring persistent storage.
        /// </summary>
        public bool Evaluate(AGISConditionExprDef expr, AGISExecutionContext ctx,
                             IAGISNodeRuntime currentRuntime = null)
        {
            if (expr == null)
                return false;

            switch (expr.kind)
            {
                case AGISConditionExprDef.ExprKind.ConstBool:
                    return expr.constValue;

                case AGISConditionExprDef.ExprKind.Leaf:
                    return EvaluateLeaf(expr.leaf, ctx, currentRuntime);

                case AGISConditionExprDef.ExprKind.Not:
                    return !Evaluate(expr.child, ctx, currentRuntime);

                case AGISConditionExprDef.ExprKind.And:
                    {
                        if (expr.children == null || expr.children.Count == 0)
                            return false;
                        for (int i = 0; i < expr.children.Count; i++)
                            if (!Evaluate(expr.children[i], ctx, currentRuntime))
                                return false;
                        return true;
                    }

                case AGISConditionExprDef.ExprKind.Or:
                    {
                        if (expr.children == null || expr.children.Count == 0)
                            return false;
                        for (int i = 0; i < expr.children.Count; i++)
                            if (Evaluate(expr.children[i], ctx, currentRuntime))
                                return true;
                        return false;
                    }

                default:
                    return false;
            }
        }

        private bool EvaluateLeaf(AGISConditionInstanceDef leaf, AGISExecutionContext ctx,
                                   IAGISNodeRuntime currentRuntime)
        {
            if (leaf == null) return false;
            if (string.IsNullOrEmpty(leaf.conditionTypeId)) return false;
            if (!_conditionTypes.TryGet(leaf.conditionTypeId, out var type)) return false;

            var accessor = AGISParamResolver.BuildAccessor(type.Schema, leaf.@params);
            var args = new AGISConditionEvalArgs(ctx, leaf, type, accessor, currentRuntime);
            return type.Evaluate(args);
        }

        /// <summary>
        /// Convenience: pick the first passing edge from a compiled node in priority order.
        /// Scope gating for grouped nodes is handled by the runner (filter by scope before calling this).
        /// Pass <paramref name="currentRuntime"/> to allow node-signal conditions to work correctly.
        /// </summary>
        public bool TryPickFirstPassingEdge(AGISRuntimeGraph graph, int fromNodeIndex,
                                             AGISExecutionContext ctx, out int compiledEdgeIndex,
                                             IAGISNodeRuntime currentRuntime = null)
        {
            compiledEdgeIndex = -1;
            if (graph == null) return false;
            if (fromNodeIndex < 0 || fromNodeIndex >= graph.Nodes.Length) return false;

            var outgoing = graph.Nodes[fromNodeIndex].OutgoingEdges;
            if (outgoing == null || outgoing.Length == 0) return false;

            for (int i = 0; i < outgoing.Length; i++)
            {
                int edgeIndex = outgoing[i];
                if (edgeIndex < 0 || edgeIndex >= graph.Edges.Length) continue;

                var edge = graph.Edges[edgeIndex];
                if (Evaluate(edge.Condition, ctx, currentRuntime))
                {
                    compiledEdgeIndex = edgeIndex;
                    return true;
                }
            }

            return false;
        }
    }
}
