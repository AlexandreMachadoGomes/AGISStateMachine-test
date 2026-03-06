// File: AGISStateMachineInstance.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Runtime executor for a single compiled graph instance (Enter/Tick/Exit + edge transitions).
// Canvas alignment:
// - Conditions only on edges (evaluated via AGISTransitionEvaluator)
// - All variability via params on node/condition types
// - Scope gating is supported via optional runtime interface for Grouped nodes

using System;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    /// <summary>
    /// Optional interface for Grouped node runtimes. Batch 4 will implement this.
    /// Used to decide if an outer edge with scopeId is eligible.
    /// </summary>
    public interface IAGISGroupedScopeRuntime
    {
        bool IsScopeActive(string scopeId);
    }

    public sealed class AGISStateMachineInstance
    {
        private readonly AGISExecutionContext _ctx;
        private readonly AGISRuntimeGraph _graph;
        private readonly AGISTransitionEvaluator _evaluator;
        private readonly AGISNodeRuntimeFactory _nodeFactory;
        private readonly AGISTransitionPolicyRuntime _policyRuntime;
        private readonly AGISDebugTrace _trace;

        private int _currentNodeIndex = -1;
        private IAGISNodeRuntime _currentRuntime;

        private float _time;
        private bool _started;

        public int CurrentNodeIndex => _currentNodeIndex;
        public AGISGuid CurrentNodeId => (_currentNodeIndex >= 0 && _currentNodeIndex < _graph.Nodes.Length) ? _graph.Nodes[_currentNodeIndex].NodeId : AGISGuid.Empty;

        public AGISStateMachineInstance(
            AGISExecutionContext ctx,
            AGISRuntimeGraph graph,
            AGISTransitionEvaluator evaluator,
            AGISNodeRuntimeFactory nodeFactory,
            AGISTransitionPolicyRuntime policyRuntime,
            AGISDebugTrace trace = null)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _nodeFactory = nodeFactory ?? throw new ArgumentNullException(nameof(nodeFactory));
            _policyRuntime = policyRuntime ?? throw new ArgumentNullException(nameof(policyRuntime));
            _trace = trace;
        }

        public void StartAtEntry()
        {
            if (_started)
                return;

            if (_graph.EntryNodeIndex < 0 || _graph.EntryNodeIndex >= _graph.Nodes.Length)
            {
                _trace?.Error("Cannot start: graph has invalid entry node index.");
                return;
            }

            _policyRuntime.Clear();
            _time = 0f;

            SetCurrentNode(_graph.EntryNodeIndex);
            _started = true;
        }

        public void Stop()
        {
            if (!_started)
                return;

            try { _currentRuntime?.Exit(); }
            catch (Exception ex) { _trace?.Error($"Exception during Exit: {ex}"); }

            _currentRuntime = null;
            _currentNodeIndex = -1;
            _started = false;
        }

        public void Tick(float dt, int maxTransitionsPerTick)
        {
            if (!_started)
                StartAtEntry();

            if (!_started)
                return; // still not started due to invalid entry

            _time += dt;

            try { _currentRuntime?.Tick(dt); }
            catch (Exception ex) { _trace?.Error($"Exception during Tick on node {CurrentNodeId}: {ex}"); }

            int transitions = 0;
            int max = maxTransitionsPerTick <= 0 ? 1 : maxTransitionsPerTick;

            while (transitions < max)
            {
                if (!TryPickTransition(out int edgeIndex))
                    break;

                if (!ApplyTransition(edgeIndex))
                    break;

                transitions++;
            }
        }

        private bool TryPickTransition(out int compiledEdgeIndex)
        {
            compiledEdgeIndex = -1;

            if (_currentNodeIndex < 0 || _currentNodeIndex >= _graph.Nodes.Length)
                return false;

            // --- AnyState pass (global interrupt priority, checked before node-specific edges) ---
            if (_graph.AnyStateEdgeIndices.Length > 0)
            {
                for (int i = 0; i < _graph.AnyStateEdgeIndices.Length; i++)
                {
                    int edgeIndex = _graph.AnyStateEdgeIndices[i];
                    if (edgeIndex < 0 || edgeIndex >= _graph.Edges.Length)
                        continue;

                    var edge = _graph.Edges[edgeIndex];

                    // Skip self-transitions (already in the target node)
                    if (edge.ToNodeIndex == _currentNodeIndex)
                        continue;

                    if (!_policyRuntime.CanFire(edge, _time, _currentRuntime))
                        continue;

                    if (_evaluator.Evaluate(edge.Condition, _ctx, _currentRuntime))
                    {
                        compiledEdgeIndex = edgeIndex;
                        return true;
                    }
                }
            }

            // --- Node-specific pass ---
            var node = _graph.Nodes[_currentNodeIndex];
            var outgoing = node.OutgoingEdges;
            if (outgoing == null || outgoing.Length == 0)
                return false;

            for (int i = 0; i < outgoing.Length; i++)
            {
                int edgeIndex = outgoing[i];
                if (edgeIndex < 0 || edgeIndex >= _graph.Edges.Length)
                    continue;

                var edge = _graph.Edges[edgeIndex];

                // Scope gating (only meaningful for edges exiting a Grouped node)
                if (!IsScopeEligible(node, edge))
                    continue;

                // Transition policy gating (cooldown/interrupt)
                if (!_policyRuntime.CanFire(edge, _time, _currentRuntime))
                    continue;

                // Condition evaluation (null = false inside evaluator).
                // Pass _currentRuntime so IAGISNodeSignal-based conditions work.
                if (_evaluator.Evaluate(edge.Condition, _ctx, _currentRuntime))
                {
                    compiledEdgeIndex = edgeIndex;
                    return true;
                }
            }

            return false;
        }

        private bool IsScopeEligible(in AGISCompiledNode fromNode, in AGISCompiledEdge edge)
        {
            if (string.IsNullOrEmpty(edge.ScopeId) || string.Equals(edge.ScopeId, "Any", StringComparison.Ordinal))
                return true;

            // Canvas: scope gating is only for edges exiting grouped macros.
            if (fromNode.Type != null && fromNode.Type.Kind == AGISNodeKind.Grouped)
            {
                if (_currentRuntime is IAGISGroupedScopeRuntime scopeRuntime)
                    return scopeRuntime.IsScopeActive(edge.ScopeId);

                // If grouped runtime not implemented yet, treat as ineligible (safe).
                return false;
            }

            // Non-grouped nodes ignore scopeId (should not normally happen).
            return true;
        }

        private bool ApplyTransition(int compiledEdgeIndex)
        {
            if (compiledEdgeIndex < 0 || compiledEdgeIndex >= _graph.Edges.Length)
                return false;

            var edge = _graph.Edges[compiledEdgeIndex];

            // Exit current node
            try { _currentRuntime?.Exit(); }
            catch (Exception ex) { _trace?.Error($"Exception during Exit on node {CurrentNodeId}: {ex}"); }

            // Record policy firing (cooldown)
            _policyRuntime.RecordFired(edge, _time);

            // Move
            if (edge.ToNodeIndex < 0 || edge.ToNodeIndex >= _graph.Nodes.Length)
            {
                _trace?.Error($"Transition target node index invalid for edge {edge.EdgeId}.");
                return false;
            }

            SetCurrentNode(edge.ToNodeIndex);
            return true;
        }

        private void SetCurrentNode(int nodeIndex)
        {
            _currentNodeIndex = nodeIndex;
            var compiledNode = _graph.Nodes[_currentNodeIndex];

            _currentRuntime = _nodeFactory.Create(_ctx, compiledNode.Def, compiledNode.Type, _trace);

            try { _currentRuntime?.Enter(); }
            catch (Exception ex) { _trace?.Error($"Exception during Enter on node {compiledNode.NodeId}: {ex}"); }
        }
    }
}
