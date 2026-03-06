// File: AGISGraphFingerprint.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Deterministic fingerprint of graph contents for caching/invalidations.
// Canvas alignment: user should NOT have to manage graph versions/revisions manually.
// Policy: cache compiled graphs by content fingerprint, not by user-maintained version.

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public static class AGISGraphFingerprint
    {
        // FNV-1a 64-bit
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Compute(AGISStateMachineGraph graph)
        {
            if (graph == null)
                return 0UL;

            ulong h = Offset;

            // Include graph identity & format version (migration-related).
            h = HashGuid(h, graph.graphId);
            h = HashInt(h, graph.version);

            h = HashGuid(h, graph.entryNodeId);

            // Nodes (order independent): sort by nodeId
            var nodes = graph.nodes ?? new List<AGISNodeInstanceDef>();
            var nodeList = new List<AGISNodeInstanceDef>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null)
                    nodeList.Add(nodes[i]);

            nodeList.Sort((a, b) => a.nodeId.ToString().CompareTo(b.nodeId.ToString()));

            for (int i = 0; i < nodeList.Count; i++)
                h = HashNode(h, nodeList[i]);

            // Edges (order independent): sort by edgeId
            var edges = graph.edges ?? new List<AGISTransitionEdgeDef>();
            var edgeList = new List<AGISTransitionEdgeDef>(edges.Count);
            for (int i = 0; i < edges.Count; i++)
                if (edges[i] != null)
                    edgeList.Add(edges[i]);

            edgeList.Sort((a, b) => a.edgeId.ToString().CompareTo(b.edgeId.ToString()));

            for (int i = 0; i < edgeList.Count; i++)
                h = HashEdge(h, edgeList[i]);

            return h;
        }

        private static ulong HashNode(ulong h, AGISNodeInstanceDef n)
        {
            h = HashGuid(h, n.nodeId);
            h = HashString(h, n.nodeTypeId);

            h = HashParamTable(h, n.@params);

            h = HashGuid(h, n.groupAssetId);
            h = HashParamTable(h, n.exposedOverrides);

            // Parallel children: order independent (concurrent semantics). Sort by child nodeId.
            if (n.parallelChildren != null && n.parallelChildren.Count > 0)
            {
                var children = new List<AGISNodeInstanceDef>();
                for (int i = 0; i < n.parallelChildren.Count; i++)
                    if (n.parallelChildren[i] != null)
                        children.Add(n.parallelChildren[i]);

                children.Sort((a, b) => a.nodeId.ToString().CompareTo(b.nodeId.ToString()));
                for (int i = 0; i < children.Count; i++)
                    h = HashNode(h, children[i]);
            }
            else
            {
                h = HashByte(h, 0);
            }

            return h;
        }

        private static ulong HashEdge(ulong h, AGISTransitionEdgeDef e)
        {
            h = HashGuid(h, e.edgeId);
            h = HashGuid(h, e.fromNodeId);
            h = HashGuid(h, e.toNodeId);
            h = HashInt(h, e.priority);
            h = HashString(h, e.scopeId);

            if (e.policy != null)
            {
                h = HashByte(h, 1);
                h = HashBool(h, e.policy.interruptible);
                h = HashFloat(h, e.policy.cooldownSeconds);
            }
            else
            {
                h = HashByte(h, 0);
            }

            // Condition expr tree: null is meaningful (null = false policy).
            h = HashConditionExpr(h, e.condition);
            return h;
        }

        private static ulong HashConditionExpr(ulong h, AGISConditionExprDef expr)
        {
            if (expr == null)
                return HashByte(h, 0);

            // Ensure leaf ids exist; this stabilizes bindings and fingerprints.
            expr.EnsureLeafIds();

            h = HashByte(h, 1);
            h = HashInt(h, (int)expr.kind);

            switch (expr.kind)
            {
                case AGISConditionExprDef.ExprKind.ConstBool:
                    h = HashBool(h, expr.constValue);
                    return h;

                case AGISConditionExprDef.ExprKind.Leaf:
                    return HashConditionLeaf(h, expr.leaf);

                case AGISConditionExprDef.ExprKind.Not:
                    return HashConditionExpr(h, expr.child);

                case AGISConditionExprDef.ExprKind.And:
                case AGISConditionExprDef.ExprKind.Or:
                    if (expr.children == null)
                        return HashInt(h, 0);

                    h = HashInt(h, expr.children.Count);
                    for (int i = 0; i < expr.children.Count; i++)
                        h = HashConditionExpr(h, expr.children[i]);
                    return h;

                default:
                    return h;
            }
        }

        private static ulong HashConditionLeaf(ulong h, AGISConditionInstanceDef leaf)
        {
            if (leaf == null)
                return HashByte(h, 0);

            h = HashByte(h, 1);
            h = HashGuid(h, leaf.conditionId);
            h = HashString(h, leaf.conditionTypeId);
            h = HashParamTable(h, leaf.@params);
            return h;
        }

        private static ulong HashParamTable(ulong h, AGISParamTable table)
        {
            if (table == null || table.values == null)
                return HashInt(h, 0);

            // Keep last occurrence per key (NormalizeKeepLast policy) WITHOUT mutating table.
            var last = new Dictionary<string, AGISValue>(StringComparer.Ordinal);
            for (int i = 0; i < table.values.Count; i++)
            {
                var pv = table.values[i];
                if (pv == null || string.IsNullOrEmpty(pv.key))
                    continue;
                last[pv.key] = pv.value;
            }

            // Sort keys for determinism
            var keys = new List<string>(last.Keys);
            keys.Sort(StringComparer.Ordinal);

            h = HashInt(h, keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                h = HashString(h, key);
                h = HashValue(h, last[key]);
            }

            return h;
        }

        private static ulong HashValue(ulong h, in AGISValue v)
        {
            h = HashInt(h, (int)v.Type);
            switch (v.Type)
            {
                case AGISParamType.Bool:
                    return HashBool(h, v.AsBool());

                case AGISParamType.Int:
                    return HashInt(h, v.AsInt());

                case AGISParamType.Float:
                    return HashFloat(h, v.AsFloat());

                case AGISParamType.String:
                    return HashString(h, v.AsString());

                case AGISParamType.Vector2:
                {
                    var x = v.AsVector2();
                    h = HashFloat(h, x.x);
                    h = HashFloat(h, x.y);
                    return h;
                }

                case AGISParamType.Vector3:
                {
                    var x = v.AsVector3();
                    h = HashFloat(h, x.x);
                    h = HashFloat(h, x.y);
                    h = HashFloat(h, x.z);
                    return h;
                }

                case AGISParamType.Guid:
                    return HashGuid(h, v.AsGuid());

                default:
                    return h;
            }
        }

        private static ulong HashGuid(ulong h, AGISGuid g) => HashString(h, g.ToString());

        private static ulong HashString(ulong h, string s)
        {
            if (s == null) s = "";
            h = HashInt(h, s.Length);
            for (int i = 0; i < s.Length; i++)
                h = HashChar(h, s[i]);
            return h;
        }

        private static ulong HashChar(ulong h, char c)
        {
            unchecked
            {
                h ^= (byte)(c & 0xFF);
                h *= Prime;
                h ^= (byte)((c >> 8) & 0xFF);
                h *= Prime;
                return h;
            }
        }

        private static ulong HashBool(ulong h, bool b) => HashByte(h, b ? (byte)1 : (byte)0);

        private static ulong HashByte(ulong h, byte b)
        {
            unchecked
            {
                h ^= b;
                h *= Prime;
                return h;
            }
        }

        private static ulong HashInt(ulong h, int x)
        {
            unchecked
            {
                h ^= (byte)(x);
                h *= Prime;
                h ^= (byte)(x >> 8);
                h *= Prime;
                h ^= (byte)(x >> 16);
                h *= Prime;
                h ^= (byte)(x >> 24);
                h *= Prime;
                return h;
            }
        }

        private static ulong HashFloat(ulong h, float f)
        {
            // Stable bitwise hashing of IEEE float
            var x = BitConverter.SingleToInt32Bits(f);
            return HashInt(h, x);
        }
    }
}
