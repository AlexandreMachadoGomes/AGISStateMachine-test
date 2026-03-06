// File: AGISGraphClone.cs
// Folder: Assets/Scripts/AX State Machine/Hierarchical/
// Purpose: Deep clone helpers for UGC graph defs.
// Canvas alignment: Grouped macro instances must NOT mutate shared ScriptableObject assets.
// Strategy: On Grouped runtime Enter, clone internal graph defs per runtime instance, then apply bindings to the clone.

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public static class AGISGraphClone
    {
        public static AGISStateMachineGraph CloneGraph(AGISStateMachineGraph src)
        {
            if (src == null) return null;

            var dst = new AGISStateMachineGraph
            {
                graphId = src.graphId,
                version = src.version,
                entryNodeId = src.entryNodeId,
                nodes = new List<AGISNodeInstanceDef>(),
                edges = new List<AGISTransitionEdgeDef>()
            };

            if (src.nodes != null)
            {
                for (int i = 0; i < src.nodes.Count; i++)
                {
                    var n = src.nodes[i];
                    if (n == null) { dst.nodes.Add(null); continue; }
                    dst.nodes.Add(CloneNode(n));
                }
            }

            if (src.edges != null)
            {
                for (int i = 0; i < src.edges.Count; i++)
                {
                    var e = src.edges[i];
                    if (e == null) { dst.edges.Add(null); continue; }
                    dst.edges.Add(CloneEdge(e));
                }
            }

            return dst;
        }

        public static AGISNodeInstanceDef CloneNode(AGISNodeInstanceDef src)
        {
            if (src == null) return null;

            var dst = new AGISNodeInstanceDef
            {
                nodeId = src.nodeId,
                nodeTypeId = src.nodeTypeId,
                @params = CloneParamTable(src.@params),
                visual = src.visual, // editor-only; shallow copy ok
                groupAssetId = src.groupAssetId,
                exposedOverrides = CloneParamTable(src.exposedOverrides),
                parallelChildren = new List<AGISNodeInstanceDef>()
            };

            if (src.parallelChildren != null && src.parallelChildren.Count > 0)
            {
                for (int i = 0; i < src.parallelChildren.Count; i++)
                    dst.parallelChildren.Add(CloneNode(src.parallelChildren[i]));
            }

            return dst;
        }

        public static AGISTransitionEdgeDef CloneEdge(AGISTransitionEdgeDef src)
        {
            if (src == null) return null;

            var dst = new AGISTransitionEdgeDef
            {
                edgeId = src.edgeId,
                fromNodeId = src.fromNodeId,
                toNodeId = src.toNodeId,
                priority = src.priority,
                scopeId = src.scopeId,
                policy = ClonePolicy(src.policy),
                condition = CloneConditionExpr(src.condition)
            };

            return dst;
        }

        public static AGISTransitionPolicy ClonePolicy(AGISTransitionPolicy src)
        {
            if (src == null) return null;
            return new AGISTransitionPolicy
            {
                interruptible = src.interruptible,
                cooldownSeconds = src.cooldownSeconds
            };
        }

        public static AGISConditionExprDef CloneConditionExpr(AGISConditionExprDef src)
        {
            if (src == null) return null;

            var dst = new AGISConditionExprDef
            {
                kind = src.kind,
                constValue = src.constValue,
                child = null,
                children = null,
                leaf = null
            };

            switch (src.kind)
            {
                case AGISConditionExprDef.ExprKind.ConstBool:
                    return dst;

                case AGISConditionExprDef.ExprKind.Leaf:
                    dst.leaf = CloneConditionLeaf(src.leaf);
                    return dst;

                case AGISConditionExprDef.ExprKind.Not:
                    dst.child = CloneConditionExpr(src.child);
                    return dst;

                case AGISConditionExprDef.ExprKind.And:
                case AGISConditionExprDef.ExprKind.Or:
                    if (src.children != null)
                    {
                        dst.children = new List<AGISConditionExprDef>(src.children.Count);
                        for (int i = 0; i < src.children.Count; i++)
                            dst.children.Add(CloneConditionExpr(src.children[i]));
                    }
                    else
                    {
                        dst.children = new List<AGISConditionExprDef>();
                    }
                    return dst;

                default:
                    return dst;
            }
        }

        public static AGISConditionInstanceDef CloneConditionLeaf(AGISConditionInstanceDef src)
        {
            if (src == null) return null;

            var dst = new AGISConditionInstanceDef
            {
                conditionId = src.conditionId,
                conditionTypeId = src.conditionTypeId,
                @params = CloneParamTable(src.@params)
            };

            return dst;
        }

        public static AGISParamTable CloneParamTable(AGISParamTable src)
        {
            var dst = new AGISParamTable();

            if (src == null || src.values == null)
            {
                dst.values = new List<AGISParamValue>();
                return dst;
            }

            dst.values = new List<AGISParamValue>(src.values.Count);
            for (int i = 0; i < src.values.Count; i++)
            {
                var pv = src.values[i];
                if (pv == null)
                {
                    dst.values.Add(null);
                    continue;
                }

                dst.values.Add(new AGISParamValue(pv.key, pv.value));
            }

            return dst;
        }
    }
}
