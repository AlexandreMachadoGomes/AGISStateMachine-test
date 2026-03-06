// File: AGISParallelNodeRuntime.cs
// Folder: Assets/Scripts/AX State Machine/Hierarchical/
// Purpose: Parallel/AND runtime node: ticks embedded child nodes concurrently.
// Canvas alignment:
// - Children are embedded node defs (no internal edges among them).
// - Outer graph still controls transitions; validator enforces single outgoing edge from the parallel node.

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISParallelNodeRuntime : IAGISNodeRuntime
    {
        private readonly AGISExecutionContext _ctx;
        private readonly AGISNodeInstanceDef _nodeDef;
        private readonly AGISNodeRuntimeFactory _factory;
        private readonly AGISDebugTrace _trace;

        private readonly List<IAGISNodeRuntime> _children = new List<IAGISNodeRuntime>();

        public AGISParallelNodeRuntime(AGISExecutionContext ctx, AGISNodeInstanceDef nodeDef, AGISNodeRuntimeFactory factory, AGISDebugTrace trace = null)
        {
            _ctx = ctx;
            _nodeDef = nodeDef;
            _factory = factory;
            _trace = trace;
        }

        public void Enter()
        {
            _children.Clear();

            if (_nodeDef?.parallelChildren == null || _nodeDef.parallelChildren.Count == 0)
                return;

            for (int i = 0; i < _nodeDef.parallelChildren.Count; i++)
            {
                var childDef = _nodeDef.parallelChildren[i];
                if (childDef == null) continue;

                var runtime = _factory.Create(_ctx, childDef, forcedType: null, trace: _trace);
                _children.Add(runtime);

                try { runtime.Enter(); }
                catch (Exception ex) { _trace?.Error($"Parallel child Enter exception: {ex}"); }
            }
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                var r = _children[i];
                if (r == null) continue;

                try { r.Tick(dt); }
                catch (Exception ex) { _trace?.Error($"Parallel child Tick exception: {ex}"); }
            }
        }

        public void Exit()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                var r = _children[i];
                if (r == null) continue;

                try { r.Exit(); }
                catch (Exception ex) { _trace?.Error($"Parallel child Exit exception: {ex}"); }
            }

            _children.Clear();
        }
    }
}
