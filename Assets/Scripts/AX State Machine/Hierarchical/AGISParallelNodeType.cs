// File: AGISParallelNodeType.cs
// Folder: Assets/Scripts/AX State Machine/Hierarchical/
// Purpose: Structural NodeType for Parallel/AND nodes. Registered automatically by the runner to reduce configuration burden.

using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISParallelNodeType : IAGISNodeType
    {
        public const string TYPE_ID = "Parallel";

        private readonly AGISNodeRuntimeFactory _factory;
        private readonly AGISDebugTrace _trace;

        private readonly AGISParamSchema _schema = new AGISParamSchema();

        public AGISParallelNodeType(AGISNodeRuntimeFactory factory, AGISDebugTrace trace = null)
        {
            _factory = factory;
            _trace = trace;
        }

        public string TypeId => TYPE_ID;
        public string DisplayName => "Parallel";
        public AGISNodeKind Kind => AGISNodeKind.Parallel;
        public AGISParamSchema Schema => _schema;

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            return new AGISParallelNodeRuntime(args.Ctx, args.NodeDef, _factory, _trace);
        }
    }
}
