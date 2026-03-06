// File: AGISNodeRuntimeFactory.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Centralized node runtime instantiation (registry + schema-driven param resolving).

using System;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISNodeRuntimeFactory
    {
        private readonly AGISNodeTypeRegistry _nodeTypes;

        public AGISNodeRuntimeFactory(AGISNodeTypeRegistry nodeTypes)
        {
            _nodeTypes = nodeTypes ?? throw new ArgumentNullException(nameof(nodeTypes));
        }

        public IAGISNodeRuntime Create(AGISExecutionContext ctx, AGISNodeInstanceDef nodeDef, IAGISNodeType forcedType = null, AGISDebugTrace trace = null)
        {
            if (nodeDef == null)
                return new NoOpNodeRuntime();

            IAGISNodeType nodeType = forcedType;
            if (nodeType == null)
                _nodeTypes.TryGet(nodeDef.nodeTypeId, out nodeType);

            if (nodeType == null)
            {
                trace?.Warn($"No NodeType registered for '{nodeDef.nodeTypeId}'. Using NoOp runtime.");
                return new NoOpNodeRuntime();
            }

            try
            {
                var accessor = AGISParamResolver.BuildAccessor(nodeType.Schema, nodeDef.@params);
                var args = new AGISNodeRuntimeCreateArgs(ctx, nodeDef, nodeType, accessor);
                var runtime = nodeType.CreateRuntime(args);

                if (runtime == null)
                {
                    trace?.Warn($"NodeType '{nodeType.TypeId}' returned null runtime. Using NoOp runtime.");
                    return new NoOpNodeRuntime();
                }

                return runtime;
            }
            catch (Exception ex)
            {
                trace?.Error($"Exception creating runtime for NodeType '{nodeType.TypeId}': {ex}");
                return new NoOpNodeRuntime();
            }
        }

        private sealed class NoOpNodeRuntime : IAGISNodeRuntime
        {
            public void Enter() { }
            public void Tick(float dt) { }
            public void Exit() { }
        }
    }
}
