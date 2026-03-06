// File: AGISGroupedNodeType.cs
// Folder: Assets/Scripts/AX State Machine/Hierarchical/
// Purpose: Structural NodeType for Grouped macro nodes. Registered automatically by the runner to reduce configuration burden.

using System;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISGroupedNodeType : IAGISNodeType
    {
        public const string TYPE_ID = "Grouped";

        private readonly Func<AGISGuid, AGISGroupedStateAsset> _groupResolver;
        private readonly AGISGraphCompiler _compiler;
        private readonly AGISTransitionEvaluator _evaluator;
        private readonly AGISNodeRuntimeFactory _nodeFactory;
        private readonly AGISGroupedParamBinder _binder;
        private readonly AGISDebugTrace _trace;

        private readonly AGISParamSchema _schema = new AGISParamSchema();

        public AGISGroupedNodeType(
            Func<AGISGuid, AGISGroupedStateAsset> groupResolver,
            AGISGraphCompiler compiler,
            AGISTransitionEvaluator evaluator,
            AGISNodeRuntimeFactory nodeFactory,
            AGISGroupedParamBinder binder,
            AGISDebugTrace trace = null)
        {
            _groupResolver = groupResolver;
            _compiler = compiler;
            _evaluator = evaluator;
            _nodeFactory = nodeFactory;
            _binder = binder;
            _trace = trace;
        }

        public string TypeId => TYPE_ID;
        public string DisplayName => "Grouped";
        public AGISNodeKind Kind => AGISNodeKind.Grouped;
        public AGISParamSchema Schema => _schema;

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            return new AGISGroupedNodeRuntime(
                args.Ctx,
                args.NodeDef,
                _groupResolver,
                _compiler,
                _evaluator,
                _nodeFactory,
                _binder,
                _trace
            );
        }
    }
}
