// File: NPCIsMovingConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when the pathfinder's desired velocity magnitude exceeds the threshold.
//
// Params:
//   threshold  (Float, default 0.1) — minimum speed (units/s) to be considered "moving"

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCIsMovingConditionType : IAGISConditionType
    {
        public string TypeId => "npc.is_moving";
        public string DisplayName => "NPC Is Moving";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("threshold", AGISParamType.Float, AGISValue.FromFloat(0.1f))
                    { displayName = "Speed Threshold", hasMin = true, floatMin = 0f },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            var pathFinder = args.Ctx.Actor != null
                ? args.Ctx.Actor.GetComponent<IAGISNPCPathFinder>()
                : null;

            if (pathFinder == null || !pathFinder.IsPathfindingActive) return false;

            float threshold = args.Params.GetFloat("threshold", 0.1f);
            return pathFinder.DesiredVelocity.sqrMagnitude > threshold * threshold;
        }
    }
}
