// File: AGISGraphValidator.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Validate UGC graphs and grouped assets against registries + schemas + structural rules (canvas-aligned).

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISGraphValidatorOptions
    {
        public bool AllowUnknownParamKeys = false;
        public bool ValidateGroupedAssets = true;
        public bool ValidateGroupedInternalGraphs = true;
    }

    public sealed class AGISGraphValidator
    {
        private readonly AGISNodeTypeRegistry _nodeTypes;
        private readonly AGISConditionTypeRegistry _conditionTypes;
        private readonly Func<AGISGuid, AGISGroupedStateAsset> _groupResolver;

        public AGISGraphValidator(AGISNodeTypeRegistry nodeTypes, AGISConditionTypeRegistry conditionTypes, Func<AGISGuid, AGISGroupedStateAsset> groupResolver = null)
        {
            _nodeTypes = nodeTypes ?? throw new ArgumentNullException(nameof(nodeTypes));
            _conditionTypes = conditionTypes ?? throw new ArgumentNullException(nameof(conditionTypes));
            _groupResolver = groupResolver;
        }

        public AGISGraphValidationReport ValidateGraph(AGISStateMachineGraph graph, AGISGraphValidatorOptions options = null)
        {
            options ??= new AGISGraphValidatorOptions();
            var report = new AGISGraphValidationReport();

            if (graph == null)
            {
                report.Error("Graph.Null", "Graph is null.");
                return report;
            }

            graph.nodes ??= new List<AGISNodeInstanceDef>();
            graph.edges ??= new List<AGISTransitionEdgeDef>();

            var nodeIndex = new Dictionary<AGISGuid, AGISNodeInstanceDef>();
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                var n = graph.nodes[i];
                if (n == null)
                {
                    report.Error("Graph.NodeNull", $"Node at index {i} is null.", $"Graph.Nodes[{i}]");
                    continue;
                }

                if (!n.nodeId.IsValid)
                {
                    report.Error("Graph.NodeIdInvalid", "Node has invalid nodeId.", $"Graph.Nodes[{i}]");
                    continue;
                }

                if (nodeIndex.ContainsKey(n.nodeId))
                {
                    report.Error("Graph.NodeIdDuplicate", "Duplicate nodeId in graph.", $"Graph.Nodes[{i}]", nodeId: n.nodeId);
                    continue;
                }

                nodeIndex.Add(n.nodeId, n);

                if (string.IsNullOrEmpty(n.nodeTypeId))
                {
                    report.Error("Graph.NodeTypeMissing", "Node has empty nodeTypeId.", $"Graph.Nodes[{i}]", nodeId: n.nodeId);
                    continue;
                }

                if (!_nodeTypes.TryGet(n.nodeTypeId, out var nodeType))
                {
                    report.Error("Graph.NodeTypeUnknown", $"Unknown nodeTypeId '{n.nodeTypeId}'.", $"Graph.Nodes[{i}]", nodeId: n.nodeId);
                    continue;
                }

                // Node params validation
                var paramIssues = new List<AGISParamIssue>();
                AGISParamResolver.Validate(nodeType.Schema, n.@params, paramIssues, allowUnknownKeys: options.AllowUnknownParamKeys);
                for (int p = 0; p < paramIssues.Count; p++)
                {
                    var issue = paramIssues[p];
                    if (issue.Severity == AGISParamIssueSeverity.Error)
                        report.Error("Graph.NodeParamInvalid", issue.Message, $"Graph.Nodes[{i}].Params", nodeId: n.nodeId);
                    else
                        report.Warn("Graph.NodeParamWarn", issue.Message, $"Graph.Nodes[{i}].Params", nodeId: n.nodeId);
                }

                // Grouped node checks
                if (nodeType.Kind == AGISNodeKind.Grouped)
                {
                    if (!n.groupAssetId.IsValid)
                        report.Error("Graph.GroupAssetMissing", "Grouped node missing groupAssetId.", $"Graph.Nodes[{i}]", nodeId: n.nodeId);

                    if (options.ValidateGroupedAssets)
                        ValidateGroupedNodeUsage(n, report, options);
                }
            }

            // Entry node exists
            if (!graph.entryNodeId.IsValid)
                report.Error("Graph.EntryMissing", "Graph entryNodeId is invalid/empty.", "Graph.Entry");
            else if (!nodeIndex.ContainsKey(graph.entryNodeId))
                report.Error("Graph.EntryNotFound", "Graph entryNodeId does not exist in node list.", "Graph.Entry", nodeId: graph.entryNodeId);

            // Edges validation + outgoing counts
            var outgoingCount = new Dictionary<AGISGuid, int>();

            for (int i = 0; i < graph.edges.Count; i++)
            {
                var e = graph.edges[i];
                if (e == null)
                {
                    report.Error("Graph.EdgeNull", $"Edge at index {i} is null.", $"Graph.Edges[{i}]");
                    continue;
                }

                if (!e.edgeId.IsValid)
                    report.Error("Graph.EdgeIdInvalid", "Edge has invalid edgeId.", $"Graph.Edges[{i}]");

                if (!e.fromNodeId.IsValid || !nodeIndex.ContainsKey(e.fromNodeId))
                    report.Error("Graph.EdgeFromInvalid", "Edge fromNodeId missing or not found.", $"Graph.Edges[{i}]", edgeId: e.edgeId);

                if (!e.toNodeId.IsValid)
                    report.Warn("Graph.EdgeToUnconnected", "Edge toNodeId is empty (unconnected/dangling). Connect this edge to a target node.", $"Graph.Edges[{i}]", edgeId: e.edgeId);
                else if (!nodeIndex.ContainsKey(e.toNodeId))
                    report.Error("Graph.EdgeToInvalid", "Edge toNodeId not found in graph nodes.", $"Graph.Edges[{i}]", edgeId: e.edgeId);

                if (e.fromNodeId.IsValid)
                {
                    outgoingCount.TryGetValue(e.fromNodeId, out var c);
                    outgoingCount[e.fromNodeId] = c + 1;
                }

                if (e.condition == null)
                    report.Warn("Graph.EdgeConditionNull", "Edge condition is null; runtime treats null as FALSE. Prefer ConstBool(True/False).", $"Graph.Edges[{i}]", edgeId: e.edgeId);
                else
                    ValidateConditionExpr(e.condition, report, $"Graph.Edges[{i}].Condition", e.edgeId, options);
            }

            // Parallel rule + grouped scopeId validity for exiting edges
            foreach (var kv in nodeIndex)
            {
                var nodeId = kv.Key;
                var node = kv.Value;

                if (string.IsNullOrEmpty(node.nodeTypeId)) continue;
                if (!_nodeTypes.TryGet(node.nodeTypeId, out var nodeType)) continue;

                if (nodeType.Kind == AGISNodeKind.Parallel)
                {
                    outgoingCount.TryGetValue(nodeId, out var count);
                    if (count > 1)
                        report.Error("Graph.ParallelTooManyEdges", "Parallel node must have ONLY ONE outgoing edge.", "Graph.Parallel", nodeId: nodeId);
                }

                if (nodeType.Kind == AGISNodeKind.Grouped && options.ValidateGroupedAssets && _groupResolver != null && node.groupAssetId.IsValid)
                {
                    var group = _groupResolver(node.groupAssetId);
                    if (group != null)
                    {
                        var scopeSet = new HashSet<string>(StringComparer.Ordinal);
                        if (group.scopes != null)
                        {
                            for (int s = 0; s < group.scopes.Count; s++)
                            {
                                var sc = group.scopes[s];
                                if (sc != null && !string.IsNullOrEmpty(sc.scopeId))
                                    scopeSet.Add(sc.scopeId);
                            }
                        }

                        for (int i = 0; i < graph.edges.Count; i++)
                        {
                            var e = graph.edges[i];
                            if (e == null) continue;
                            if (e.fromNodeId != nodeId) continue;

                            if (!string.IsNullOrEmpty(e.scopeId) && !string.Equals(e.scopeId, "Any", StringComparison.Ordinal))
                            {
                                if (!scopeSet.Contains(e.scopeId))
                                    report.Error("Graph.ScopeInvalid", $"Edge references unknown scopeId '{e.scopeId}' in grouped asset.", $"Graph.Edges[{i}].Scope", nodeId: nodeId, edgeId: e.edgeId, groupAssetId: node.groupAssetId);
                            }
                        }
                    }
                    else
                    {
                        report.Error("Graph.GroupAssetNotFound", "Grouped asset could not be resolved for validation.", "Graph.Grouped", nodeId: nodeId, groupAssetId: node.groupAssetId);
                    }
                }
            }

            return report;
        }

        public AGISGraphValidationReport ValidateGroupedAsset(AGISGroupedStateAsset group, AGISGraphValidatorOptions options = null)
        {
            options ??= new AGISGraphValidatorOptions();
            var report = new AGISGraphValidationReport();

            if (group == null)
            {
                report.Error("Group.Null", "Grouped asset is null.");
                return report;
            }

            if (!group.groupAssetId.IsValid)
                report.Error("Group.IdInvalid", "Grouped asset has invalid groupAssetId.", "Group.Asset", groupAssetId: group.groupAssetId);

            if (options.ValidateGroupedInternalGraphs && group.internalGraph != null)
            {
                // scope internal nodeId validation
                var internalNodeIds = new HashSet<AGISGuid>();
                if (group.internalGraph.nodes != null)
                {
                    for (int i = 0; i < group.internalGraph.nodes.Count; i++)
                    {
                        var n = group.internalGraph.nodes[i];
                        if (n != null && n.nodeId.IsValid)
                            internalNodeIds.Add(n.nodeId);
                    }
                }

                if (group.scopes != null)
                {
                    for (int s = 0; s < group.scopes.Count; s++)
                    {
                        var scope = group.scopes[s];
                        if (scope == null) continue;
                        scope.internalNodeIds ??= new List<AGISGuid>();

                        for (int i = 0; i < scope.internalNodeIds.Count; i++)
                        {
                            var id = scope.internalNodeIds[i];
                            if (!id.IsValid || !internalNodeIds.Contains(id))
                                report.Error("Group.ScopeNodeInvalid", $"Scope '{scope.scopeId}' references missing internal nodeId.", $"Group.Scopes[{s}]", groupAssetId: group.groupAssetId);
                        }
                    }
                }

                // validate internal graph itself (no recursion into grouped)
                var internalReport = ValidateGraph(group.internalGraph, new AGISGraphValidatorOptions
                {
                    AllowUnknownParamKeys = options.AllowUnknownParamKeys,
                    ValidateGroupedAssets = false,
                    ValidateGroupedInternalGraphs = false
                });

                for (int i = 0; i < internalReport.Issues.Count; i++)
                    report.Add(internalReport.Issues[i]);
            }

            // bindings validity
            if (group.bindings != null)
            {
                var internalNodes = new HashSet<AGISGuid>();
                var internalEdges = new Dictionary<AGISGuid, AGISTransitionEdgeDef>();
                if (group.internalGraph != null)
                {
                    if (group.internalGraph.nodes != null)
                    {
                        for (int i = 0; i < group.internalGraph.nodes.Count; i++)
                        {
                            var n = group.internalGraph.nodes[i];
                            if (n != null && n.nodeId.IsValid) internalNodes.Add(n.nodeId);
                        }
                    }
                    if (group.internalGraph.edges != null)
                    {
                        for (int i = 0; i < group.internalGraph.edges.Count; i++)
                        {
                            var e = group.internalGraph.edges[i];
                            if (e != null && e.edgeId.IsValid) internalEdges[e.edgeId] = e;
                        }
                    }
                }

                var exposedById = new Dictionary<AGISGuid, AGISExposedParamDef>();
                if (group.exposedParams != null)
                {
                    for (int i = 0; i < group.exposedParams.Count; i++)
                    {
                        var ep = group.exposedParams[i];
                        if (ep != null && ep.exposedId.IsValid) exposedById[ep.exposedId] = ep;
                    }
                }

                for (int b = 0; b < group.bindings.Count; b++)
                {
                    var binding = group.bindings[b];
                    if (binding == null) continue;

                    if (!binding.exposedId.IsValid || !exposedById.ContainsKey(binding.exposedId))
                    {
                        report.Error("Group.BindingExposedMissing", "Binding references missing/invalid exposedId.", $"Group.Bindings[{b}]", groupAssetId: group.groupAssetId);
                        continue;
                    }

                    if (binding.targets == null || binding.targets.Count == 0)
                    {
                        report.Warn("Group.BindingNoTargets", "Binding has no targets.", $"Group.Bindings[{b}]", groupAssetId: group.groupAssetId);
                        continue;
                    }

                    for (int t = 0; t < binding.targets.Count; t++)
                    {
                        var target = binding.targets[t];
                        if (target == null)
                        {
                            report.Error("Group.TargetNull", "Binding target is null.", $"Group.Bindings[{b}].Targets[{t}]", groupAssetId: group.groupAssetId);
                            continue;
                        }

                        if (target.kind == AGISParamTarget.TargetKind.InternalNodeParam)
                        {
                            if (!target.internalNodeId.IsValid || !internalNodes.Contains(target.internalNodeId))
                                report.Error("Group.TargetNodeMissing", "Target internalNodeId missing in internal graph.", $"Group.Bindings[{b}].Targets[{t}]", groupAssetId: group.groupAssetId);

                            if (string.IsNullOrEmpty(target.paramKey))
                                report.Error("Group.TargetParamKeyMissing", "Target paramKey is empty.", $"Group.Bindings[{b}].Targets[{t}]", groupAssetId: group.groupAssetId);
                        }
                        else if (target.kind == AGISParamTarget.TargetKind.InternalEdgeConditionParam)
                        {
                            if (!target.internalEdgeId.IsValid || !internalEdges.ContainsKey(target.internalEdgeId))
                                report.Error("Group.TargetEdgeMissing", "Target internalEdgeId missing in internal graph.", $"Group.Bindings[{b}].Targets[{t}]", groupAssetId: group.groupAssetId);

                            if (string.IsNullOrEmpty(target.conditionParamKey))
                                report.Error("Group.TargetCondParamKeyMissing", "Target conditionParamKey is empty.", $"Group.Bindings[{b}].Targets[{t}]", groupAssetId: group.groupAssetId);

                            if (target.internalEdgeId.IsValid && internalEdges.TryGetValue(target.internalEdgeId, out var edge) && edge?.condition != null)
                            {
                                var leafIds = new HashSet<AGISGuid>();
                                CollectLeafConditionIds(edge.condition, leafIds);

                                if (!target.internalConditionId.IsValid)
                                    report.Warn("Group.TargetConditionIdMissing", "Target internalConditionId is empty; binding may be ambiguous if edge has multiple leaves.", $"Group.Bindings[{b}].Targets[{t}]", groupAssetId: group.groupAssetId);
                                else if (!leafIds.Contains(target.internalConditionId))
                                    report.Error("Group.TargetConditionIdNotFound", "Target internalConditionId not found among edge leaf conditions.", $"Group.Bindings[{b}].Targets[{t}]", groupAssetId: group.groupAssetId);
                            }
                        }
                        else
                        {
                            report.Error("Group.TargetKindUnknown", "Unknown target kind.", $"Group.Bindings[{b}].Targets[{t}]", groupAssetId: group.groupAssetId);
                        }
                    }
                }
            }

            return report;
        }

        private void ValidateGroupedNodeUsage(AGISNodeInstanceDef node, AGISGraphValidationReport report, AGISGraphValidatorOptions options)
        {
            if (_groupResolver == null)
            {
                report.Warn("Graph.GroupResolverMissing", "No groupResolver provided; cannot validate grouped assets/bindings.", "Graph.Grouped", nodeId: node.nodeId, groupAssetId: node.groupAssetId);
                return;
            }

            if (!node.groupAssetId.IsValid) return;

            var group = _groupResolver(node.groupAssetId);
            if (group == null)
            {
                report.Error("Graph.GroupAssetNotFound", "Grouped asset could not be resolved.", "Graph.Grouped", nodeId: node.nodeId, groupAssetId: node.groupAssetId);
                return;
            }

            var exposedSchema = BuildSchemaFromExposedParams(group.exposedParams);
            var issues = new List<AGISParamIssue>();
            AGISParamResolver.Validate(exposedSchema, node.exposedOverrides, issues, allowUnknownKeys: options.AllowUnknownParamKeys);

            for (int i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                if (issue.Severity == AGISParamIssueSeverity.Error)
                    report.Error("Graph.GroupOverrideInvalid", issue.Message, "Graph.Grouped.ExposedOverrides", nodeId: node.nodeId, groupAssetId: node.groupAssetId);
                else
                    report.Warn("Graph.GroupOverrideWarn", issue.Message, "Graph.Grouped.ExposedOverrides", nodeId: node.nodeId, groupAssetId: node.groupAssetId);
            }

            if (options.ValidateGroupedAssets)
            {
                var groupReport = ValidateGroupedAsset(group, options);
                for (int i = 0; i < groupReport.Issues.Count; i++)
                    report.Add(groupReport.Issues[i]);
            }
        }

        private void ValidateConditionExpr(AGISConditionExprDef expr, AGISGraphValidationReport report, string path, AGISGuid edgeId, AGISGraphValidatorOptions options)
        {
            if (expr == null)
            {
                report.Warn("Cond.ExprNull", "Condition expression node is null (treated as FALSE).", path, edgeId: edgeId);
                return;
            }

            expr.EnsureLeafIds();

            switch (expr.kind)
            {
                case AGISConditionExprDef.ExprKind.ConstBool:
                    return;

                case AGISConditionExprDef.ExprKind.Leaf:
                    {
                        if (expr.leaf == null)
                        {
                            report.Error("Cond.LeafNull", "Leaf is null.", path, edgeId: edgeId);
                            return;
                        }

                        if (!expr.leaf.conditionId.IsValid)
                            report.Error("Cond.LeafIdInvalid", "Leaf conditionId is invalid.", path, edgeId: edgeId);

                        if (string.IsNullOrEmpty(expr.leaf.conditionTypeId))
                        {
                            report.Error("Cond.TypeMissing", "Leaf conditionTypeId is empty.", path, edgeId: edgeId);
                            return;
                        }

                        if (!_conditionTypes.TryGet(expr.leaf.conditionTypeId, out var condType))
                        {
                            report.Error("Cond.TypeUnknown", $"Unknown conditionTypeId '{expr.leaf.conditionTypeId}'.", path, edgeId: edgeId);
                            return;
                        }

                        var issues = new List<AGISParamIssue>();
                        AGISParamResolver.Validate(condType.Schema, expr.leaf.@params, issues, allowUnknownKeys: options.AllowUnknownParamKeys);
                        for (int i = 0; i < issues.Count; i++)
                        {
                            var issue = issues[i];
                            if (issue.Severity == AGISParamIssueSeverity.Error)
                                report.Error("Cond.ParamInvalid", issue.Message, path, edgeId: edgeId);
                            else
                                report.Warn("Cond.ParamWarn", issue.Message, path, edgeId: edgeId);
                        }

                        return;
                    }

                case AGISConditionExprDef.ExprKind.Not:
                    ValidateConditionExpr(expr.child, report, path + ".Not", edgeId, options);
                    return;

                case AGISConditionExprDef.ExprKind.And:
                case AGISConditionExprDef.ExprKind.Or:
                    {
                        if (expr.children == null || expr.children.Count == 0)
                        {
                            report.Warn("Cond.ChildrenEmpty", "Logical node has no children.", path, edgeId: edgeId);
                            return;
                        }

                        for (int i = 0; i < expr.children.Count; i++)
                            ValidateConditionExpr(expr.children[i], report, $"{path}.Children[{i}]", edgeId, options);
                        return;
                    }

                default:
                    report.Error("Cond.KindUnknown", "Unknown condition expr kind.", path, edgeId: edgeId);
                    return;
            }
        }

        private static AGISParamSchema BuildSchemaFromExposedParams(List<AGISExposedParamDef> exposedParams)
        {
            var schema = new AGISParamSchema();
            if (exposedParams == null) return schema;

            for (int i = 0; i < exposedParams.Count; i++)
            {
                var ep = exposedParams[i];
                if (ep == null || string.IsNullOrEmpty(ep.publicKey)) continue;

                var spec = new AGISParamSpec
                {
                    key = ep.publicKey,
                    displayName = ep.displayName,
                    tooltip = ep.tooltip,
                    category = ep.category,
                    type = ep.type,
                    defaultValue = ep.defaultValue,
                    hasMin = ep.hasMin,
                    hasMax = ep.hasMax,
                    floatMin = ep.min,
                    floatMax = ep.max,
                    required = false,
                    step = ep.step
                };

                schema.AddOrReplace(spec);
            }

            return schema;
        }

        private static void CollectLeafConditionIds(AGISConditionExprDef expr, HashSet<AGISGuid> outIds)
        {
            if (expr == null || outIds == null) return;

            switch (expr.kind)
            {
                case AGISConditionExprDef.ExprKind.Leaf:
                    if (expr.leaf != null && expr.leaf.conditionId.IsValid)
                        outIds.Add(expr.leaf.conditionId);
                    return;

                case AGISConditionExprDef.ExprKind.Not:
                    CollectLeafConditionIds(expr.child, outIds);
                    return;

                case AGISConditionExprDef.ExprKind.And:
                case AGISConditionExprDef.ExprKind.Or:
                    if (expr.children == null) return;
                    for (int i = 0; i < expr.children.Count; i++)
                        CollectLeafConditionIds(expr.children[i], outIds);
                    return;

                default:
                    return;
            }
        }
    }
}
