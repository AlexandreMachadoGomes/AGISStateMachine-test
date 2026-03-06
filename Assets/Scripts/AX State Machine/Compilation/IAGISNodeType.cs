// File: IAGISNodeType.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Code-provided NodeType contract + runtime node lifecycle interfaces (canvas-aligned Enter/Tick/Exit).

using UnityEngine;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public enum AGISNodeKind
    {
        Normal   = 0,
        Grouped  = 1,
        Parallel = 2,
        AnyState = 3,   // Virtual source node for global interrupt transitions
    }

    public interface IAGISNodeType
    {
        string TypeId { get; }
        string DisplayName { get; }
        AGISNodeKind Kind { get; }

        /// <summary>
        /// Universal ParamSchema for this node type (code-provided).
        /// Node instances store only ParamTable overrides.
        /// </summary>
        AGISParamSchema Schema { get; }

        /// <summary>
        /// Create a runtime node instance. This can be implemented after infrastructure is stable.
        /// </summary>
        IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args);
    }

    /// <summary>
    /// Canvas-aligned node lifecycle.
    /// </summary>
    public interface IAGISNodeRuntime
    {
        void Enter();
        void Tick(float dt);
        void Exit();
    }

    /// <summary>
    /// Execution context (generic services only). Flesh out later with movement/combat/anim/time/random/log etc.
    /// </summary>
    public sealed class AGISExecutionContext
    {
        public readonly GameObject Actor;
        public readonly IAGISBlackboard Blackboard;

        public AGISExecutionContext(GameObject actor, IAGISBlackboard blackboard)
        {
            Actor = actor;
            Blackboard = blackboard;
        }
    }

    /// <summary>
    /// Minimal runtime blackboard contract.
    /// You can replace with a richer typed/variant store later.
    /// </summary>
    public interface IAGISBlackboard
    {
        bool TryGet<T>(string key, out T value);
        void Set<T>(string key, T value);
        bool Remove(string key);
    }

    public readonly struct AGISNodeRuntimeCreateArgs
    {
        public readonly AGISExecutionContext Ctx;
        public readonly AGISNodeInstanceDef NodeDef;
        public readonly IAGISNodeType NodeType;
        public readonly IAGISParamAccessor Params;

        public AGISNodeRuntimeCreateArgs(AGISExecutionContext ctx, AGISNodeInstanceDef nodeDef, IAGISNodeType nodeType, IAGISParamAccessor @params)
        {
            Ctx = ctx;
            NodeDef = nodeDef;
            NodeType = nodeType;
            Params = @params;
        }
    }
}
