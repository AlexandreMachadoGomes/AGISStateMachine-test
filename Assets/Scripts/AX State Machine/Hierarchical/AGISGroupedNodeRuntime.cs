// File: AGISGroupedNodeRuntime.cs
// Folder: Assets/Scripts/AX State Machine/Hierarchical/
// Purpose: Grouped macro runtime node: runs an internal state machine instance, supports scope gating and exposed param binding.
// Canvas alignment:
// - Internal macro graph obeys same rules (conditions only on edges).
// - Outer edges may specify scopeId; eligibility is restricted by internal current node membership in that scope.
// - Exposed params bind to stable targets (node params and condition leaf params).
// - Binder applies on Enter + on Change.
// - MUST NOT mutate shared ScriptableObject assets: internal graph is cloned per runtime instance.

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISGroupedNodeRuntime : IAGISNodeRuntime, IAGISGroupedScopeRuntime
    {
        private readonly AGISExecutionContext _ctx;
        private readonly AGISNodeInstanceDef _outerNodeDef;

        private readonly Func<AGISGuid, AGISGroupedStateAsset> _groupResolver;

        private readonly AGISGraphCompiler _compiler;
        private readonly AGISTransitionEvaluator _evaluator;
        private readonly AGISNodeRuntimeFactory _nodeFactory;

        private readonly AGISGroupedParamBinder _binder;
        private readonly AGISDebugTrace _trace;

        private AGISGroupedStateAsset _groupAsset;
        private AGISStateMachineGraph _internalGraphClone;
        private AGISRuntimeGraph _internalCompiled;
        private AGISStateMachineInstance _internalInstance;
        private AGISTransitionPolicyRuntime _internalPolicy;

        private readonly Dictionary<string, HashSet<AGISGuid>> _scopeMap = new Dictionary<string, HashSet<AGISGuid>>(StringComparer.Ordinal);

        private ulong _lastOverridesFp;

        private const int InternalMaxTransitionsPerTick = 4;

        public AGISGroupedNodeRuntime(
            AGISExecutionContext ctx,
            AGISNodeInstanceDef outerNodeDef,
            Func<AGISGuid, AGISGroupedStateAsset> groupResolver,
            AGISGraphCompiler compiler,
            AGISTransitionEvaluator evaluator,
            AGISNodeRuntimeFactory nodeFactory,
            AGISGroupedParamBinder binder,
            AGISDebugTrace trace = null)
        {
            _ctx = ctx;
            _outerNodeDef = outerNodeDef;
            _groupResolver = groupResolver;
            _compiler = compiler;
            _evaluator = evaluator;
            _nodeFactory = nodeFactory;
            _binder = binder;
            _trace = trace;
        }

        public void Enter()
        {
            CleanupInternal();

            if (_outerNodeDef == null || !_outerNodeDef.groupAssetId.IsValid)
            {
                _trace?.Warn("Grouped runtime Enter: missing groupAssetId.");
                return;
            }

            if (_groupResolver == null)
            {
                _trace?.Warn("Grouped runtime Enter: groupResolver not provided.");
                return;
            }

            _groupAsset = _groupResolver(_outerNodeDef.groupAssetId);
            if (_groupAsset == null)
            {
                _trace?.Warn($"Grouped runtime Enter: failed to resolve group asset {_outerNodeDef.groupAssetId}.");
                return;
            }

            BuildScopeMap(_groupAsset);

            _internalGraphClone = AGISGraphClone.CloneGraph(_groupAsset.internalGraph);

            ApplyBindingsIfNeeded(force: true);

            _internalCompiled = _compiler.Compile(_internalGraphClone);

            _internalPolicy = new AGISTransitionPolicyRuntime();
            _internalInstance = new AGISStateMachineInstance(_ctx, _internalCompiled, _evaluator, _nodeFactory, _internalPolicy, _trace);
            _internalInstance.StartAtEntry();
        }

        public void Tick(float dt)
        {
            if (_internalInstance == null)
                return;

            ApplyBindingsIfNeeded(force: false);

            _internalInstance.Tick(dt, InternalMaxTransitionsPerTick);
        }

        public void Exit()
        {
            CleanupInternal();
        }

        public bool IsScopeActive(string scopeId)
        {
            if (string.IsNullOrEmpty(scopeId) || string.Equals(scopeId, "Any", StringComparison.Ordinal))
                return true;

            if (_internalInstance == null)
                return false;

            var current = _internalInstance.CurrentNodeId;
            if (!current.IsValid)
                return false;

            if (_scopeMap.TryGetValue(scopeId, out var set) && set != null)
                return set.Contains(current);

            return false;
        }

        private void ApplyBindingsIfNeeded(bool force)
        {
            if (_groupAsset == null || _internalGraphClone == null || _outerNodeDef == null)
                return;

            var fp = AGISBindingChangeDetector.FingerprintParamTable(_outerNodeDef.exposedOverrides);
            if (!force && fp == _lastOverridesFp)
                return;

            _lastOverridesFp = fp;

            try
            {
                _binder.ApplyBindings(_groupAsset, _outerNodeDef, _internalGraphClone);
            }
            catch (Exception ex)
            {
                _trace?.Error($"Grouped binder exception: {ex}");
            }
        }

        private void BuildScopeMap(AGISGroupedStateAsset group)
        {
            _scopeMap.Clear();

            if (group == null || group.scopes == null)
                return;

            for (int i = 0; i < group.scopes.Count; i++)
            {
                var scope = group.scopes[i];
                if (scope == null || string.IsNullOrEmpty(scope.scopeId))
                    continue;

                if (!_scopeMap.TryGetValue(scope.scopeId, out var set))
                {
                    set = new HashSet<AGISGuid>();
                    _scopeMap[scope.scopeId] = set;
                }

                if (scope.internalNodeIds == null) continue;

                for (int n = 0; n < scope.internalNodeIds.Count; n++)
                {
                    var id = scope.internalNodeIds[n];
                    if (id.IsValid)
                        set.Add(id);
                }
            }
        }

        private void CleanupInternal()
        {
            try { _internalInstance?.Stop(); }
            catch (Exception ex) { _trace?.Error($"Grouped internal Stop exception: {ex}"); }

            _internalInstance = null;
            _internalPolicy = null;
            _internalCompiled = null;
            _internalGraphClone = null;
            _groupAsset = null;

            _scopeMap.Clear();
            _lastOverridesFp = 0;
        }
    }
}
