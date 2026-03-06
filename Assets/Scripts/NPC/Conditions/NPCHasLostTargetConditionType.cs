// File: NPCHasLostTargetConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when the target has been continuously outside the detection volume for
//          longer than `timeout` seconds.
//
// Reads npc.target_time_lost from AGISActorState, which is written each tick by
// NPCFollowTargetNodeType when use_detection_memory = true.
//
// Typical usage:
//   Chase state (NPCFollowTarget, use_detection_memory = true)
//     └─ exit edge: NPCHasLostTarget (timeout = 3) → Patrol / Idle
//
// Params:
//   timeout  (Float, default 3.0) — seconds without detection before this returns true

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCHasLostTargetConditionType : IAGISConditionType
    {
        public string TypeId      => "npc.has_lost_target";
        public string DisplayName => "NPC Has Lost Target";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("timeout", AGISParamType.Float, AGISValue.FromFloat(3f))
                    { displayName = "Timeout",
                      tooltip     = "Seconds the target must be continuously undetected before this " +
                                    "condition returns true. Requires NPCFollowTarget with " +
                                    "use_detection_memory = true on the chase state.",
                      hasMin = true, floatMin = 0f },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            if (args.Ctx.Actor == null) return false;

            var actorState = args.Ctx.Actor.GetComponent<AGISActorState>();
            if (actorState == null) return false;

            float timeLost = actorState.GetFloat("npc.target_time_lost");
            float timeout  = args.Params.GetFloat("timeout", 3f);

            return timeLost >= timeout;
        }
    }
}
