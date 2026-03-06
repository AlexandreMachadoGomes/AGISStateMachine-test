// File: IAGISConditionType.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Code-provided ConditionType contract. Evaluated by AGISTransitionEvaluator against edge expression trees.

using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public interface IAGISConditionType
    {
        string TypeId { get; }
        string DisplayName { get; }

        /// <summary>
        /// Universal ParamSchema for this condition type (code-provided).
        /// Leaf instances store only ParamTable overrides.
        /// </summary>
        AGISParamSchema Schema { get; }

        /// <summary>
        /// Evaluate this condition leaf.
        /// Return true if condition is met.
        /// </summary>
        bool Evaluate(in AGISConditionEvalArgs args);
    }

    public readonly struct AGISConditionEvalArgs
    {
        public readonly AGISExecutionContext Ctx;
        public readonly AGISConditionInstanceDef ConditionDef;
        public readonly IAGISConditionType ConditionType;
        public readonly IAGISParamAccessor Params;

        /// <summary>
        /// The node runtime that is currently active when this condition is being evaluated.
        /// Cast to IAGISNodeSignal to check node-local completion state.
        /// May be null if the evaluator is called outside of a transition check.
        /// </summary>
        public readonly IAGISNodeRuntime CurrentRuntime;

        public AGISConditionEvalArgs(AGISExecutionContext ctx, AGISConditionInstanceDef def, IAGISConditionType type, IAGISParamAccessor @params, IAGISNodeRuntime currentRuntime = null)
        {
            Ctx = ctx;
            ConditionDef = def;
            ConditionType = type;
            Params = @params;
            CurrentRuntime = currentRuntime;
        }
    }
}
